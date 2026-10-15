using UnityEngine;

public class ResolutionCoordinator : MonoBehaviour
{
    public enum Outcome
    {
        None,
        Victory,
        GameOver
    }

    [SerializeField] private SystemUIView view;
    [SerializeField] private SystemUIAudio outcomeAudio;
    public event System.Action<Outcome> OutcomeClaimed;

    public Outcome CurrentOutcome { get; private set; } = Outcome.None;

    public void ShowVictory()
    {
        TryShowVictory();
    }

    public bool TryShowVictory()
    {
        if(CampusLessonDirector.Instance!=null && !CampusLessonDirector.Instance.ExerciseStarted) return false;
        if (!TryClaim(Outcome.Victory))
            return false;
        if (view != null)
            view.ShowVictory();
        return true;
    }

    public void ShowGameOver()
    {
        if (!TryClaimGameOver())
            return;
        if (view != null)
            view.ShowGameOver();
    }

    // FailureUI retains its existing presentation/health behavior after claiming this gate.
    public bool TryClaimGameOver() => TryClaim(Outcome.GameOver);

    private bool TryClaim(Outcome outcome)
    {
        if (CurrentOutcome != Outcome.None)
            return false;
        CurrentOutcome = outcome;
        OutcomeClaimed?.Invoke(outcome);
        if (outcomeAudio != null) outcomeAudio.PlayOutcome(outcome);
        return true;
    }

    public void ResetResolution()
    {
        CurrentOutcome = Outcome.None;
        if (view != null)
            view.ShowGameplay();
    }
}
