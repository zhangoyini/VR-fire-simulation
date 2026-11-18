using Harbourview.Journey;
using Jiao.HealthTime;
using TMPro;
using UnityEngine;

// Coordinates the integrated scene without replacing the team's health, menu or journey systems.
[DefaultExecutionOrder(70)]
public sealed class CampusExperience : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private TextMeshProUGUI floorLabel;
    [SerializeField] private TextMeshProUGUI debriefLabel;
    [SerializeField] private AudioSource alarm;
    private float startTime;
    private int previousFloor;
    private bool finished;
    private CampusSmokeExposure smokeExposure;
    private HealthTimeBar healthBar;
    private HealthTimeController health;
    private CampusLocomotion locomotion;
    private CampusRescueStudent student;
    private ResolutionCoordinator resolution;
    private CampusDebriefReport report;
    private void Start()
    {
        startTime = Time.time;
        smokeExposure = FindObjectOfType<CampusSmokeExposure>();
        healthBar = FindObjectOfType<HealthTimeBar>(true);
        health = HealthTimeController.Instance;
        locomotion = trackedHead != null ? trackedHead.GetComponentInParent<CampusLocomotion>() : null;
        student = FindObjectOfType<CampusRescueStudent>();
        if (health != null) health.HealthLost += RecordLoss;
        if (smokeExposure != null) smokeExposure.ExposureMeasured += RecordExposure;
        if (smokeExposure != null) smokeExposure.ProtectionChanged += OnProtectionChanged;
        BuildProtectionCues();
        JourneyDirector.Instance?.Request(JourneyState.Awareness);
        if (alarm != null && CampusLessonDirector.Instance==null) alarm.PlayDelayed(9f);
        resolution = FindObjectOfType<ResolutionCoordinator>();
        if (resolution != null) resolution.OutcomeClaimed += OnOutcome;
    }
    public void BeginTimedExercise()
    {
        startTime = Time.time;
        report = new CampusDebriefReport { initialHealth = health != null ? health.CurrentHealth : 0 };
        SessionLogger.Write("DEBRIEF_RUN_STARTED");
    }

    private void RecordLoss(float amount, HealthTimeController.LossSource source, Vector3? position)
    {
        if (!finished) report?.AddLoss(amount, source, position);
    }

    private void RecordExposure(float seconds, bool protection, bool teleport)
    {
        if (finished || report == null) return;
        report.ObserveProtection(protection, Time.time - startTime);
        report.MeasureSmoke(seconds, protection, teleport);
    }

    private int ExerciseFloor(int headFloor) => locomotion == null ? headFloor :
        Mathf.Clamp(Mathf.FloorToInt((locomotion.transform.position.y + .15f) / 3.8f) + 1, 1, 4);
    private void Update()
    {
        if (trackedHead == null || finished) return;
        bool towelProtected = smokeExposure != null && smokeExposure.IsProtected;
        healthBar?.SetTowelProtection(towelProtected);
        int floor = Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y - 0.8f) / 3.8f) + 1, 1, 4);
        report?.Observe(ExerciseFloor(floor), CampusLessonDirector.Instance?.TargetTowel, towelProtected, Time.time - startTime);
        if (floorLabel != null) floorLabel.text = $"{floor}F   HP {Mathf.RoundToInt((HealthTimeController.Instance != null ? HealthTimeController.Instance.HealthPercent : 1) * 100)} / 血量";
        if (floorLabel != null)
        {
            string status = StatusLine(smokeExposure != null && smokeExposure.InSmoke,
                                       smokeExposure != null && smokeExposure.IsProtected,
                                       smokeExposure != null && smokeExposure.TowelReady);
            if (status.Length > 0) floorLabel.text += "\n" + status;
        }
        if (floor != previousFloor)
        {
            previousFloor = floor;
            SessionLogger.Write("CAMPUS_FLOOR", "{\"floor\":" + floor + "}");
            if (floor < 4) JourneyDirector.Instance?.Request(JourneyState.Descending);
        }
    }
    /// <summary>HUD 第二行状态文案：烟中已防护 / 烟中未防护 / 烟外毛巾就位 / 无提示。</summary>
    internal static string StatusLine(bool inSmoke, bool isProtected, bool towelReady)
    {
        if (inSmoke)
            return isProtected
                ? "<color=#69d2ff>防护生效 · 掉血减缓 / Protected · damage reduced</color>"
                : "<color=#ffc676>烟雾区 · 尽快离开 / Smoke · Move out</color>";
        if (towelReady)
            return "<color=#69d2ff>湿毛巾已就位 / Wet towel ready</color>";
        return string.Empty;
    }

    private void OnOutcome(ResolutionCoordinator.Outcome outcome)
    {
        finished = true;
        CampusMessagePresenter.Instance?.Hide();
        foreach (var teleporter in FindObjectsOfType<VRTeleporter>()) teleporter.enabled = false;
        if (alarm != null) alarm.Stop();
        JourneyDirector.Instance?.Request(JourneyState.Debrief);
        if (debriefLabel != null)
        {
            int elapsed = Mathf.RoundToInt(Time.time - startTime);
            string result = outcome == ResolutionCoordinator.Outcome.Victory ?
                "主任务完成 · 已到达操场\nMain objective complete · Schoolyard reached\n" : "";
            debriefLabel.text = result + $"本次练习用时 {elapsed / 60:D2}:{elapsed % 60:D2}  /  Practice time\n" +
                "识别警报 · 跟随标识 · 路线受阻时重新选择\nNotice cues · Follow signs · Choose another route\n" +
                "请勿使用电梯或跳窗逃生。\nAvoid elevators and window escape.";
        }
        var lesson = CampusLessonDirector.Instance;
        if (report == null || lesson == null) return;

        int endFloor = ExerciseFloor(previousFloor);
        report.duration = Time.time - startTime;
        report.success = outcome == ResolutionCoordinator.Outcome.Victory;
        report.depleted = health != null && health.IsDepleted;
        report.endHealth = health != null ? health.CurrentHealth : 0;
        bool inRestroom = lesson.Restroom != null && trackedHead != null && lesson.Restroom.bounds.Contains(trackedHead.position);
        report.endLocation = report.success ? "一楼室外操场安全集合点" : inRestroom ? "四楼 411 卫生间" :
            endFloor > 0 ? endFloor + " 楼" : "未记录位置";
        report.endLocationEnglish = report.success ? "Schoolyard assembly point (1F)" : inRestroom ? "Restroom 411 (4F)" :
            endFloor > 0 ? $"Floor {endFloor}" : "Location unavailable";
        report.Observe(endFloor, lesson.TargetTowel, smokeExposure != null && smokeExposure.IsProtected, report.duration);
        report.Finish(lesson, student);
        SessionLogger.Write("DEBRIEF_SUMMARY", JsonUtility.ToJson(report));
        SessionLogger.Instance?.Flush();

        var panel = resolution.GetComponent<SystemUIView>()?.OutcomePanel(outcome);
        var navigation = resolution.GetComponent<SystemUINavigation>();
        var font = debriefLabel != null ? debriefLabel.font : CampusMessagePresenter.Instance?.DisplayFont;
        if (panel != null && navigation != null && font != null)
        {
            var debrief = new CampusDebriefView(panel, font, navigation, resolution.GetComponent<SystemUIAudio>(), report);
            StartCoroutine(debrief.Play());
        }
    }
    private void OnDestroy()
    {
        if (resolution != null) resolution.OutcomeClaimed -= OnOutcome;
        if (health != null) health.HealthLost -= RecordLoss;
        if (smokeExposure != null) smokeExposure.ExposureMeasured -= RecordExposure;
        if (smokeExposure != null) smokeExposure.ProtectionChanged -= OnProtectionChanged;
    }

    // ===== 湿毛巾防护：文字与音效反馈 =====
    private AudioClip protectionOnCue, protectionOffCue;

    private void BuildProtectionCues()
    {
        protectionOnCue = MakeTone(new[] { 660f, 990f }, .16f, .35f);
        protectionOffCue = MakeTone(new[] { 520f }, .14f, .25f);
    }

    private void OnProtectionChanged(bool active)
    {
        AudioClip clip = active ? protectionOnCue : protectionOffCue;
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
    }

    /// <summary>合成一段短提示音，避免依赖外部音频资源。</summary>
    internal static AudioClip MakeTone(float[] frequencies, float stepSeconds, float volume)
    {
        const int sampleRate = 44100;
        int perStep = Mathf.Max(1, Mathf.RoundToInt(stepSeconds * sampleRate));
        int total = perStep * frequencies.Length;
        var data = new float[total];
        for (int s = 0; s < frequencies.Length; s++)
        {
            float f = frequencies[s];
            for (int i = 0; i < perStep; i++)
            {
                int index = s * perStep + i;
                float t = i / (float)sampleRate;
                // 两端各 8ms 淡入淡出，避免爆音
                float fade = Mathf.Min(1f, Mathf.Min(i, perStep - 1 - i) / (sampleRate * .008f));
                data[index] = Mathf.Sin(2f * Mathf.PI * f * t) * volume * Mathf.Clamp01(fade);
            }
        }
        var clip = AudioClip.Create("towel_protection", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
