using UnityEngine;
using TMPro;

public class FloorManager : MonoBehaviour
{
    [Header("Floor Settings")]
    [SerializeField] private int currentFloor = 4;
    [SerializeField] private int minFloor = 1;
    [SerializeField] private int maxFloor = 4;

    [Header("Floor Display")]
    [SerializeField] private TMP_Text floorText;

    [Header("Stair Visibility")]
    [SerializeField] private GameObject topFloorFill;
    [SerializeField] private GameObject upStairGroup;
    [SerializeField] private GameObject downStairGroup;
    [SerializeField] private GameObject groundFloorFill;

    public int CurrentFloor => currentFloor;

    private void Start()
    {
        UpdateFloorDisplay();
        UpdateStairVisibility();
    }

    public bool CanGoDown()
    {
        return currentFloor > minFloor;
    }

    public bool CanGoUp()
    {
        return currentFloor < maxFloor;
    }

    public bool GoDown()
    {
        if (!CanGoDown())
        {
            Debug.Log("Already at the lowest floor.");
            return false;
        }

        currentFloor--;
        UpdateFloorDisplay();
        UpdateStairVisibility();

        Debug.Log("Current Floor: " + currentFloor);
        return true;
    }

    public bool GoUp()
    {
        if (!CanGoUp())
        {
            Debug.Log("Already at the top floor.");
            return false;
        }

        currentFloor++;
        UpdateFloorDisplay();
        UpdateStairVisibility();

        Debug.Log("Current Floor: " + currentFloor);
        return true;
    }

    private void UpdateFloorDisplay()
    {
        if (floorText != null)
        {
            floorText.text = currentFloor + "F";
        }
    }

    private void UpdateStairVisibility()
    {
        // 默认都显示
        topFloorFill.SetActive(true);
        upStairGroup.SetActive(true);
        downStairGroup.SetActive(true);
        groundFloorFill.SetActive(false);

        // 4F：没有更高楼层
        if (currentFloor == maxFloor)
        {
            upStairGroup.SetActive(false);
        }
        else
        {
            topFloorFill.SetActive(false);
        }

        // 1F：没有更低楼层
        if (currentFloor == minFloor)
        {
            downStairGroup.SetActive(false);
            groundFloorFill.SetActive(true);
        }
    }
}