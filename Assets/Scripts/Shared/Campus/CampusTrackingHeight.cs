using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

// Offset a shared tracking origin, never scale the world or overwrite HMD/hand poses.
[DefaultExecutionOrder(-60)]
public sealed class CampusTrackingHeight : MonoBehaviour
{
    [SerializeField] private Transform trackedHead, trackingSpace;
    [SerializeField, Range(1.4f,1.9f)] private float standingEyeHeight=1.70f;
    private float stableTime, lastSample;
    private readonly List<XRInputSubsystem> subsystems=new List<XRInputSubsystem>();
    private XRInputSubsystem activeSubsystem;
    public bool FloorRelative { get; private set; }
    public bool Calibrated { get; private set; }
    public float HeightOffset => trackingSpace!=null?trackingSpace.localPosition.y:0;
    private void Update()
    {
        if(Time.timeScale==0 || trackedHead==null || trackingSpace==null) return;
        SubsystemManager.GetInstances(subsystems);
        foreach(var subsystem in subsystems)
        {
            if(!subsystem.running) continue;
            if(activeSubsystem!=subsystem)
            {
                activeSubsystem=subsystem;
                if((subsystem.GetSupportedTrackingOriginModes()&TrackingOriginModeFlags.Floor)!=0)
                    subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                Recalibrate();
            }
            bool floor=subsystem.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor;
            if(floor!=FloorRelative) { FloorRelative=floor; Recalibrate(); }
            if(floor) trackingSpace.localPosition=Vector3.zero;
            break;
        }
        if(Calibrated) return;
        var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool valid=device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked;
        Observe(trackedHead.localPosition.y,valid,Time.deltaTime);
    }
    public void Observe(float rawHeight,bool tracked,float seconds)
    { Observe(rawHeight,tracked,seconds,FloorRelative); }
    public void Observe(float rawHeight,bool tracked,float seconds,bool floorRelative)
    {
        if(!tracked) { stableTime=0; return; }
        if(Calibrated || trackingSpace==null || seconds<=0 || float.IsNaN(rawHeight) || float.IsInfinity(rawHeight)) return;
        FloorRelative=floorRelative;
        if(floorRelative) { trackingSpace.localPosition=Vector3.zero; Calibrated=true; return; }
        // Wait for a stable valid pose, not the scene's editor preview height.
        stableTime=Mathf.Abs(rawHeight-lastSample)<.025f?stableTime+seconds:0;
        lastSample=rawHeight;
        if(stableTime<.65f) return;
        // Positive floor-like height must not be raised to a nominal adult height:
        // doing that would also lift the real floor beyond the user's hand reach.
        trackingSpace.localPosition=Vector3.up*(rawHeight<.45f?standingEyeHeight-rawHeight:0);
        Calibrated=true;
    }
    public void Recalibrate()
    { Calibrated=false; stableTime=0; }
}
