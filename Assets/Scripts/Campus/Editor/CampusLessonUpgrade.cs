using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class CampusSceneBuilder
{
    [MenuItem("Tools/Campus/Apply Guided Classroom Opening (keeps building)")]
    public static void ApplyLessonUpdate()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup="Assets/Scenes/Legacy/CampusTraining_v9_PreEmbodiment_Backup.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup)) throw new IOException("Cannot back up campus scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath); ConfigureGuidedLesson();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save guided training.");
        AssetDatabase.SaveAssets(); Validate(); Debug.Log("CAMPUS_LESSON_UPDATE_COMPLETE");
    }
    private static void ConfigureGuidedLesson()
    {
        var old=GameObject.Find("07 Guided Classroom Lesson"); if(old!=null) UnityEngine.Object.DestroyImmediate(old);
        InitMaterials();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Resolve("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset"));
        var player=GameObject.Find("05 Player & Shared Systems");
        var movement=player.GetComponentInChildren<CampusLocomotion>(true);
        head=movement.GetComponentInChildren<Camera>(true).transform;
        ConfigureDetailedHands();
        ConfigureForwardHUD(player);
        foreach(var hand in player.GetComponentsInChildren<CampusHandInteractor>(true)) SetFloat(hand,"pickupDistance",.20f);
        ConfigureTrackingHeight(movement);
        var damage=player.GetComponentInChildren<CampusSmokeExposure>(true);
        SetFloat(damage,"graceSeconds",6); SetFloat(damage,"damagePerSecond",2f); SetFloat(damage,"coveredDamageMultiplier",.4f);
        foreach(var oldFade in head.GetComponentsInChildren<Canvas>(true).Where(c=>c.name=="Seating fade").ToArray()) UnityEngine.Object.DestroyImmediate(oldFade.gameObject);
        var oldConfirm=head.Find("Lesson contact confirmation"); if(oldConfirm!=null) UnityEngine.Object.DestroyImmediate(oldConfirm.gameObject);
        var root=new GameObject("07 Guided Classroom Lesson"); var lesson=root.AddComponent<CampusLessonDirector>();
        Set(lesson,"trackedHead",head); Set(lesson,"locomotion",movement);
        var classroom=UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t=>t.name=="401 教室");
        var classroomDoor=classroom.GetComponentInChildren<CampusHingeInteractable>(true); Set(lesson,"classroomDoor",classroomDoor);
        SetArray(lesson,"practiceWindows",UnityEngine.Object.FindObjectsOfType<RuntimeFireWindow>(true).Where(w=>
        { var p=w.GetComponent<Renderer>().bounds.center; return p.x>-20 && p.x<-10 && p.z>9 && p.y>11.4f && p.y<15; }).Cast<UnityEngine.Object>().ToArray());
        Set(lesson,"smokeSpread",UnityEngine.Object.FindObjectOfType<CampusSmokeSpread>());
        Set(lesson,"fireOrigin",UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t=>t.name=="4F reading room fire origin").gameObject);
        var experience=player.GetComponentInChildren<CampusExperience>(true);
        Set(lesson,"alarm",new SerializedObject(experience).FindProperty("alarm").objectReferenceValue);
        var gameplay=(GameObject)new SerializedObject(player.GetComponentInChildren<SystemUIView>(true)).FindProperty("gameplayUIRoot").objectReferenceValue;
        Set(lesson,"gameplayHUD",gameplay); Set(lesson,"minimap",gameplay.GetComponentsInChildren<Canvas>(true).First(c=>c.name.StartsWith("Live minimap")).gameObject);
        Set(lesson,"targetTowel",UnityEngine.Object.FindObjectsOfType<CampusTowelInteractable>(true).OrderByDescending(t=>t.transform.position.y).First());
        var area=new GameObject("411 restroom lesson bounds"); area.transform.SetParent(root.transform,false);
        var volume=area.AddComponent<BoxCollider>(); volume.isTrigger=true; volume.center=new Vector3(-15,13.1f,-5.7f); volume.size=new Vector3(9.5f,3.4f,7.2f); Set(lesson,"restroom",volume);
        var plume=new GameObject("401 opening doorway smoke"); plume.transform.SetParent(root.transform,false);
        var smokeSource=UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true).First(p=>p.name=="4F corridor x=-10");
        var smoke=UnityEngine.Object.Instantiate(smokeSource,plume.transform); smoke.name="Doorway teaching smoke";
        smoke.transform.SetPositionAndRotation(new Vector3(-12.2f,13.15f,2.5f),Quaternion.identity);
        var shape=smoke.shape; shape.scale=new Vector3(1.2f,.65f,1.0f);
        var emission=smoke.emission; emission.rateOverTime=11;
        var main=smoke.main; main.startSize=new ParticleSystem.MinMaxCurve(.7f,1.1f); main.maxParticles=75;
        var plumeCollider=plume.AddComponent<BoxCollider>(); plumeCollider.isTrigger=true;
        plumeCollider.center=new Vector3(-12.2f,12.9f,2.4f); plumeCollider.size=new Vector3(2.1f,2.7f,2.8f);
        var exposure=plume.AddComponent<CampusSmokeVolume>(); exposure.enabled=false;
        Set(lesson,"doorwayPlume",plume); Set(lesson,"doorwayExposure",exposure); plume.SetActive(false);
        CreateLessonPresentation(root,lesson);
        CreateStoryProps(root,lesson,player);
        CreatePracticeBooks(root,lesson);
        ConfigureLessonDestination(lesson);
        var routeRoot=new GameObject("Lesson floor chevrons (no ray)"); routeRoot.transform.SetParent(root.transform,false);
        var route=routeRoot.AddComponent<CampusLessonRoute>(); var arrows=new Transform[3];
        for(int i=0;i<arrows.Length;i++)
        {
            var arrow=Group("Route chevron "+i,routeRoot.transform); arrows[i]=arrow;
            foreach(int side in new[]{-1,1})
            {
                var wing=Cube("Chevron wing",arrow,Vector3.zero,new Vector3(.26f,.02f,.085f),ObjectiveMaterial(),false,false);
                wing.isStatic=false; wing.transform.localPosition=new Vector3(side*.065f,0,0);
                wing.transform.localRotation=Quaternion.Euler(0,side*45,0);
            }
            arrow.gameObject.SetActive(false);
        }
        SetArray(route,"arrows",arrows.Cast<UnityEngine.Object>().ToArray()); Set(lesson,"route",route);
        // Restore the known 401 starting position only; never rotate a tracked head.
        movement.transform.SetPositionAndRotation(new Vector3(-12.2f,11.4f,5.2f),Quaternion.Euler(0,180,0));
        var presentation=(GameObject)new SerializedObject(lesson).FindProperty("lessonPanel").objectReferenceValue;
        presentation.transform.SetPositionAndRotation(head.TransformPoint(new Vector3(0,-.10f,1.6f)),head.rotation);
    }
    private static void ConfigureTrackingHeight(CampusLocomotion movement)
    {
        var space=movement.transform.Find("Calibrated tracking space");
        if(space==null)
        {
            space=Group("Calibrated tracking space",movement.transform);
            head.SetParent(space,true);
            foreach(var pose in movement.GetComponentsInChildren<Valve.VR.SteamVR_Behaviour_Pose>(true))
            { pose.transform.SetParent(space,true); pose.origin=space; }
        }
        space.localPosition=Vector3.zero; space.localScale=Vector3.one;
        foreach(var pose in movement.GetComponentsInChildren<Valve.VR.SteamVR_Behaviour_Pose>(true)) { pose.transform.SetParent(space,true); pose.origin=space; }
        // Visible interaction proxies follow controller world poses. Sharing this parent
        // also keeps them aligned in previews and before the first tracking update.
        foreach(var proxy in UnityEngine.Object.FindObjectsOfType<DummyController>(true)) proxy.transform.SetParent(space,true);
        var height=movement.GetComponent<CampusTrackingHeight>() ?? movement.gameObject.AddComponent<CampusTrackingHeight>();
        Set(height,"trackedHead",head); Set(height,"trackingSpace",space); SetFloat(height,"standingEyeHeight",1.70f);
        movement.GetComponent<CharacterController>().height=1.7f; movement.GetComponent<CharacterController>().center=Vector3.up*.85f;
        head.localPosition=new Vector3(head.localPosition.x,1.70f,head.localPosition.z);
        if(head.GetComponent<CampusTrackedHeadPose>()==null) head.gameObject.AddComponent<CampusTrackedHeadPose>();
        foreach(var legacy in head.GetComponents<Behaviour>()) if(legacy.GetType().Name=="TrackedPoseDriver") legacy.enabled=false;
        foreach(var proxy in UnityEngine.Object.FindObjectsOfType<DummyController>(true))
        {
            var pose=proxy.GetComponent<CampusTrackedHandPose>() ?? proxy.gameObject.AddComponent<CampusTrackedHandPose>();
            Set(pose,"trackingSpace",space); Set(pose,"trackedHead",head);
            var source=new SerializedObject(proxy); Set(pose,"trackedController",((GameObject)source.FindProperty("controller").objectReferenceValue).transform);
            SetInt(pose,"inputSource",source.FindProperty("inputSource").intValue);
        }
    }

    // SDK generation updates its existing generated input files; all authored logic stays in Scripts.
    public static void GenerateLessonInput()
    {
        Valve.VR.SteamVR_Input.InitializeFile(true);
        Valve.VR.SteamVR_Input_Generator.BeginGeneration();
        Debug.Log("CAMPUS_A_KEY_INPUT_GENERATED");
    }
    private static void ConfigureForwardHUD(GameObject player)
    {
        var ui=player.GetComponentInChildren<SystemUIView>(true);
        var gameplay=(GameObject)new SerializedObject(ui).FindProperty("gameplayUIRoot").objectReferenceValue;
        var healthAnchor=gameplay.GetComponent<WorldSpaceUIAnchor>();
        foreach(var anchor in player.GetComponentsInChildren<WorldSpaceUIAnchor>(true))
        {
            var setting=new SerializedObject(anchor); var target=(Transform)setting.FindProperty("presentationRoot").objectReferenceValue;
            // Contact-operated pause/outcome menus stay reachable; reading HUDs stay farther away.
            Vector3 offset=new Vector3(0,0,.70f);
            if(target!=null && target.name.StartsWith("Live minimap")) { offset=new Vector3(0,-.38f,1.6f); target.localScale=Vector3.one*.005f; }
            else if(anchor==healthAnchor) offset=new Vector3(0,.47f,1.6f);
            else if(target!=null)
            {
                float width=target.GetComponentsInChildren<RectTransform>(true).Select(r=>r.rect.width*Mathf.Abs(r.lossyScale.x)).DefaultIfEmpty(0).Max();
                if(width>.001f) target.localScale*=.70f/width;
            }
            SetVector(anchor,"headLocalOffset",offset); SetBool(anchor,"followHead",true);
        }
        if(healthAnchor!=null) SetVector(healthAnchor,"headLocalOffset",new Vector3(0,.47f,1.6f));
        var label=gameplay.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.name=="Campus Floor HUD");
        if(label!=null) { label.rectTransform.anchoredPosition=new Vector2(0,.16f); label.rectTransform.localScale=Vector3.one*.004f; }
    }
    private static void CreatePracticeBooks(GameObject root,CampusLessonDirector lesson)
    {
        var books=new CampusPracticeBook[2];
        for(int i=0;i<books.Length;i++)
        {
            var book=Group("Grabbable practice book "+(i+1),root.transform);
            book.position=new Vector3(-12.2f+(i==0?-.23f:.24f),12.185f,4.55f);
            var pages=TeachingCube("Practice pages",book,Vector3.zero,new Vector3(.18f,.032f,.24f),white);
            pages.GetComponent<Renderer>().sharedMaterial=white;
            foreach(int side in new[]{-1,1}) TeachingCube("Practice cover",book,new Vector3(0,side*.02f,0),new Vector3(.19f,.006f,.25f),teal).GetComponent<Renderer>().sharedMaterial=i==0?teal:wood;
            var collider=book.gameObject.AddComponent<BoxCollider>(); collider.size=new Vector3(.19f,.05f,.25f);
            var body=book.gameObject.AddComponent<Rigidbody>(); body.mass=.25f; body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            books[i]=book.gameObject.AddComponent<CampusPracticeBook>();
        }
        SetArray(lesson,"practiceBooks",books.Cast<UnityEngine.Object>().ToArray());
    }
    private static void CreateLessonPresentation(GameObject root,CampusLessonDirector lesson)
    {
        var panel=new GameObject("Animated training panel"); panel.transform.SetParent(root.transform,false); Set(lesson,"lessonPanel",panel);
        var canvasRoot=new GameObject("Teaching text",typeof(RectTransform),typeof(Canvas)); canvasRoot.transform.SetParent(panel.transform,false);
        canvasRoot.transform.localScale=Vector3.one*.002f; canvasRoot.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var group=canvasRoot.AddComponent<CanvasGroup>(); group.interactable=false; group.blocksRaycasts=false;
        var background=MapImage("Teaching card",canvasRoot.transform,Vector2.zero,new Vector2(760,480),new Color(.025f,.075f,.09f,.94f));
        var heading=UItext("Lesson heading",canvasRoot.transform,new Vector2(710,50),29); heading.rectTransform.anchoredPosition=new Vector2(0,194);
        var bodyText=UItext("Lesson explanation",canvasRoot.transform,new Vector2(710,142),24); bodyText.rectTransform.anchoredPosition=new Vector2(0,100);
        var hint=UItext("Lesson hint",canvasRoot.transform,new Vector2(710,72),19); hint.rectTransform.anchoredPosition=new Vector2(0,-202);
        foreach(var text in new[]{heading,bodyText,hint}) { text.enableWordWrapping=true; text.enableAutoSizing=true; text.fontSizeMin=16; text.fontSizeMax=text.fontSize; }
        heading.text="头显与安全 / Headset & safety";
        bodyText.text="自然转头观察；调整佩戴并确认周围空间安全。不适时暂停。\nLook around naturally. Check your fit and clear space; pause if uncomfortable.";
        hint.text="可自由移动与练习；教学自动继续，A 可跳过。\nExplore freely. Teaching advances automatically; A skips.";
        Set(lesson,"title",heading); Set(lesson,"explanation",bodyText); Set(lesson,"footer",hint);
        canvasRoot.AddComponent<CampusUIForeground>().Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Campus/Shaders/CampusForegroundUI.shader"),AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader"));
        var demo=new GameObject("Animated 3D instruction models"); demo.transform.SetParent(panel.transform,false); demo.transform.localPosition=new Vector3(0,-.17f,-.04f);
        var animation=demo.AddComponent<CampusLessonAnimation>(); Set(lesson,"demonstration",animation);
        var layout=root.AddComponent<CampusLessonLayout>(); Set(layout,"trackedHead",head); Set(layout,"panel",panel.transform); Set(layout,"demo",demo.transform);
        Set(layout,"background",background.rectTransform); Set(layout,"title",heading); Set(layout,"body",bodyText); Set(layout,"footer",hint); layout.Arrange(true);
        Set(layout,"textGroup",group);
        var progress=MapImage("Reading progress",canvasRoot.transform,new Vector2(-355,-242),new Vector2(0,4),new Color(.17f,.68f,.9f,1));
        progress.rectTransform.pivot=new Vector2(0,.5f); Set(layout,"readingProgress",progress);
        var chalk=TeachingMaterial("Teaching light",new Color(.86f,.94f,.96f));
        var ink=TeachingMaterial("Teaching dark",new Color(.07f,.18f,.22f)); var blue=TeachingMaterial("Teaching blue",new Color(.15f,.6f,.85f));
        var yellow=TeachingMaterial("Teaching highlight",new Color(1,.72f,.12f));
        var hmdGroup=Group("Headset demonstration",demo.transform); Set(animation,"headsetGroup",hmdGroup.gameObject);
        TeachingSphere("Headset fit reference head",hmdGroup,new Vector3(0,.03f,.20f),new Vector3(.24f,.29f,.23f),blue);
        var hmd=Group("Animated headset fitting",hmdGroup); Set(animation,"headset",hmd);
        TeachingSphere("Headset visor",hmd,new Vector3(0,.015f,0),new Vector3(.34f,.17f,.15f),chalk);
        foreach(float x in new[]{-.08f,0,.08f}) TeachingSphere("Front sensor diagram",hmd,new Vector3(x,.015f,-.075f),new Vector3(.030f,.075f,.018f),ink);
        TeachingCube("Face cushion",hmd,new Vector3(0,.015f,.08f),new Vector3(.28f,.13f,.05f),ink);
        foreach(int s in new[]{-1,1}) TeachingCube("Head strap",hmd,new Vector3(s*.16f,.03f,.15f),new Vector3(.035f,.05f,.32f),ink);
        var controls=Group("Controller and push demonstration",demo.transform); Set(animation,"controllerGroup",controls.gameObject);
        CreateControllerTeaching(controls,animation,chalk,ink,blue,yellow,hint);
        var towels=Group("Wetting and face-cover demonstration",demo.transform); Set(animation,"towelGroup",towels.gameObject);
        TeachingCube("Demo basin",towels,new Vector3(-.18f,-.10f,0),new Vector3(.32f,.055f,.20f),chalk);
        TeachingCube("Demo faucet",towels,new Vector3(-.12f,.09f,.025f),new Vector3(.035f,.18f,.035f),ink);
        var lever=TeachingCube("Demo tap lever",towels,new Vector3(-.12f,.17f,0),new Vector3(.12f,.025f,.025f),yellow); Set(animation,"tapLever",lever);
        var water=TeachingCube("Demo flowing water",towels,new Vector3(-.12f,.04f,-.025f),new Vector3(.012f,.18f,.012f),blue); Set(animation,"water",water);
        var cloth=TeachingCube("Animated demo towel",towels,new Vector3(-.12f,0,0),new Vector3(.16f,.02f,.10f),blue); Set(animation,"cloth",cloth);
        var face=Group("Demo head reference",towels); face.localPosition=new Vector3(.24f,.10f,.035f); Set(animation,"face",face);
        TeachingSphere("Demo head",face,Vector3.zero,new Vector3(.17f,.21f,.15f),chalk);
        TeachingCube("Mouth and nose reference",face,new Vector3(0,-.02f,-.08f),new Vector3(.065f,.045f,.015f),yellow);
        var smoke=Group("Smoke health demonstration",demo.transform); Set(animation,"smokeGroup",smoke.gameObject);
        for(int i=0;i<5;i++) TeachingSphere("Demo smoke puff",smoke,new Vector3(-.2f+i*.085f,.04f+Mathf.Sin(i)*.05f,0),Vector3.one*.14f,ink);
        var bar=Group("Animated health strip",smoke); bar.localPosition=new Vector3(.22f,.12f,-.04f);
        TeachingCube("Health strip",bar,new Vector3(0,0,0),new Vector3(.24f,.045f,.025f),yellow); Set(animation,"healthFill",bar);
        animation.Show(CampusLessonDirector.Phase.Controls,0); animation.Tick(.1f);
        // A full-view black fade is local to this flow; never reused as a head-tracking lock.
        var blackout=new GameObject("Seating fade",typeof(RectTransform),typeof(Canvas)); blackout.transform.SetParent(head,false);
        blackout.transform.localPosition=new Vector3(0,0,.15f); blackout.transform.localScale=Vector3.one*.001f;
        blackout.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var fade=MapImage("Fade",blackout.transform,Vector2.zero,new Vector2(2000,2000),new Color(0,0,0,0)); Set(lesson,"fade",fade);
        blackout.AddComponent<CampusUIForeground>().Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Campus/Shaders/CampusForegroundUI.shader"));
    }
    private static Material TeachingMaterial(string name,Color color)
    {
        string path="Assets/Art/Campus/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Campus/Teaching Prop Foreground")); AssetDatabase.CreateAsset(material,path); }
        material.shader=Shader.Find("Campus/Teaching Prop Foreground"); material.SetColor("_BaseColor",color); EditorUtility.SetDirty(material); return material;
    }
    private static Transform TeachingCube(string name,Transform parent,Vector3 p,Vector3 size,Material material)
    { var go=Cube(name,parent,Vector3.zero,size,material,false,false); go.isStatic=false; go.transform.localPosition=p; go.GetComponent<Renderer>().sortingOrder=31000; return go.transform; }
    private static Transform TeachingSphere(string name,Transform parent,Vector3 p,Vector3 size,Material material)
    { var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; go.GetComponent<Renderer>().sortingOrder=31000; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform; }
    private static Transform TeachingController(Transform parent,float x,Material material,Material darkMaterial)
    {
        var root=Group("Quest-style controller diagram",parent); root.localPosition=new Vector3(x,0,0);
        var grip=TeachingCube("Controller grip",root,new Vector3(0,0,0),new Vector3(.075f,.17f,.07f),material); grip.localRotation=Quaternion.Euler(-15,0,0);
        TeachingSphere("Controller face",root,new Vector3(0,.10f,0),new Vector3(.115f,.055f,.105f),darkMaterial); return root;
    }
    private static void CreateStoryProps(GameObject root,CampusLessonDirector lesson,GameObject player)
    {
        // One unobstructed personal desk in the existing aisle: the book has a real
        // tabletop to rest on, rather than floating between imported chair/desk units.
        var desk=Group("Opening personal desk and seat",root.transform);
        Vector3 p=new Vector3(-12.2f,11.4f,4.55f);
        Cube("Opening desk tabletop",desk,p+Vector3.up*.735f,new Vector3(.85f,.04f,.65f),wood);
        foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1})
            Cube("Opening desk leg",desk,p+new Vector3(x*.36f,.35f,z*.25f),new Vector3(.045f,.70f,.045f),dark);
        Cube("Opening seat cushion",desk,new Vector3(-12.2f,11.82f,5.25f),new Vector3(.44f,.065f,.4f),teal,false,false);
        Cube("Opening seat back",desk,new Vector3(-12.2f,12.15f,5.46f),new Vector3(.44f,.50f,.055f),teal,false,false);
        var rest=Group("Book resting point",desk); rest.position=p+Vector3.up*.78f; Set(lesson,"bookRest",rest);
        var standing=Group("Opening standing position",root.transform); standing.SetPositionAndRotation(new Vector3(-12.2f,11.4f,5.2f),Quaternion.Euler(0,180,0)); Set(lesson,"storyStandingPoint",standing);
        var book=Group("Opening story book",root.transform);
        TeachingCube("Book pages",book,Vector3.zero,new Vector3(.22f,.032f,.30f),white).GetComponent<Renderer>().sharedMaterial=white;
        TeachingCube("Book cover",book,new Vector3(0,.020f,0),new Vector3(.23f,.008f,.31f),teal).GetComponent<Renderer>().sharedMaterial=teal;
        var pagePivot=Group("Story page hinge",book); pagePivot.localPosition=new Vector3(-.11f,.03f,0);
        var page=TeachingCube("Turning page",pagePivot,new Vector3(.11f,0,0),new Vector3(.22f,.002f,.30f),white); page.GetComponent<Renderer>().sharedMaterial=white;
        Set(lesson,"storyPage",pagePivot);
        Set(lesson,"storyBook",book); book.gameObject.SetActive(false);
        var hands=player.GetComponentsInChildren<CampusHandInteractor>(true).OrderBy(h=>h.InputSource==Valve.VR.SteamVR_Input_Sources.LeftHand?0:1).ToArray();
        SetArray(lesson,"trackedHandRenderers",hands.SelectMany(h=>h.GetComponentsInChildren<Renderer>(true)).Cast<UnityEngine.Object>().ToArray());
        for(int i=0;i<2;i++)
        {
            var hand=Group(i==0?"Opening left hand":"Opening right hand",root.transform);
            foreach(var source in hands[i].GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var stored=BakeHandInProxySpace(hands[i],source,"Story hand "+i+" "+source.name);
                var go=new GameObject("Story glove mesh"); go.transform.SetParent(hand,false);
                go.AddComponent<MeshFilter>().sharedMesh=stored; go.AddComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            Set(lesson,i==0?"storyLeftHand":"storyRightHand",hand); hand.gameObject.SetActive(false);
        }
    }
}
