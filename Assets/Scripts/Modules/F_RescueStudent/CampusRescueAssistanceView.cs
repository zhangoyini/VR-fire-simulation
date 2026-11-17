using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Assistance feedback stays out of the centre of the view and has no colliders.
public sealed class CampusRescueAssistanceView : MonoBehaviour
{
    private const string Reminder =
        "传送后，同学会沿路线跟上你\nYour classmate will follow after each teleport";
    private Transform head, panel, routeAnchor, routeRoot;
    private TextMeshPro status;
    private CampusLessonRoute route;
    private bool visible;
    private Vector3[] waypoints;
    private Material markerMaterial;

    public void Begin(Transform trackedHead, Vector3[] points)
    {
        head = trackedHead;
        if (head == null) return;
        waypoints = points;
        EnsureView();
        if (waypoints != null && waypoints.Length > 0) route.Show(waypoints);
        SetVisible(true);
        Refresh();
    }

    private void EnsureView()
    {
        if (panel != null) return;
        markerMaterial = Resources.Load<Material>("TeleportDestinationMarker");
        panel = new GameObject("Assisting classmate reminder", typeof(RectTransform)).transform;
        panel.SetParent(head, false);
        panel.localPosition = new Vector3(0, -.29f, 1.7f);
        status = panel.gameObject.AddComponent<TextMeshPro>();
        status.font = CampusMessagePresenter.Instance != null ?
            CampusMessagePresenter.Instance.DisplayFont : TMP_Settings.defaultFontAsset;
        status.fontSize = .42f;
        status.rectTransform.sizeDelta = new Vector2(1.7f, .3f);
        status.alignment = TextAlignmentOptions.Center;
        status.color = new Color(.85f, .97f, 1f);
        status.enableWordWrapping = true;
        panel.gameObject.AddComponent<CampusUIForeground>().Configure(null);

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
        if (panel != null) panel.gameObject.SetActive(value);
        if (routeRoot != null) routeRoot.gameObject.SetActive(value);
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
        routeAnchor.position = transform.position + Vector3.up * 1.65f;
        route.Tick(routeAnchor);
        Refresh();
    }

    private void Refresh()
    {
        if (status == null || head == null) return;
        string objective = "同学正在跟随 · 前往二楼避难区\nClassmate following · Go to the 2F refuge";
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector3 toRoute = waypoints[Mathf.Min(route.WaypointIndex, waypoints.Length - 1)] - head.position;
            toRoute.y = 0;
            float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward, Vector3.up), toRoute, Vector3.up);
            if (Mathf.Abs(turn) >= 35)
                objective = Mathf.Abs(turn) > 135 ?
                    "同学正在跟随 · 避难区在身后\nClassmate following · Refuge behind you" : turn > 0 ?
                    "同学正在跟随 · 避难区在右侧\nClassmate following · Refuge on your right" :
                    "同学正在跟随 · 避难区在左侧\nClassmate following · Refuge on your left";
        }
        Vector3 toChair = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
        float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward, Vector3.up), toChair, Vector3.up);
        string location = Mathf.Abs(angle) < 55 ? "" : Mathf.Abs(angle) > 135 ?
            "\n同学在身后 / Classmate behind you" : angle > 0 ?
            "\n同学在右侧 / Classmate on your right" : "\n同学在左侧 / Classmate on your left";
        string message = objective + "\n" + Reminder + location;
        if (status.text != message) status.text = message;
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
