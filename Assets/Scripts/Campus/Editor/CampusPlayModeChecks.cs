using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Harbourview.Journey;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hardware-independent regression checks. Never changes the saved scene or SteamVR configuration.
[InitializeOnLoad]
public static class CampusPlayModeChecks
{
    private const string Flag="Campus.AutomatedChecks";
    static CampusPlayModeChecks() { EditorApplication.playModeStateChanged+=state=> {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag,false))
            new GameObject("Campus QA (temporary)").AddComponent<CampusCheckDriver>();
    }; }
    public static void Run()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        foreach(var script in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)) {
            if(script==null) continue;
            if((script.GetType().Namespace??"").StartsWith("Valve.VR") || script is DummyController || script is VRTeleporter ||
                script is VRFireSafetyInteraction || script is VRMenuRayPointer || script is VRControlTutorial || script is FireSafetyPrefabSupport)
                script.enabled=false;
        }
        SessionState.SetBool(Flag,true);
        EditorApplication.isPlaying=true;
    }
    internal static void Finish(bool success,string report)
    {
        SessionState.SetBool(Flag,false);
        Directory.CreateDirectory("Documentation"); File.WriteAllText("Documentation/PlayModeChecks.txt",report);
        Debug.Log((success?"CAMPUS_PLAY_CHECKS_PASS":"CAMPUS_PLAY_CHECKS_FAIL")+"\n"+report);
        EditorApplication.Exit(success?0:1);
    }
}

