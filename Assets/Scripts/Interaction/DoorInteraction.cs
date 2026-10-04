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
    [SerializeField] private Transform singleDoorPanel;

    [Header("Double Door")]
    [SerializeField] private Transform leftDoorPanel;
    [SerializeField] private Transform rightDoorPanel;

    [Header("Interaction Reference")]
    [Tooltip("Used to determine which side of the door the user is standing on.")]
    [SerializeField] private Transform doorReference;

    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 120f;

    [Tooltip("If the door opens in the wrong direction, change this between 1 and -1.")]
    [SerializeField] private float frontOpenDirection = 1f;

    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;

    private Quaternion singleClosedRotation;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;

    private bool isOpening = false;
    private bool isOpen = false;

    private void Start()
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(false);
        }
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

    private void Awake()
    {
        if (doorReference == null)
        {
            doorReference = transform;
        }

        if (doorType == DoorType.Single && singleDoorPanel != null)
        {
            singleClosedRotation = singleDoorPanel.localRotation;
        }

        if (doorType == DoorType.Double)
        {
            if (leftDoorPanel != null)
            {
                leftClosedRotation = leftDoorPanel.localRotation;
            }

            if (rightDoorPanel != null)
            {
                rightClosedRotation = rightDoorPanel.localRotation;
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
        Debug.Log("Door OnInteractStart called");
        if (isOpening || isOpen || handTransform == null)
        {
            return;
        }

        HideHint();
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
        if (singleDoorPanel == null)
        {
            Debug.LogWarning("Single door panel is not assigned.");
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
        if (leftDoorPanel == null || rightDoorPanel == null)
        {
            Debug.LogWarning("Double door panel are not assigned.");
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
                singleDoorPanel.localRotation,
                targetRotation) > 0.1f)
        {
            singleDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    singleDoorPanel.localRotation,
                    targetRotation,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        singleDoorPanel.localRotation = targetRotation;

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
                leftDoorPanel.localRotation,
                leftTarget) > 0.1f
            ||
            Quaternion.Angle(
                rightDoorPanel.localRotation,
                rightTarget) > 0.1f)
        {
            leftDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    leftDoorPanel.localRotation,
                    leftTarget,
                    openSpeed * Time.deltaTime
                );

            rightDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    rightDoorPanel.localRotation,
                    rightTarget,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        leftDoorPanel.localRotation = leftTarget;
        rightDoorPanel.localRotation = rightTarget;

        isOpening = false;
        isOpen = true;
    }

    public void CloseDoor()
    {
        if (isOpening || !isOpen)
        {
            return;
        }

        if (doorType == DoorType.Single)
        {
            StartCoroutine(RotateSingleDoorClosed());
        }
        else
        {
            StartCoroutine(RotateDoubleDoorClosed());
        }
    }

    private IEnumerator RotateSingleDoorClosed()
    {
        isOpening = true;

        while (
            Quaternion.Angle(
                singleDoorPanel.localRotation,
                singleClosedRotation) > 0.1f)
        {
            singleDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    singleDoorPanel.localRotation,
                    singleClosedRotation,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        singleDoorPanel.localRotation = singleClosedRotation;

        isOpening = false;
        isOpen = false;
    }

    private IEnumerator RotateDoubleDoorClosed()
    {
        isOpening = true;

        while (
            Quaternion.Angle(
                leftDoorPanel.localRotation,
                leftClosedRotation) > 0.1f
            ||
            Quaternion.Angle(
                rightDoorPanel.localRotation,
                rightClosedRotation) > 0.1f)
        {
            leftDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    leftDoorPanel.localRotation,
                    leftClosedRotation,
                    openSpeed * Time.deltaTime
                );

            rightDoorPanel.localRotation =
                Quaternion.RotateTowards(
                    rightDoorPanel.localRotation,
                    rightClosedRotation,
                    openSpeed * Time.deltaTime
                );

            yield return null;
        }

        leftDoorPanel.localRotation = leftClosedRotation;
        rightDoorPanel.localRotation = rightClosedRotation;

        isOpening = false;
        isOpen = false;
    }

}
