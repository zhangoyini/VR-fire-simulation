using UnityEngine;
using Valve.VR;

// One unscaled tracking-space conversion. No arm extension or head-relative scaling.
[DefaultExecutionOrder(20)]
public sealed class CampusTrackedHandPose : MonoBehaviour
{
    [SerializeField] private Transform trackingSpace,trackedController,trackedHead;
    [SerializeField] private SteamVR_Input_Sources inputSource;
    private CampusHandVisual visual;
    public bool PhysicalTrackingValid { get; private set; }
    public Transform TrackingSpace => trackingSpace;
    public void FollowRigTranslation(Vector3 delta)
    {
        if(trackingSpace!=null && !transform.IsChildOf(trackingSpace)) transform.position+=delta;
    }
    private void Awake() { visual=GetComponent<CampusHandVisual>(); }
    private void Update()
    {
        bool tracked=CampusControllerInput.TryPose(CampusControllerInput.Node(inputSource),out var position,out var rotation);
        if(tracked) ApplyLocalPose(position,rotation);
        else
        {
            var pose=trackedController!=null?trackedController.GetComponent<SteamVR_Behaviour_Pose>():null;
            tracked=SteamVR.active && pose!=null && pose.isValid;
            if(tracked) transform.SetPositionAndRotation(trackedController.position,trackedController.rotation);
            else if(!CampusControllerInput.HardwarePresent && trackedHead!=null)
            {
                // Visible editor preview, never substituted for a lost tracked controller.
                transform.SetPositionAndRotation(trackedHead.TransformPoint(new Vector3(inputSource==SteamVR_Input_Sources.LeftHand?-.22f:.22f,-.32f,.40f)),trackedHead.rotation);
                tracked=true;
            }
        }
        PhysicalTrackingValid=tracked && CampusControllerInput.HardwarePresent;
        visual?.SetTrackingVisible(tracked);
    }
    public void ApplyLocalPose(Vector3 position,Quaternion rotation)
    {
        transform.SetPositionAndRotation(trackingSpace.TransformPoint(position),trackingSpace.rotation*rotation);
        visual?.SetTrackingVisible(true);
    }
}
