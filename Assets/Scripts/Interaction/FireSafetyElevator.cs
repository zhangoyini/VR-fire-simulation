using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireSafetyPrefabSupport))]
public sealed class FireSafetyElevator : MonoBehaviour
{
    [SerializeField] private Collider upButton;
    [SerializeField] private Collider downButton;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;

    public bool TryGetButton(Collider hit, Vector3 viewer, out Transform target)
    {
        target = null;
        if (!isActiveAndEnabled || (hit != upButton && hit != downButton)) return false;
        if (Vector3.Distance(viewer, hit.transform.position) > interactionDistance) return false;
        target = hit.transform;
        return true;
    }
}
