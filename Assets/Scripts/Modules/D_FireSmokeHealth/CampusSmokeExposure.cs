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
        var health = HealthTimeController.Instance; if (Time.timeScale == 0 || health == null || health.IsPaused || health.IsDepleted) return;
        if(CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.DamageSuppressed) return;
        IsProtected=CampusTowelInteractable.HasProtection(trackedHead);
        if (!inside) { exposure = 0; return; }
        float previous = exposure; exposure += Mathf.Max(0,seconds);
        // Keep the smoke clock while protected: repeatedly moving the towel away must
        // not grant a fresh grace period in the same smoke cloud.
        float damagingTime = Mathf.Max(0, exposure - graceSeconds) - Mathf.Max(0, previous - graceSeconds);
        if (damagingTime > 0) health.TakeDamage(damagingTime * damagePerSecond * (IsProtected?coveredDamageMultiplier:1) * health.DrainMultiplier);
    }
}
