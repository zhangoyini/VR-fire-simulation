using UnityEngine;

public sealed class CampusElevatorButton : MonoBehaviour, IInteractable
{
    private Transform owner;
    public void OnInteractStart(Transform hand) { if (owner == null) owner = hand; }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand)
    { if (owner != hand) return; owner = null; CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.ElevatorWarning); }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    private void OnDisable() { owner = null; }
}
