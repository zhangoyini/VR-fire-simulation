using UnityEngine;

// Presentation only: follows the tracked head for a HUD, or places an outcome once.
public class WorldSpaceUIAnchor : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private Transform presentationRoot;
    [SerializeField] private bool followHead;
    [SerializeField] private Vector3 headLocalOffset = new Vector3(0f, -0.15f, 0.55f);

    private void OnEnable()
    {
        Place();
    }

    private void LateUpdate()
    {
        if (followHead)
            Place();
    }

    private void Place()
    {
        if (trackedHead == null || presentationRoot == null)
            return;

        if (followHead)
        {
            presentationRoot.SetPositionAndRotation(
                trackedHead.TransformPoint(headLocalOffset), trackedHead.rotation);
            return;
        }

        // Keep the failure menu upright and fixed after it appears, even when looking down.
        Vector3 forward = Vector3.ProjectOnPlane(trackedHead.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(trackedHead.up, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        Quaternion facing = Quaternion.LookRotation(forward.normalized, Vector3.up);
        Vector3 offset = facing * headLocalOffset;

        // Placement clearance only; button input continues to use Henry's trigger system.
        if (Physics.Raycast(trackedHead.position, offset.normalized, out RaycastHit hit,
            offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            offset = offset.normalized * Mathf.Max(0.05f, hit.distance - 0.05f);

        presentationRoot.SetPositionAndRotation(trackedHead.position + offset, facing);
    }
}
