using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static partial class CampusPreview
{
    public static void Render()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        CampusSceneBuilder.Validate();
        FireSafetyPrefabSetup.Validate();
        var systems=GameObject.Find("05 Player & Shared Systems"); systems.SetActive(false);
        var cameraObject=new GameObject("Preview Camera"); var camera=cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>(); camera.nearClipPlane=.05f; camera.farClipPlane=180;
        cameraObject.tag="MainCamera";
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.74f,.82f,.85f); camera.fieldOfView=65;
        Directory.CreateDirectory("Documentation/Previews");
        foreach(var particle in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) particle.Simulate(4.5f,true,false,true);
        Shot(camera,"Exterior",new Vector3(49,26,-52),new Vector3(0,7,-1));
        Shot(camera,"Corridor_4F",new Vector3(16,13.05f,-.5f),new Vector3(-8,13,0));
        Shot(camera,"Classroom_4F",new Vector3(-10.8f,13,8.8f),new Vector3(-16,12.8f,3));
        Shot(camera,"Stair_B",new Vector3(23,9.2f,-.8f),new Vector3(22.2f,9.4f,3.8f));
        Shot(camera,"Refuge_2F",new Vector3(-1.4f,5.5f,-3),new Vector3(-5.5f,5.1f,-7));
        Shot(camera,"Elevator_4F",new Vector3(7.4f,13,.2f),new Vector3(5,12.8f,-2.5f));
        Shot(camera,"Restroom_4F",new Vector3(-11.8f,13,-3.1f),new Vector3(-16.5f,12.7f,-7.9f));
        Shot(camera,"RestroomFixtures_4F",new Vector3(-13,13,-5),new Vector3(-18,12.7f,-2.55f));
        Shot(camera,"ReadingRoomFire_4F",new Vector3(-5,13,-3.05f),new Vector3(-7,12.8f,-7.7f));
        Shot(camera,"BlockedStair_3F",new Vector3(-23,9.25f,-.25f),new Vector3(-21.5f,8.55f,1.9f));
        Shot(camera,"Assembly",new Vector3(-14,3.5f,-37),new Vector3(0,2,-30));
        RenderSmokeProgression(camera);
        RenderEvacuationDetails(camera,systems);
        RenderUI(camera,systems);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        RenderMainMenuForeground();
        Debug.Log("CAMPUS_PREVIEWS_COMPLETE");
    }
    private static void RenderSmokeProgression(Camera camera)
    {
        var spread=UnityEngine.Object.FindObjectOfType<CampusSmokeSpread>();
        if(spread==null) return;
        // Initialise the hinge as Play mode does; preview changes are never saved to the scene.
        typeof(CampusHingeInteractable).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(spread.RoomDoor,null);
        spread.RoomDoor.SetAngle(60); spread.ResetSpread();
        // ResetSpread starts systems in Play mode. In an editor-only render, start them explicitly.
        foreach(var stage in spread.Stages) stage.particles.Play();
        var particles=UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
        float elapsed=0;
        foreach(int target in new[]{5,20,45,120,170,220,280})
        {
            while(elapsed<target)
            {
                spread.Advance(.25f);
                foreach(var particle in particles) particle.Simulate(.25f,true,false,false);
                elapsed+=.25f;
            }
            Shot(camera,"SmokeSpread_"+target+"s",new Vector3(-2.2f,13,1.1f),new Vector3(-3,13,-4));
            Debug.Log("SMOKE_PREVIEW t="+target+" room="+spread.RoomAge+" corridor="+spread.CorridorAge+" nearParticles="+spread.Stages[2].particles.particleCount);
            if(target>=20 && spread.Stages[2].particles.particleCount==0) throw new System.InvalidOperationException("Progressive smoke preview did not emit particles.");
            if(target==20 || target==45)
            {
                Shot(camera,"SmokeEastLab_"+target+"s",new Vector3(0,13.05f,0),new Vector3(16,13,0));
                Shot(camera,"SmokeWestRestroom_"+target+"s",new Vector3(0,13.05f,0),new Vector3(-16,13,0));
            }
            if(target==120 || target==170 || target==220)
            {
                int floor=target==120?3:target==170?2:1;
                Shot(camera,"SmokeLowerFloor_"+floor+"F_"+target+"s",new Vector3(18,(floor-1)*3.8f+1.65f,0),new Vector3(-2,(floor-1)*3.8f+1.65f,0));
                var stage=System.Linq.Enumerable.First(spread.Stages,s=>s.floor==floor && s.particles.name.Contains("corridor") && s.particles.transform.position.x==18);
                if(stage.particles.particleCount==0) throw new System.InvalidOperationException("Lower-floor smoke never rendered on "+floor+"F");
                Debug.Log("LOWER_FLOOR_SMOKE_RENDER_PASS floor="+floor+" particles="+stage.particles.particleCount);
            }
        }
    }
    private static void RenderEvacuationDetails(Camera camera,GameObject systems)
    {
        var spread=UnityEngine.Object.FindObjectOfType<CampusSmokeSpread>(); spread.ResetSpread();
        foreach(var particle in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        systems.SetActive(true);
        foreach(var c in systems.GetComponentsInChildren<Camera>(true)) c.enabled=false;
        var models=systems.GetComponentsInChildren<CampusHandVisual>(true);
        for(int i=0;i<models.Length;i++)
        {
            models[i].transform.SetPositionAndRotation(new Vector3(i==0?-.12f:.12f,12.88f,.45f),Quaternion.identity);
            models[i].SetGesture(false,false,false); models[i].Tick(1);
        }
        camera.fieldOfView=60;
        RenderHandPose(camera,models,"DetailedHands_Relaxed");
        foreach(var model in models) { model.SetGesture(true,false,false); model.Tick(1); }
        RenderHandPose(camera,models,"DetailedHands_Trigger");
        if(System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes("Documentation/Previews/DetailedHands_Relaxed.png"),File.ReadAllBytes("Documentation/Previews/DetailedHands_Trigger.png")))
            throw new System.InvalidOperationException("Hand gesture render did not change.");
        Debug.Log("HAND_GESTURE_RENDER_PASS (relaxed and curled geometry differ)");
        systems.SetActive(false);
        var stair=System.Linq.Enumerable.First(UnityEngine.Object.FindObjectsOfType<CampusStairWindow>(),w=>w.transform.position.x<0 && w.transform.position.y>11);
        typeof(CampusHingeInteractable).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(stair.Hinge,null);
        stair.Hinge.SetAngle(90);
        Shot(camera,"StairWindow_Open90",new Vector3(-23,13.05f,.8f),new Vector3(-23,13,-1.5f));
        stair.Hinge.SetAngle(0);
        Shot(camera,"Stair_A_OriginalDebris",new Vector3(-23,9.25f,-.25f),new Vector3(-21.5f,8.55f,1.9f));
    }
    // Editor-only Camera.Render can reuse stale GPU skinning within the same frame.
    // Bake current bone transforms for each still image; never save these temporary meshes.
    private static void RenderHandPose(Camera camera,CampusHandVisual[] models,string name)
    {
        var renderers=new System.Collections.Generic.List<SkinnedMeshRenderer>();
        var temporary=new System.Collections.Generic.List<GameObject>();
        var meshes=new System.Collections.Generic.List<Mesh>();
        foreach(var model in models)
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=new Mesh(); renderer.BakeMesh(mesh); meshes.Add(mesh);
            var go=new GameObject("Temporary baked preview hand"); go.transform.SetParent(renderer.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            renderer.enabled=false; renderers.Add(renderer); temporary.Add(go);
        }
        Shot(camera,name,new Vector3(0,13.05f,0),new Vector3(0,12.87f,.55f));
        foreach(var go in temporary) UnityEngine.Object.DestroyImmediate(go);
        foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
        foreach(var renderer in renderers) renderer.enabled=true;
    }
    public static void RenderHandPreview()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        var go=new GameObject("Preview camera"); var camera=go.AddComponent<Camera>(); go.AddComponent<UniversalAdditionalCameraData>();
        camera.nearClipPlane=.05f; camera.farClipPlane=130; camera.fieldOfView=60;
        RenderEvacuationDetails(camera,GameObject.Find("05 Player & Shared Systems"));
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("HAND_PREVIEW_COMPLETE");
    }
    private static void RenderUI(Camera camera,GameObject systems)
    {
        systems.SetActive(true);
        var head=systems.GetComponentInChildren<Camera>(true).transform;
        foreach(var c in systems.GetComponentsInChildren<Camera>(true)) c.enabled=false;
        camera.fieldOfView=80;
        var ui=systems.GetComponentInChildren<SystemUIView>(true);
        var experience=systems.GetComponentInChildren<CampusExperience>(true);
        // Editor screenshots do not run Start/Update; initialise the same HUD values explicitly.
        var floor=(TMPro.TextMeshProUGUI)typeof(CampusExperience).GetField("floorLabel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(experience);
        floor.text="4F   HP 100 / 血量";
        var bar=systems.GetComponentInChildren<Jiao.HealthTime.HealthTimeBar>(true);
        typeof(Jiao.HealthTime.HealthTimeBar).GetMethod("OnHealthChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(bar,new object[]{1f});
        var flash=(UnityEngine.UI.Image)typeof(Jiao.HealthTime.HealthTimeBar).GetField("warningImage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bar);
        if(flash!=null) flash.enabled=false;
        ui.ShowGameplay(); PlaceUI(systems);
        Shot(camera,"GameplayHUD",head.position,head.position+head.forward*2);
        var vignette=systems.GetComponentInChildren<CampusComfortVignette>(true);
        typeof(CampusComfortVignette).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(vignette,null);
        vignette.ReportMotion(1,false); vignette.Tick(.2f);
        Shot(camera,"GameplayHUD_ComfortWalk",head.position,head.position+head.forward*2);
        ValidateComfortImage();
        vignette.ResetMotion(); vignette.Tick(.3f);
        ui.ShowVictory();
        var label=(TMPro.TextMeshProUGUI)typeof(CampusExperience).GetField("debriefLabel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(experience);
        label.text="本次练习用时 03:12 / Practice time\n识别警报 · 跟随标识 · 路线受阻时重新选择\nNotice cues · Follow signs · Choose another route\n请勿使用电梯或跳窗逃生。\nAvoid elevators and window escape.";
        PlaceUI(systems); Shot(camera,"DebriefUI",head.position,head.position+head.forward*2);
        ui.ShowGameplay(); PlaceUI(systems);
        systems.GetComponentInChildren<CampusMessagePresenter>(true).Show(CampusMessagePresenter.WindowWarning);
        Shot(camera,"WindowWarning_1m",head.position,head.position+head.forward*2);
        foreach(var text in systems.GetComponentsInChildren<TMPro.TMP_Text>(true)) Debug.Log("UI_LAYOUT "+text.name+" pos="+text.rectTransform.anchoredPosition+" size="+text.rectTransform.sizeDelta+" text="+text.text);
        RenderUIOcclusion(camera,systems,head,ui);
    }
    private static void ValidateComfortImage()
    {
        var shader=Shader.Find("Campus/Comfort Vignette");
        if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new System.InvalidOperationException("Comfort shader failed graphical compilation.");
        var normal=new Texture2D(2,2); normal.LoadImage(File.ReadAllBytes("Documentation/Previews/GameplayHUD.png"));
        var comfort=new Texture2D(2,2); comfort.LoadImage(File.ReadAllBytes("Documentation/Previews/GameplayHUD_ComfortWalk.png"));
        float edge=normal.GetPixel(50,50).grayscale, darkened=comfort.GetPixel(50,50).grayscale;
        float centre=normal.GetPixel(720,450).grayscale, preserved=comfort.GetPixel(720,450).grayscale;
        UnityEngine.Object.DestroyImmediate(normal); UnityEngine.Object.DestroyImmediate(comfort);
        if(edge<.05f || darkened>=edge*.8f || Mathf.Abs(centre-preserved)>.03f) throw new System.InvalidOperationException("Comfort overlay must darken the edge and preserve the central view: "+edge+" -> "+darkened+", centre "+centre+" -> "+preserved);
        Debug.Log("COMFORT_RENDER_PASS (edge darkens; centre preserved)");
    }
    private static void PlaceUI(GameObject systems)
    {
        foreach(var anchor in systems.GetComponentsInChildren<WorldSpaceUIAnchor>())
            typeof(WorldSpaceUIAnchor).GetMethod("Place",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(anchor,null);
    }
    private static void Shot(Camera camera,string name,Vector3 p,Vector3 look)
    {
        foreach(var text in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) text.ForceMeshUpdate();
        foreach(var root in UnityEngine.Object.FindObjectsOfType<CampusUIForeground>()) root.Refresh();
        Canvas.ForceUpdateCanvases();
        camera.transform.position=p; camera.transform.LookAt(look);
        var target=new RenderTexture(1440,900,24); target.Create(); camera.targetTexture=target; camera.Render();
        var old=RenderTexture.active; RenderTexture.active=target;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1440,900),0,0); image.Apply();
        File.WriteAllBytes("Documentation/Previews/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=old; camera.targetTexture=null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
    }
}
