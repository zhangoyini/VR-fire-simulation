using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// At most six received objectives: two columns keep the entire list readable in VR.
public sealed class CampusTaskCardList
{
    public struct Entry
    {
        public string chinese, english;
        public bool complete;
        public Entry(string chinese, string english, bool complete = false)
        { this.chinese = chinese; this.english = english; this.complete = complete; }
    }

    public readonly GameObject Root;
    private readonly GameObject[] cards = new GameObject[6];
    private readonly TMP_Text[] labels = new TMP_Text[6];
    private readonly Image[] checks = new Image[6];
    public CampusTaskCardList(Transform parent, TMP_FontAsset font)
    {
        Root = new GameObject("Received task cards", typeof(RectTransform), typeof(CanvasGroup));
        Root.transform.SetParent(parent, false);
        var heading = CampusTutorialView.CreateLabel(font, Root.transform, new Vector2(0, 205), new Vector2(860, 48), 28,
            "任务详情 / Tasks");
        heading.color = Color.white;
        for (int i = 0; i < cards.Length; i++)
        {
            var face = CampusTutorialView.Picture(Root.transform,
                new Vector2(i % 2 == 0 ? -220 : 220, 110 - i / 2 * 140), new Vector2(430, 140), Color.white,
                FireTrainingUIStyle.Load("panels/notice_card"));
            face.name = "Task card " + i;
            cards[i] = face.gameObject;
            // Coordinates match the blank frame in notice_card.png. The text
            // begins to its right, never in the frame or paper's transparent margins.
            checks[i] = CampusTutorialView.Picture(face.transform, new Vector2(-140, -2), new Vector2(36, 36), Color.white,
                FireTrainingUIStyle.Load("icons/icon_check"));
            checks[i].name = "Completion check";
            checks[i].preserveAspect = true;
            labels[i] = CampusTutorialView.CreateLabel(font, face.transform, new Vector2(42, -2), new Vector2(250, 64), 20, "");
            labels[i].alignment = TextAlignmentOptions.MidlineLeft;
            labels[i].fontSizeMin = 16;
            labels[i].overflowMode = TextOverflowModes.Truncate;
            checks[i].gameObject.SetActive(false);
            cards[i].SetActive(false);
        }
    }

    public void SetEntries(IReadOnlyList<Entry> entries)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            bool visible = entries != null && i < entries.Count;
            cards[i].SetActive(visible);
            if (!visible) continue;
            var entry = entries[i];
            string copy = entry.chinese + "\n<size=78%>" + entry.english + "</size>";
            if (labels[i].text != copy) labels[i].text = copy;
            checks[i].gameObject.SetActive(entry.complete);
        }
    }
    public void Show(float alpha)
    {
        Root.SetActive(alpha > 0);
        Root.GetComponent<CanvasGroup>().alpha = alpha;
    }
}
