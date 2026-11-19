using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep safety notices below the actual health slider, including while looking around.
[DefaultExecutionOrder(200)]
public sealed class CampusCompactWarning : MonoBehaviour
{
    private Transform head;
    private RectTransform healthRect;
    private readonly Vector3[] corners = new Vector3[4];
    private const float HalfHeight = 180f * .0013f * .5f;
    private Image warningIcon;
    private TMP_Text warningLabel;

    public static CanvasGroup Create(Transform head, TMP_FontAsset font, out TMP_Text label)
    {
        var group = CampusTutorialView.CanvasAt("Compact warning below health", head, Vector3.zero, .0013f);
        var backing = CampusTutorialView.Picture(group.transform, Vector2.zero, new Vector2(560, 180), Color.white,
            FireTrainingUIStyle.Load("panels/warning_card"));
        label = CampusTutorialView.CreateLabel(font, group.transform, Vector2.zero, new Vector2(450, 112), 22, "");
        label.fontSizeMin = 18;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        var placement = group.gameObject.AddComponent<CampusCompactWarning>();
        placement.warningLabel = label;
        placement.warningIcon = CampusTutorialView.Picture(group.transform, new Vector2(-192, 0), new Vector2(62, 62), Color.white);
        placement.warningIcon.preserveAspect = true;
        placement.warningIcon.gameObject.SetActive(false);
        placement.head = head;
        var health = Object.FindObjectOfType<Jiao.HealthTime.HealthTimeBar>(true);
        var slider = health != null ? health.GetComponentInChildren<Slider>(true) : null;
        placement.healthRect = slider != null ? slider.transform as RectTransform : null;
        placement.Place();
        return group;
    }

    public void SetMessage(string message)
    {
        string path = message == CampusMessagePresenter.ElevatorWarning ? "icons/icon_no_elevator" :
            message == CampusMessagePresenter.WindowWarning ? "icons/icon_no_jump" : null;
        warningIcon.gameObject.SetActive(path != null);
        if (path != null) FireTrainingUIStyle.Apply(warningIcon, path);
        warningLabel.rectTransform.anchoredPosition = path != null ? new Vector2(40, 0) : Vector2.zero;
        warningLabel.rectTransform.sizeDelta = new Vector2(path != null ? 370 : 450, 112);
        warningLabel.text = message;
    }

    private void LateUpdate() => Place();
    public void Place()
    {
        if (head == null) return;
        float bottom = .42f, depth = 1.6f, centreX = 0;
        if (healthRect != null)
        {
            healthRect.GetWorldCorners(corners);
            Vector3 left = head.InverseTransformPoint(corners[0]);
            Vector3 right = head.InverseTransformPoint(corners[3]);
            bottom = Mathf.Min(left.y, right.y);
            centreX = (left.x + right.x) * .5f;
            depth = (left.z + right.z) * .5f;
            if (depth < .3f) { bottom = .42f; depth = 1.6f; centreX = 0; }
        }
        transform.SetPositionAndRotation(head.TransformPoint(new Vector3(centreX, bottom - .035f - HalfHeight, depth)), head.rotation);
    }
}
