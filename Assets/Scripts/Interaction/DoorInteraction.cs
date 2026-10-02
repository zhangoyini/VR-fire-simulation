using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteraction : MonoBehaviour,IInteractable
{    public enum DoorType
    {
        Single,
        Double
    }

    [Header("Door Type")]
    [SerializeField] private DoorType doorType = DoorType.Single;

    [Header("Single Door")]
    [SerializeField] private Transform singleDoorHinge;

    [Header("Double Door")]
    [SerializeField] private Transform leftDoorHinge;
    [SerializeField] private Transform rightDoorHinge;

    [Header("Interaction Reference")]
    [Tooltip("Used to determine which side of the door the user is standing on.")]
    [SerializeField] private Transform doorReference;

    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 120f;

    [Tooltip("If the door opens in the wrong direction, change this between 1 and -1.")]
    [SerializeField] private float frontOpenDirection = 1f;

    private Quaternion singleClosedRotation;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;

    private bool isOpening = false;
    private bool isOpen = false;

    private void Awake()
    {
        if (doorReference == null)
        {
            doorReference = transform;
        }

        if (doorType == DoorType.Single && singleDoorHinge != null)
        {
            singleClosedRotation = singleDoorHinge.localRotation;
        }

        if (doorType == DoorType.Double)
        {
            if (leftDoorHinge != null)
            {
                leftClosedRotation = leftDoorHinge.localRotation;
            }

            if (rightDoorHinge != null)
            {
                rightClosedRotation = rightDoorHinge.localRotation;
            }
        }
    }

    //手靠近、碰到门把手时
    public void OnHoverEnter()
    {
        // Later:
        // show hand icon / highlight / haptic feedback
    }

    //手离开物体时
    public void OnHoverExit()
    {
        // Later:
        // hide hand icon / remove highlight
    }


    //按下 Grab / Pinch 开始交互时
    public void OnInteractStart(Transform handTransform)
    {
        if (isOpening || isOpen || handTransform == null)
        {
            return;
        }

        float side = GetUserSide(handTransform);

        if (doorType == DoorType.Single)
        {
            OpenSingleDoor(side);
        }
        else
        {
            OpenDoubleDoor(side);
        }
    }

    //持续按住期间
    public void OnInteract(Transform handTransform)
    {
        // Not needed for this version.
        // Door opens automatically after interaction starts.
    }

    //松开时
    public void OnInteractEnd(Transform handTransform)
    {
        // Not needed for this version.
    }

    private float GetUserSide(Transform handTransform)
    {
        Vector3 directionToUser =
            handTransform.position - doorReference.position;

        float dot =
            Vector3.Dot(doorReference.forward, directionToUser);

        // User is on the "front" side
        if (dot >= 0)
        {
            return 1f;
        }

        // User is on the "back" side
        return -1f;
    }

    private void OpenSingleDoor(float userSide)
    {
        if (singleDoorHinge == null)
        {
            Debug.LogWarning("Single door hinge is not assigned.");
            return;
        }

        /*
         * User on front side:
         * door rotates toward the opposite side.
         *
         * User on back side:
         * rotation direction reverses.
         */
        float direction =
            userSide * frontOpenDirection;

        Quaternion targetRotation =
            singleClosedRotation *
            Quaternion.Euler(0f, direction * openAngle, 0f);

        StartCoroutine(
            RotateSingleDoor(targetRotation)
        );
    }

    private void OpenDoubleDoor(float userSide)
    {
        if (leftDoorHinge == null || rightDoorHinge == null)
        {
            Debug.LogWarning("Double door hinges are not assigned.");
            return;
        }

        float direction =
            userSide * frontOpenDirection;

        /*
         * Two leaves rotate in opposite directions.
         */
        Quaternion leftTarget =
            leftClosedRotation *
            Quaternion.Euler(0f, direction * openAngle, 0f);

        Quaternion rightTarget =
            rightClosedRotation *
            Quaternion.Euler(0f, -direction * openAngle, 0f);

        StartCoroutine(
            RotateDoubleDoor(leftTarget, rightTarget)
        );
    }

    private IEnumerator RotateSingleDoor(
        Quaternion targetRotation)
    {
        isOpening = true;

        while (
            Quaternion.Angle(
                singleDoorHinge.localRotation,
                targetRotation) > 0.1f)
        {
            singleDoorHinge.localRotation =
                Quaternion.RotateTowards(
                    singleDoorHinge.localRotation,
                    targetRotation,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        singleDoorHinge.localRotation = targetRotation;

        isOpening = false;
        isOpen = true;
    }

    private IEnumerator RotateDoubleDoor(
        Quaternion leftTarget,
        Quaternion rightTarget)
    {
        isOpening = true;

        while (
            Quaternion.Angle(
                leftDoorHinge.localRotation,
                leftTarget) > 0.1f
            ||
            Quaternion.Angle(
                rightDoorHinge.localRotation,
                rightTarget) > 0.1f)
        {
            leftDoorHinge.localRotation =
                Quaternion.RotateTowards(
                    leftDoorHinge.localRotation,
                    leftTarget,
                    openSpeed * Time.deltaTime
                );

            rightDoorHinge.localRotation =
                Quaternion.RotateTowards(
                    rightDoorHinge.localRotation,
                    rightTarget,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        leftDoorHinge.localRotation = leftTarget;
        rightDoorHinge.localRotation = rightTarget;

        isOpening = false;
        isOpen = true;
    }


}
