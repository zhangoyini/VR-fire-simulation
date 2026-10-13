using UnityEngine;

public sealed class CampusElevatorButton : MonoBehaviour, IInteractable
{
    private Transform owner;
    public void OnInteractStart(Transform hand) { if (owner == null) owner = hand; }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand)
    {
        if (owner == hand) owner = null;
        // MODEL API PLACEHOLDER: decorative elevator buttons; module 03 restores bilingual safety warning.
    }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    private void OnDisable() { owner = null; }
}
