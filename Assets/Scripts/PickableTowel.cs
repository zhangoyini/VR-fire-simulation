using UnityEngine;

public class PickableTowel : MonoBehaviour, IInteractable
{
    private Rigidbody body;
    private bool wasKinematic;
    private bool wasUsingGravity;
    private Transform holdingHand;
    private Vector3 handLocalPosition;
    private Quaternion handLocalRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void OnHoverEnter() { }

    public void OnHoverExit() { }

    public void OnInteractStart(Transform handTransform)
    {
        if (holdingHand != null)
            return;

        holdingHand = handTransform;
        handLocalPosition = handTransform.InverseTransformPoint(transform.position);
        handLocalRotation = Quaternion.Inverse(handTransform.rotation) * transform.rotation;

        if (body != null)
        {
            wasKinematic = body.isKinematic;
            wasUsingGravity = body.useGravity;
            if (!wasKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    public void OnInteract(Transform handTransform)
    {
        if (holdingHand != handTransform)
            return;

        Vector3 targetPosition = handTransform.TransformPoint(handLocalPosition);
        Quaternion targetRotation = handTransform.rotation * handLocalRotation;

        if (body != null)
        {
            body.position = targetPosition;
            body.rotation = targetRotation;
        }
        else
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }
    }

    public void OnInteractEnd(Transform handTransform)
    {
        if (holdingHand != handTransform)
            return;

        if (body != null)
        {
            body.isKinematic = wasKinematic;
            body.useGravity = wasUsingGravity;
        }

        holdingHand = null;
    }
}
