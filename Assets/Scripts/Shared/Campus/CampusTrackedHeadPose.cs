using UnityEngine;
using UnityEngine.XR;

// Always uses a real headset pose; cinematics never fabricate or freeze head tracking.
[DefaultExecutionOrder(-70)]
public sealed class CampusTrackedHeadPose : MonoBehaviour
{
    private void Update() { ReadPose(); }
    private void OnEnable() { Application.onBeforeRender+=ReadPose; }
    private void OnDisable() { Application.onBeforeRender-=ReadPose; }
    private void ReadPose()
    {
        if(CampusControllerInput.TryPose(XRNode.Head,out var position,out var rotation))
            transform.SetLocalPositionAndRotation(position,rotation);
    }
}
