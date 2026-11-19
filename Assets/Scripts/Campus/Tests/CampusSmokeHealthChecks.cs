#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Jiao.HealthTime;
using UnityEngine;

public sealed partial class CampusLessonDriver
{
    private IEnumerator CheckSmokeHealthRuntime(HealthTimeController health, CampusSmokeExposure exposure,
        CampusTowelInteractable towel, CampusHandInteractor hand, Transform head)
    {
        PutAtFace(hand.transform,towel,head);
        exposure.ResetExposure(); health.ResetHealth(); exposure.enabled=true;
        try
        {
            yield return new WaitForSeconds(3.5f);
            float strength=CampusSmokeExposure.SmokeStrengthAt(head.position);
            Check(strength==.75f && exposure.IsProtected,"Real Update samples fully developed 3F smoke and stable towel protection");
            float hp=health.CurrentHealth, started=Time.time;
            yield return new WaitForSeconds(1f);
            float expected=(.1f+strength)*.2f*(Time.time-started);
            Check(Mathf.Abs(hp-health.CurrentHealth-expected)<.025f,
                "Real Update order produces 0.17 HP/s with 3F wet-towel protection");
            hand.transform.position=head.position+Vector3.right; towel.OnInteract(hand.transform); Physics.SyncTransforms();
            yield return null;
            hp=health.CurrentHealth; started=Time.time;
            yield return new WaitForSeconds(1f);
            expected=(.1f+strength)*(Time.time-started);
            Check(!exposure.IsProtected && Mathf.Abs(hp-health.CurrentHealth-expected)<.025f,
                "Real Update restores 0.85 HP/s on 3F after removing the towel");
        }
        finally
        {
            exposure.enabled=false;
            PutAtFace(hand.transform,towel,head); exposure.ResetExposure(); exposure.Tick(false,.25f); health.ResetHealth();
        }
    }

