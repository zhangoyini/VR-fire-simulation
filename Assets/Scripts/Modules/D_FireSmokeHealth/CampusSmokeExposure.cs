using Jiao.HealthTime;
using UnityEngine;

public sealed class CampusSmokeExposure : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private float graceSeconds = 6f;
    [SerializeField] private float damagePerSecond = 2f;
    // Gameplay balance only: a cloth does not filter toxic fire gases.
    [SerializeField,Range(.1f,1)] private float coveredDamageMultiplier = .4f;
    private float exposure;
    public float Exposure => exposure;
    public float GraceSeconds => graceSeconds;
    public float DamagePerSecond => damagePerSecond;
    public float CoveredDamageMultiplier => coveredDamageMultiplier;
    public bool IsProtected { get; private set; }
    private void Update()
    {
        var health = HealthTimeController.Instance;
        if (health == null || health.IsPaused || health.IsDepleted) return;
        bool inside = false;
        foreach (var zone in CampusSmokeVolume.Active)
            if (zone != null && trackedHead != null && zone.Contains(trackedHead.position)) { inside = true; break; }
        Tick(inside, Time.deltaTime);
    }
    public void Tick(bool inside, float seconds)
    {
        // MODEL API PLACEHOLDER: smoke does not remove health until module 05 is integrated.
        exposure = 0;
        IsProtected = false;
    }
}
