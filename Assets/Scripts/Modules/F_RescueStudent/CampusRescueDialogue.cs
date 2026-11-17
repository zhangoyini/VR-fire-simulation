using TMPro;
using UnityEngine;

// Reuses the lesson HUD palette, font and controller-ray buttons at the bottom centre.
internal sealed class CampusRescueDialogue
{
    private readonly GameObject root;
    internal VRMenuButton Reply { get; }
    internal bool Visible => root != null && root.activeSelf;

    internal CampusRescueDialogue(Transform head, UnityEngine.Events.UnityAction follow)
    {
        var canvas = CampusTutorialView.CanvasAt("Classmate conversation", head,
            new Vector3(0, -.43f, 1.45f), .0017f);
        root = canvas.gameObject;
        var font = CampusMessagePresenter.Instance != null
            ? CampusMessagePresenter.Instance.DisplayFont : TMP_Settings.defaultFontAsset;
        var ink = new Color(.025f, .07f, .085f, .96f);
        var mint = new Color(.45f, .95f, .83f);
        CampusTutorialView.Picture(root.transform, Vector2.zero, new Vector2(760, 230), mint);
        CampusTutorialView.Picture(root.transform, Vector2.zero, new Vector2(754, 224), ink);
        var speaker = CampusTutorialView.CreateLabel(font, root.transform, new Vector2(0, 81),
            new Vector2(706, 33), 23, "轮椅同学 / Classmate");
        speaker.color = mint;
        speaker.alignment = TextAlignmentOptions.Left;
        var line = CampusTutorialView.CreateLabel(font, root.transform, new Vector2(0, 21),
            new Vector2(706, 80), 27,
            "我好慌，不知道该怎么办……\n<size=83%>I'm so lost. I don't know what to do...</size>");
        line.alignment = TextAlignmentOptions.Left;
        var answer = CampusTutorialView.Picture(root.transform, new Vector2(0, -70),
            new Vector2(700, 53), new Color(.10f, .24f, .27f));
        answer.gameObject.name = "Reply - Follow me";
        var collider = answer.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(700, 53, 12);
        collider.center = new Vector3(0, 0, -6);
        Reply = answer.gameObject.AddComponent<VRMenuButton>();
        Reply.Bind(follow, null);
        CampusTutorialView.CreateLabel(font, answer.transform, Vector2.zero,
            new Vector2(680, 47), 25, "跟我来。 / Follow me.");
        foreach (var pointer in Object.FindObjectsOfType<VRMenuRayPointer>(true))
            pointer.RegisterMenuButton(Reply);
        // Generate all atlas submeshes before applying foreground materials to them.
        foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        var foreground = root.AddComponent<CampusUIForeground>();
        foreground.Configure(Shader.Find("Campus/Foreground UI"));
        foreground.Refresh();
        root.SetActive(false);
    }

    internal void Show(bool visible) { if (root != null) root.SetActive(visible); }
    internal void Dispose()
    {
        if (root == null) return;
        if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
    }
}
