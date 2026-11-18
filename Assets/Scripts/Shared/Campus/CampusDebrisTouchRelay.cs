using UnityEngine;

// Receive events on the same object as each existing solid debris collider.
public sealed class CampusDebrisTouchRelay : MonoBehaviour
{
    [SerializeField] private CampusDebrisTouchPrompt prompt;
    [SerializeField] private Collider debrisCollider;
    internal bool IsContactEnabled => isActiveAndEnabled && debrisCollider != null &&
        debrisCollider.enabled && debrisCollider.gameObject.activeInHierarchy;

    private void OnTriggerEnter(Collider other) { Forward(other); }
    // Recover contacts after a component is re-enabled; the root deduplicates each pair.
    private void OnTriggerStay(Collider other) { Forward(other); }
    private void Forward(Collider other)
    {
        if (IsContactEnabled && prompt != null) prompt.Touch(this, other);
    }
    private void OnTriggerExit(Collider other)
    {
        if (prompt != null) prompt.Leave(this, other);
    }
    private void OnDisable()
    {
        if (prompt != null) prompt.RemoveSource(this);
    }
}
