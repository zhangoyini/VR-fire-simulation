using Jiao.HealthTime;
using System.Collections.Generic;
using UnityEngine;

public sealed class CampusSmokeExposure : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private float graceSeconds = 3f;
    [SerializeField] private float damagePerSecond = 2f;
    // Gameplay balance only: a cloth does not filter toxic fire gases.
    [SerializeField,Range(.1f,1)] private float coveredDamageMultiplier = .4f;
    [SerializeField,Min(.1f)] private float equivalentWalkingSpeed = 1.8f;
    private float exposure;
    private bool wasInside;
    public float Exposure => exposure;
    public float GraceSeconds => graceSeconds;
    public float DamagePerSecond => damagePerSecond;
    public float CoveredDamageMultiplier => coveredDamageMultiplier;
    public bool IsProtected { get; private set; }
    private void Update()
    {
        var health = HealthTimeController.Instance;
        if (health == null || health.IsPaused || health.IsDepleted) return;
        Tick(trackedHead!=null && ContainsSmoke(trackedHead.position), Time.deltaTime);
    }
    private static bool ContainsSmoke(Vector3 point)
    {
        foreach(var zone in CampusSmokeVolume.Active) if(zone!=null && zone.Contains(point)) return true;
        return false;
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
            if(b-a>.000001f) Tick(ContainsSmoke(Vector3.Lerp(from,to,(a+b)*.5f)),seconds*(b-a));
        }
        Tick(ContainsSmoke(to),0);
    }
    public void Tick(bool inside, float seconds)
    {
        var health = HealthTimeController.Instance; if (Time.timeScale == 0 || health == null || health.IsPaused || health.IsDepleted) return;
        if(inside && !wasInside) CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.SmokeWarning);
        wasInside=inside;
        if(!inside) { exposure=0; IsProtected=false; return; }
        if(CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.DamageSuppressed) return;
        IsProtected=CampusTowelInteractable.HasProtection(trackedHead);
        float previous = exposure; exposure += Mathf.Max(0,seconds);
        // Keep the smoke clock while protected: repeatedly moving the towel away must
        // not grant a fresh grace period in the same smoke cloud.
        float damagingTime = Mathf.Max(0, exposure - graceSeconds) - Mathf.Max(0, previous - graceSeconds);
        if (damagingTime > 0) health.TakeDamage(damagingTime * damagePerSecond * (IsProtected?coveredDamageMultiplier:1) * health.DrainMultiplier);
    }
}
