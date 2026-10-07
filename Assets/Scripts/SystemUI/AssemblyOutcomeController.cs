using Harbourview.Journey;
using Jiao.HealthTime;
using UnityEngine;

// Scene-local endpoint integration. Entry health/state transfer belongs to the transition owner.
public class AssemblyOutcomeController : MonoBehaviour
{
    [SerializeField] private SafeZoneEndpoint endpoint;
    [SerializeField] private GeoFence completionZone;
    [SerializeField] private ResolutionCoordinator resolution;
    [SerializeField] private HealthTimeController health;
    [SerializeField] private HealthTimeBar healthBar;
    [SerializeField] private Jiao.HealthTime.AudioManager audioManager;

    private void Start()
    {
        if (endpoint == null || completionZone == null || resolution == null || health == null)
        {
            Debug.LogError("[AssemblyOutcome] Missing endpoint, zone, outcome gate or health reference.", this);
            enabled = false;
            return;
        }
        endpoint.onReached.AddListener(OnReached);
        health.Depleted += OnDepleted;
    }

    private void OnDestroy()
    {
        if (endpoint != null) endpoint.onReached.RemoveListener(OnReached);
        if (health != null) health.Depleted -= OnDepleted;
    }

    private void OnReached()
    {
        if (endpoint.Type != SafeZoneEndpoint.EndpointType.GroundAssembly || health.IsDepleted)
            return;
        if (!resolution.TryShowVictory())
            return;

        health.Pause(true);
        if (healthBar != null) healthBar.StopWarning();
        if (audioManager != null) audioManager.StopWarning();
        completionZone.enabled = false;
        SessionLogger.Write("TRAINING_COMPLETED");
    }

    private void OnDepleted()
    {
        // Prevent a later arrival from changing the journey state after failure.
        completionZone.enabled = false;
    }
}
