using Harbourview.Journey;
using Jiao.HealthTime;
using UnityEngine;

public sealed class CampusRefugeCall : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private Collider refugeVolume;
    [SerializeField] private ResolutionCoordinator resolution;
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { }
    public void OnInteractStart(Transform hand)
    {
        if(CampusLessonDirector.Instance!=null && !CampusLessonDirector.Instance.ExerciseStarted) return;
        if (trackedHead == null || refugeVolume == null || resolution == null) return;
        if (!refugeVolume.bounds.Contains(trackedHead.position)) return;
        JourneyDirector.Instance?.Request(JourneyState.Refuge);
        if (!resolution.TryShowVictory()) return;
        HealthTimeController.Instance?.Pause(true);
        FindObjectOfType<HealthTimeBar>()?.StopWarning();
        Jiao.HealthTime.AudioManager.Instance?.StopWarning();
        SessionLogger.Write("REFUGE_INTERCOM_CONFIRMED");
    }
}
