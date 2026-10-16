#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed partial class CampusRevisionDriver
{
    private IEnumerator EvacuationChecks()
    {
        var movement = FindObjectOfType<CampusLocomotion>(true);
        var rig = movement.transform; var head = Camera.main.transform; var body = movement.Body;
        foreach (var model in FindObjectsOfType<CampusHandVisual>(true))
        {
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Check(renderer != null && renderer.sharedMesh != null && model.Bones.Length == 31 && renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit", "Textured 31-bone glove replaces the flat hand: "+model.name);
            Check(model.GetComponentsInChildren<LineRenderer>(true).All(l=>l.GetComponent<VRTeleporter>()!=null || l.GetComponentInParent<VRMenuRayPointer>()!=null), "3D glove retains teleport and menu rays");
            model.SetGesture(false,false,false); model.Tick(1); Quaternion rest = model.Bones[8].localRotation;
            var restMesh=new Mesh(); renderer.BakeMesh(restMesh);
            Vector3 proxy = model.transform.position;
            model.SetGesture(true,false,false); model.Tick(1);
            Check(Quaternion.Angle(rest,model.Bones[8].localRotation) > 5 && model.transform.position == proxy, "Trigger curls fingers without moving the tracked hand");
            var fistMesh=new Mesh(); renderer.BakeMesh(fistMesh);
            Check(restMesh.vertices.Zip(fistMesh.vertices,(a,b)=>Vector3.Distance(a,b)).Max()>.01f,"Finger pose deforms the actual skinned mesh, not only empty joints"); Destroy(restMesh); Destroy(fistMesh);
            model.SetGesture(true,true,false); model.Tick(1);
            Check(Quaternion.Angle(rest,model.Bones[8].localRotation)<.01f, "Door/window push restores the relaxed palm pose");
            var baked = new Mesh(); renderer.BakeMesh(baked);
            Check(baked.bounds.size.magnitude > .1f && baked.bounds.size.magnitude < .5f, "Glove skinning remains at a plausible hand scale"); Destroy(baked);
        }
        Check(FindObjectsOfType<CampusHandVisual>(true).Length==2, "Both tracked hands use articulated 3D geometry");
        foreach (var arrow in FindObjectsOfType<Transform>().Where(t=>t.name=="Unambiguous exit arrow" && Mathf.Abs(t.parent.position.z)<2 && Mathf.Abs(t.parent.position.x)<=20))
        {
            Vector3 desired = arrow.parent.position.x <= 0 ? Vector3.left : Vector3.right;
            Check(Vector3.Dot(arrow.right,desired)>.99f, "EXIT arrow points toward the nearest staircase at "+arrow.parent.position);
        }
        Check(FindObjectsOfType<Transform>().Where(t=>t.name=="Evacuation direction arrow" && Mathf.Abs(t.position.z)<1).All(t=>Vector3.Dot(t.forward,t.position.x<=0?Vector3.left:Vector3.right)>.99f), "Floor arrows agree with nearest-stair EXIT signs");
        var windows = FindObjectsOfType<CampusStairWindow>();
        Check(windows.Length==8, "Every A/B stair landing has an interactive window on all four floors");
        var hand = new GameObject("Stair window QA hand").transform;
        var realHands=FindObjectsOfType<CampusHandInteractor>(true);
        foreach (var window in windows)
        {
            var hinge=window.Hinge; var pane=hinge.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Opening glass pane");
            Check(window.GetComponentsInChildren<Renderer>().All(r=>!r.gameObject.isStatic && !r.isPartOfStaticBatch) && hinge.GetComponent<Rigidbody>().isKinematic,"Stair moving glass is dynamic geometry, not frozen static batches");
            foreach(var input in realHands)
            {
                foreach(var other in realHands) { other.Release(); other.transform.position=Vector3.one*100; }
                hinge.SetAngle(0);
                body.enabled=false; rig.SetPositionAndRotation(new Vector3(window.transform.position.x,window.transform.position.y-1.9f,-.57f),Quaternion.identity); body.enabled=true;
                Vector3 atGlass=window.transform.position+Vector3.down*.5f+Vector3.forward*.11f;
                input.transform.position+=atGlass-input.ContactPoint; Physics.SyncTransforms(); input.RefreshContact();
                Check(Field<MonoBehaviour>(input,"candidate")==hinge && Vector3.Distance(input.ContactPoint,head.position)<.75f,"Actual reachable palm contact selects A/B stair window: "+window.transform.position+" / "+input.InputSource);
                input.ProcessGrip(true,true,false); input.transform.position+=Vector3.back*.30f; input.ProcessGrip(false,true,false);
                Check(hinge.IsHeldBy(input.transform) && Mathf.Abs(hinge.Angle-90)<.1f && pane.transform.position.z<window.transform.position.z-.5f,"Actual trigger and physical push opens visible stair pane fully");
                input.ProcessGrip(false,false,true);
            }
            hinge.SetAngle(0); Vector3 closed=pane.transform.position;
            hand.position=closed+Vector3.forward*.04f; hinge.OnInteractStart(hand);
            hand.position+=Vector3.back*.15f; hinge.OnInteract(hand);
            Check(Mathf.Abs(hinge.Angle-45)<.1f && pane.transform.position.z<closed.z, "A 15 cm outward push opens the stair glass by 45 degrees: "+window.transform.position);
            hand.position+=Vector3.back*.15f; hinge.OnInteract(hand); hinge.OnInteractEnd(hand);
            Check(Mathf.Abs(hinge.Angle-90)<.1f, "Stair glass stops at 90 degrees");
        }
        var warned=windows[0].Hinge; warned.OnInteractStart(hand); warned.OnInteractEnd(hand);
        Check(CampusMessagePresenter.Instance.CurrentMessage==CampusMessagePresenter.WindowWarning, "Second grip on a stair window presents the bilingual no-jump warning");
        yield return new WaitForSeconds(1.8f); Check(warned.Angle>35, "Stair glass stays open for the warning interval");
        yield return new WaitForSeconds(1f); Check(warned.Angle==0, "Stair glass automatically closes after two seconds plus the closing animation");
        foreach(var window in windows) window.Hinge.SetAngle(0); Destroy(hand.gameObject);

        // The user restored the original A-stair pile. Verify B's complete clear descent;
        // A's real obstacle is checked below rather than pretending both routes are clear.
        head.localPosition=new Vector3(0,1.65f,0); head.localRotation=Quaternion.identity;
        foreach(int side in new[]{1})
        {
            float x=side*23;
            body.enabled=false; rig.SetPositionAndRotation(new Vector3(x+1.45f,11.4f,.7f),Quaternion.identity); body.enabled=true; Physics.SyncTransforms();
            movement.Step(Vector2.zero,0,.025f);
            for(int floor=3;floor>=1;floor--)
            {
                WalkTo(movement,new Vector2(x+1.45f,4.3f));
                WalkTo(movement,new Vector2(x-1.45f,4.3f));
                WalkTo(movement,new Vector2(x-1.45f,.7f));
                Check(Mathf.Abs(rig.position.y-(floor-1)*3.8f)<.3f, "Physical descent reaches "+floor+"F using Stair "+(side<0?"A":"B"));
                if(floor>1) WalkTo(movement,new Vector2(x+1.45f,.7f));
            }
        }
        var pile=FindObjectsOfType<Transform>().Single(t=>t.name=="3F Stair A collapsed storage pile");
        var cabinet=pile.Find("Toppled storage cabinet");
        Check(pile.localScale==Vector3.one && Vector3.Distance(cabinet.position,new Vector3(-21.55f,8.12f,1.55f))<.02f,"The cabinet and cartons are restored at original 3F A-stair scale/position");
        Check(pile.GetComponentsInChildren<Collider>().Length>=10 && !pile.GetComponentsInChildren<TextMeshPro>().Any(t=>t.text.Contains("CLOSED")),"Real debris remains collidable, without the CLOSED sign or an invisible blockade");
        body.enabled=false; rig.position=new Vector3(12,11.4f,0); body.enabled=true; Physics.SyncTransforms();
        var spread=FindObjectOfType<CampusSmokeSpread>(); spread.RoomDoor.SetAngle(60); spread.ResetSpread();
        var east=spread.Stages.Single(s=>s.particles.name=="4F corridor x=14");
        var west=spread.Stages.Single(s=>s.particles.name=="4F corridor x=-14");
        Check(Mathf.Approximately(east.emissionRate,west.emissionRate) && Mathf.Approximately(east.maximumSize,west.maximumSize) && Mathf.Approximately(east.fillSeconds,west.fillSeconds), "Both corridor directions use identical smoke speed, density and size parameters");
        Check(Mathf.Abs((east.delay-3)/Mathf.Abs(14+2.2f)-(west.delay-3)/Mathf.Abs(-14+2.2f))<.0001f,"Smoke arrival depends on path length, not a preferred escape side");
        spread.Advance(35);
        Check(west.exposure.enabled && spread.Stages.Where(s=>s.floor<=2).All(s=>!s.exposure.enabled), "Nearby smoke appears first without immediate lower-floor exposure");
        var arrivals=spread.Stages.Where(s=>s.particles.name.Contains("corridor") && s.floor<4).GroupBy(s=>s.floor).ToDictionary(g=>g.Key,g=>g.Min(s=>s.delay));
        Check(arrivals[3]<arrivals[2] && arrivals[2]<arrivals[1], "Smoke enters lower corridors in 3F, 2F, 1F order");
        foreach(int floor in new[]{3,2,1})
        {
            spread.ResetSpread(); spread.Advance(8+arrivals[floor]+9);
            Check(spread.Stages.Any(s=>s.floor==floor && s.particles.name.Contains("corridor") && s.exposure.enabled), "Visible staged smoke activates exposure on "+floor+"F");
            Check(spread.Stages.Where(s=>s.floor<floor && s.particles.name.Contains("corridor")).All(s=>!s.exposure.enabled), "No lower corridor fills before the "+floor+"F front");
        }
        spread.ResetSpread(); spread.Advance(250);
        Check(spread.Stages.All(s=>s.exposure.enabled), "A sustained fire can eventually spread throughout all four floors");
        Check(spread.Stages.Sum(s=>s.particles.main.maxParticles)<2500, "The staged smoke pool is capped below 2500 particles");
        spread.ResetSpread(); spread.RoomDoor.SetAngle(0);
        yield return PresentationChecks();
    }
    private void WalkTo(CampusLocomotion movement,Vector2 target)
    {
        movement.Step(Vector2.zero,0,.025f);
        for(int i=0;i<400;i++)
        {
            Vector3 p=movement.transform.position;
            Vector2 offset=target-new Vector2(p.x,p.z);
            if(offset.magnitude<.08f) { movement.Step(Vector2.zero,0,.025f); return; }
            movement.Step(offset.normalized,0,.025f);
        }
        throw new InvalidOperationException("Stair route blocked toward "+target+" at "+movement.transform.position);
    }
}
#endif
