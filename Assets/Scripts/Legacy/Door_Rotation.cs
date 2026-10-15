using UnityEngine;

public class Door_Rotation : MonoBehaviour, IInteractable
{
    [Header("Door hinge")]
    [Tooltip("Assign the door hinge pivot. If empty, this object's transform is used.")]
    [SerializeField] private Transform hinge;
    [SerializeField, Min(0f)] private float maxSwingAngle = 110f;

    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;

    private Transform activeHand;
    private Vector3 previousHandPosition;
    private float currentSwingAngle;

    private Transform Hinge => hinge != null ? hinge : transform;

    private void Start()
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(false);
        }
    }


    public void OnHoverEnter() {
        Debug.Log("Door hover enter");
        if (interactionHint != null)
        {
            interactionHint.SetActive(true);
        }
    }

    public void OnHoverExit() {
        Debug.Log("Door hover exist");
        if (interactionHint != null)
        {
            interactionHint.SetActive(false);
        }
    }

    public void OnInteractStart(Transform handTransform)
    {
        activeHand = handTransform;
        previousHandPosition = handTransform.position;
    }

    public void OnInteract(Transform handTransform)
    {
        if (activeHand != handTransform)
            return;

        Transform pivot = Hinge;
        Vector3 hingeAxis = pivot.up;
        Vector3 previousOffset = Vector3.ProjectOnPlane(previousHandPosition - pivot.position, hingeAxis);
        Vector3 currentOffset = Vector3.ProjectOnPlane(handTransform.position - pivot.position, hingeAxis);

        if (previousOffset.sqrMagnitude > 0.0001f && currentOffset.sqrMagnitude > 0.0001f)
        {
            float requestedDelta = Vector3.SignedAngle(previousOffset, currentOffset, hingeAxis);
            float nextAngle = Mathf.Clamp(currentSwingAngle + requestedDelta, -maxSwingAngle, maxSwingAngle);
            float appliedDelta = nextAngle - currentSwingAngle;

            if (!Mathf.Approximately(appliedDelta, 0f))
            {
                transform.RotateAround(pivot.position, hingeAxis, appliedDelta);
                currentSwingAngle = nextAngle;
            }
        }

        previousHandPosition = handTransform.position;
    }

    public void OnInteractEnd(Transform handTransform)
    {
        if (activeHand == handTransform)
            activeHand = null;
    }
}
