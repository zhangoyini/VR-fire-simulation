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
        var lesson = CampusLessonDirector.Instance;
        if (lesson == null || !lesson.ExerciseStarted || Time.timeScale == 0) return;
        if (trackedHead == null || refugeVolume == null || resolution == null ||
            resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None) return;
        if (!refugeVolume.bounds.Contains(trackedHead.position)) return;
        lesson.ConfirmRescue();
    }
}
