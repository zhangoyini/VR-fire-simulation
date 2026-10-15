using Harbourview.Journey;
using Jiao.HealthTime;
using TMPro;
using UnityEngine;

// Coordinates the integrated scene without replacing the team's health, menu or journey systems.
public sealed class CampusExperience : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private TextMeshProUGUI floorLabel;
    [SerializeField] private TextMeshProUGUI debriefLabel;
    [SerializeField] private AudioSource alarm;
    private float startTime;
    private int previousFloor;
    private bool finished;
    private void Start()
    {
        startTime = Time.time;
        JourneyDirector.Instance?.Request(JourneyState.Awareness);
        if (alarm != null && CampusLessonDirector.Instance==null) alarm.PlayDelayed(9f);
        var resolution = FindObjectOfType<ResolutionCoordinator>();
        if (resolution != null) resolution.OutcomeClaimed += OnOutcome;
    }
    public void BeginTimedExercise() { startTime=Time.time; }
    private void Update()
    {
        if (trackedHead == null || finished) return;
        int floor = Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y - 0.8f) / 3.8f) + 1, 1, 4);
        if (floorLabel != null) floorLabel.text = $"{floor}F   HP {Mathf.RoundToInt((HealthTimeController.Instance != null ? HealthTimeController.Instance.HealthPercent : 1) * 100)} / 血量";
        if(floorLabel!=null && CampusTowelInteractable.HasProtection(trackedHead)) floorLabel.text+="\n湿毛巾遮挡 / Wet towel in position";
        if (floor != previousFloor)
        {
            previousFloor = floor;
            SessionLogger.Write("CAMPUS_FLOOR", "{\"floor\":" + floor + "}");
            if (floor < 4) JourneyDirector.Instance?.Request(JourneyState.Descending);
        }
    }
    private void OnOutcome(ResolutionCoordinator.Outcome outcome)
    {
        finished = true;
        foreach (var teleporter in FindObjectsOfType<VRTeleporter>()) teleporter.enabled = false;
        if (alarm != null) alarm.Stop();
        JourneyDirector.Instance?.Request(JourneyState.Debrief);
        if (debriefLabel != null)
        {
            int elapsed = Mathf.RoundToInt(Time.time - startTime);
            debriefLabel.text = $"本次练习用时 {elapsed / 60:D2}:{elapsed % 60:D2}  /  Practice time\n" +
                "识别警报 · 跟随标识 · 路线受阻时重新选择\nNotice cues · Follow signs · Choose another route\n" +
                "请勿使用电梯或跳窗逃生。\nAvoid elevators and window escape.";
        }
    }
    private void OnDestroy()
    {
        var resolution = FindObjectOfType<ResolutionCoordinator>();
        if (resolution != null) resolution.OutcomeClaimed -= OnOutcome;
    }
}
