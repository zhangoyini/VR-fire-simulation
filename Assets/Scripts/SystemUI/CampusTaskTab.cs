using TMPro;
using UnityEngine;

public static class CampusTaskTab
{
    public static TMP_Text CreateLabels(Transform parent, TMP_FontAsset font, out TMP_Text key)
    {
        // The key frame in task_tab.png is centred at about 14% of its width.
        key = CampusTutorialView.CreateLabel(font, parent, new Vector2(-74, 0), new Vector2(28, 30), 23, "Y");
        key.alignment = TextAlignmentOptions.Center;
        var title = CampusTutorialView.CreateLabel(font, parent, new Vector2(17, 0), new Vector2(142, 32), 19,
            "任务详情  Tasks");
        title.alignment = TextAlignmentOptions.Center;
        return title;
    }
}
