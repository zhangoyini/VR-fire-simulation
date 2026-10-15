#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEngine;
using Valve.VR;

// Play-mode test component is outside Editor; excluded entirely from player builds.
public sealed partial class CampusRevisionDriver : MonoBehaviour
{
    private readonly List<string> checks=new List<string>(); private float deadline;
    private void Start() { deadline=Time.realtimeSinceStartup+100; StartCoroutine(Guard(Checks())); }
    private void Update() { if(Time.realtimeSinceStartup>deadline) Finish(false,string.Join("\n",checks)+"\nTimeout"); }
    private static void Finish(bool pass,string report)
    {
        SessionState.SetBool("Campus.RevisionQA",false);
        File.WriteAllText("Documentation/RevisionChecks.txt",report);
        Debug.Log((pass?"CAMPUS_REVISION_PASS":"CAMPUS_REVISION_FAIL")+"\n"+report);
        EditorApplication.Exit(pass?0:1);
    }
    private IEnumerator Guard(IEnumerator test)
    {
        // Unroll nested enumerators here so an assertion cannot silently strand batch mode.
        var stack=new Stack<IEnumerator>(); stack.Push(test);
        while(stack.Count>0)
        {
            object next;
            try { var current=stack.Peek(); if(!current.MoveNext()) { stack.Pop(); continue; } next=current.Current; }
            catch(Exception e) { Finish(false,string.Join("\n",checks)+"\n"+e); yield break; }
            if(next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish(true,string.Join("\n",checks)+"\nQuest input / haptics / comfort / GPU performance: not hardware tested.");
    }
    private void Check(bool condition,string label) { if(!condition) throw new InvalidOperationException("FAIL: "+label); checks.Add("PASS: "+label); }
    private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    private IEnumerator Checks()
    {
        yield return null; yield return null;
        var head=Camera.main.transform; var health=HealthTimeController.Instance;
        var movement=FindObjectOfType<CampusLocomotion>(true); var rig=movement.transform; var body=movement.Body;
        var spread=FindObjectOfType<CampusSmokeSpread>(); spread.enabled=false; spread.ResetSpread();
        var hands=FindObjectsOfType<CampusHandInteractor>(true);
        Check(FindObjectsOfType<VRTeleporter>(true).Length==0 && FindObjectsOfType<VRFireSafetyInteraction>(true).Length==0 && FindObjectsOfType<VRMenuRayPointer>(true).Length==0,"No teleport or interaction/menu ray adapters remain in the new scene");
        Check(hands.Length==2 && hands.Select(h=>Field<SteamVR_Input_Sources>(h,"inputSource")).Distinct().Count()==2,"Left and right contact inputs are separately bound");
        Check(SteamVR_Input.GetVector2Action("Walk")!=null,"Left-stick Walk action exists");
        Check(Field<AudioClip[]>(movement,"footstepClips").Length==3 && Field<AudioSource>(movement,"footsteps")!=null,"Walking retains three varied footstep clips");
        Check(FindObjectsOfType<CampusTowelInteractable>().Count(t=>!t.IsWet)==4 && FindObjectsOfType<CampusFaucet>().Length==8,"Every floor starts with one dry held towel and two switchable faucets");
        Check(FindObjectsOfType<Transform>().Count(t=>t.name=="Corridor elevator bank")==4,"Four elevator banks remain accessible from the corridor");
        Check(FindObjectsOfType<Transform>().Count(t=>t.name=="Bookcase back" && t.position.y>11)>=10,"4F reading room retains ten bookcases");
        Check(FindObjectsOfType<Transform>().Count(t=>t.name=="4F reading room fire origin")==1 && !FindObjectsOfType<Transform>().Any(t=>t.name=="Contained fire cue (blocked stair)"),"Only the 4F reading room is the main fire source");
        Check(FindObjectsOfType<CampusSmokeVolume>(true).Length==63 && spread.Stages.Length==62,"Smoke retains 62 staged four-floor volumes plus the gated lesson doorway plume");
        Check(!FindObjectsOfType<TextMeshPro>(true).Any(t=>t.text.Contains("STAIR A / 楼梯") || t.text.Contains("STAIR B / 楼梯")),"All standalone stair floor plates are removed");
        Check(!FindObjectsOfType<TextMeshPro>(true).Any(t=>t.text=="封闭 / CLOSED") && FindObjectsOfType<CampusRouteCue>(true).Length==0,"Obsolete CLOSED sign and route-closed trigger are removed");
        Check(FindObjectsOfType<Transform>().Count(t=>t.name=="Cardboard archive carton")==6 && !FindObjectsOfType<Transform>().Any(t=>t.name=="Debris physical blockage"),"The storage pile remains without the artificial route blocker");
        Check(FindObjectsOfType<MeshFilter>().All(m=>m.sharedMesh!=null),"All visible mesh filters have geometry");
        Check(FindObjectsOfType<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null && m.shader!=null && m.shader.name!="Hidden/InternalErrorShader")),"All visible renderers have supported materials");
        health.ResetHealth(); yield return new WaitForSeconds(.2f); Check(health.HealthPercent>.999f,"Clear air causes no passive countdown damage");
        body.enabled=false; rig.SetPositionAndRotation(new Vector3(0,11.4f,0),Quaternion.identity); head.localPosition=new Vector3(0,1.65f,0); head.localRotation=Quaternion.identity; body.enabled=true; Physics.SyncTransforms();
        foreach(var input in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
        {
            movement.Step(Vector2.zero,0,.01f); Vector3 before=rig.position;
            for(int i=0;i<10;i++) movement.Step(input,0,.05f);
            Check(Vector3.Dot(rig.position-before,new Vector3(input.x,0,input.y))>.5f,"Continuous left-stick direction "+input);
        }
        Quaternion tracking=head.localRotation; Vector3 headBefore=head.position;
        foreach(float turn in new[]{1f,1f,0f,-1f}) for(int i=0;i<12;i++) movement.Step(Vector2.zero,turn,.05f);
        Check(Mathf.Abs(Mathf.DeltaAngle(0,rig.eulerAngles.y))<.01f && head.localRotation==tracking && Vector3.ProjectOnPlane(head.position-headBefore,Vector3.up).magnitude<.01f,"Right-stick hold, reverse and re-centre never turn the rig or tracked head");
        head.localRotation=Quaternion.Euler(0,90,0); movement.Step(Vector2.zero,0,.01f); Vector3 start=rig.position;
        for(int i=0;i<10;i++) movement.Step(Vector2.up,0,.1f);
        Check(Vector3.ProjectOnPlane(rig.position-start,Vector3.up).magnitude>1.5f,"Walking reaches 1.8 m/s");
        start=rig.position; head.localRotation=Quaternion.Euler(0,160,0);
        for(int i=0;i<4;i++) movement.Step(Vector2.up,0,.1f);
        Check(Mathf.Abs(rig.position.z-start.z)<.01f && rig.position.x-start.x>.6f,"Natural head turns do not steer an ongoing walk");
        movement.Step(Vector2.zero,0,.05f); start=rig.position; movement.Step(Vector2.zero,0,.1f);
        Check(Vector3.ProjectOnPlane(rig.position-start,Vector3.up).magnitude<.001f,"Stick release stops without coasting");
        head.localRotation=Quaternion.identity;
        var vignette=FindObjectOfType<CampusComfortVignette>(); float fov=Camera.main.fieldOfView;
        Check(vignette!=null && Field<CampusComfortVignette>(movement,"comfortVignette")==vignette,"Comfort vignette stays bound to the tracked camera");
        vignette.ReportMotion(1,false); vignette.Tick(.2f);
        Check(vignette.IsVisible && vignette.Strength>.5f && Camera.main.fieldOfView==fov,"Comfort shading activates without changing FOV");
        vignette.ResetMotion(); vignette.Tick(.3f); Check(!vignette.IsVisible,"Comfort shading fades when standing still");
        body.enabled=false; rig.SetPositionAndRotation(new Vector3(19.45f,11.4f,6),Quaternion.identity); body.enabled=true; Physics.SyncTransforms(); movement.Step(Vector2.zero,0,.01f);
        for(int i=0;i<24;i++) movement.Step(Vector2.right,0,.05f);
        Check(rig.position.x<19.85f,"The character cannot walk through the building wall");
        body.enabled=false; rig.SetPositionAndRotation(new Vector3(21.55f,0,.8f),Quaternion.identity); body.enabled=true; Physics.SyncTransforms(); movement.Step(Vector2.zero,0,.01f);
        for(int i=0;i<52;i++) movement.Step(Vector2.up,0,.05f);
        Check(rig.position.y>1.7f && rig.position.z>3.8f,"Walking ascends physical stair B");
        for(int i=0;i<52;i++) movement.Step(Vector2.down,0,.05f);
        Check(rig.position.y<.25f,"Walking descends to the landing");
        var mini=FindObjectOfType<CampusMinimap>(); body.enabled=false; rig.position=new Vector3(-10,11.4f,0); body.enabled=true; mini.Refresh(); Vector2 dot=Field<RectTransform>(mini,"playerDot").anchoredPosition;
        rig.position+=Vector3.right*5; mini.Refresh(); Check(mini.CurrentFloor==4 && Field<RectTransform>(mini,"playerDot").anchoredPosition.x>dot.x,"The minimap updates floor and player position");
        rig.position=new Vector3(0,0,-30); mini.Refresh(); Check(mini.CurrentFloor==1 && Field<GameObject>(mini,"outdoorArea").activeSelf,"Ground-floor map includes outdoor assembly");
        body.enabled=false; rig.position=new Vector3(12,11.4f,0); body.enabled=true; Physics.SyncTransforms();
        var hand=new GameObject("QA hand").transform;
        var door=FindObjectsOfType<CampusHingeInteractable>().First(h=>h.transform.parent!=null && h.transform.parent.name=="Interactive classroom door");
        door.SetAngle(0); var contact=door.GetComponentInChildren<BoxCollider>();
        foreach(var inputHand in hands) inputHand.transform.position=contact.bounds.center+door.transform.parent.forward*.12f;
        Physics.SyncTransforms(); foreach(var inputHand in hands) inputHand.RefreshContact();
        Check(hands.All(h=>Field<MonoBehaviour>(h,"candidate")==door),"Both hands detect a door surface without rays");
        hands[0].ProcessGrip(false,true,false); Check(door.IsHeldBy(hands[0].transform),"A held trigger can acquire a hinge upon contact"); hands[0].ProcessGrip(false,false,true);
        var renderer=door.GetComponentInChildren<MeshRenderer>(); var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); Check(block.GetColor("_BaseColor").r>.7f,"Contact gold-highlights only the target");
        hands[0].transform.position=Vector3.one*100; hands[0].RefreshContact(); renderer.GetPropertyBlock(block); Check(block.GetColor("_BaseColor").r>.7f,"Leaving one hand retains the other hand's highlight");
        hands[1].transform.position=Vector3.one*100; hands[1].RefreshContact(); renderer.GetPropertyBlock(block); Check(block.isEmpty,"Leaving both hands restores original materials");
        foreach(float radius in new[]{.25f,.65f,1.2f})
        {
            door.SetAngle(0); hand.position=door.transform.TransformPoint(new Vector3(radius,1.1f,-.02f)); door.OnInteractStart(hand);
            Check(door.Angle==0,"Press alone never automatically opens a door");
            hand.position+=door.transform.forward*.15f; door.OnInteract(hand);
            Check(Mathf.Abs(Mathf.Abs(door.Angle)-45)<.1f,"15 cm push produces 45 degrees independent of hinge radius "+radius);
            door.OnInteractEnd(hand); Check(Mathf.Abs(Mathf.Abs(door.Angle)-45)<.1f,"Released door retains its angle");
        }
        door.SetAngle(0); hand.position=door.transform.TransformPoint(new Vector3(.7f,1.1f,0)); door.OnInteractStart(hand);
        Vector3 local=rig.InverseTransformPoint(hand.position), rigPosition=rig.position; Quaternion rotation=rig.rotation;
        rig.position+=new Vector3(.5f,0,.3f); rig.RotateAround(head.position,Vector3.up,30); hand.position=rig.TransformPoint(local); door.OnInteract(hand);
        Check(Mathf.Abs(door.Angle)<.01f,"Body movement never creates fake physical push distance");
        rig.SetPositionAndRotation(rigPosition,rotation); hand.position=rig.TransformPoint(local); door.OnInteractEnd(hand);
        foreach(var inputHand in hands)
        {
            door.SetAngle(0); inputHand.transform.position=door.transform.TransformPoint(new Vector3(.7f,1.1f,0)); Check(inputHand.Begin(door),"Either hand can own the hinge");
            inputHand.transform.position+=door.transform.forward*.3f; door.OnInteract(inputHand.transform); Check(Mathf.Abs(Mathf.Abs(door.Angle)-90)<.1f,"Either hand reaches 90 degrees with 30 cm push"); inputHand.Release();
        }
        Check(hands[0].Begin(door) && !hands[1].Begin(door),"Two hands cannot simultaneously own the same hinge"); hands[0].Release();
        var window=FindObjectsOfType<RuntimeFireWindow>().First(w=>w.name=="Wall_3m_Window_H 4F North 2"); var pane=window.HighlightTarget; var hinge=pane.GetComponentInParent<CampusHingeInteractable>();
        Check(FindObjectsOfType<RuntimeFireWindow>().Length==78 && hinge!=null && window.UsesPhysicalHands,"All 78 original windows retain split glass and physical hinges");
        hinge.SetAngle(0); var vertices=pane.GetComponent<MeshFilter>().sharedMesh.vertices; int farthest=Enumerable.Range(0,vertices.Length).OrderByDescending(i=>(vertices[i]-vertices[0]).sqrMagnitude).First();
        float width=Vector3.Distance(pane.TransformPoint(vertices[0]),pane.TransformPoint(vertices[farthest]));
        hand.position=hinge.transform.TransformPoint(new Vector3(.7f,0,.1f)); hinge.OnInteractStart(hand); Check(hinge.Angle==0,"Window press alone does not open it");
        hand.position=hinge.transform.position+hinge.transform.rotation*new Vector3(.7f,0,-.7f); hinge.OnInteract(hand);
        Check(hinge.Angle>35 && hinge.Angle<=90,"Window outward push respects the angle limit");
        Check(width>.1f && Mathf.Abs(width-Vector3.Distance(pane.TransformPoint(vertices[0]),pane.TransformPoint(vertices[farthest])))<.001f,"Glass rotation is not sheared by non-uniform wall scale");
        hinge.OnInteractEnd(hand); hinge.OnInteractStart(hand); hinge.OnInteractEnd(hand);
        Check(CampusMessagePresenter.Instance.CurrentMessage.Contains("Do not attempt") && CampusMessagePresenter.Instance.CurrentMessage.Contains("发生火灾"),"Existing window warning stays bilingual");
        Check(Mathf.Approximately(Field<Transform>(CampusMessagePresenter.Instance,"panel").localPosition.z,1),"Warning remains world-space at one metre");
        yield return new WaitForSeconds(1.8f); Check(hinge.Angle>35,"Window stays open during its two-second warning"); yield return new WaitForSeconds(1f); Check(hinge.Angle==0,"Window closes after the warning");
        var button=FindObjectOfType<CampusElevatorButton>(); button.OnInteractStart(hand); button.OnInteractEnd(hand); Check(CampusMessagePresenter.Instance.CurrentMessage.Contains("elevator") && CampusMessagePresenter.Instance.CurrentMessage.Contains("电梯"),"Elevator contact retains the bilingual warning");
        spread.RoomDoor.SetAngle(0); spread.ResetSpread(); spread.Advance(7); Check(spread.CorridorAge==0 && spread.Stages.Where(s=>!s.roomClock).All(s=>!s.exposure.enabled),"Smoke initially accumulates only inside the reading room");
        spread.Advance(10); Check(spread.Stages[1].particles.emission.rateOverTime.constant>0 && !spread.Stages.Last().exposure.enabled,"Doorway leakage precedes distant smoke");
        spread.ResetSpread(); spread.Advance(20); float closedFlow=spread.CorridorAge; spread.RoomDoor.SetAngle(60); spread.ResetSpread(); spread.Advance(20);
        Check(spread.CorridorAge>closedFlow*2,"An open reading-room door accelerates smoke escape");
        Check(spread.Stages[2].exposure.enabled && !spread.Stages.Last().exposure.enabled,"Exposure follows the nearby visible smoke front");
        spread.Advance(50); Check(spread.Stages.Where(s=>s.floor==4).All(s=>s.exposure.enabled) && spread.Stages.Where(s=>s.floor==1).All(s=>!s.exposure.enabled),"Fourth-floor smoke forms before first-floor exposure");
        Check(spread.Stages.All(s=>s.particles.collision.enabled && s.particles.main.maxParticles<=100),"All smoke uses bounded pools and physical collision");
        yield return EvacuationChecks();
        var exposure=FindObjectOfType<CampusSmokeExposure>(); health.ResetHealth(); exposure.Tick(false,0); exposure.Tick(true,5.9f); Check(health.HealthPercent==1,"Brief smoke exposure retains its six-second grace period"); exposure.Tick(true,1.1f); Check(Mathf.Abs(health.HealthPercent-.98f)<.001f,"Prolonged exposure removes 2 HP per second");
        exposure.Tick(false,0); exposure.Tick(true,2); Check(Mathf.Abs(health.HealthPercent-.98f)<.001f,"Leaving smoke resets exposure without extra damage");
        yield return TowelWaterChecks(hands,head,health,exposure);
        var pause=FindObjectOfType<SystemUIPauseController>(); pause.ShowPause(); Vector3 paused=rig.position; movement.Step(Vector2.up,1,.1f); Check(Time.timeScale==0 && rig.position==paused,"Pause blocks walking");
        float hp=health.HealthPercent, age=spread.RoomAge; exposure.Tick(true,10); spread.Advance(10); Check(health.HealthPercent==hp && spread.RoomAge==age,"Pause blocks damage and smoke progression"); pause.ResumeGame();
        health.ResetHealth(); body.enabled=false; rig.SetPositionAndRotation(new Vector3(0,0,-30),Quaternion.identity); body.enabled=true; Physics.SyncTransforms(); yield return new WaitForSeconds(2.5f);
        var resolution=FindObjectOfType<ResolutionCoordinator>(); Check(resolution.CurrentOutcome==ResolutionCoordinator.Outcome.Victory,"Outdoor assembly completion remains integrated");
        resolution.ResetResolution(); health.ResetHealth(); health.TakeDamage(100); Check(resolution.CurrentOutcome==ResolutionCoordinator.Outcome.GameOver,"Health depletion preserves the failure outcome"); Check(!resolution.TryShowVictory(),"Arrival cannot overwrite failure");
        Destroy(hand.gameObject);
    }
}
#endif
