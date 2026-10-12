using UnityEngine;

public class EscapeSuccessZone : MonoBehaviour
{
    [SerializeField] private ResolutionCoordinator resolution;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && resolution != null)
            resolution.ShowVictory();
    }
}
