using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Step = CampusClassroomTutorial.Step;

internal sealed class CampusNavigationPanels
{
    private enum Panel { None, Map, Tasks }

    internal const float ZoomSeconds = .5f;
    private static readonly Vector3 MapCorner = new Vector3(-.45f, -.34f, 1.45f);
    private static readonly Vector3 TaskCorner = new Vector3(.57f, .26f, 1.45f);
    private static readonly Vector3 PanelCenter = new Vector3(0, 0, 1.45f);
    private static readonly Color Ink = new Color(.025f, .07f, .085f, .96f);

    private readonly GameObject root;
    private readonly CanvasGroup mapGroup, taskGroup;
    private readonly TMP_Text mapLabel, taskLabel, taskText;
    private readonly Image mapBadge;
    private readonly RectTransform mapRect, mapContent, taskRect;
    private float mapZoom, taskZoom, taskHeight = 145f;
    private Panel requested;
    private bool wasX, wasY;

    internal bool IsExpanded => requested != Panel.None || mapZoom > 0 || taskZoom > 0;

    internal CampusNavigationPanels(Transform head, TMP_FontAsset font, GameObject minimap)
    {
        root = new GameObject("Campus map and task HUD");
        root.transform.SetParent(head, false);

        mapGroup = CampusTutorialView.CanvasAt("Minimap panel", root.transform, MapCorner, .0017f);
        mapBadge = CampusTutorialView.Picture(mapGroup.transform, Vector2.zero, new Vector2(205, 49), Ink);
        mapRect = mapBadge.rectTransform;
        mapLabel = CampusTutorialView.CreateLabel(font, mapGroup.transform, Vector2.zero, new Vector2(200, 42), 21, "X   小地图");
        var mapBody = Object.Instantiate(minimap, mapGroup.transform, false);
        foreach (var anchor in mapBody.GetComponents<WorldSpaceUIAnchor>()) anchor.enabled = false;
        foreach (var foreground in mapBody.GetComponents<CampusUIForeground>()) foreground.enabled = false;
        foreach (var label in mapBody.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * .65f;
            label.enableAutoSizing = true;
        }
        mapBody.GetComponent<Canvas>().overrideSorting = false;
        mapBody.transform.localRotation = Quaternion.identity;
        mapContent = mapBody.GetComponent<RectTransform>();
        mapBody.SetActive(true);

        taskGroup = CampusTutorialView.CanvasAt("Task details panel", root.transform, TaskCorner, .0017f);
        taskRect = CampusTutorialView.Picture(taskGroup.transform, Vector2.zero, new Vector2(205, 52), Ink).rectTransform;
        taskLabel = CampusTutorialView.CreateLabel(font, taskGroup.transform, Vector2.zero, new Vector2(196, 48), 21, "Y   任务详情");
        taskText = CampusTutorialView.CreateLabel(font, taskGroup.transform, Vector2.zero, new Vector2(300, 124), 19, "");
        taskText.alignment = TextAlignmentOptions.Left;
        root.AddComponent<CampusUIForeground>().Configure(Shader.Find("Campus/Foreground UI"));
        Hide(false, false);
    }

    internal void ShowPractice(Step step, float age, float dt)
    {
        if (step < Step.MapDemo) return;
        root.SetActive(true);
        mapGroup.gameObject.SetActive(true);
        taskGroup.gameObject.SetActive(step >= Step.TaskDemo);
        mapGroup.alpha = step == Step.MapDemo ? Mathf.SmoothStep(0, 1, age) : 1;
        taskGroup.alpha = step == Step.TaskDemo ? Mathf.SmoothStep(0, 1, age) : 1;
        SetTaskText("<b>任务详情</b>\n查看教室出口\n\n到门内侧的标记处。");
        requested = step == Step.MapClose ? Panel.Map : step == Step.TaskClose ? Panel.Tasks : Panel.None;
        Animate(dt);
    }

    internal void ShowExercise(bool x, bool y, float dt, string details)
    {
        root.SetActive(true);
        mapGroup.gameObject.SetActive(true);
        taskGroup.gameObject.SetActive(true);
        mapGroup.alpha = taskGroup.alpha = 1;
        if (y && !wasY) requested = requested == Panel.Tasks ? Panel.None : Panel.Tasks;
        else if (x && !wasX) requested = requested == Panel.Map ? Panel.None : Panel.Map;
        wasX = x;
        wasY = y;
        SetTaskText(details);
        Animate(dt);
    }

    internal void Hide(bool x, bool y, bool fold = false)
    {
        wasX = x;
        wasY = y;
        if (fold)
        {
            requested = Panel.None;
            mapZoom = taskZoom = 0;
        }
        root.SetActive(false);
    }

    private void SetTaskText(string text)
    {
        if (taskText.text == text) return;
        taskText.text = text;
        taskHeight = Mathf.Clamp(taskText.GetPreferredValues(text, 300, 0).y + 26f, 145f, 255f);
        taskText.rectTransform.sizeDelta = new Vector2(300, taskHeight - 24f);
    }

    private void Animate(float dt)
    {
        float amount = dt / ZoomSeconds;
        mapZoom = Mathf.MoveTowards(mapZoom, requested == Panel.Map && taskZoom == 0 ? 1f : 0f, amount);
        taskZoom = Mathf.MoveTowards(taskZoom, requested == Panel.Tasks && mapZoom == 0 ? 1f : 0f, amount);
        float mapScale = Mathf.SmoothStep(0f, 1f, mapZoom);
        float taskScale = Mathf.SmoothStep(0f, 1f, taskZoom);
        mapGroup.transform.localPosition = Vector3.Lerp(MapCorner, PanelCenter, mapScale);
        mapGroup.transform.localScale = Vector3.one * Mathf.Lerp(.0017f, .0033f, mapScale);
        mapContent.localScale = Vector3.one * Mathf.Lerp(.56f, 1f, mapScale);
        mapContent.anchoredPosition3D = new Vector3(0, Mathf.Lerp(67f, 0, mapScale), 0);
        mapLabel.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(0, -106, mapScale));
        mapLabel.alpha = 1f - mapScale;
        mapBadge.canvasRenderer.SetAlpha(1f - mapScale);
        mapRect.sizeDelta = Vector2.Lerp(new Vector2(205, 49), new Vector2(268, 44), mapScale);
        mapRect.anchoredPosition = mapLabel.rectTransform.anchoredPosition;
        taskGroup.transform.localPosition = Vector3.Lerp(TaskCorner, PanelCenter, taskScale);
        taskGroup.transform.localScale = Vector3.one * Mathf.Lerp(.0017f, .0033f, taskScale);
        taskRect.sizeDelta = Vector2.Lerp(new Vector2(205, 52), new Vector2(324, taskHeight), taskScale);
        taskLabel.alpha = 1f - taskScale;
        taskText.alpha = taskScale;
    }

    internal void Dispose() => Object.Destroy(root);
}
