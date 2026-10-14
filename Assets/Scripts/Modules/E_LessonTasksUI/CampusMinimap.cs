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
    public int CurrentFloor { get; private set; }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (trackedHead == null || playerDot == null) return;
        CurrentFloor = Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y - .8f) / 3.8f) + 1, 1, 4);
        bool ground=CurrentFloor==1;
        if(buildingPlan!=null) { buildingPlan.localScale=new Vector3(1,ground?.496f:1,1); buildingPlan.anchoredPosition=new Vector2(0,ground?32.67f:0); }
        if(outdoorArea!=null) outdoorArea.SetActive(ground);
        if(stairA!=null) stairA.color=new Color(.18f,.7f,.4f);
        float mapY=ground?(Mathf.Clamp(trackedHead.position.z,-38,10)+14)/48*112:Mathf.Clamp(trackedHead.position.z,-10,10)/20*94;
        playerDot.anchoredPosition = new Vector2(Mathf.Clamp(trackedHead.position.x, -26, 26) / 52 * 240, mapY);
        playerDot.localRotation = Quaternion.Euler(0, 0, -trackedHead.eulerAngles.y);
        if (floorText != null) floorText.text = CurrentFloor + "F   N ↑";
    }
}
