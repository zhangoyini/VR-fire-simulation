using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Stage = CampusRescueStudent.EscortStage;

// A centred mission-style card stays readable over the world and other HUD panels.
public sealed class CampusRescueAssistanceView : MonoBehaviour
{
    private Transform head, panel, routeAnchor, routeRoot;
    private TMP_Text status;
    private CampusRescueNoticeCard noticeCard;
    private CampusLessonRoute route;
    private bool visible;
    private Material markerMaterial;
    private CampusRescueStudent student;
    private Stage CurrentStage => student != null ? student.Stage : Stage.Door;

    public void Begin(Transform trackedHead, Vector3[] points)
    {
        head = trackedHead;
        if (head == null) return;
        student = GetComponent<CampusRescueStudent>();
        EnsureView();
        if (points != null && points.Length > 0) route.Show(points);
        SetVisible(true);
    }

    private void EnsureView()
    {
        if (panel != null) return;
        markerMaterial = Resources.Load<Material>("TeleportDestinationMarker");
        var font = CampusMessagePresenter.Instance != null ?
            CampusMessagePresenter.Instance.DisplayFont : TMP_Settings.defaultFontAsset;
        noticeCard = CampusRescueNoticeCard.Create(head, font, "Assisting classmate reminder");
        panel = noticeCard.transform;
        status = noticeCard.Label;

        // World-space roots must not translate or rotate with the moving chair.
        routeRoot = new GameObject("Wheelchair refuge route").transform;
        route = routeRoot.gameObject.AddComponent<CampusLessonRoute>();
        routeAnchor = new GameObject("Wheelchair route origin").transform;
        routeAnchor.SetParent(routeRoot, false);
        var arrows = new Transform[3];
        for (int i = 0; i < arrows.Length; i++)
        {
            var line = MakeLine("Refuge route chevron", routeRoot);
            line.positionCount = 3;
            line.SetPositions(new[] { new Vector3(-.13f, 0, -.1f), Vector3.zero, new Vector3(.13f, 0, -.1f) });
            line.startColor = line.endColor = new Color(.2f, .9f, 1);
            arrows[i] = line.transform;
        }
        route.Configure(arrows);
    }

    private LineRenderer MakeLine(string label, Transform parent)
    {
        var go = new GameObject(label);
        go.layer = Physics.IgnoreRaycastLayer;
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = markerMaterial;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = .025f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    public void SetVisible(bool value)
    {
        visible = value;
        Refresh();
        if (routeRoot != null) routeRoot.gameObject.SetActive(value && CurrentStage == Stage.Door);
    }

    public void Hide()
    {
        SetVisible(false);
        route?.Hide();
    }

    private void Update()
    {
        if (!visible || head == null) return;
        // Place the next arrows beyond the chair so its body cannot cover them.
        if (routeRoot.gameObject.activeSelf)
        {
            routeAnchor.position = transform.position + Vector3.up * 1.65f;
            route.Tick(routeAnchor);
        }
    }

    private void Refresh()
    {
        if (status == null || head == null) return;
        Stage stage = CurrentStage;
        string languageBreak = stage == Stage.Door ? "\n\n" : "\n";
        string message = Instruction(stage) + languageBreak + "<size=88%>" + Instruction(stage, english: true) + "</size>";
        panel.localPosition = new Vector3(0, stage == Stage.Door ? 0 : .31f, 1.3f);
        panel.localScale = Vector3.one * (stage == Stage.Door ? .0016f : .0012f);
        var messages = CampusMessagePresenter.Instance;
        bool warning = messages != null && (messages.HasPriorityWarning ||
            (messages.IsVisible && messages.CurrentMessage == CampusMessagePresenter.RescueStairWarning));
        if (visible && !warning) noticeCard.Show(message, new Color(.85f, .97f, 1f));
        else panel.gameObject.SetActive(false);
    }

    internal static string Instruction(Stage stage, bool english = false)
    {
        switch (stage)
        {
            case Stage.Highlight: return english ? "Lead your classmate through the door to the highlighted circle in the center of the refuge." :
                "请带同学穿过门，前往避难区中央的高亮圈。";
            case Stage.WaitForClassmate: return english ? "You’ve reached the circle. Wait for your classmate to catch up." :
                "已到达高亮圈，请等候同学跟上。";
            case Stage.Call: return english ? "Your classmate has arrived. Press the call button with the red bell next to the door." :
                "同学已到达避难区。请按门边墙上带红铃标识的求救按钮。";
            default: return english ? "Lead your classmate to the 2F refuge and open the door.\nOpen it all the way so the wheelchair can pass." :
                "请带同学前往二楼避难区，并打开避难区的门。\n开门时请将门完全打开，给轮椅留出通行空间。";
        }
    }

    private void OnDestroy()
    {
        if (panel != null) DestroyView(panel.gameObject);
        if (routeRoot != null) DestroyView(routeRoot.gameObject);
    }

    private static void DestroyView(GameObject target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
