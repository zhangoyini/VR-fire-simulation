using UnityEngine;
using Valve.VR;

// Controller-driven fallback poses: no optical hand tracking or external downloads required.
// Only finger bones animate; the tracked proxy/wrist and interaction distances never move.
public sealed class CampusHandVisual : MonoBehaviour
{
    [SerializeField] private Transform[] bones;
    [SerializeField] private SteamVR_Input_Sources inputSource;
    [SerializeField] private SteamVR_Skeleton_Pose relaxed, fist, point;
    private bool gripping, pushing, pointing;
    private bool presentationVisible=true,trackingVisible=true;
    private SkinnedMeshRenderer[] meshes;
    public Transform[] Bones => bones;
    public Vector3 ContactPoint => bones!=null && bones.Length>1 && bones[1]!=null?bones[1].position:transform.position;
    public void SetPresentationVisible(bool visible) { presentationVisible=visible; RefreshVisibility(); }
    public void SetTrackingVisible(bool visible) { trackingVisible=visible; RefreshVisibility(); }
    private void RefreshVisibility()
    {
        if(meshes==null) meshes=GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bool visible=presentationVisible && trackingVisible;
        var root=transform.Find("Detailed tracked glove"); if(root!=null && visible && !root.gameObject.activeSelf) root.gameObject.SetActive(true);
        foreach(var renderer in meshes) if(renderer!=null) { renderer.enabled=visible; renderer.updateWhenOffscreen=true; }
    }
    public void Configure(Transform[] rig, SteamVR_Input_Sources source,
        SteamVR_Skeleton_Pose rest, SteamVR_Skeleton_Pose closed, SteamVR_Skeleton_Pose finger)
    { bones = rig; inputSource = source; relaxed = rest; fist = closed; point = finger; meshes=null; Tick(1); }
    public void SetGesture(bool grip, bool push, bool button)
    { gripping = grip; pushing = push; pointing = button; }
    private void LateUpdate() { Tick(Time.unscaledDeltaTime); RefreshVisibility(); }
    public void Tick(float dt)
    {
        var pose = gripping && pointing ? point : gripping && !pushing ? fist : relaxed;
        var hand = pose != null ? pose.GetHand(inputSource) : null;
        if (hand == null || bones == null || hand.boneRotations == null) return;
        float blend = 1 - Mathf.Exp(-16 * Mathf.Max(0, dt));
        // SteamVR's ordered hierarchy: 0=root, 1=wrist, 2..30=fingers/auxiliary joints.
        // Do not apply the pose asset's HMD-relative root/wrist offset to a controller.
        for (int i = 2; i < Mathf.Min(bones.Length, hand.boneRotations.Length); i++)
        {
            if (bones[i] == null) continue;
            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, hand.boneRotations[i], blend);
            if (hand.bonePositions != null && i < hand.bonePositions.Length)
                bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, hand.bonePositions[i], blend);
        }
    }
}