public sealed class CampusCheckDriver : MonoBehaviour
{
    private readonly List<string> checks=new List<string>();
    private float deadline;
    private void Start() { deadline=Time.realtimeSinceStartup+45; StartCoroutine(Guard(Checks())); }
    private void Update() { if(Time.realtimeSinceStartup>deadline) CampusPlayModeChecks.Finish(false,string.Join("\n",checks)+"\nTimed out"); }
    private IEnumerator Guard(IEnumerator routine)
    {
        while(true) {
            object yielded;
            try { if(!routine.MoveNext()) break; yielded=routine.Current; }
            catch(Exception e) { CampusPlayModeChecks.Finish(false,string.Join("\n",checks)+"\n"+e); yield break; }
            yield return yielded;
        }
        CampusPlayModeChecks.Finish(true,string.Join("\n",checks)+"\nVR hardware input / Quest GPU timing: not tested.");
    }
    private IEnumerator Checks()
    {
        yield return null; yield return null;
        var head=Camera.main.transform;
        var health=HealthTimeController.Instance;
        Check(health!=null && health.HealthPercent>.99f,"One active health system initialises correctly");
        var floorLabel=Get<TextMeshProUGUI>(FindObjectOfType<CampusExperience>(),"floorLabel");
        Check(floorLabel.gameObject.activeInHierarchy && floorLabel.text.StartsWith("4F") && Vector3.Distance(floorLabel.transform.position,head.position)<1.3f,
            "Gameplay HUD displays the initial fourth floor within view");
        health.Pause(true);
        var pause=FindObjectOfType<SystemUIPauseController>();
        pause.ShowPause(); Check(Time.timeScale==0,"Pause freezes simulation");
        yield return new WaitForSecondsRealtime(.1f); pause.ResumeGame(); Check(Time.timeScale==1,"Resume restores simulation");
        health.Pause(true);
        var teleporter=FindObjectsOfType<VRTeleporter>(true).First();
        var clearance=typeof(VRTeleporter).GetMethod("HasStandingClearance",BindingFlags.Instance|BindingFlags.NonPublic);
        int steps=0;
        foreach(var area in FindObjectsOfType<TeleportArea>()) {
            if(!area.name.StartsWith("Up flight") && !area.name.StartsWith("Return flight")) continue;
            if(area.transform.position.x<0) continue;
            var bounds=area.GetComponent<Collider>().bounds;
            Vector3 point=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
            if(!(bool)clearance.Invoke(teleporter,new object[]{point})) throw new InvalidOperationException("Blocked stair B tread: "+area.name);
            steps++;
        }
        Check(steps==60,"All 60 treads of continuous stair B have standing clearance");
        // Use a fixed classroom window, not one intersecting a room-divider edge.
        var window=FindObjectsOfType<RuntimeFireWindow>().OrderByDescending(w=>w.transform.position.y).First(w=>w.transform.position.z>0 && Mathf.Abs(w.transform.position.x+14)<.1f);
        Transform pane=window.HighlightTarget;
        Check(pane!=null,"Window wall and glass split successfully at runtime");
        Vector3 viewer=pane.position-window.transform.forward*.9f;
        head.position=viewer;
        Check(window.CanInteract(viewer) && !window.CanInteract(viewer+Vector3.right*10),"Window proximity gate accepts near / rejects far");
        var paneCollider=pane.GetComponent<Collider>();
        Check(RuntimeFireWindow.TryFind(paneCollider,out var found) && found==window,"Closed pane maps to its prefab interaction");
        window.Open(viewer); yield return new WaitForSeconds(.85f);
        Check(window.IsOpen && Mathf.Abs(Get<float>(window,"angle"))==90,"First action opens right-hand hinge by 90 degrees");
        var opening=Get<BoxCollider>(window,"openWindowTarget");
        Check(opening.enabled && RuntimeFireWindow.TryFind(opening,out found) && found==window,"Open window still targets original model opening");
        var ray=FindObjectsOfType<VRFireSafetyInteraction>(true).First();
        var origin=new GameObject("QA ray origin").transform; origin.position=viewer; origin.LookAt(opening.bounds.center);
        Put(ray,"rayOrigin",origin); Put(ray,"vrCamera",head.GetComponent<Camera>());
        Physics.SyncTransforms(); Invoke(ray,"UpdateInteractionRay"); Invoke(ray,"EndInteraction");
        var warning=Get<TMP_Text>(ray,"messageText");
        if(warning==null) {
            object[] hitArgs={origin.position,origin.forward,default(RaycastHit)};
            bool hit=(bool)typeof(VRFireSafetyInteraction).GetMethod("TryGetFirstBlockingHit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ray,hitArgs);
            var info=(RaycastHit)hitArgs[2];
            throw new InvalidOperationException("Window ray failed: hit="+hit+" collider="+(info.collider!=null?info.collider.name:"none")+" viewer="+head.position+" expected="+viewer+" window="+window.name+" reach="+window.CanInteract(head.position));
        }
        Check(warning!=null && warning.text.Contains("Do not attempt to jump") && warning.text.Contains("发生火灾"),"Second trigger release produces bilingual window warning");
        Check(Mathf.Approximately(Get<GameObject>(ray,"messageRoot").transform.localPosition.z,1),"Warning remains world-space at 1 metre");
        yield return new WaitForSeconds(1.7f); Check(window.IsOpen,"Glass stays open during 2-second warning interval");
        yield return new WaitForSeconds(1.2f); Check(!window.IsOpen,"Glass automatically closes after warning interval and animation");
        var lift=FindObjectsOfType<FireSafetyElevator>().First();
        var up=Get<Collider>(lift,"upButton"); var down=Get<Collider>(lift,"downButton");
        Check(up!=null && down!=null && lift.TryGetButton(up,up.bounds.center,out _) && lift.TryGetButton(down,down.bounds.center,out _) && !lift.TryGetButton(up,up.bounds.center+Vector3.right*4,out _),"Both elevator buttons enforce 1.5m proximity");
        var towel=FindObjectsOfType<TowelPickup>().First(); towel.OnInteractStart(head);
        Check(Mathf.Approximately((float)Invoke(health,"GetTotalMultiplier"),.5f),"Protective towel applies timed health-drain reduction");
        towel.OnInteractStart(head); Check(Mathf.Approximately((float)Invoke(health,"GetTotalMultiplier"),.5f),"Consumed towel cannot stack its effect again");
        health.ResetHealth(); health.Pause(true);
        var smoke=FindObjectsOfType<SmokeZone>().First(); var body=FindObjectOfType<CampusBodyTracker>().GetComponent<Collider>();
        smoke.SendMessage("OnTriggerEnter",body); Check(Mathf.Approximately((float)Invoke(health,"GetTotalMultiplier"),3),"Smoke applies three-times drain");
        smoke.SendMessage("OnTriggerExit",body); Check(Mathf.Approximately((float)Invoke(health,"GetTotalMultiplier"),1),"Leaving smoke clears multiplier");
        var door=FindObjectsOfType<DoorInteraction>().First(d=>Get<int>(d,"doorType")==0);
        door.OnInteractStart(head); yield return new WaitForSeconds(.85f);
        Check(Get<bool>(door,"isOpen"),"Single classroom door opens");
        door.CloseDoor(); yield return new WaitForSeconds(.85f); Check(!Get<bool>(door,"isOpen"),"Classroom door closes");
        var doubleDoor=FindObjectsOfType<DoorInteraction>().First(d=>Get<int>(d,"doorType")==1);
        doubleDoor.OnInteractStart(head); yield return new WaitForSeconds(.85f); Check(Get<bool>(doubleDoor,"isOpen"),"Both leaves of the ground exit open");
        var res=FindObjectOfType<ResolutionCoordinator>();
        var assembly=FindObjectOfType<AssemblyOutcomeController>();
        var assemblyZone=Get<GeoFence>(assembly,"completionZone");
        head.position=assemblyZone.transform.position+Vector3.up*.2f;
        Physics.SyncTransforms(); yield return new WaitForSeconds(.25f);
        Check(assemblyZone.IsInside,"Tracked body enters outdoor endpoint through actual physics trigger");
        yield return new WaitForSeconds(2f);
        Check(res.CurrentOutcome==ResolutionCoordinator.Outcome.Victory && health.IsPaused,"Outdoor assembly completes in the same scene");
        var ui=FindObjectOfType<SystemUIView>();
        Check(Get<GameObject>(ui,"victoryPanel").activeInHierarchy,"Complete victory panel is actually visible");
        Check(Get<TextMeshProUGUI>(FindObjectOfType<CampusExperience>(),"debriefLabel").text.Contains("Practice time"),"Learning review is populated");
        Check(SceneManager.GetActiveScene().name=="CampusTraining","No stair or endpoint scene transition");
        res.ResetResolution(); health.ResetHealth();
        var call=FindObjectOfType<CampusRefugeCall>(); head.position=Get<Collider>(call,"refugeVolume").bounds.center; call.OnInteractStart(head);
        Check(res.CurrentOutcome==ResolutionCoordinator.Outcome.Victory && health.IsPaused,"2F refuge intercom confirms safe endpoint");
        res.ResetResolution(); health.ResetHealth(); Put(health,"currentHealth",.00001f);
        yield return null; yield return null;
        Check(res.CurrentOutcome==ResolutionCoordinator.Outcome.GameOver,"Health depletion preserves failure outcome");
        Check(Get<GameObject>(ui,"gameOverPanel").activeInHierarchy,"Failure panel is actually visible");
        Get<GeoFence>(assembly,"completionZone").onDwellElapsed.Invoke();
        Check(res.CurrentOutcome==ResolutionCoordinator.Outcome.GameOver,"Arrival cannot overwrite an existing failure");
        Check(FindObjectOfType<CampusExperience>()!=null && FindObjectOfType<SessionLogger>()!=null,"Unified experience and session logging remain active");
    }
    private void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException("FAIL: "+label); checks.Add("PASS: "+label); }
    private static T Get<T>(object obj,string field) {
        object value=obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        if(typeof(T)==typeof(int) && value.GetType().IsEnum) return (T)(object)Convert.ToInt32(value);
        return (T)value;
    }
    private static void Put(object obj,string field,object value) { obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value); }
    private static object Invoke(object obj,string method) { return obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null); }
}
