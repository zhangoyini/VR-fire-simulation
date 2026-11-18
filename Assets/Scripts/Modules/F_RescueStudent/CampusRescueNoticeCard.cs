using TMPro;
using UnityEngine;

// The escort guidance and stair warning share the mission board's centred, dark card style.
[DefaultExecutionOrder(10001)]
public sealed class CampusRescueNoticeCard : MonoBehaviour
{
    private TMP_Text label;
    private Canvas canvas;
    private CampusUIForeground foreground;
    public TMP_Text Label => label;

    public static CampusRescueNoticeCard Create(Transform head, TMP_FontAsset font, string title)
    {
        var group = CampusTutorialView.CanvasAt(title, head, new Vector3(0, 0, 1.3f), .0016f);
        CampusTutorialView.Picture(group.transform, Vector2.zero, new Vector2(620, 210),
            new Color(.025f, .07f, .085f, .96f));
        var card = group.gameObject.AddComponent<CampusRescueNoticeCard>();
        card.canvas = group.GetComponent<Canvas>();
        card.label = CampusTutorialView.CreateLabel(font, group.transform, Vector2.zero,
            new Vector2(580, 180), 24, "");
        card.foreground = group.gameObject.AddComponent<CampusUIForeground>();
        card.foreground.Configure(Shader.Find("Campus/Foreground UI"));
        card.PlaceInFront();
        group.gameObject.SetActive(false);
        return card;
    }

    public void Show(string message, Color colour)
    {
        gameObject.SetActive(true);
        if (label.text != message)
        {
            label.text = message;
            label.ForceMeshUpdate();
            foreground.Refresh();
        }
        label.color = colour;
        PlaceInFront();
    }

    // Foreground refreshes its materials periodically; keep this card above the task/map HUD.
    private void LateUpdate() => PlaceInFront();
    private void PlaceInFront()
    {
        if (canvas == null) return;
        // The central card must not cover the pause or outcome menus.
        canvas.enabled = Time.timeScale > 0 && !(CampusLessonDirector.Instance?.NavigationPanelOpen ?? false);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31000;
    }
}
