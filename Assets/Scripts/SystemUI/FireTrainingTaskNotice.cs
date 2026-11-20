using TMPro;
using UnityEngine;

// Shared by the live task publisher and the editor's rendered text-fit check.
public static class FireTrainingTaskNotice
{
    public static CanvasGroup Create(Transform parent, TMP_FontAsset font, out TMP_Text label)
    {
        var group = CampusTutorialView.CanvasAt("Exercise task notification", parent,
            new Vector3(.57f, -.06f, 1.45f), .0014f);
        var background = CampusTutorialView.Picture(group.transform, Vector2.zero,
            new Vector2(560, 350), Color.white, FireTrainingUIStyle.Load("panels/main_screen_base"));
        background.preserveAspect = true;
        label = CampusTutorialView.CreateLabel(font, group.transform, new Vector2(0, -4),
            new Vector2(450, 240), 26, "");
        label.fontSizeMin = 22;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        return group;
    }
}
