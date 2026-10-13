using UnityEngine;

// One Player-tagged body volume for smoke, refuge and assembly triggers, following the HMD.
[DefaultExecutionOrder(50)]
public sealed class CampusBodyTracker : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    private void FixedUpdate()
    {
        if (trackedHead != null) transform.position = trackedHead.position + Vector3.down * 0.7f;
    }
}
