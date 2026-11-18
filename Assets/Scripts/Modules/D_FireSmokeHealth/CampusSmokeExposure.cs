using Jiao.HealthTime;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class CampusSmokeExposure : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private float graceSeconds = CampusHealthSmokeSettings.GraceSeconds;
    [SerializeField] private float damagePerSecond = CampusHealthSmokeSettings.SmokeDamage;
    // Gameplay balance only: a cloth does not filter toxic fire gases.
    [SerializeField,Range(.05f,1f)] private float coveredDamageMultiplier = CampusHealthSmokeSettings.CoveredMultiplier;
    [Tooltip("湿毛巾要稳定遮挡这么久才算生效，避免手抖导致防护状态频繁闪烁")]
    [SerializeField,Min(0f)] private float protectionSettleSeconds = CampusHealthSmokeSettings.ProtectionSettleSeconds;
    [SerializeField,Min(.1f)] private float equivalentWalkingSpeed = 1.8f;
    private float exposure;
    private bool wasInside;
    private float protectionHeld;
    /// <summary>防护状态变化（true = 湿毛巾开始生效）。供 HUD 文字与音效使用。</summary>
    internal event System.Action<bool> ProtectionChanged;
    public float Exposure => exposure;
    public bool InSmoke => wasInside;
    public float GraceSeconds => graceSeconds;
    public float DamagePerSecond => damagePerSecond;
    public float CoveredDamageMultiplier => coveredDamageMultiplier;
    public bool TowelReady => CampusTowelInteractable.HasProtection(trackedHead);
    public bool ProtectionStable => TowelReady && protectionHeld >= protectionSettleSeconds;
    internal float BaseProtectionMultiplier { get; private set; } = 1f;
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

    internal void Tick(float strength, float seconds) => Tick(strength, seconds,
        trackedHead != null ? trackedHead.position : Vector3.zero, false);

    public void ResetExposure()
    {
        exposure = protectionHeld = 0f;
        wasInside = false;
        BaseProtectionMultiplier = 1f;
        SetProtected(false);
    }

    private void Tick(float strength, float seconds, Vector3 position, bool teleport)
    {
        var health = HealthTimeController.Instance; if (Time.timeScale == 0 || health == null || health.IsPaused || health.IsDepleted) return;
        bool inside = strength > 0f;
        if(inside && !wasInside) CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.SmokeWarning);
        wasInside=inside;
        seconds = Mathf.Max(0f, seconds);
        bool holding = TowelReady;
        if (!holding) protectionHeld = 0f;
        // Only real game time settles a towel. Synthetic walking time never does.
        float unprotectedSeconds = teleport ? (ProtectionStable ? 0f : seconds) :
            holding ? Mathf.Clamp(protectionSettleSeconds - protectionHeld, 0f, seconds) : seconds;
        if (!teleport)
        {
            if (holding) protectionHeld = Mathf.Min(protectionSettleSeconds, protectionHeld + seconds);
            BaseProtectionMultiplier = seconds > 0f
                ? (unprotectedSeconds + (seconds - unprotectedSeconds) * coveredDamageMultiplier) / seconds
                : ProtectionStable ? coveredDamageMultiplier : 1f;
        }
        SetProtected(inside && ProtectionStable);
        // Clean air resets smoke grace, but preserves a towel already settled outside smoke.
        if(!inside) { exposure=0; return; }
        if(CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.DamageSuppressed) return;
        if (unprotectedSeconds > 0f) ExposureMeasured?.Invoke(unprotectedSeconds, false, teleport);
        if (seconds > unprotectedSeconds) ExposureMeasured?.Invoke(seconds - unprotectedSeconds, true, teleport);
        float previous = exposure; exposure += seconds;
        // Keep the smoke clock while protected: repeatedly moving the towel away must
        // not grant a fresh grace period in the same smoke cloud.
        float graceRemaining = Mathf.Max(0f, graceSeconds - previous);
        float unprotectedDamageTime = Mathf.Max(0f, unprotectedSeconds - graceRemaining);
        float protectedDamageTime = Mathf.Max(0f, seconds - Mathf.Max(unprotectedSeconds, graceRemaining));
        float weightedTime = unprotectedDamageTime + protectedDamageTime * coveredDamageMultiplier;
        if (weightedTime > 0f) health.TakeSmokeDamage(weightedTime * damagePerSecond * strength * health.DrainMultiplier, position);
    }

    private void SetProtected(bool value)
    {
        if (IsProtected == value) return;
        IsProtected = value;
        ProtectionChanged?.Invoke(value);
    }
}
