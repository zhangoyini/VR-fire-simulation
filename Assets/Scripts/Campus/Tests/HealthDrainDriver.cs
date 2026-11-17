#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Jiao.HealthTime;
using UnityEngine;

/// <summary>
/// 无头显环境下的掉血曲线探针：直接驱动血量与烟雾组件，不需要头显或手柄。
/// 由 HealthDrainChecks.Run() 在进入 Play 模式后创建。
/// </summary>
public sealed class HealthDrainDriver : MonoBehaviour
{
    private readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
    private bool failed;

    private void Start() { StartCoroutine(Probe()); }

    private IEnumerator Probe()
    {
        yield return null; yield return null;

        var health = HealthTimeController.Instance;
        var exposure = FindObjectOfType<CampusSmokeExposure>();
        if (health == null || exposure == null)
        {
            Finish(false, "找不到 HealthTimeController 或 CampusSmokeExposure");
            yield break;
        }

        // 教学阶段默认冻结掉血，这里把导演标记为“已跳过教学”，让血量真正开始计算
        var lesson = FindObjectOfType<CampusLessonDirector>(true);
        if (lesson != null)
        {
            var flag = typeof(CampusLessonDirector).GetField("bypassed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            flag?.SetValue(lesson, true);
        }

        // 让烟雾组件自己走 Update，但不让真实环境干扰：先把曝光状态清零
        exposure.Tick(false, 0f);
        health.ResetHealth();
        yield return new WaitForSeconds(.2f);

        float idleRate = health.CurrentDrainPerSecond;
        float before = health.HealthPercent;
        Time.timeScale = 20f;                       // 20 倍速：等待 10 秒“缩放时间”= 10 秒游戏时间
        yield return new WaitForSeconds(10f);
        Time.timeScale = 1f;
        float passiveLoss = (before - health.HealthPercent) * 100f;

        // 吸烟 10 秒：宽限 3 秒，应扣 (10-3) x 1.0 = 7 点
        float beforeSmoke = health.HealthPercent;
        exposure.Tick(true, 10f);
        float smokeLoss = (beforeSmoke - health.HealthPercent) * 100f;

        // 湿毛巾：不接真毛巾时无法触发 CoversFace，这里校验倍率是否已生效
        float covered = exposure.CoveredDamageMultiplier;
        exposure.Tick(false, 0f);

        // HUD 三种状态的文案：不需要头显，直接调静态方法
        string hudProtected = CampusExperience.StatusLine(true, true, false);
        string hudSmoke = CampusExperience.StatusLine(true, false, false);
        string hudReady = CampusExperience.StatusLine(false, false, true);
        string hudNone = CampusExperience.StatusLine(false, false, false);
        bool hudOk = hudProtected.Contains("防护生效") && hudSmoke.Contains("烟雾区")
                     && hudReady.Contains("湿毛巾已就位") && hudNone.Length == 0;

        // 提示音：合成结果必须非静音且长度正确
        var onCue = CampusExperience.MakeTone(new[] { 660f, 990f }, .16f, .35f);
        var offCue = CampusExperience.MakeTone(new[] { 520f }, .14f, .25f);
        bool toneOk = IsAudible(onCue, .16f * 2) && IsAudible(offCue, .14f);

        lines.Add("== 掉血曲线探针 ==");
        lines.Add($"无烟每秒掉血        : {idleRate:F3} HP/s");
        lines.Add($"无烟 10 秒实测      : -{passiveLoss:F2} HP（期望 -0.20 ± 0.06）");
        lines.Add($"烟中 10 秒实测      : -{smokeLoss:F2} HP（期望 -7.00 ± 0.30）");
        lines.Add($"湿毛巾伤害倍率      : {covered:F2}（期望 0.20）");
        lines.Add($"推算 烟中无防护     : {1f + health.CurrentBaseDrainPerSecond:F2} HP/s");
        lines.Add($"推算 烟中有防护     : {1f * covered + health.CurrentBaseDrainPerSecond * covered:F2} HP/s");
        lines.Add($"[HUD] 烟中已防护     : {hudProtected}");
        lines.Add($"[HUD] 烟中未防护     : {hudSmoke}");
        lines.Add($"[HUD] 烟外毛巾就位   : {hudReady}");
        lines.Add($"[HUD] 无特殊状态     : {(hudNone.Length == 0 ? "（空，正确）" : hudNone)}");
        lines.Add($"[音效] 生效提示音     : {onCue.samples} 采样 × {onCue.channels} 声道（{onCue.length:F2}s）");
        lines.Add($"[音效] 解除提示音     : {offCue.samples} 采样 × {offCue.channels} 声道（{offCue.length:F2}s）");
        lines.Add("提示：画面位置与音量大小仍需在头显里确认，这里只验证文案与波形。");

        bool okIdle = Mathf.Abs(passiveLoss - .2f) <= .06f;
        bool okSmoke = Mathf.Abs(smokeLoss - 7f) <= .3f;
        bool okCovered = Mathf.Abs(covered - .2f) <= .001f;
        bool pass = okIdle && okSmoke && okCovered && hudOk && toneOk;
        if (!okIdle) lines.Add("FAIL: 无烟掉血不在预期范围——检查 idleDrainPerSecond 与 drainOnlyInSmoke");
        if (!okSmoke) lines.Add("FAIL: 烟雾伤害不在预期范围——检查 graceSeconds 与 damagePerSecond");
        if (!okCovered) lines.Add("FAIL: 毛巾倍率不是 0.2——检查 coveredDamageMultiplier");
        if (!hudOk) lines.Add("FAIL: HUD 三态文案不符——检查 CampusExperience.StatusLine");
        if (!toneOk) lines.Add("FAIL: 提示音为空或长度不对——检查 CampusExperience.MakeTone");

        Finish(pass, string.Join("\n", lines));
    }

    private static bool IsAudible(AudioClip clip, float expectedSeconds)
    {
        if (clip == null || clip.samples <= 0) return false;
        if (Mathf.Abs(clip.length - expectedSeconds) > .05f) return false;
        var data = new float[Mathf.Min(clip.samples, 4410)];
        clip.GetData(data, 0);
        float peak = 0f;
        foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
        return peak > .05f;
    }

    private void Finish(bool pass, string report)
    {
        failed = !pass;
        string path = "Documentation/HealthDrainReport.txt";
        try { File.WriteAllText(path, report); } catch { /* 只读目录时忽略 */ }
        Debug.Log((pass ? "HEALTH_DRAIN_PASS\n" : "HEALTH_DRAIN_FAIL\n") + report);

        Time.timeScale = 1f;
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);   // 命令行：用返回码表达结果
        else
            UnityEditor.EditorApplication.isPlaying = false;    // 编辑器内：只退出 Play，不关 Unity
    }
}
#endif
