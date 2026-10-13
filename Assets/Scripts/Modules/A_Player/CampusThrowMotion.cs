using UnityEngine;

// Sample physical motion in tracking/rig space, never artificial joystick translation.
[DefaultExecutionOrder(25)]
public sealed class CampusThrowMotion : MonoBehaviour
{
    private struct Pose { public Vector3 p; public Quaternion q; public float time; }
    private readonly Pose[] samples=new Pose[24];
    private int count;
    private Transform space;
    public bool ReleaseAllowed { get; set; }
    private void Awake() { space=GetComponentInParent<CampusLocomotion>()?.transform; }
    private void Update()
    {
        var pose=GetComponent<CampusTrackedHandPose>();
        if(Time.timeScale==0 || (pose!=null && !pose.PhysicalTrackingValid)) { ResetHistory(); return; }
        Observe(space!=null?space.InverseTransformPoint(transform.position):transform.position,
            space!=null?Quaternion.Inverse(space.rotation)*transform.rotation:transform.rotation,Time.unscaledTime);
    }
    public void ResetHistory() { count=0; ReleaseAllowed=false; }
    public void Observe(Vector3 position,Quaternion rotation,float time)
    {
        if(count>0 && time<=samples[count-1].time) return;
        if(count==samples.Length) { System.Array.Copy(samples,1,samples,0,samples.Length-1); count--; }
        samples[count++]=new Pose { p=position,q=rotation,time=time };
    }
    public bool GetVelocity(float now,out Vector3 linear,out Vector3 angular)
    {
        // MODEL API PLACEHOLDER: safe gravity drop only; no sampled throw velocity.
        linear = angular = Vector3.zero;
        return false;
    }
    private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    public static void ApplyRelease(Transform hand,Rigidbody body)
    {
        body.velocity=body.angularVelocity=Vector3.zero;
        var motion=hand!=null?hand.GetComponent<CampusThrowMotion>():null;
        if(Time.timeScale>0 && motion!=null && motion.ReleaseAllowed && motion.GetVelocity(Time.unscaledTime,out var velocity,out var spin))
        {
            body.velocity=velocity; body.angularVelocity=spin;
            // Tangential speed at the held prop, including a wrist flick.
            body.velocity=Vector3.ClampMagnitude(body.velocity+Vector3.Cross(spin,body.worldCenterOfMass-hand.position),7);
        }
        body.WakeUp();
    }
}
