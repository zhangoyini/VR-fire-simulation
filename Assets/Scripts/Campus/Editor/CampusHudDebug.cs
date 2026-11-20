#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only HUD tuning overlay for the CampusTraining tutorial.
// Enters Play mode automatically, never ships in a build and never edits saved assets.
[InitializeOnLoad]
public static class CampusHudDebugBootstrap
{
    static CampusHudDebugBootstrap()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SceneManager.GetActiveScene().name.Equals("CampusTraining", StringComparison.Ordinal)) return;
            if (UnityEngine.Object.FindObjectOfType<CampusHudDebug>() != null) return;
            var host = new GameObject("HUD debug (temporary)");
            host.AddComponent<CampusHudDebug>();
        };
    }
}

public sealed class CampusHudDebug : MonoBehaviour
{
    private enum Target { None, Prompt, Notice, Task, Warning, Lesson }

    private const string LongPromptA =
        "想暂停或退出时，按下左摇杆打开菜单。\n现在试一次。用手柄射线指向「继续」，按下扳机确认返回练习。\n<size=80%>Click the left stick to open the pause menu. Try it now.\nAim the controller ray at Resume and press the trigger to confirm.</size>";
    private const string LongPromptB =
        "蓝色圆环表示可以到达，松开摇杆即可瞬移。\n红色叉号表示无法到达，请重新选择落点。\n<size=80%>Blue ring means you can teleport there.\nRed cross means choose another landing point.</size>";
    private const string LongPromptC =
        "手靠近高亮处，按住扳机握住窗扇，再向外推。\n<size=80%>Reach for the highlight, hold the trigger and push the window outward.</size>";
    private const string LongPromptD =
        "按左手柄的 Y 查看任务详情，再按 Y 收起\n<size=80%>Press Y on the left controller to open or close Tasks.</size>";
    private const string LongNotice =
        "可选支线 · 轮椅救援\n与同学交谈，选择“跟我来”，带他到避难区。\n<size=83%>Optional · Talk to your classmate, choose “Follow me”, and lead him to the refuge.</size>";
    private const string LongTask =
        "<b>任务详情 / Tasks</b>\n<color=#80551b><b>当前目标</b></color>  <b>任务 2 · 浸湿毛巾并下到三楼</b>  <size=80%>2 · Wet towel, reach 3F · Active</size>\n" +
        "用空闲手开水龙头并浸湿毛巾。捂住口鼻，沿楼梯下到三楼。\n<size=83%>Use your free hand to wet the towel. Cover your nose and mouth, then take the stairs to 3F.</size>\n" +
        "<color=#315c75>进行中</color>  可选支线 · 轮椅救援  <size=80%>Optional · Rescue · Active</size>\n" +
        "走近同学并选择“跟我来”。每次传送后，同学会平滑跟上；带他进入避难区。\n<size=83%>Approach your classmate and choose “Follow me”. He will roll after you each time you teleport.</size>\n" +
        "<color=#315c75>待处理</color>  任务 4 · 打开通往操场的出口  <size=80%>4 · Open the exit · Active</size>\n" +
        "沿一楼走廊到大厅双开门，握住扳机推开任意一扇。\n<size=83%>Follow the 1F corridor to the double doors. Hold the trigger to open either door.</size>\n" +
        "<color=#80551b>安全提示 · Safety</color>  烟雾暴露 3 秒后扣血，传送也计时；湿毛巾只能减缓扣血，不能过滤一氧化碳。\n" +
        "<size=83%>Smoke costs HP after 3 s, including during teleport. A wet towel slows HP loss but cannot filter carbon monoxide.</size>";
    private const string LongWarning =
        "发生火灾时，请勿试图跳窗逃生！\nDo not attempt to jump out of a window to escape during a fire";
    private const string LessonBody =
        "火灾发生后，先判断门口与走廊的烟雾方向。\n" +
        "用湿毛巾捂住口鼻，压低身体，沿疏散标识前往最近的楼梯。\n" +
        "路线受阻时不要折返，改走东侧 B 楼梯，再沿一楼出口到操场集合。\n" +
        "<size=80%>Check the smoke at the door first. Cover your nose and mouth with a wet towel, keep low and follow the exit signs. " +
        "If the route is blocked, use Stair B on the east side and leave through the ground floor.</size>";

    private static readonly string[] PromptSamples = { LongPromptA, LongPromptB, LongPromptC, LongPromptD };

    private CampusLessonDirector director;
    private Target target;
    private string label = "";
    private TMP_Text text;
    private RectTransform card;
    private Transform canvas;
    private CanvasGroup group;
    private bool allowDistance;
    private float padX, padY;
    private float w, h, fontSize, distance, scale;
    private float origW, origH, origFont, origDistance, origScale;
    private bool autoHeight;
    private int sample;
    private TMP_Text readout;
    private readonly Dictionary<KeyCode, float> repeatAt = new Dictionary<KeyCode, float>();

