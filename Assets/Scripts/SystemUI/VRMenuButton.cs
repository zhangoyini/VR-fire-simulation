using UnityEngine;
using UnityEngine.Events;

public class VRMenuButton : MonoBehaviour, IInteractable
{
    [SerializeField] private UnityEvent onClick = new UnityEvent();
    [SerializeField] private SystemUIAudio clickAudio;
    private static int lastActivationFrame = -1;

    public void OnHoverEnter() { }

    public void OnHoverExit() { }

    public void OnInteractStart(Transform handTransform)
    {
        if (!isActiveAndEnabled || lastActivationFrame == Time.frameCount)
            return;

        // Contact (Update) and ray (LateUpdate) share a press. Contact takes priority,
        // including when it targets a different menu button. Listeners may hide this UI.
        lastActivationFrame = Time.frameCount;
        if (clickAudio != null) clickAudio.PlayClick();
        onClick.Invoke();
    }

    // The controller may keep calling these after the button's panel is hidden.
    public void OnInteract(Transform handTransform) { }

    public void OnInteractEnd(Transform handTransform) { }
}
