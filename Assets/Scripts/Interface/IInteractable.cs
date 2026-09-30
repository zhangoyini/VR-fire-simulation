using UnityEngine;

public interface IInteractable
{
    // Called when the VR hand hovers/touches the object
    public void OnHoverEnter();

    // Called when the VR hand stops touching the object
    public void OnHoverExit();

    // Called when the user presses the Grab/Pinch button
    public void OnInteract(Transform handTransform);
}

