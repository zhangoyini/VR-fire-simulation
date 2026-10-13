using UnityEngine;
using System.Collections;
using Valve.VR;
using System.Collections.Generic;

public class Door_Rotation : MonoBehaviour, IInteractable
{
    [Header("Door hinge")]
    [Tooltip("Assign the door hinge pivot. If empty, this object's transform is used.")]
    [SerializeField] private Transform hinge;
    [SerializeField, Min(0f)] private float maxSwingAngle = 110f;

    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;

    [Header("Haptic Feedback")]
    [SerializeField] private SteamVR_Action_Vibration hapticAction;
    [SerializeField] private SteamVR_Input_Sources hapticSource = SteamVR_Input_Sources.RightHand;
    [SerializeField] private float hapticDuration = 0.03f;
    [SerializeField] private float hapticFrequency = 80f;
    [SerializeField] private float hapticAmplitude = 0.25f;
    [SerializeField] private float hapticInterval = 0.06f;

    [Header("Auto Close")]
    [SerializeField] private float closeSpeed = 90f;

    private Quaternion closedRotation;
    private bool isClosing;

    private float nextHapticTime;

    private Transform activeHand;
    private Vector3 previousHandPosition;
    private float currentSwingAngle;

    private Transform Hinge => hinge != null ? hinge : transform;

    private void Start()
    {
        closedRotation = transform.localRotation;

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

                PlayHaptic();
            }
        }

        previousHandPosition = handTransform.position;
    }

    public void OnInteractEnd(Transform handTransform)
    {
        if (activeHand == handTransform)
            activeHand = null;
    }


    public void CloseDoor()
    {
        if (!isClosing)
        {
            StartCoroutine(CloseDoorCoroutine());
        }
    }

    private IEnumerator CloseDoorCoroutine()
    {
        isClosing = true;

        while (Quaternion.Angle(transform.localRotation, closedRotation) > 0.1f)
        {
            transform.localRotation =
                Quaternion.RotateTowards(
                    transform.localRotation,
                    closedRotation,
                    closeSpeed * Time.deltaTime
                );

            yield return null;
        }

        transform.localRotation = closedRotation;
        currentSwingAngle = 0f;
        isClosing = false;
    }

    private void PlayHaptic()
    {
        if (hapticAction == null)
            return;

        if (Time.time < nextHapticTime)
            return;

        hapticAction.Execute(
            0f,
            hapticDuration,
            hapticFrequency,
            hapticAmplitude,
            hapticSource
        );

        nextHapticTime = Time.time + hapticInterval;
    }

    public void ShowHint()
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(true);
        }
    }

    public void HideHint()
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(false);
        }
    }
}
