using TMPro;
using UnityEngine;

public sealed class CampusMinimap : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private RectTransform playerDot;
    [SerializeField] private TMP_Text floorText;
    [SerializeField] private RectTransform buildingPlan;
    [SerializeField] private GameObject outdoorArea;
    [SerializeField] private UnityEngine.UI.Image stairA;
    [SerializeField] private TMP_Text room01Label;
    [SerializeField] private TMP_Text room02Label;
    [SerializeField] private TMP_Text room03Label;
    [SerializeField] private TMP_Text room04Label;
    [SerializeField] private TMP_Text room11Label;
    [SerializeField] private TMP_Text room12Label;
    [SerializeField] private TMP_Text room13Label;
    [SerializeField] private TMP_Text room14Label;
    private int lastLabelFloor = -1;
    private void OnEnable()
    {
        lastLabelFloor = -1;
        var background = transform.Find("Map background");
        if (background != null)
        {
            FireTrainingUIStyle.Apply(background.GetComponent<UnityEngine.UI.Image>(), "panels/map_frame");
            ((RectTransform)background).sizeDelta = new Vector2(310, 205);
        }
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = FireTrainingUIStyle.Ink;
            if (label.rectTransform.sizeDelta.x == 40)
                label.rectTransform.sizeDelta = new Vector2(40, 24);
        }
        if (playerDot != null)
        {
            FireTrainingUIStyle.Apply(playerDot.GetComponent<UnityEngine.UI.Image>(), "icons/icon_player_arrow");
            playerDot.sizeDelta = new Vector2(13, 13);
            var facing = playerDot.Find("Player facing");
            if (facing != null) facing.gameObject.SetActive(false);
            playerDot.SetAsLastSibling();
        }
    }
    public int CurrentFloor { get; private set; }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (trackedHead == null || playerDot == null) return;
        CurrentFloor = Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y - .8f) / 3.8f) + 1, 1, 4);
        if (lastLabelFloor != CurrentFloor) RefreshRoomLabels();
        bool ground=CurrentFloor==1;
        if(buildingPlan!=null) { buildingPlan.localScale=new Vector3(1,ground?.496f:1,1); buildingPlan.anchoredPosition=new Vector2(0,ground?32.67f:0); }
        if(outdoorArea!=null) outdoorArea.SetActive(ground);
        if(stairA!=null) stairA.color=new Color(.18f,.7f,.4f);
        float mapY=ground?(Mathf.Clamp(trackedHead.position.z,-38,10)+14)/48*112:Mathf.Clamp(trackedHead.position.z,-10,10)/20*94;
        playerDot.anchoredPosition = new Vector2(Mathf.Clamp(trackedHead.position.x, -26, 26) / 52 * 240, mapY);
        playerDot.localRotation = Quaternion.Euler(0, 0, -trackedHead.eulerAngles.y);
        if (floorText != null) floorText.text = CurrentFloor + "F   N ↑";
    }

    private void RefreshRoomLabels()
    {
        UpdateRoomLabel(room01Label, "01教室");
        UpdateRoomLabel(room02Label, "02教室");
        UpdateRoomLabel(room03Label, "03教室");
        UpdateRoomLabel(room04Label, "04教室");
        UpdateRoomLabel(room11Label, "11卫生间");
        UpdateRoomLabel(room12Label, CurrentFloor == 1 ? "12门厅" : CurrentFloor == 2 ? "12避难区" : "12阅览室");
        UpdateRoomLabel(room13Label, "13电梯");
        UpdateRoomLabel(room14Label, "14实验室");
        lastLabelFloor = CurrentFloor;
    }

    private void UpdateRoomLabel(TMP_Text label, string suffix)
    {
        if (label == null) return;
        label.text = CurrentFloor + suffix;
        // Assign an absolute scale so floor changes and re-enabling cannot accumulate compensation.
        label.rectTransform.localScale = new Vector3(1, CurrentFloor == 1 ? 1f / .496f : 1, 1);
    }
}
