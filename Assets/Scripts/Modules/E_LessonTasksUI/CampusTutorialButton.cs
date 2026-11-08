using UnityEngine;

// Uses the same contact + trigger press path as the classroom's other buttons.
public sealed class CampusTutorialButton : MonoBehaviour, IInteractable
{
    private Transform owner;
    public bool Pressed { get; private set; }
    public bool IsHeld => owner != null;
    public void OnInteractStart(Transform hand)
    {
        if (owner != null) return;
        owner = hand;
        Pressed = true;
    }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { if (owner == hand) owner = null; }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
}
