using UnityEngine;

public interface IInteractable
{
    // Called when the VR hand hovers/touches the object
    public void OnHoverEnter();

    // Called when the VR hand stops touching the object
    public void OnHoverExit();

    // Called once when the user presses Grab/Pinch while touching this object.
    public void OnInteractStart(Transform handTransform);

    // Called every frame while Grab/Pinch remains held.
    public void OnInteract(Transform handTransform);

    // Called once when the user releases Grab/Pinch.
    public void OnInteractEnd(Transform handTransform);
}

