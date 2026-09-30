using UnityEngine;
using Valve.VR;

public class DummyController : MonoBehaviour
{
    [SerializeField] private GameObject controller;
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any;

    private SteamVR_Action_Boolean grabAction;
    private IInteractable currentInteractable;
    private IInteractable heldInteractable;
    private readonly System.Collections.Generic.List<IInteractable> nearbyInteractables =
        new System.Collections.Generic.List<IInteractable>();
    private readonly System.Collections.Generic.Dictionary<IInteractable, int> colliderCounts =
        new System.Collections.Generic.Dictionary<IInteractable, int>();

    private void Awake()
    {
        grabAction = SteamVR_Input.GetBooleanAction("GrabPinch");
    }

    private void Update()
    {
        if (controller != null)
        {
            transform.position = controller.transform.position;
            transform.rotation = controller.transform.rotation;
        }

        if (grabAction == null)
            return;

        if (grabAction.GetStateDown(inputSource) && currentInteractable != null)
        {
            heldInteractable = currentInteractable;
            heldInteractable.OnInteractStart(transform);
        }

        if (heldInteractable != null && grabAction.GetState(inputSource))
        {
            heldInteractable.OnInteract(transform);
        }

        if (heldInteractable != null && grabAction.GetStateUp(inputSource))
        {
            heldInteractable.OnInteractEnd(transform);
            heldInteractable = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = FindInteractable(other);

        if (interactable != null)
        {
            int count;
            colliderCounts.TryGetValue(interactable, out count);
            colliderCounts[interactable] = count + 1;

            if (count == 0)
            {
                nearbyInteractables.Add(interactable);
                SetCurrentInteractable(interactable);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = FindInteractable(other);

        if (interactable == null)
            return;

        int count;
        if (!colliderCounts.TryGetValue(interactable, out count))
            return;

        if (count > 1)
        {
            colliderCounts[interactable] = count - 1;
            return;
        }

        colliderCounts.Remove(interactable);
        nearbyInteractables.Remove(interactable);
        if (interactable == currentInteractable)
        {
            currentInteractable.OnHoverExit();
            currentInteractable = null;

            // Fall back to the most recently entered object still in range.
            if (nearbyInteractables.Count > 0)
                SetCurrentInteractable(nearbyInteractables[nearbyInteractables.Count - 1]);
        }
    }

    private static IInteractable FindInteractable(Collider other)
    {
        // Interactable scripts are often placed on a parent while the collider is on a child.
        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    private void SetCurrentInteractable(IInteractable interactable)
    {
        if (currentInteractable == interactable)
            return;

        if (currentInteractable != null)
            currentInteractable.OnHoverExit();

        currentInteractable = interactable;
        currentInteractable.OnHoverEnter();
    }

    private void OnDisable()
    {
        if (heldInteractable != null)
        {
            heldInteractable.OnInteractEnd(transform);
            heldInteractable = null;
        }

        if (currentInteractable != null)
            currentInteractable.OnHoverExit();

        currentInteractable = null;
        nearbyInteractables.Clear();
        colliderCounts.Clear();
    }
}
