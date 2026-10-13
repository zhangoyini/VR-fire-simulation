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
        // MODEL API PLACEHOLDER: static building diagram. Module 06 restores real-time floor/position updates.
    }
}