    private void Awake()
    {
        BuildReadout();
        Debug.Log("[HUD] 调试组件已就绪：F1 提示条 / F2 任务通知 / F3 任务详情 / F4 安全警告 / F5 教学卡片 / Esc 退出。\n" +
                  "← → 调宽，↑ ↓ 调高，+ - 调字号，[ ] 调距离，A 自动高度，G 换文案，R 复位，P 打印数值。");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) Show(Target.Prompt);
        else if (Input.GetKeyDown(KeyCode.F2)) Show(Target.Notice);
        else if (Input.GetKeyDown(KeyCode.F3)) Show(Target.Task);
        else if (Input.GetKeyDown(KeyCode.F4)) Show(Target.Warning);
        else if (Input.GetKeyDown(KeyCode.F5)) Show(Target.Lesson);
        if (target == Target.None) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Stop(); return; }
        if (Hold(KeyCode.LeftArrow)) ChangeSize(-20f, 0f);
        if (Hold(KeyCode.RightArrow)) ChangeSize(20f, 0f);
        if (Hold(KeyCode.UpArrow)) ChangeSize(0f, 8f);
        if (Hold(KeyCode.DownArrow)) ChangeSize(0f, -8f);
        if (Hold(KeyCode.Equals) || Hold(KeyCode.KeypadPlus)) ChangeFont(2f);
        if (Hold(KeyCode.Minus) || Hold(KeyCode.KeypadMinus)) ChangeFont(-2f);
        if (Hold(KeyCode.LeftBracket)) ChangeDistance(-0.05f);
        if (Hold(KeyCode.RightBracket)) ChangeDistance(0.05f);
        if (Input.GetKeyDown(KeyCode.A)) { autoHeight = !autoHeight; Changed(); }
        if (Input.GetKeyDown(KeyCode.P)) Print();
        if (Input.GetKeyDown(KeyCode.R)) ResetValues();
        if (Input.GetKeyDown(KeyCode.G))
        {
            sample = (sample + 1) % PromptSamples.Length;
            Show(Target.Prompt);
        }
    }

    // The tutorial writes the same rect every frame; reapply after it has run.
    private void LateUpdate() => Apply();

    private void Show(Target next)
    {
        director = director != null ? director : FindObjectOfType<CampusLessonDirector>();
        if (director == null)
        {
            Debug.LogWarning("[HUD] 没有找到 CampusLessonDirector，确认当前场景是 CampusTraining。");
            return;
        }
        ClearPrevious();
        switch (next)
        {
            case Target.Prompt: ShowPrompt(); break;
            case Target.Notice: ShowNotice(); break;
            case Target.Task: ShowTask(); break;
            case Target.Warning: ShowWarning(); break;
            case Target.Lesson: ShowLesson(); break;
        }
    }

    private void ShowPrompt()
    {
        object tutorial = Get(director, "tutorial");
        if (tutorial == null)
        {
            Debug.LogWarning("[HUD] 当前不在操作教学阶段（tutorial 为空）。请从场景开头进入，或在菜单里重开一局。");
            return;
        }
        object view = Get(tutorial, "view");
        var prompt = (TMP_Text)Get(view, "prompt");
        var background = (RectTransform)Get(view, "promptBackground");
        var promptGroup = (CanvasGroup)Get(view, "promptGroup");
        if (prompt == null || background == null || promptGroup == null) return;
        promptGroup.gameObject.SetActive(true);
        prompt.text = PromptSamples[sample];
        Bind(Target.Prompt, "提示条 Current instruction", promptGroup.transform, background, prompt, promptGroup, true);
    }

    private void ShowNotice()
    {
        object panels = Get(director, "panels");
        if (panels == null) { Debug.LogWarning("[HUD] 任务 HUD 尚未创建。"); return; }
        var panelRoot = (GameObject)Get(panels, "root");
        if (panelRoot != null) panelRoot.SetActive(true);
        var noticeGroup = (CanvasGroup)Get(panels, "noticeGroup");
        var noticeText = (TMP_Text)Get(panels, "noticeText");
        if (noticeGroup == null || noticeText == null) return;
        var background = noticeGroup.GetComponentInChildren<Image>(true);
        if (background == null) return;
        noticeGroup.gameObject.SetActive(true);
        noticeText.text = LongNotice;
        Bind(Target.Notice, "任务通知 Exercise task notification", noticeGroup.transform, background.rectTransform, noticeText, noticeGroup, true);
    }

    private void ShowTask()
    {
        object panels = Get(director, "panels");
        if (panels == null) { Debug.LogWarning("[HUD] 任务 HUD 尚未创建。"); return; }
        var panelRoot = (GameObject)Get(panels, "root");
        if (panelRoot != null) panelRoot.SetActive(true);
        var taskGroup = (CanvasGroup)Get(panels, "taskGroup");
        var taskText = (TMP_Text)Get(panels, "taskText");
        var taskRect = (RectTransform)Get(panels, "taskRect");
        if (taskGroup == null || taskText == null || taskRect == null) return;
        taskGroup.gameObject.SetActive(true);
        // Mimic the expanded layout so the panel is readable while tuning.
        taskGroup.transform.localPosition = new Vector3(0f, 0f, 1.45f);
        taskGroup.transform.localScale = Vector3.one * 0.0033f;
        taskRect.sizeDelta = new Vector2(494f, 320f);
        taskText.rectTransform.sizeDelta = new Vector2(470f, 300f);
        taskText.text = LongTask;
        Bind(Target.Task, "任务详情 Task details", taskGroup.transform, taskRect, taskText, taskGroup, true);
    }

    private void ShowWarning()
    {
        var presenter = CampusMessagePresenter.Instance;
        if (presenter == null) { Debug.LogWarning("[HUD] 没有找到 CampusMessagePresenter。"); return; }
        presenter.Show(LongWarning);
        var panel = (Transform)Get(presenter, "panel");
        var warningText = (TMP_Text)Get(presenter, "text");
        if (panel == null || warningText == null) return;
        Set(presenter, "hideAt", float.MaxValue);
        var background = panel.GetComponentInChildren<Image>(true);
        if (background == null) return;
        Bind(Target.Warning, "安全警告 Safety warning", panel, background.rectTransform, warningText, null, true);
    }

    private void ShowLesson()
    {
        var layout = director.GetComponent<CampusLessonLayout>();
        var panel = (GameObject)Get(director, "lessonPanel");
        if (layout == null || panel == null) return;
        panel.SetActive(true);
        var title = (TMP_Text)Get(layout, "title");
        var body = (TMP_Text)Get(layout, "body");
        var footer = (TMP_Text)Get(layout, "footer");
        var textGroup = (CanvasGroup)Get(layout, "textGroup");
        if (title == null || body == null) return;
        title.text = "主任务 · 逃到空旷处 / Main objective";
        body.text = LessonBody;
        if (footer != null) footer.text = "即将恢复自由活动。 / Control resumes shortly.";
        // Arrange after the copy is in place so the card measures the new text.
        layout.ApplyArtwork();
        layout.Arrange(true);
        layout.Tick(1f);
        if (textGroup != null) textGroup.alpha = 1f;
        // Bind the body to itself: resizing grows the paragraph, not the whole card.
        Bind(Target.Lesson, "教学卡片 Lesson card", body.transform.parent, body.rectTransform, body, textGroup, false);
    }

    private void ClearPrevious()
    {
        if (target == Target.Prompt || target == Target.Notice || target == Target.Task)
        {
            if (group != null) group.gameObject.SetActive(false);
        }
        if (target == Target.Warning)
        {
            var presenter = CampusMessagePresenter.Instance;
            if (presenter != null) presenter.Hide();
        }
        if (target == Target.Lesson)
        {
            var panel = (GameObject)Get(director, "lessonPanel");
            if (panel != null) panel.SetActive(false);
        }
    }

    private void Bind(Target owner, string name, Transform canvasRoot, RectTransform background,
        TMP_Text label_, CanvasGroup canvasGroup, bool distanceAllowed)
    {
        target = owner;
        director.ManualTickForChecks = true;
        label = name;
        canvas = canvasRoot;
        card = background;
        text = label_;
        group = canvasGroup;
        allowDistance = distanceAllowed;
        padX = Mathf.Max(0f, card.sizeDelta.x - text.rectTransform.sizeDelta.x);
        padY = Mathf.Max(0f, card.sizeDelta.y - text.rectTransform.sizeDelta.y);
        w = card.sizeDelta.x;
        h = card.sizeDelta.y;
        fontSize = text.enableAutoSizing
            ? (text.fontSizeMax > 1f ? text.fontSizeMax : text.fontSize)
            : text.fontSize;
        distance = distanceAllowed ? canvas.localPosition.z : 0f;
        scale = canvas.localScale.x;
        origW = w; origH = h; origFont = fontSize; origDistance = distance; origScale = scale;
        autoHeight = false;
        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
        Changed();
    }

    private void ChangeSize(float dw, float dh)
    {
        w = Mathf.Max(160f, w + dw);
        h = Mathf.Max(48f, h + dh);
        autoHeight = false;
        Changed();
    }

    private void ChangeFont(float delta)
    {
        fontSize = Mathf.Clamp(fontSize + delta, 8f, 72f);
        Changed();
    }

    private void ChangeDistance(float delta)
    {
        if (!allowDistance) { Debug.Log("[HUD] 教学卡片跟随固定世界位置，不能调距离。"); return; }
        distance = Mathf.Clamp(distance + delta, 0.45f, 4f);
        Changed();
    }

    private void Changed()
    {
        Apply();
        Print();
        UpdateReadout();
    }

    private void Apply()
    {
        if (target == Target.None || text == null || card == null) return;
        if (autoHeight)
        {
            float preferred = text.GetPreferredValues(text.text, Mathf.Max(40f, w - padX), 0f).y;
            h = Mathf.Clamp(preferred + padY + 4f, 48f, 900f);
        }
        var cardSize = new Vector2(w, h);
        if (card.sizeDelta != cardSize) card.sizeDelta = cardSize;
        var textSize = new Vector2(Mathf.Max(20f, w - padX), Mathf.Max(16f, h - padY));
        if (text.rectTransform.sizeDelta != textSize) text.rectTransform.sizeDelta = textSize;
        if (Mathf.Abs(text.fontSize - fontSize) > 0.01f) text.fontSize = fontSize;
        if (group != null && group.alpha < 1f) group.alpha = 1f;
        if (canvas != null)
        {
            if (allowDistance && !Mathf.Approximately(canvas.localPosition.z, distance))
                canvas.localPosition = new Vector3(canvas.localPosition.x, canvas.localPosition.y, distance);
            if (!Mathf.Approximately(canvas.localScale.x, scale))
                canvas.localScale = Vector3.one * scale;
        }
    }

    private void ResetValues()
    {
        w = origW; h = origH; fontSize = origFont; distance = origDistance; scale = origScale;
        autoHeight = false;
        Changed();
    }

    private void Print()
    {
        if (text == null) return;
        text.ForceMeshUpdate();
        string state = text.isTextOverflowing ? "溢出" : "正常";
        Debug.Log($"[HUD] {label} | W={w:F0} H={h:F0} fontSize={fontSize:F1} distance={distance:F2} scale={scale:F4} " +
                  $"| {state} | 需要高度≈{text.GetPreferredValues(text.text, Mathf.Max(40f, w - padX), 0f).y + padY:F0}", text);
    }

    private void UpdateReadout()
    {
        if (readout == null) return;
        text?.ForceMeshUpdate();
        string state = text != null && text.isTextOverflowing ? "OVERFLOW" : "ok";
        readout.text = $"W {w:F0}  H {h:F0}  F {fontSize:F0}  D {distance:F2}  {state}\n" +
                       "F1 prompt  F2 notice  F3 tasks  F4 warning  F5 lesson\n" +
                       "arrows size   +/- font   [ ] dist   A auto   R reset   Esc quit";
    }

    private void BuildReadout()
    {
        var camera = Camera.main != null ? Camera.main.transform : transform;
        var host = new GameObject("HUD debug readout", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        host.transform.SetParent(camera, false);
        host.transform.localPosition = new Vector3(0f, -0.235f, 0.95f);
        host.transform.localScale = Vector3.one * 0.0009f;
        host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        host.GetComponent<CanvasGroup>().interactable = host.GetComponent<CanvasGroup>().blocksRaycasts = false;
        var background = new GameObject("readout background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(host.transform, false);
        background.rectTransform.sizeDelta = new Vector2(600f, 104f);
        background.color = new Color(0f, 0f, 0f, 0.74f);
        background.raycastTarget = false;
        var label = new GameObject("readout text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(host.transform, false);
        label.rectTransform.sizeDelta = new Vector2(560f, 92f);
        label.font = LoadFont();
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "HUD debug";
        readout = label;
    }

    private static TMP_FontAsset LoadFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        return font != null ? font : TMP_Settings.defaultFontAsset;
    }

    private bool Hold(KeyCode key, float interval = 0.09f)
    {
        if (!Input.GetKey(key)) return false;
        if (!Input.GetKeyDown(key) && Time.unscaledTime < Next(key)) return false;
        repeatAt[key] = Time.unscaledTime + interval;
        return true;
    }

    private float Next(KeyCode key) => repeatAt.TryGetValue(key, out float time) ? time : 0f;

    private void Stop()
    {
        ResetValues();
        if (director != null) director.ManualTickForChecks = false;
        var presenter = CampusMessagePresenter.Instance;
        if (presenter != null) Set(presenter, "hideAt", Time.time);
        target = Target.None;
        text = null; card = null; canvas = null; group = null;
        if (readout != null) readout.text = "HUD debug stopped";
        Debug.Log("[HUD] 已退出调试，恢复正常流程。");
    }

    private static object Get(object owner, string name)
    {
        if (owner == null) return null;
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? field.GetValue(owner) : null;
    }

    private static void Set(object owner, string name, object value)
    {
        if (owner == null) return;
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(owner, value);
    }
}
#endif
