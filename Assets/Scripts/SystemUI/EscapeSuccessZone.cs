using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EscapeSuccessZone : MonoBehaviour
{
    [SerializeField] private ResolutionCoordinator resolution;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private Transform playerRoot;
    [SerializeField, Min(0.1f)] private float dwellSeconds = 0.8f;

    private bool playerInside;
    private float dwellTimer;

    private void Awake()
    {
        if (resolution == null)
            Debug.LogError($"{nameof(EscapeSuccessZone)} on {name} has no ResolutionCoordinator assigned.", this);

        if (playerCollider == null && playerRoot == null)
            Debug.LogError($"{nameof(EscapeSuccessZone)} on {name} needs a player collider or player root.", this);

        bool hasTrigger = false;
        foreach (Collider collider in GetComponents<Collider>())
        {
            if (collider != null && collider.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
            Debug.LogError($"{nameof(EscapeSuccessZone)} on {name} needs at least one trigger collider.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = true;
            dwellTimer = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Covers a teleport that places the body inside the zone without a reliable Enter callback.
        if (IsPlayer(other) && !playerInside)
        {
            playerInside = true;
            dwellTimer = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
            dwellTimer = 0f;
        }
    }

    private void Update()
    {
        if (!playerInside || resolution == null
            || resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None)
            return;

        dwellTimer += Time.deltaTime;
        if (dwellTimer < dwellSeconds)
            return;

        if (resolution.TryShowVictory())
            enabled = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (playerCollider != null)
            return other == playerCollider;

        if (playerRoot != null)
            return other.transform == playerRoot || other.transform.IsChildOf(playerRoot);

        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }
}
