using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteraction : MonoBehaviour,IInteractable
{
    //手靠近、碰到门把手时
    public void OnHoverEnter()
    {
        throw new System.NotImplementedException();
    }

    //手离开物体时
    public void OnHoverExit()
    {
        throw new System.NotImplementedException();
    }

    //持续按住期间
    public void OnInteract(Transform handTransform)
    {
        throw new System.NotImplementedException();
    }

    //松开时
    public void OnInteractEnd(Transform handTransform)
    {
        throw new System.NotImplementedException();
    }

    //按下 Grab / Pinch 开始交互时
    public void OnInteractStart(Transform handTransform)
    {
        throw new System.NotImplementedException();
    }

}
