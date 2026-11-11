using Jiao.HealthTime;
using System.Collections.Generic;
using UnityEngine;

public sealed class CampusSmokeExposure : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private float graceSeconds = 3f;
    [SerializeField] private float damagePerSecond = 1f;
    // Gameplay balance only: a cloth does not filter toxic fire gases.
    [SerializeField,Range(.05f,1f)] private float coveredDamageMultiplier = .5f;
    [SerializeField,Min(.1f)] private float equivalentWalkingSpeed = 1.8f;
    private float exposure;
    private bool wasInside;
    public float Exposure => exposure;
    public bool InSmoke => wasInside;
    public float GraceSeconds => graceSeconds;
    public float DamagePerSecond => damagePerSecond;
    public float CoveredDamageMultiplier => coveredDamageMultiplier;
    internal bool HasTowelProtection => CampusTowelInteractable.HasProtection(trackedHead);
    public bool IsProtected { get; private set; }
    internal event System.Action<float, bool, bool> ExposureMeasured;
    private void Update()
    {
        var health = HealthTimeController.Instance;
        if (health == null || health.IsPaused || health.IsDepleted) return;
        Vector3 position = trackedHead != null ? trackedHead.position : Vector3.zero;
        Tick(trackedHead != null ? SmokeStrengthAt(position) : 0f, Time.deltaTime, position, false);
    }
    internal static float SmokeStrengthAt(Vector3 point)
    {
        float strength = 0f;
        foreach (var zone in CampusSmokeVolume.Active)
            if (zone != null && zone.Strength > strength && zone.Contains(point)) strength = zone.Strength;
        return strength;
    }
    public void RecordTeleport(Vector3 from,Vector3 to)
    {
        // Exact box entry/exit fractions avoid skipping thin clouds or double-counting overlaps.
        var boundaries=new List<float> { 0,1 };
        foreach(var zone in CampusSmokeVolume.Active)
            if(zone!=null && zone.SegmentInterval(from,to,out float enter,out float exit))
            { boundaries.Add(enter); boundaries.Add(exit); }
        boundaries.Sort();
        float seconds=Vector3.Distance(from,to)/Mathf.Max(.1f,equivalentWalkingSpeed);
        for(int i=1;i<boundaries.Count;i++)
        {
            float a=boundaries[i-1],b=boundaries[i];
            if(b-a>.000001f)
            {
                Vector3 position = Vector3.Lerp(from,to,(a+b)*.5f);
                Tick(SmokeStrengthAt(position),seconds*(b-a),position,true);
            }
        }
        Tick(SmokeStrengthAt(to),0,to,true);
    }
    public void Tick(bool inside, float seconds) => Tick(inside ? 1f : 0f, seconds,
        trackedHead != null ? trackedHead.position : Vector3.zero, false);

    private void Tick(float strength, float seconds, Vector3 position, bool teleport)
    {
        var health = HealthTimeController.Instance; if (Time.timeScale == 0 || health == null || health.IsPaused || health.IsDepleted) return;
        bool inside = strength > 0f;
        if(inside && !wasInside) CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.SmokeWarning);
        wasInside=inside;
        if(!inside) { exposure=0; IsProtected=false; return; }
        if(CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.DamageSuppressed) return;
        IsProtected=HasTowelProtection;
        if (seconds > 0) ExposureMeasured?.Invoke(seconds, IsProtected, teleport);
        float previous = exposure; exposure += Mathf.Max(0,seconds);
        // Keep the smoke clock while protected: repeatedly moving the towel away must
        // not grant a fresh grace period in the same smoke cloud.
        float damagingTime = Mathf.Max(0, exposure - graceSeconds) - Mathf.Max(0, previous - graceSeconds);
        if (damagingTime > 0) health.TakeSmokeDamage(damagingTime * damagePerSecond * strength * (IsProtected?coveredDamageMultiplier:1) * health.DrainMultiplier, position);
    }
}
