using UnityEngine;

// Uses the same contact + trigger press path as the classroom's other buttons.
public sealed class CampusTutorialButton : MonoBehaviour, IInteractable
{
    private Transform owner;
    private bool freePractice;
    public bool Pressed { get; private set; }
    public bool IsHeld => owner != null;
    public bool GreenLight { get; private set; }
    internal void BeginFreePractice()
    {
        freePractice = true;
        GreenLight = true;
    }
    public void OnInteractStart(Transform hand)
    {
        if (owner != null) return;
        owner = hand;
        Pressed = true;
        GreenLight = !freePractice || !GreenLight;
    }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { if (owner == hand) owner = null; }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
}
