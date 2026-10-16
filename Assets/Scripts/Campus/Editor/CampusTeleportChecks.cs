using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiao.HealthTime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

public static class CampusTeleportChecks
{
    private static readonly List<string> results=new List<string>();
    private static void Check(bool value,string description)
    { if(!value) throw new InvalidOperationException(description); results.Add("PASS: "+description); }
    private static void Set(object target,string name,object value)
    { target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value); }
    private static void Call(object target,string method)
    { target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null); }
    private static void ResetOwner()
    { typeof(VRTeleporter).GetMethod("ResetAimOwner",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null); }

    // Batch entry point: upgrade and persist only the campus scene, then run isolated physics checks.
    public static void ApplyAndRun()
    { RunChecks(true); }

    public static void Run()
    { RunChecks(false); }

    private static void RunChecks(bool apply)
    {
        results.Clear();
        try
        {
            if(apply) CampusTeleportSetup.Apply();
            EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
            var configured=UnityEngine.Object.FindObjectsOfType<VRTeleporter>(true);
            Check(configured.Length==2 && configured.All(t=>t.MaxTeleportDistance==3),"Saved scene has two 3m teleporters");
            Check(configured.Select(t=>t.InputSource).Distinct().Count()==2,"Left/right hands have distinct input sources");
            var menuRays=UnityEngine.Object.FindObjectsOfType<VRMenuRayPointer>(true);
            Check(menuRays.Length==2 && menuRays.All(ray=>new SerializedObject(ray).FindProperty("menuButtons").arraySize==7),
                "Both hands have dedicated rays for all seven pause/outcome buttons");
            var configuredMovement=new SerializedObject(UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true));
            Check(((CampusLocomotion)configuredMovement.targetObject).gameObject.activeInHierarchy,"Saved Player and shared systems are enabled for direct testing");
            Check(configuredMovement.FindProperty("teleportOnly").boolValue && configuredMovement.FindProperty("footsteps").objectReferenceValue==null && configuredMovement.FindProperty("footstepClips").arraySize==0,"Saved scene uses teleport with no footsteps");
            Check(UnityEngine.Object.FindObjectsOfType<TeleportArea>(true).Length>=140,"Floors, stairs and outdoor evacuation paths are marked");
            var pause=new SerializedObject(UnityEngine.Object.FindObjectOfType<SystemUIPauseController>(true));
            Check(pause.FindProperty("teleport").objectReferenceValue!=null && pause.FindProperty("additionalTeleports").arraySize==1,"Pause controls both teleporters");
            CheckStairRoute(configured[0]);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RunIsolated();
            File.WriteAllLines("Documentation/TeleportChecks.txt",results.Concat(new[]{"Headset input, visual comfort and audio: require hardware verification."}));
            Debug.Log("CAMPUS_TELEPORT_CHECKS_PASS\n"+string.Join("\n",results));
        }
        catch(Exception exception)
        {
            File.WriteAllLines("Documentation/TeleportChecks.txt",results.Concat(new[]{"FAIL: "+exception}));
            Debug.LogException(exception); throw;
        }
    }

    private static void CheckStairRoute(VRTeleporter teleport)
    {
        var movement=UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true);
        // Activate only the unsaved test copy; the user's saved activation state is preserved.
        for(Transform parent=movement.transform;parent!=null;parent=parent.parent) parent.gameObject.SetActive(true);
        var rig=movement.transform; var body=movement.GetComponent<CharacterController>(); body.enabled=false;
        var head=(Transform)new SerializedObject(movement).FindProperty("trackedHead").objectReferenceValue;
        Vector3 offset=head.position-rig.position; offset.y=0;
        var standing=typeof(VRTeleporter).GetMethod("HasStandingClearance",BindingFlags.Instance|BindingFlags.NonPublic);
        var path=typeof(VRTeleporter).GetMethod("HasClearTravelPath",BindingFlags.Instance|BindingFlags.NonPublic);
        Vector3 previous=new Vector3(24.45f,11.4f,.7f);
        var targets=new List<Vector3>();
        for(int floor=3;floor>=1;floor--)
        {
            float y=(floor-1)*3.8f;
            for(int step=9;step>=0;step--) targets.Add(new Vector3(24.45f,y+1.9f+(step+1)*.19f,3.76f-step*.28f));
            targets.Add(new Vector3(24.45f,y+1.9f,4.65f));
            targets.Add(new Vector3(23,y+1.9f,4.65f)); targets.Add(new Vector3(21.55f,y+1.9f,4.65f));
            for(int step=9;step>=0;step--) targets.Add(new Vector3(21.55f,y+(step+1)*.19f,1.24f+step*.28f));
            targets.Add(new Vector3(21.55f,y,.7f));
            if(floor>1) { targets.Add(new Vector3(23,y,.7f)); targets.Add(new Vector3(24.45f,y,.7f)); }
        }
        foreach(var target in targets)
        {
            rig.position=previous-offset; Physics.SyncTransforms();
            bool marked=Physics.Raycast(target+Vector3.up*.06f,Vector3.down,out var hit,.12f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider.GetComponent<TeleportArea>()!=null;
            Check(marked && teleport.IsWithinRange(target) && (bool)standing.Invoke(teleport,new object[]{target}) && (bool)path.Invoke(teleport,new object[]{target}),"Stair B teleport route "+previous+" -> "+target);
            previous=target;
        }
    }

    private static VRTeleporter MakeTeleporter(Transform rig,Transform head,SteamVR_Input_Sources source)
    {
        var go=new GameObject(source.ToString());
        go.transform.position=new Vector3(1.5f,1,0); go.transform.rotation=Quaternion.Euler(90,0,0);
        var teleport=go.AddComponent<VRTeleporter>();
        Set(teleport,"playerRoot",rig); Set(teleport,"vrCamera",head); Set(teleport,"inputSource",source);
        Set(teleport,"useCampusInput",true); Set(teleport,"maxTeleportDistance",2f); Set(teleport,"maxHeightChange",2f);
        Set(teleport,"teleportLayer",(LayerMask)~0); Call(teleport,"Awake"); return teleport;
    }

    private static void RunIsolated()
    {
        Time.timeScale=1; ResetOwner(); CampusSmokeVolume.Active.Clear();
        var rig=new GameObject("QA rig"); var body=rig.AddComponent<CharacterController>();
        body.height=1.65f; body.radius=.23f; body.center=Vector3.up*.825f;
        var head=new GameObject("QA head"); head.tag="MainCamera"; head.AddComponent<Camera>();
        head.transform.SetParent(rig.transform,false); head.transform.localPosition=Vector3.up*1.65f;
        var movement=rig.AddComponent<CampusLocomotion>(); Set(movement,"trackedHead",head.transform); Call(movement,"Awake");
        var health=new GameObject("QA health").AddComponent<HealthTimeController>();
        Set(health,"logEvents",false); Call(health,"Awake"); health.ResetHealth();
        var messages=new GameObject("QA warning").AddComponent<CampusMessagePresenter>(); Call(messages,"Awake");
        var exposure=new GameObject("QA exposure").AddComponent<CampusSmokeExposure>(); Set(exposure,"trackedHead",head.transform);
        var resolution=new GameObject("QA outcome").AddComponent<ResolutionCoordinator>();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="QA floor";
        floor.transform.position=Vector3.down*.05f; floor.transform.localScale=new Vector3(20,.1f,20); floor.AddComponent<TeleportArea>();
        var left=MakeTeleporter(rig.transform,head.transform,SteamVR_Input_Sources.LeftHand);
        var right=MakeTeleporter(rig.transform,head.transform,SteamVR_Input_Sources.RightHand);
        Check(left.IsWithinRange(Vector3.right*2) && !left.IsWithinRange(Vector3.right*2.001f),"2m accepted; 2.001m rejected");
        Check(!left.IsWithinRange(new Vector3(1.8f,1,0)),"Range includes vertical displacement on stairs");
        head.transform.localPosition+=Vector3.right*.35f;
        Check(left.IsWithinRange(Vector3.right*2.35f) && !left.IsWithinRange(Vector3.left*1.66f),"Distance is measured below the tracked head, including room-scale offset");
        head.transform.localPosition=Vector3.up*1.65f;
        Set(left,"maxTeleportDistance",3f);
        Check(left.IsWithinRange(Vector3.forward*3) && !left.IsWithinRange(Vector3.forward*3.001f),"Campus 3m boundary is enforced");
        Check(!left.IsWithinRange(new Vector3(0,2.01f,1)) && left.IsWithinRange(new Vector3(0,1.9f,2f)),"3m range retains the 2m height cap and permits shorter stair hops");
        foreach(float height in new[]{.9f,1.2f,1.5f})
            foreach(float pitch in new[]{0f,15f,30f,60f})
        {
            left.transform.position=new Vector3(.22f,height,.4f);
            left.transform.rotation=Quaternion.Euler(pitch,0,0);
            ResetOwner(); left.ProcessInput(true);
            var arc=left.GetComponent<LineRenderer>();
            float landingDistance=Vector3.Distance(rig.transform.position,arc.GetPosition(arc.positionCount-1));
            Check(left.HasValidTarget && (pitch<=30?landingDistance>=2.2f && landingDistance<3:landingDistance>.9f && landingDistance<1.5f),
                "Aim reaches useful forward/near floor range at hand height "+height+" / downward pitch "+pitch);
            // Cancel without moving so all height/angle combinations share the same floor origin.
            Call(left,"OnDisable"); left.ProcessInput(false);
        }
        Set(left,"maxTeleportDistance",2f);
        left.transform.position=new Vector3(1.5f,1,0); left.transform.rotation=Quaternion.Euler(90,0,0);
        Vector3 before=rig.transform.position;
        for(int i=0;i<10;i++) movement.Step(Vector2.up,1,.05f);
        Check(Vector3.ProjectOnPlane(rig.transform.position-before,Vector3.up).magnitude<.001f && rig.transform.rotation==Quaternion.identity,"Joystick input cannot slide or turn the rig");
        body.enabled=false; rig.transform.position=Vector3.zero; body.enabled=true; Physics.SyncTransforms();
        left.ProcessInput(true); Check(left.IsAiming && left.HasValidTarget,"Forward input acquires a valid marked floor");
        right.ProcessInput(true); Check(!right.IsAiming,"Second hand cannot take over an active aim");
        left.ProcessInput(false); Check(Mathf.Abs(rig.transform.position.x-1.5f)<.001f && body.enabled,"Release teleports and restores the CharacterController");
        right.ProcessInput(false); Check(Mathf.Abs(rig.transform.position.x-1.5f)<.001f,"Second-hand release cannot cause another teleport");
        ResetOwner(); right.ProcessInput(true); Check(right.IsAiming,"Right hand can independently aim"); right.enabled=false; Call(right,"OnDisable");
        Check(!right.IsAiming && !right.GetComponent<LineRenderer>().enabled,"Disabling a teleporter cancels its arc"); right.enabled=true;
        right.ProcessInput(true); Check(!right.IsAiming,"Re-enabling while the stick is held requires release before another aim"); right.ProcessInput(false);
        ResetOwner(); left.ProcessInput(true); Time.timeScale=0; left.ProcessInput(false);
        Check(!left.IsAiming && !left.GetComponent<LineRenderer>().enabled,"Pause cancels an aim without teleporting"); Time.timeScale=1;
        movement.SetScriptedLock(true); left.ProcessInput(true); Check(!left.IsAiming,"Scripted lesson lock blocks teleport"); movement.SetScriptedLock(false); left.ProcessInput(false);
        resolution.TryClaimGameOver(); right.ProcessInput(true); Check(!right.IsAiming,"Outcome blocks teleport"); resolution.ResetResolution(); right.ProcessInput(false);
        body.enabled=false; rig.transform.position=Vector3.zero; body.enabled=true;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(.75f,.85f,0); wall.transform.localScale=new Vector3(.1f,1.7f,2);
        ResetOwner(); left.ProcessInput(true); Check(!left.HasValidTarget,"A wall between the player and destination blocks teleport even when the arc starts beyond it"); left.ProcessInput(false); UnityEngine.Object.DestroyImmediate(wall);
        floor.GetComponent<TeleportArea>().enabled=false;
        // The marker must be present and enabled to authorize a target.
        ResetOwner(); left.ProcessInput(true); Check(!left.HasValidTarget,"Disabled teleport areas reject landing"); left.ProcessInput(false); floor.GetComponent<TeleportArea>().enabled=true;
        var cloud=new GameObject("QA smoke"); var volume=cloud.AddComponent<BoxCollider>(); volume.isTrigger=true;
        volume.size=new Vector3(20,4,4); cloud.transform.position=new Vector3(0,1.65f,0);
        var smoke=cloud.AddComponent<CampusSmokeVolume>(); Call(smoke,"OnEnable");
        exposure.Tick(false,0); exposure.Tick(true,0);
        Check(messages.IsVisible && messages.CurrentMessage==CampusMessagePresenter.SmokeWarning && health.HealthPercent==1,"Smoke entry warns immediately before the grace period expires");
        messages.Hide(); exposure.Tick(true,0); Check(!messages.IsVisible,"Remaining in smoke does not repeat the warning");
        exposure.Tick(false,0); exposure.Tick(true,0); Check(messages.IsVisible,"Leaving and re-entering smoke warns again");
        exposure.Tick(false,0); health.ResetHealth();
        for(int i=0;i<2;i++) exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-4/1.8f)<.001f && health.HealthPercent==1,"Two 2m teleports count 2.22s of smoke without premature damage");
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-6/1.8f)<.001f && Mathf.Abs(health.HealthPercent-(1-(6/1.8f-3)*2/100))<.0001f,"Third teleport crosses the 3s threshold and applies proportional damage");
        var grip=new GameObject("QA towel hand").transform; grip.SetParent(rig.transform,false);
        grip.position=head.transform.TransformPoint(new Vector3(0,-.1f,.065f));
        var cloth=new GameObject("QA towel"); cloth.AddComponent<Rigidbody>(); cloth.AddComponent<BoxCollider>().size=new Vector3(.2f,.15f,.02f);
        var towel=cloth.AddComponent<CampusTowelInteractable>(); Call(towel,"Awake"); Call(towel,"OnEnable");
        Set(towel,"<IsWet>k__BackingField",true); towel.OnInteractStart(grip); Physics.SyncTransforms();
        exposure.Tick(false,0); health.ResetHealth(); exposure.Tick(true,3);
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(exposure.IsProtected && Mathf.Abs(health.HealthPercent-(1-2/1.8f*2*.4f/100))<.0001f,"A wet towel covering the face still reduces equivalent teleport damage");
        towel.OnInteractEnd(grip); CampusTowelInteractable.Active.Remove(towel); UnityEngine.Object.DestroyImmediate(cloth);
        exposure.Tick(false,0); health.ResetHealth();
        var overlapping=UnityEngine.Object.Instantiate(cloud); var overlappingSmoke=overlapping.GetComponent<CampusSmokeVolume>(); Call(overlappingSmoke,"OnEnable");
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-2/1.8f)<.001f,"Overlapping smoke volumes count once");
        UnityEngine.Object.DestroyImmediate(overlapping); CampusSmokeVolume.Active.Remove(overlappingSmoke);
        exposure.Tick(false,0); volume.size=new Vector3(.02f,4,4); cloud.transform.position=new Vector3(1,1.65f,0);
        float hp=health.HealthPercent; exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(exposure.Exposure==0 && health.HealthPercent==hp && messages.IsVisible,"Crossing a thin cloud warns and clean-air exit resets exposure");
        Check(smoke.SegmentInterval(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0),out float a,out float b) && Mathf.Abs(b-a-.01f)<.0001f,"Thin smoke intersections are calculated exactly");
        exposure.Tick(false,0); messages.Hide(); Time.timeScale=0; exposure.Tick(true,10);
        Check(!messages.IsVisible && health.HealthPercent==hp,"Paused smoke processing causes no warning or damage"); Time.timeScale=1;
        results.Add("PASS: smoke warning uses existing five-second safety-message presentation");
        CheckMenuRays();
    }

    private static void CheckMenuRays()
    {
        var rig=new GameObject("QA menu rig"); rig.transform.position=Vector3.up*100;
        var buttons=new List<VRMenuButton>();
        int clicks=0;
        foreach(string name in new[]{"Pause","Failure","Victory"})
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name="QA "+name;
            go.transform.SetParent(rig.transform,false); go.transform.localPosition=new Vector3(0,0,1);
            go.transform.localScale=new Vector3(.3f,.2f,.03f);
            var button=go.AddComponent<VRMenuButton>();
            var click=new UnityEngine.Events.UnityEvent(); click.AddListener(()=>clicks++); Set(button,"onClick",click);
            buttons.Add(button); go.SetActive(false);
        }
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=rig.transform.position+Vector3.forward*.5f;
        wall.transform.localScale=new Vector3(2,2,.1f);
        var resetClick=typeof(VRMenuButton).GetField("lastActivationFrame",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(var source in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
        {
            var hand=new GameObject("QA menu "+source); hand.transform.SetParent(rig.transform,false);
            var ray=hand.AddComponent<VRMenuRayPointer>();
            Set(ray,"trackedController",hand.transform); Set(ray,"playerRoot",rig.transform);
            Set(ray,"inputSource",source); Set(ray,"useCampusInput",true); Set(ray,"downwardPitchDegrees",0f);
            Set(ray,"menuButtons",buttons.ToArray());
            Set(ray,"laserShader",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/SystemUI/VRMenuLaser.shader")); Call(ray,"Awake");
            foreach(var button in buttons)
            {
                ray.ProcessInput(false); Check(!ray.IsVisible && ray.CurrentTarget==null,"Menu ray hidden during gameplay: "+source);
                Time.timeScale=button==buttons[0]?0:1;
                int before=clicks; button.gameObject.SetActive(true); resetClick.SetValue(null,-1);
                ray.ProcessInput(true);
                Check(ray.IsVisible && ray.CurrentTarget==button && clicks==before,"Menu opens without clicking an already held trigger: "+button.name+" / "+source);
                ray.ProcessInput(false); ray.ProcessInput(true);
                Check(clicks==before+1,"Fresh trigger clicks foreground menu beyond a wall while retaining rig-child buttons: "+button.name+" / "+source);
                resetClick.SetValue(null,-1); ray.ProcessInput(true);
                Check(clicks==before+1,"Held trigger cannot repeat menu activation");
                ray.enabled=false; Call(ray,"OnDisable"); ray.enabled=true; ray.ProcessInput(true);
                Check(clicks==before+1,"Re-enabled menu ray requires release before clicking");
                button.gameObject.SetActive(false); ray.ProcessInput(false);
                Check(!ray.IsVisible && ray.CurrentTarget==null,"Closing the menu hides its ray");
            }
            UnityEngine.Object.DestroyImmediate(hand);
        }
        Time.timeScale=1; UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(wall);
    }
}
