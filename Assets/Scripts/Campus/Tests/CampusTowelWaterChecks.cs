#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jiao.HealthTime;
using UnityEngine;

public sealed partial class CampusRevisionDriver
{
    private IEnumerator TowelWaterChecks(CampusHandInteractor[] hands,Transform head,HealthTimeController health,CampusSmokeExposure exposure)
    {
        var faucets=FindObjectsOfType<CampusFaucet>(); var towels=FindObjectsOfType<CampusTowelInteractable>().OrderByDescending(t=>t.transform.position.y).ToArray();
        Check(faucets.All(f=>!f.IsOn && !f.FlowVisual.activeSelf && !f.WaterVolume.enabled),"All eight faucets initially have water off and no active wetting volume");
        Check(faucets.All(f=>f.WaterAudio!=null && f.WaterAudio.clip!=null && f.WaterAudio.spatialBlend==1),"Every faucet has a spatial running-water loop");
        Check(towels.All(t=>t.GetComponent<TowelPickup>()==null && t.GetComponent<PickableTowel>()==null),"New restroom towels replace the old consumed/healing pickups without duplicate interactions");
        Check(towels.All(t=>t.Body.mass<.3f && t.Body.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic),"Lightweight solid towels use continuous collision for dropping and re-grabbing");
        var basins=FindObjectsOfType<MeshCollider>().Where(c=>c.name=="Open ceramic basin").ToArray();
        Check(basins.Length==8 && FindObjectsOfType<MeshCollider>().Count(c=>c.name=="Sealed oval basin surround")==8,"All eight bowls have sealed oval countertop surrounds");
        Physics.SyncTransforms();
        foreach(var basin in basins)
        {
            Vector3 start=basin.transform.position+new Vector3(.15f,1.05f,0);
            Check(Physics.Raycast(start,Vector3.down,out var bowlHit,.8f,~0,QueryTriggerInteraction.Ignore) && bowlHit.collider==basin &&
                bowlHit.point.y<basin.transform.position.y+.85f,"A towel can reach inside the open bowl; no counter slab blocks it");
            start=basin.transform.position+new Vector3(.405f,1.05f,.245f);
            Check(Physics.Raycast(start,Vector3.down,out var cornerHit,.8f,~0,QueryTriggerInteraction.Ignore) &&
                cornerHit.collider.name=="Sealed oval basin surround" && Mathf.Abs(cornerHit.point.y-basin.transform.position.y-.94f)<.005f,
                "Countertop corners around the oval bowl are closed rather than leaving gaps");
        }
        bool exposureEnabled=exposure.enabled; exposure.enabled=false;
        for(int side=0;side<2;side++)
        {
            var owner=hands[side]; var free=hands[1-side]; var towel=towels[side];
            var faucet=faucets.OrderBy(f=>Vector3.Distance(f.WaterVolume.transform.position,towel.transform.position)).First();
            var pulses=new List<float>(); Action<float> record=amplitude=>pulses.Add(amplitude); owner.HapticRequested+=record;
            owner.transform.SetPositionAndRotation(towel.Surface.bounds.center,Quaternion.identity); free.transform.position=Vector3.one*100;
            Physics.SyncTransforms(); owner.RefreshContact();
            Check(Field<MonoBehaviour>(owner,"candidate")==towel,"A hand selects the dry towel surface without a ray: "+owner.InputSource);
            // The second hand also tests arriving while its trigger is already held.
            owner.ProcessGrip(side==0,true,false);
            Check(towel.IsHeld && towel.HoldingHand==owner.transform && towel.Body.isKinematic && !towel.Body.useGravity,"Held trigger attaches the towel to "+owner.InputSource);
            Check(pulses.Count==1 && Mathf.Approximately(pulses[0],.3f),"Pickup routes one vibration request to the owning controller: "+owner.InputSource);
            Check(!free.Begin(towel) && towel.HoldingHand==owner.transform,"A second hand cannot steal the same held towel");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(0,-.10f,.13f)));
            health.ResetHealth(); exposure.Tick(false,0); exposure.Tick(true,7);
            Check(!towel.CoversFace(head) && Mathf.Abs(health.HealthPercent-.98f)<.001f,"A dry towel near the mouth does not stop smoke damage");
            PositionCloth(owner,towel,faucet.WaterVolume.transform.position); towel.TickWetting(10);
            Check(!towel.IsWet && towel.WetProgress==0,"Water-off basin contact cannot wet a towel");
            PressFaucet(free,faucet);
            Check(faucet.IsOn && faucet.FlowVisual.activeSelf && faucet.WaterVolume.enabled,"Free hand toggles the faucet on while the other hand holds the towel");
            float lever=faucet.transform.localEulerAngles.x;
            free.ProcessGrip(false,true,false);
            Check(faucet.IsOn && Mathf.Abs(Mathf.DeltaAngle(lever,faucet.transform.localEulerAngles.x))<.01f,"Holding the faucet trigger does not repeatedly toggle water");
            free.ProcessGrip(false,false,true);
            towel.TickWetting(.5f); Check(!towel.IsWet && towel.WetProgress>.3f,"The towel needs sustained contact rather than instantly becoming wet");
            PositionCloth(owner,towel,faucet.WaterVolume.transform.position+Vector3.right*.7f); towel.TickWetting(.1f);
            Check(towel.WetProgress==0,"Moving away from the water interrupts wetting");
            PositionCloth(owner,towel,faucet.WaterVolume.transform.position); towel.TickWetting(1.49f);
            Check(!towel.IsWet,"A short watering interval still leaves the towel dry"); towel.TickWetting(.02f);
            Check(towel.IsWet && towel.WetProgress==1 && towel.GetComponent<Renderer>().sharedMaterial.name=="Wet cotton towel","Sustained running water changes the cloth to its wet material/state");
            Check(towel.Body.isKinematic && towel.HoldingHand==owner.transform,"Wetting never releases or consumes the held towel");
            Check(Mathf.Approximately(health.DrainMultiplier,1),"A wet towel grants no permanent or timed health multiplier");
            PressFaucet(free,faucet); free.ProcessGrip(false,false,true);
            Check(!faucet.IsOn && !faucet.FlowVisual.activeSelf && !faucet.WaterVolume.enabled,"A second press turns water and its wetting volume off");
            Check(towel.IsWet,"Turning the tap off does not instantly dry a wet towel");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(.6f,-.1f,.1f)));
            health.ResetHealth(); exposure.Tick(false,0); exposure.Tick(true,7);
            Check(!exposure.IsProtected && Mathf.Abs(health.HealthPercent-.98f)<.001f,"A wet towel held away from the face does not stop smoke damage");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(0,-.1f,-.12f)));
            Check(!towel.CoversFace(head),"A towel behind the head is not treated as mouth/nose coverage");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(0,-.10f,.13f)));
            health.ResetHealth(); exposure.Tick(false,0); exposure.Tick(true,12);
            Check(towel.CoversFace(head) && exposure.IsProtected && Mathf.Abs(health.HealthPercent-.952f)<.001f,"Sustained wet cloth coverage reduces but never eliminates smoke damage for "+owner.InputSource);
            Check(exposure.Exposure==12,"Protection does not reset the ongoing smoke exposure clock");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(0,-.1f,.7f))); exposure.Tick(true,1);
            Check(Mathf.Abs(health.HealthPercent-.932f)<.001f,"Moving the wet towel away restores damage without a fresh grace exploit");
            PositionCloth(owner,towel,head.TransformPoint(new Vector3(0,-.10f,.13f))); exposure.Tick(true,1);
            Check(Mathf.Abs(health.HealthPercent-.924f)<.001f,"Bringing the still-held wet towel back slows damage again");
            float beforeDrop=towel.transform.position.y;
            owner.ProcessGrip(false,false,true);
            Check(!towel.IsHeld && !towel.Body.isKinematic && towel.Body.useGravity && towel.gameObject.activeSelf,"Trigger release drops a real, reusable towel rather than hiding it");
            Check(pulses.Count==2 && Mathf.Approximately(pulses[1],.22f),"Release routes one vibration request to the same controller: "+owner.InputSource);
            owner.HapticRequested-=record;
            exposure.Tick(true,1);
            Check(!exposure.IsProtected && Mathf.Abs(health.HealthPercent-.904f)<.001f,"Dropped wet cloth immediately loses smoke protection, even beside the face");
            owner.transform.position=Vector3.one*100; free.transform.position=Vector3.one*100;
            yield return new WaitForSeconds(.4f);
            Check(towel.transform.position.y<beforeDrop-.25f && towel.IsWet,"Released wet towel falls under gravity and keeps its wet state");
            free.transform.position=towel.Surface.bounds.center; Physics.SyncTransforms(); free.RefreshContact(); free.ProcessGrip(true,true,false);
            Check(towel.HoldingHand==free.transform && towel.IsWet,"The other hand can re-grab a dropped wet towel");
            // Lost release edges also release the cloth when the held state becomes false.
            free.ProcessGrip(false,false,false);
            Check(!towel.IsHeld && towel.Body.useGravity,"A missing trigger-up event cannot leave a towel stuck to the hand");
        }
        var dry=towels[2]; var tap=faucets.OrderBy(f=>Vector3.Distance(f.WaterVolume.transform.position,dry.transform.position)).First();
        var h=hands[0]; h.transform.SetPositionAndRotation(dry.transform.position,Quaternion.identity); h.Begin(dry); PositionCloth(h,dry,tap.WaterVolume.transform.position); tap.SetWater(true);
        var pause=FindObjectOfType<SystemUIPauseController>(); pause.ShowPause(); dry.TickWetting(5); tap.OnInteractStart(hands[1].transform);
        Check(!dry.IsWet && tap.IsOn,"Pause freezes wetting and ignores faucet switch input");
        h.ProcessGrip(false,true,false); Check(!dry.IsHeld,"Pause safely releases held cloth without leaving a persistent shield");
        pause.ResumeGame(); tap.SetWater(false);
        Check(!CampusTowelInteractable.HasProtection(head),"No dropped towel anywhere in the scene grants passive protection");
        exposure.Tick(false,0); exposure.enabled=exposureEnabled; health.ResetHealth();
        foreach(var hand in hands) { hand.Release(); hand.transform.position=Vector3.one*100; hand.RefreshContact(); }
    }
    private void PositionCloth(CampusHandInteractor hand,CampusTowelInteractable towel,Vector3 p)
    { hand.transform.position=p-hand.transform.rotation*new Vector3(0,0,.065f); towel.OnInteract(hand.transform); Physics.SyncTransforms(); }
    private void PressFaucet(CampusHandInteractor hand,CampusFaucet tap)
    {
        hand.transform.position=tap.GetComponent<BoxCollider>().bounds.center; Physics.SyncTransforms(); hand.RefreshContact();
        Check(Field<MonoBehaviour>(hand,"candidate")==tap,"Only the faucet switch is selected at direct contact"); hand.ProcessGrip(true,true,false);
    }
}
#endif