    private void CheckSmokeHealthIntegration(HealthTimeController health, CampusSmokeExposure exposure,
        CampusTowelInteractable towel, CampusHandInteractor hand, Transform head, CampusSmokeSpread sceneSpread)
    {
        var root = new GameObject("Smoke health regression fixtures");
        Action<bool> cover = value =>
        {
            if (value) PutAtFace(hand.transform,towel,head);
            else { hand.transform.position=head.position+Vector3.right; towel.OnInteract(hand.transform); Physics.SyncTransforms(); }
        };
        try
        {
            Check(Mathf.Approximately(Field<float>(health,"baseDrainPerSecond"),.1f) &&
                Mathf.Approximately(Field<float>(health,"idleDrainPerSecond"),.02f) &&
                exposure.DamagePerSecond==1f && exposure.CoveredDamageMultiplier==.2f,
                "Saved health and smoke settings preserve the independent design values");
            Check(sceneSpread.LowerFloorAge>0 && sceneSpread.LowerFloorAge<sceneSpread.CorridorAge,
                "Formal lower-floor propagation excludes opening prewarm time");
            var stages = new CampusSmokeSpread.Stage[4];
            for (int i=0;i<4;i++)
            {
                var node=new GameObject("Smoke fixture floor "+(4-i)); node.transform.SetParent(root.transform);
                node.transform.position=new Vector3(1000+i*10,1000,1000);
                var particles=node.AddComponent<ParticleSystem>();
                var box=node.AddComponent<BoxCollider>(); box.isTrigger=true; box.center=Vector3.zero; box.size=Vector3.one*4;
                var zone=node.AddComponent<CampusSmokeVolume>();
                stages[i]=new CampusSmokeSpread.Stage { particles=particles,exposure=zone,floor=4-i,
                    delay=i*20,fillSeconds=10,strength=(4-i)*.25f,emissionRate=10 };
            }
            var spread=root.AddComponent<CampusSmokeSpread>(); spread.Configure(null,null,stages);
            spread.PrepareOpening(); spread.Advance(5);
            Check(stages[0].exposure.enabled && stages[0].Density==1 && spread.LowerFloorAge==0 &&
                stages.Skip(1).All(s=>s.Density==0 && !s.exposure.enabled && s.exposure.Strength==0),
                "Prewarming prepares only 4F, including when the alarm continues before the drill");
            spread.BeginExerciseSpread(); spread.Advance(21);
            Check(stages[1].Density>0 && !stages[1].exposure.enabled && stages[2].Density==0,
                "A faint new lower-floor plume appears before its damage volume activates");
            spread.Advance(4);
            float halfEmission=stages[1].particles.emission.rateOverTime.constant;
            float halfStrength=CampusSmokeExposure.SmokeStrengthAt(stages[1].particles.transform.position);
            Check(Mathf.Abs(halfStrength-.375f)<.0001f && stages[1].exposure.enabled &&
                stages.Skip(2).All(s=>!s.exposure.enabled),
                $"Half-grown 3F smoke has half of its 0.75 cap while lower floors stay clear (strength={halfStrength}, density={stages[1].Density})");
            Time.timeScale=0; spread.Advance(100);
            Check(spread.LowerFloorAge==25,"Pause freezes the independent lower-floor clock");
            Time.timeScale=1; spread.Advance(5);
            Check(stages[1].exposure.Strength==.75f &&
                stages[1].particles.emission.rateOverTime.constant>halfEmission,
                "Visible smoke emission and damage strength grow from the same stage progress");
            spread.Advance(20);
            Check(stages[2].exposure.Strength==.5f && !stages[3].exposure.enabled,
                "2F develops before 1F receives smoke exposure");
            spread.Advance(20);
            Check(stages[3].exposure.Strength==.25f,"1F eventually reaches its quarter-strength cap");

            cover(false); exposure.ResetExposure(); health.ResetHealth();
            exposure.Tick(false,10); health.Tick(10);
            Check(Mathf.Abs((1-health.HealthPercent)*100-.2f)<.002f,"The complete clear-air path loses 0.2 HP over ten seconds");
            foreach (var stage in stages)
            {
                float strength=CampusSmokeExposure.SmokeStrengthAt(stage.particles.transform.position);
                foreach (bool protectedTowel in new[]{false,true})
                {
                    cover(protectedTowel); exposure.ResetExposure(); health.ResetHealth();
                    if(protectedTowel) exposure.Tick(false,.25f);
                    exposure.Tick(strength,3); health.Tick(3);
                    Check(Mathf.Abs((1-health.HealthPercent)*100-(protectedTowel?.06f:.3f))<.002f,
                        $"Floor {stage.floor}: grace exempts smoke damage while base consumption remains (protected={protectedTowel})");
                    float before=health.CurrentHealth;
                    exposure.Tick(strength,10); health.Tick(10);
                    float expected=(.1f+stage.floor*.25f)*(protectedTowel?.2f:1f)*10;
                    Check(Mathf.Abs(before-health.CurrentHealth-expected)<.003f,
                        $"Floor {stage.floor}: real base plus smoke damage totals {expected:F2} HP in ten seconds (protected={protectedTowel})");
                }
            }
            cover(true); exposure.ResetExposure(); health.ResetHealth();
            exposure.Tick(false,.125f);
            Check(!exposure.ProtectionStable && Mathf.Approximately(health.CurrentBaseDrainPerSecond,.1f),
                "A newly raised towel does not reduce base loss before settling");
            Vector3 cloud=stages[0].particles.transform.position;
            exposure.RecordTeleport(cloud-Vector3.right*.9f,cloud+Vector3.right*.9f);
            Check(!exposure.ProtectionStable && !exposure.IsProtected,
                "One equivalent second of teleport exposure cannot settle a towel held for 0.125 real seconds");
            Time.timeScale=0; exposure.Tick(false,10);
            Check(!exposure.ProtectionStable,"Paused time cannot settle towel protection");
            Time.timeScale=1; exposure.Tick(false,.125f);
            Check(exposure.ProtectionStable && !exposure.IsProtected &&
                string.IsNullOrEmpty(CampusExperience.StatusLine(false,false,exposure.TowelReady)),
                "A towel can settle in clean air without adding protection wording to the HUD");
            exposure.Tick(true,0);
            var experience=FindObjectOfType<CampusExperience>(); experience.SendMessage("Update");
            Check(exposure.IsProtected && Mathf.Approximately(health.CurrentBaseDrainPerSecond,.02f) &&
                Field<bool>(FindObjectOfType<HealthTimeBar>(true),"towelProtected") &&
                FindObjectOfType<HealthTimeBar>(true).CurrentStateIcon == "icon_protection" &&
                !Field<TMPro.TextMeshProUGUI>(experience,"floorLabel").text.Contains("防护生效"),
                "Smoke damage, base loss, the blue bar and protection icon share stable protection");

            cover(false); exposure.ResetExposure(); health.ResetHealth(); exposure.Tick(true,3);
            cover(true); float hp=health.CurrentHealth;
            float unprotected=0,protectedTime=0;
            Action<float,bool,bool> measured=(seconds,active,teleport)=>
            { if(active) protectedTime+=seconds; else unprotected+=seconds; };
            exposure.ExposureMeasured+=measured;
            exposure.Tick(true,1); health.Tick(1);
            exposure.ExposureMeasured-=measured;
            Check(Mathf.Abs(hp-health.CurrentHealth-.44f)<.002f && unprotected==.25f && protectedTime==.75f,
                "A one-second real interval splits both losses and exposure at the 0.25-second settle boundary");
            cover(false); exposure.ResetExposure(); health.ResetHealth(); exposure.Tick(true,2.9f);
            cover(true); hp=health.CurrentHealth; exposure.Tick(true,.5f); health.Tick(.5f);
            Check(Mathf.Abs(hp-health.CurrentHealth-.23f)<.002f,
                "An interval crossing both grace and settle boundaries integrates their overlap exactly");
            float age=exposure.Exposure; cover(false); exposure.Tick(true,0);
            Check(!exposure.IsProtected && exposure.Exposure==age && health.CurrentBaseDrainPerSecond==.1f,
                "Removing the towel immediately clears protection without resetting smoke grace");
            spread.ResetSpread();
            Check(spread.RoomAge==0 && spread.LowerFloorAge==0 && stages.All(s=>s.Density==0 && !s.exposure.enabled),
                "Reset clears every clock, smoke stage and damage volume");
        }
        finally
        {
            Time.timeScale=1;
            DestroyImmediate(root);
            cover(true); exposure.ResetExposure(); exposure.Tick(false,.25f); health.ResetHealth();
        }
    }
}
#endif
