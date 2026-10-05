using UnityEngine;
using UnityEngine.Events;

public class VRMenuButton : MonoBehaviour, IInteractable
{
    [SerializeField] private UnityEvent onClick = new UnityEvent();

    public void OnHoverEnter() { }

    public void OnHoverExit() { }

    public void OnInteractStart(Transform handTransform)
    {
        if (!isActiveAndEnabled)
            return;

        // DummyController calls this once per press; listeners may hide this button.
        onClick.Invoke();
    }

    // The controller may keep calling these after the button's panel is hidden.
    public void OnInteract(Transform handTransform) { }

    public void OnInteractEnd(Transform handTransform) { }
}
