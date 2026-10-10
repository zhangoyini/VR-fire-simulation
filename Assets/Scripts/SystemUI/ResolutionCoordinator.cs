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

    public Outcome CurrentOutcome { get; private set; } = Outcome.None;

    public void ShowVictory()
    {
        if (CurrentOutcome != Outcome.None)
            return;

        CurrentOutcome = Outcome.Victory;
        if (view != null)
            view.ShowVictory();
    }

    public void ShowGameOver()
    {
        if (CurrentOutcome != Outcome.None)
            return;

        CurrentOutcome = Outcome.GameOver;
        if (view != null)
            view.ShowGameOver();
    }

    public void ResetResolution()
    {
        CurrentOutcome = Outcome.None;
        if (view != null)
            view.ShowGameplay();
    }
}
