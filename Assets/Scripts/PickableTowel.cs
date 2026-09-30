using UnityEngine;

public class PickableTowel : MonoBehaviour, IInteractable
{
    [Header("Throwing")]
    [SerializeField, Min(0f)] private float throwVelocityMultiplier = 1f;
    [SerializeField, Min(0f)] private float maxThrowSpeed = 8f;

    private Rigidbody body;
    private bool wasKinematic;
    private bool wasUsingGravity;
    private Transform holdingHand;
    private Vector3 handLocalPosition;
    private Quaternion handLocalRotation;
    private Vector3 previousTargetPosition;
    private float previousSampleTime;
    private Vector3 releaseVelocity;

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
        previousTargetPosition = transform.position;
        previousSampleTime = Time.time;
        releaseVelocity = Vector3.zero;

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

        SampleThrowVelocity(targetPosition);

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

        Vector3 finalTargetPosition = handTransform.TransformPoint(handLocalPosition);
        Quaternion finalTargetRotation = handTransform.rotation * handLocalRotation;
        SampleThrowVelocity(finalTargetPosition);

        if (body != null)
        {
            body.position = finalTargetPosition;
            body.rotation = finalTargetRotation;
            body.isKinematic = wasKinematic;
            body.useGravity = wasUsingGravity;

            if (!body.isKinematic)
                body.velocity = Vector3.ClampMagnitude(releaseVelocity * throwVelocityMultiplier, maxThrowSpeed);
        }
        else
        {
            transform.SetPositionAndRotation(finalTargetPosition, finalTargetRotation);
        }

        holdingHand = null;
    }

    private void SampleThrowVelocity(Vector3 targetPosition)
    {
        float sampleTime = Time.time;
        float elapsed = sampleTime - previousSampleTime;

        if (elapsed > 0.0001f)
        {
            Vector3 measuredVelocity = (targetPosition - previousTargetPosition) / elapsed;
            // Smooth small tracking jitters while retaining quick hand flicks.
            releaseVelocity = Vector3.Lerp(releaseVelocity, measuredVelocity, 0.65f);
        }

        previousTargetPosition = targetPosition;
        previousSampleTime = sampleTime;
    }
}
