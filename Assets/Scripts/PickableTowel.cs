using UnityEngine;

public class PickableTowel : MonoBehaviour, IInteractable
{
    private Transform originalParent;
    private Rigidbody body;
    private bool wasKinematic;
    private Transform holdingHand;

    private void Awake()
    {
        originalParent = transform.parent;
        body = GetComponent<Rigidbody>();
    }

    public void OnHoverEnter() { }

    public void OnHoverExit() { }

    public void OnInteractStart(Transform handTransform)
    {
        if (holdingHand != null)
            return;

        holdingHand = handTransform;
        if (body != null)
        {
            wasKinematic = body.isKinematic;
            body.isKinematic = true;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // Preserve the towel's hand-relative position and rotation when picked up.
        transform.SetParent(handTransform, true);
    }

    public void OnInteract(Transform handTransform)
    {
        // Parenting makes the towel follow the controller for the duration of the hold.
    }

    public void OnInteractEnd(Transform handTransform)
    {
        if (holdingHand != handTransform)
            return;

        transform.SetParent(originalParent, true);
        if (body != null)
            body.isKinematic = wasKinematic;

        holdingHand = null;
    }
}
