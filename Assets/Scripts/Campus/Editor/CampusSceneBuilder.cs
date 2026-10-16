using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Harbourview.Journey;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor-only, deterministic construction. The saved scene needs no procedural builder at runtime.
public static partial class CampusSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Campus/CampusTraining.unity";
    private const float Storey = 3.8f;
    private static Material cream, teal, wood, floor, dark, green, red, brass, glass, grass, paving, white;
    private static TMP_FontAsset font;
    private static Transform head, systems;
    private static Collider body;
    private static ResolutionCoordinator resolution;
    private static readonly Dictionary<Material, Material> propMaterials = new Dictionary<Material, Material>();
    private static readonly Color[] accents = { new Color(.14f,.50f,.46f), new Color(.35f,.48f,.67f), new Color(.67f,.42f,.24f), new Color(.25f,.43f,.47f) };

    [MenuItem("Tools/Campus/Rebuild Four Storey Campus (replaces generated scene)")]
    public static void RebuildWithConfirmation()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (EditorUtility.DisplayDialog("Rebuild Campus", "This regenerates CampusTraining only. Save any hand-edited version under another name first.", "Rebuild", "Cancel")) Build();
    }

    public static void Build()
    {
        propMaterials.Clear();
        BackupPreviousCampus();
        Folder("Assets/Scenes/Campus"); Folder("Assets/Art/Campus/Materials"); Folder("Assets/Prefabs/Campus");
        InitMaterials();
        ConfigurePhysicalWindowAsset();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Resolve("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset"));
        if (font == null) throw new InvalidOperationException("Missing bilingual font.");
        CloneTeamSystems();
        var architecture = Group("01 Architecture");
        var interiors = Group("02 Furnishing & Interactive Objects");
        var exterior = Group("03 Campus Grounds");
        var experience = Group("04 Training Zones");
        SetupLighting();
        for (int level = 0; level < 4; level++) BuildRevisedFloor(level, architecture, interiors, experience);
        BuildStairTower(-23, "A", architecture, experience);
        BuildStairTower(23, "B", architecture, experience);
        FinishStairConnections(architecture);
        BuildGrounds(exterior, experience);
        Cube("Roof parapet north", architecture, new Vector3(0,15.6f,10), new Vector3(40,.7f,.22f), teal);
        Cube("Roof parapet south", architecture, new Vector3(0,15.6f,-10), new Vector3(40,.7f,.22f), teal);
        Cube("Main roof", architecture, new Vector3(0,15.1f,0), new Vector3(40,.2f,20), cream);
        Cube("Entrance canopy", exterior, new Vector3(0,3.5f,-12.6f), new Vector3(7,.18f,5.4f), wood);
        for (int side = -1; side <= 1; side += 2)
            Cube("Canopy column", exterior, new Vector3(side*3.2f,1.7f,-14.6f), new Vector3(.18f,3.4f,.18f), dark);
        Sign("HARBOURVIEW\nLEARNING BLOCK  /  教学楼", exterior, new Vector3(0,4.45f,-10.22f), 0, new Vector2(7,1.2f), teal, .38f);
        Sign("FIRE SAFETY PRACTICE  /  消防疏散教学", exterior, new Vector3(0,2.95f,-15.25f), 0, new Vector2(6,.38f), teal, .18f);
        WireExperience();
        UpgradePlayerControls();
        ConfigureComfortAndSmoke();
        CampusTeleportSetup.ConfigureScene();
        RecordGeneratedPrefabOverrides();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        ConfigureMainMenu();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu/MainMenu.unity", true),
            new EditorBuildSettingsScene(ScenePath, true)
        };
        EditorSceneManager.OpenScene(ScenePath);
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("CAMPUS_BUILD_COMPLETE " + ScenePath);
    }

    private static void CloneTeamSystems()
    {
        // SampleScene has the team's complete victory/pause/failure presentation;
        // whole_floor contains a null victory-panel binding and is kept only as an archive.
        var source = EditorSceneManager.OpenScene(Resolve("Assets/Scenes/SampleScene.unity"), OpenSceneMode.Single);
        var transfer = new GameObject("05 Player & Shared Systems");
        string[] names = { "[CameraRig]", "RightControllerDummy", "LeftControllerDummy", "SystemUIRoot", "HealthTimeSystems", "EventSystem" };
        foreach (var root in source.GetRootGameObjects())
            if (names.Contains(root.name)) root.transform.SetParent(transfer.transform, true);
        // Instantiate as one hierarchy so all cross-object references are remapped together.
        var copy = UnityEngine.Object.Instantiate(transfer);
        var target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.MoveGameObjectToScene(copy, target);
        EditorSceneManager.CloseScene(source, true); // deliberately do not save the source hierarchy
        SceneManager.SetActiveScene(target);
        copy.name = "05 Player & Shared Systems";
        systems = copy.transform;
        var rig = copy.GetComponentsInChildren<Valve.VR.SteamVR_PlayArea>(true).First().transform;
        rig.SetPositionAndRotation(new Vector3(-12.2f, 11.4f, 5.2f), Quaternion.Euler(0,180,0));
        head = rig.GetComponentInChildren<Camera>(true).transform;
        head.gameObject.tag = "MainCamera";
        head.localPosition = new Vector3(0,1.65f,0); // sensible editor preview; tracked HMD pose replaces it in VR
        head.GetComponent<Camera>().nearClipPlane = .05f;
        head.GetComponent<Camera>().farClipPlane = 130f;
        if (head.GetComponent<VRControlTutorial>() == null) head.gameObject.AddComponent<VRControlTutorial>();
        foreach(var oldRay in copy.GetComponentsInChildren<VRFireSafetyInteraction>(true)) UnityEngine.Object.DestroyImmediate(oldRay);
        var right = copy.GetComponentsInChildren<DummyController>(true).First(d=>d.name=="RightControllerDummy").gameObject;
        SetInt(right.GetComponent<DummyController>(),"inputSource",(int)Valve.VR.SteamVR_Input_Sources.RightHand);
        var leftPose=rig.GetComponentsInChildren<Valve.VR.SteamVR_Behaviour_Pose>(true).FirstOrDefault(p=>p.inputSource==Valve.VR.SteamVR_Input_Sources.LeftHand);
        if(leftPose!=null && !copy.GetComponentsInChildren<DummyController>(true).Any(d=>d.gameObject.name=="LeftControllerDummy")) {
            var left=UnityEngine.Object.Instantiate(right,systems); left.name="LeftControllerDummy";
            Set(left.GetComponent<DummyController>(),"controller",leftPose.gameObject);
            SetInt(left.GetComponent<DummyController>(),"inputSource",(int)Valve.VR.SteamVR_Input_Sources.LeftHand);
            SetInt(left.GetComponent<VRTeleporter>(),"inputSource",(int)Valve.VR.SteamVR_Input_Sources.LeftHand);
        }
        foreach(var dummy in copy.GetComponentsInChildren<DummyController>(true).Where(d=>d.gameObject!=right)) {
            Set(dummy,"controller",leftPose.gameObject); SetInt(dummy,"inputSource",(int)Valve.VR.SteamVR_Input_Sources.LeftHand);
            SetInt(dummy.GetComponent<VRTeleporter>(),"inputSource",(int)Valve.VR.SteamVR_Input_Sources.LeftHand);
        }
        SetInt(right.GetComponent<VRTeleporter>(),"inputSource",(int)Valve.VR.SteamVR_Input_Sources.RightHand);
        if (right.GetComponent<VRFireSafetyInteraction>() == null) {
            // The teleporter and trigger ray must NOT share a LineRenderer.
            var rayObject = new GameObject("Right Trigger Interaction"); rayObject.transform.SetParent(right.transform, false);
            var ray = rayObject.AddComponent<VRFireSafetyInteraction>();
            rayObject.GetComponent<LineRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Resolve("Assets/Scripts/Interaction/Resources/FireSafetyRay.mat"));
            Set(ray, "vrCamera", head.GetComponent<Camera>());
        }
        foreach (var teleporter in copy.GetComponentsInChildren<VRTeleporter>(true)) {
            Set(teleporter,"playerRoot",rig); Set(teleporter,"vrCamera",head); SetInt(teleporter,"teleportLayer",~0);
            SetFloat(teleporter,"maxHeightChange",2.2f);
        }
        var tracker = new GameObject("Tracked Player Body"); tracker.transform.SetParent(systems,false);
        tracker.tag = "Player";
        var capsule = tracker.AddComponent<CapsuleCollider>(); capsule.radius=.22f; capsule.height=1.3f; capsule.isTrigger=true;
        var rb=tracker.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false;
        Set(tracker.AddComponent<CampusBodyTracker>(),"trackedHead",head); body=capsule;
        resolution=copy.GetComponentInChildren<ResolutionCoordinator>(true);
        foreach(var nav in copy.GetComponentsInChildren<SystemUINavigation>(true)) SetString(nav,"gameplayEntrySceneName","CampusTraining");
        foreach(var failure in copy.GetComponentsInChildren<FailureUI>(true)) SetString(failure,"restartSceneName","CampusTraining");
        var pause=copy.GetComponentInChildren<SystemUIPauseController>(true);
        SetArray(pause,"additionalTeleports",copy.GetComponentsInChildren<VRTeleporter>(true).Where(t=>t.gameObject!=right).Cast<UnityEngine.Object>().ToArray());
        SetArray(pause,"menuInteractions",copy.GetComponentsInChildren<VRFireSafetyInteraction>(true).Cast<UnityEngine.Object>().ToArray());
        foreach(var text in copy.GetComponentsInChildren<TMP_Text>(true)) text.font=font;
        foreach(var text in copy.GetComponentsInChildren<TMP_Text>(true))
            if(text.text.Contains("训练完成")) text.text="已到达安全地点\nSAFE ENDPOINT REACHED";
        // Provide legible bilingual floating prompts without changing the team's timing logic.
        var prompt=new GameObject("Health Prompt",typeof(RectTransform),typeof(Canvas));
        prompt.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        prompt.transform.localScale=Vector3.one*.002f;
        var tmp=UItext("Text",prompt.transform,new Vector2(650,90),28);
        PrefabUtility.SaveAsPrefabAsset(prompt,"Assets/Prefabs/Campus/HealthPrompt.prefab");
        UnityEngine.Object.DestroyImmediate(prompt);
        Set(copy.GetComponentInChildren<FloatingTextManager>(true),"floatingTextPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Campus/HealthPrompt.prefab"));
    }

    private static void BuildFloor(int level, Transform architecture, Transform interiors, Transform zones)
    {
        float y=level*Storey;
        var structure=Group($"{level+1}F Structure",architecture);
        var furnishing=Group($"{level+1}F Rooms",interiors);
        Material accent=Mat("Floor "+(level+1)+" Accent",accents[level]);
        Cube("Walkable floor",structure,new Vector3(0,y-.12f,0),new Vector3(40,.24f,20),floor,true);
        Cube("Corridor runner",structure,new Vector3(0,y+.006f,0),new Vector3(40,.012f,2.1f),accent,false,false);
        foreach(int side in new[]{-1,1}) {
            float z=side*10;
            for(int w=0;w<10;w++) {
                float x=-18+4*w;
                if(level==0 && side==-1 && w==4) continue;
                if(level==0 && side==-1 && w==5) continue;
                var window=Prefab(Resolve("Assets/Stair Prefab/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab"),structure);
                window.name=$"Wall_3m_Window_H {level+1}F {(side>0?"North":"South")} {w+1}";
                window.transform.SetPositionAndRotation(new Vector3(x,y,z),Quaternion.Euler(0,side>0?180:0,0));
                window.transform.localScale=new Vector3(2,1.2f,1.5f);
                foreach(var renderer in window.GetComponentsInChildren<MeshRenderer>(true)) {
                    var mats=renderer.sharedMaterials;
                    for(int m=0;m<mats.Length;m++) mats[m]=m==2?glass:(m==1?dark:cream);
                    renderer.sharedMaterials=mats;
                }
            }
            Cube("Facade band",structure,new Vector3(0,y+3.65f,z),new Vector3(40,.3f,.45f),accent);
            Cube("Facade sill",structure,new Vector3(0,y+.75f,z+side*.22f),new Vector3(40,.12f,.3f),teal);
            for(int x=-20;x<=20;x+=10) Cube("Facade pier",structure,new Vector3(x,y+1.8f,z),new Vector3(.3f,3.6f,.6f),cream);
            // Four rooms on each side, with actual door openings onto the central corridor.
            for(int room=0;room<4;room++) {
                float cx=-15+room*10;
                string type=side>0?"CLASSROOM":(room==0?"SEMINAR":room==1?(level==1?"REFUGE":"READING ROOM"):room==2?"LIFT LOBBY":"SCIENCE LAB");
                var roomRoot=Group($"{level+1}{(side>0?'0':'1')}{room+1} {type}",furnishing);
                Partition(roomRoot,cx,y,side,accent);
                Sign($"{level+1}{(side>0?'0':'1')}{room+1}   {type}\n{(type=="REFUGE"?"避难区":type=="LIFT LOBBY"?"电梯厅":type=="READING ROOM"?"阅览室":type=="SCIENCE LAB"?"科学实验室":"教学空间")}",roomRoot,new Vector3(cx-2,y+2.6f,side*1.81f),side>0?0:180,new Vector2(3.1f,.55f),teal,.17f);
                if(type=="LIFT LOBBY") BuildElevator(roomRoot,new Vector3(cx,y,-7.5f));
                else if(type=="REFUGE") BuildRefuge(roomRoot,zones,new Vector3(cx,y,-6));
                else FurnishRoom(roomRoot,cx,y,side,type,level);
            }
            for(int wall=-20;wall<=20;wall+=10)
                Cube("Room dividing wall",structure,new Vector3(wall,y+1.8f,side*6),new Vector3(.2f,3.6f,8),cream);
            Cube("End wall",structure,new Vector3(side*20,y+1.8f,6),new Vector3(.2f,3.6f,8),cream);
            Cube("End wall",structure,new Vector3(side*20,y+1.8f,-6),new Vector3(.2f,3.6f,8),cream);
        }
        for(int x=-18;x<=18;x+=6) {
            Cube("Ceiling light housing",structure,new Vector3(x,y+3.54f,0),new Vector3(1.8f,.06f,.48f),dark,false,false);
            Cube("Ceiling light diffuser",structure,new Vector3(x,y+3.50f,0),new Vector3(1.64f,.03f,.34f),white,false,false);
            if(x%12==6 || x==-18) FillLight(structure,new Vector3(x,y+3.05f,0),7,1.5f);
        }
        Sign($"{level+1}F   ← STAIR A    楼梯    STAIR B →",furnishing,new Vector3(0,y+2.8f,1.75f),0,new Vector2(6,.45f),green,.22f);
        Sign($"{level+1}F   ← STAIR B    楼梯    STAIR A →",furnishing,new Vector3(0,y+2.8f,-1.75f),180,new Vector2(6,.45f),green,.22f);
        for(int x=-16;x<=16;x+=8) {
            FloorArrow(furnishing,new Vector3(x,y+.025f,0),x<0?-90:90,green);
            Planter(furnishing,new Vector3(x+2,y,1.25f),.46f);
        }
        MapBoard(furnishing,new Vector3(0,y+1.75f,1.8f),level+1);
        SafetyStation(furnishing,new Vector3(-9,y,-1.3f),level==3);
        if(level>=2) SmokeZone(zones,new Vector3(-16,y+1.6f,0),new Vector3(6,3.1f,3.5f));
        // Wide, permanently open ground-floor lobby entrance. Upper floors remain enclosed.
        if(level==0) {
            Cube("Entry left infill",structure,new Vector3(-3,1.8f,-10),new Vector3(2,3.6f,.25f),cream);
            Cube("Entry right infill",structure,new Vector3(3,1.8f,-10),new Vector3(2,3.6f,.25f),cream);
            Cube("Entry lintel",structure,new Vector3(0,3.15f,-10),new Vector3(4,.9f,.25f),teal);
            // Lobby passage replaces the partitions between the two middle south rooms.
            Cube("Reception desk",furnishing,new Vector3(2.7f,.55f,-5),new Vector3(2.2f,1.1f,.7f),wood);
            Sign("WELCOME  /  欢迎\nEXIT → ASSEMBLY AREA",furnishing,new Vector3(0,2.75f,-9.7f),180,new Vector2(3.5f,.7f),green,.2f);
            BuildDoubleDoor(furnishing,new Vector3(0,0,-10));
        }
    }

    private static void Partition(Transform parent,float cx,float y,int side,Material accent)
    {
        float z=side*2;
        // 1.6 m doorway offset to the right of each room, leaving a clear centre aisle.
        float doorX=cx+2.8f;
        Cube("Corridor wall left",parent,new Vector3(cx-1.5f,y+1.8f,z),new Vector3(7,3.6f,.18f),cream);
        Cube("Corridor wall right",parent,new Vector3(cx+4.4f,y+1.8f,z),new Vector3(1.2f,3.6f,.18f),cream);
        Cube("Door lintel",parent,new Vector3(doorX,y+3.1f,z),new Vector3(1.6f,1,.18f),accent);
        Cube("Wall skirting",parent,new Vector3(cx-1.5f,y+.13f,z-side*.12f),new Vector3(7,.24f,.04f),accent,false,false);
        // Ground entrance passage and lift lobby doors are open; teaching rooms are interactive.
        if(y==0 && side<0 && (cx==-5 || cx==5)) return;
        BuildDoor(parent,new Vector3(doorX,y,z),side>0?0:180,1.55f);
    }

    private static void BuildDoor(Transform parent,Vector3 p,float yaw,float width,float height=2.55f,bool vision=true)
    {
        var root=Group("Interactive classroom door",parent); root.position=p; root.rotation=Quaternion.Euler(0,yaw,0);
        var pivot=Group("Hinge",root); pivot.localPosition=new Vector3(-width/2,0,0);
        var leaf=Cube("Door leaf",pivot,Vector3.zero,new Vector3(width,height,.08f),wood);
        leaf.transform.localPosition=new Vector3(width/2,height/2,0);
        if(vision) { var lite=Cube("Vision panel",pivot,Vector3.zero,new Vector3(width*.62f,.75f,.09f),glass,false,false); lite.transform.localPosition=new Vector3(width/2,1.8f,0); }
        var handle=Cube("Push bar",pivot,Vector3.zero,new Vector3(width*.7f,.06f,.14f),brass,false,false);
        handle.transform.localPosition=new Vector3(width/2,1.1f,-.09f);
        var door=root.gameObject.AddComponent<DoorInteraction>(); Set(door,"singleDoorPanel",pivot); Set(door,"doorReference",root);
        var trigger=new GameObject("Door proximity"); trigger.transform.SetParent(root,false);
        var col=trigger.AddComponent<BoxCollider>(); col.size=new Vector3(width+ .4f,2.6f,2); col.center=Vector3.up*1.3f; col.isTrigger=true;
        Set(trigger.AddComponent<DoorProximityTrigger>(),"doorInteraction",door);
        var hint=Sign("按下并松开右扳机开门\nTRIGGER → OPEN DOOR",root,p+Vector3.up*2.15f+root.forward*-.13f,yaw,new Vector2(1.4f,.45f),teal,.12f);
        Set(door,"interactionHint",hint.gameObject); hint.gameObject.SetActive(false);
        foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic=false;
    }

    private static void BuildDoubleDoor(Transform parent,Vector3 p)
    {
        var root=Group("Interactive double exit door",parent); root.position=p;
        var left=Group("Left hinge",root); left.localPosition=new Vector3(-1.9f,0,0);
        var right=Group("Right hinge",root); right.localPosition=new Vector3(1.9f,0,0);
        foreach(var pivot in new[]{left,right}) {
            float sign=pivot==left?1:-1;
            var leaf=Cube("Exit leaf",pivot,p,new Vector3(1.85f,2.65f,.08f),wood);
            leaf.transform.localPosition=new Vector3(sign*.925f,1.325f,0);
            var bar=Cube("Panic bar",pivot,p,new Vector3(1.45f,.08f,.12f),brass,false,false);
            bar.transform.localPosition=new Vector3(sign*.925f,1.12f,.08f);
        }
        var door=root.gameObject.AddComponent<DoorInteraction>(); SetInt(door,"doorType",1);
        Set(door,"leftDoorPanel",left); Set(door,"rightDoorPanel",right); Set(door,"doorReference",root);
        var trigger=new GameObject("Exit door proximity"); trigger.transform.SetParent(root,false);
        var col=trigger.AddComponent<BoxCollider>(); col.size=new Vector3(4,2.6f,2.8f); col.center=Vector3.up*1.3f; col.isTrigger=true;
        Set(trigger.AddComponent<DoorProximityTrigger>(),"doorInteraction",door);
        foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic=false;
    }

    private static void FurnishRoom(Transform root,float cx,float y,int side,string type,int level)
    {
        bool library=type=="READING ROOM";
        if(library) {
            for(int shelf=0;shelf<3;shelf++) Bookshelf(root,new Vector3(cx-3+shelf*2.2f,y,side*8.7f));
            for(int row=0;row<2;row++) {
                Prop("table1",root,new Vector3(cx-1.5f+row*3,y,side*5.5f),.78f,0);
                for(int s=-1;s<=1;s+=2) Prop("chair",root,new Vector3(cx-1.5f+row*3,y,side*5.5f+s*.85f),.92f,s>0?180:0);
            }
            Sign("READ · REFLECT · EXPLORE",root,new Vector3(cx,y+2.9f,side*8.95f),side>0?0:180,new Vector2(6,.5f),teal,.25f);
        } else {
            float facing=side>0?180:0;
            for(int row=0;row<3;row++) for(int col=0;col<4;col++) {
                float x=cx-3.35f+col*1.65f;
                float z=side*(4.3f+row*1.55f);
                Prop("chair with table",root,new Vector3(x,y,z),.94f,facing);
                if(type=="SCIENCE LAB") {
                    var monitor=Cube("Lab monitor",root,new Vector3(x,y+1.12f,z),new Vector3(.42f,.3f,.055f),dark,false,false);
                    Cube("Monitor stand",root,new Vector3(x,y+.94f,z),new Vector3(.12f,.12f,.12f),dark,false,false);
                }
            }
            Cube("Teaching board frame",root,new Vector3(cx-1,y+2,side*2.18f),new Vector3(4.8f,1.55f,.1f),wood,false,false);
            Cube("Teaching board",root,new Vector3(cx-1,y+2,side*2.26f),new Vector3(4.6f,1.37f,.06f),teal,false,false);
            Sign(type=="SCIENCE LAB"?"SCIENCE & DISCOVERY\n观察 · 实验 · 创新":"LEARN · CREATE · COLLABORATE\n学习 · 创造 · 协作",root,new Vector3(cx-1,y+2.05f,side*2.31f),side>0?180:0,new Vector2(4.4f,1.1f),teal,.25f,Color.white);
            PlainTable(root,new Vector3(cx-2.8f,y,side*3.1f),new Vector2(1.8f,.85f));
            Cube("Projector",root,new Vector3(cx,y+3.15f,side*5),new Vector3(.42f,.16f,.32f),white,false,false);
            Cube("Projector mount",root,new Vector3(cx,y+3.4f,side*5),new Vector3(.05f,.35f,.05f),dark,false,false);
        }
        Prop("locker1",root,new Vector3(cx-4.25f,y,side*8.2f),1.8f,90);
        Planter(root,new Vector3(cx+4,y,side*8.6f),.5f);
        FillLight(root,new Vector3(cx,y+3.2f,side*5.5f),8,1.2f);
        Sign(level%2==0?"IDEAS START HERE\n好奇 · 探索 · 实践":"TOGETHER WE LEARN\n分享 · 合作 · 成长",root,new Vector3(cx+4.72f,y+2,side*5.5f),-90,new Vector2(2,1.2f),Mat("Floor "+(level+1)+" Accent",accents[level]),.2f);
        // A different coloured acoustic strip gives each floor its own visual identity.
        for(int i=0;i<8;i++) Cube("Acoustic timber slat",root,new Vector3(cx-4.72f,y+2.2f,side*(3.2f+i*.55f)),new Vector3(.05f,1.2f,.12f),wood,false,false);
    }

    private static void BuildElevator(Transform root,Vector3 position)
    {
        var lift=Prefab(Resolve("Assets/prefab/elevator(button))+wu.prefab"),root);
        lift.transform.localRotation=Quaternion.identity;
        Bounds b=BoundsOf(lift);
        lift.transform.localScale*=3.25f/b.size.y;
        b=BoundsOf(lift);
        lift.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);
        b=BoundsOf(lift);
        var safety=lift.GetComponent<FireSafetyElevator>();
        var up=new SerializedObject(safety).FindProperty("upButton").objectReferenceValue as Collider;
        if(up!=null) {
            Vector3 front=up.bounds.center-b.center; front.y=0;
            if(front.sqrMagnitude>.01f) lift.transform.RotateAround(b.center,Vector3.up,Vector3.SignedAngle(front,Vector3.forward,Vector3.up));
        }
        // Decorative vendor door behaviour must not compete with the safety-only lift buttons.
        foreach(var script in lift.GetComponentsInChildren<MonoBehaviour>(true))
            if(script!=null && script.GetType().Namespace=="SojaExiles") UnityEngine.Object.DestroyImmediate(script);
        ConvertPropMaterials(lift);
        lift.name="elevator(button))+wu — Safety Interaction";
        Sign("LIFT  /  电梯\nFIRE: USE STAIRS",root,position+new Vector3(0,3.3f,1.6f),180,new Vector2(3,.6f),red,.2f);
    }

    private static void BuildRefuge(Transform room,Transform zones,Vector3 p)
    {
        Cube("Refuge floor marking",room,p+Vector3.up*.015f,new Vector3(7,.02f,5),Mat("Refuge floor",new Color(.56f,.73f,.7f)),false,false);
        for(int i=0;i<3;i++) Bench(room,p+new Vector3(-2+i*2,0,-2));
        var zone=Group("2F Refuge Occupancy",zones); zone.position=p+Vector3.up*1.6f;
        var volume=zone.gameObject.AddComponent<BoxCollider>(); volume.size=new Vector3(9,3.2f,7.5f); volume.isTrigger=true;
        var panel=Cube("Refuge intercom — trigger to call",room,p+new Vector3(3.8f,1.4f,2.5f),new Vector3(.6f,.8f,.18f),teal);
        var call=panel.AddComponent<CampusRefugeCall>(); Set(call,"trackedHead",head); Set(call,"refugeVolume",volume); Set(call,"resolution",resolution);
        Cube("Intercom button",room,p+new Vector3(3.8f,1.3f,2.62f),new Vector3(.2f,.2f,.08f),green,false,false);
        Sign("2F REFUGE  /  二层避难区\n到达后使用对讲机确认，等待协助\nUSE INTERCOM TO CONFIRM AND WAIT",room,p+new Vector3(0,2.6f,3.74f),0,new Vector2(5.5f,1),green,.22f);
        Sign("CALL  /  呼叫",room,p+new Vector3(3.8f,2.05f,2.4f),0,new Vector2(1.2f,.35f),green,.17f);
        FillLight(room,p+Vector3.up*3.1f,8,1.4f);
    }

    private static void BuildStairTower(float x,string id,Transform architecture,Transform zones)
    {
        var tower=Group("Stair "+id+" — continuous physical stairs",architecture);
        for(int level=0;level<4;level++) {
            float y=level*Storey;
            Cube("Full landing "+(level+1)+"F",tower,new Vector3(x,y-.1f,0),new Vector3(6,.2f,2.2f),floor,true);
            if (id != "A" || level < 2)
                Sign($"{level+1}F\nSTAIR {id} / 楼梯",tower,new Vector3(x+2.75f,y+1.8f,0),90,new Vector2(1.8f,1.2f),teal,.3f);
            if(level==3) continue;
            Cube("Half landing",tower,new Vector3(x,y+1.8f,4.7f),new Vector3(6,.2f,1.8f),floor,true);
            for(int step=0;step<10;step++) {
                float h=(step+1)*.19f;
                Cube($"Up flight {level+1}.{step+1}",tower,new Vector3(x-1.45f,y+h-.095f,1.24f+step*.28f),new Vector3(2.2f,.19f,.28f),floor,true);
                Cube($"Return flight {level+1}.{step+1}",tower,new Vector3(x+1.45f,y+1.9f+h-.095f,3.76f-step*.28f),new Vector3(2.2f,.19f,.28f),floor,true);
                Cube("Tread safety stripe",tower,new Vector3(x-1.45f,y+h+.003f,1.115f+step*.28f),new Vector3(2.05f,.006f,.04f),brass,false,false);
                Cube("Tread safety stripe",tower,new Vector3(x+1.45f,y+1.9f+h+.003f,3.885f-step*.28f),new Vector3(2.05f,.006f,.04f),brass,false,false);
            }
            Rail(tower,new Vector3(x-.25f,y+1,1.12f),new Vector3(x-.25f,y+2.9f,3.92f));
            Rail(tower,new Vector3(x+.25f,y+2.9f,3.92f),new Vector3(x+.25f,y+4.8f,1.12f));
            Rail(tower,new Vector3(x-2.65f,y+1,1.12f),new Vector3(x-2.65f,y+2.9f,3.92f));
            Rail(tower,new Vector3(x+2.65f,y+2.9f,3.92f),new Vector3(x+2.65f,y+4.8f,1.12f));
        }
        Cube("Tower outer wall",tower,new Vector3(x+Mathf.Sign(x)*3,7.5f,2),new Vector3(.22f,15.2f,7.5f),teal);
        Cube("Tower back wall",tower,new Vector3(x,7.5f,5.7f),new Vector3(6,15.2f,.22f),cream);
        foreach(int side in new[]{-1,1}) Cube("Tower front pier",tower,new Vector3(x+side*2.2f,7.5f,-1.2f),new Vector3(1.6f,15.2f,.18f),cream);
        for(int level=0;level<4;level++) {
            float y=level*Storey;
            Cube("Tower spandrel",tower,new Vector3(x,y+.3f,-1.2f),new Vector3(2.8f,.6f,.18f),teal);
            Cube("Tower window",tower,new Vector3(x,y+1.9f,-1.2f),new Vector3(2.8f,2.6f,.08f),glass);
            Cube("Tower lintel",tower,new Vector3(x,y+3.5f,-1.2f),new Vector3(2.8f,.6f,.18f),teal);
            Cube("Tower mullion",tower,new Vector3(x,y+1.9f,-1.24f),new Vector3(.07f,2.6f,.08f),dark);
            FillLight(tower,new Vector3(x,y+3.2f,2),7,1.4f);
        }
        Cube("Tower roof",tower,new Vector3(x,15.1f,2.25f),new Vector3(6.4f,.25f,7.3f),teal);
        // Join the building at z=0; no slab bridges across the open stair flights.
        if(id=="A") {
            Cube("3F A blocked training gate",tower,new Vector3(x+1.45f,8.8f,1.06f),new Vector3(2.2f,2.4f,.16f),red);
            Sign("ROUTE BLOCKED / 路线受阻\nUSE STAIR B / 请改走楼梯 B",tower,new Vector3(x+1.45f,9,.96f),0,new Vector2(2.15f,1.1f),red,.18f,Color.white);
            var cue=Group("Blocked stair A prompt",zones); cue.position=new Vector3(x,9.1f,-.1f);
            var col=cue.gameObject.AddComponent<BoxCollider>(); col.size=new Vector3(5.5f,2.8f,1.4f); col.isTrigger=true;
            cue.gameObject.AddComponent<CampusRouteCue>();
            var fire=Prefab(Resolve("Assets/prefab/fire.prefab"),tower);
            fire.name="Contained fire cue (blocked stair)"; fire.transform.position=new Vector3(x+1.45f,6.9f,2.9f); fire.transform.localScale=Vector3.one*.4f;
            foreach(var ps in fire.GetComponentsInChildren<ParticleSystem>(true)) { var main=ps.main; main.maxParticles=Mathf.Min(main.maxParticles,48); }
        }
        // Ground-floor exit through the tower's connection into the central corridor.
        Sign("EXIT → LOBBY / 出口通往大堂",tower,new Vector3(x,2.65f,-1.09f),180,new Vector2(5,.5f),green,.22f);
    }

    private static void BuildGrounds(Transform outside,Transform zones)
    {
        Cube("Campus lawn",outside,new Vector3(0,-.22f,-4),new Vector3(108,.25f,92),grass);
        Cube("Front evacuation plaza",outside,new Vector3(0,-.09f,-24),new Vector3(58,.18f,28),paving,true);
        Cube("Rear maintenance path",outside,new Vector3(0,-.09f,14),new Vector3(55,.18f,7),paving,true);
        for(int side=-1;side<=1;side+=2) {
            Cube("Side walkway",outside,new Vector3(side*29,-.09f,0),new Vector3(4,.18f,25),paving,true);
            for(int i=0;i<4;i++) { Tree(outside,new Vector3(side<0?-34-i%2*4:52,0,-12-i*7)); Bench(outside,new Vector3(side*25,0,-18-i*4)); }
            for(int i=0;i<3;i++) Planter(outside,new Vector3(side*9,0,-16-i*5),1.1f);
        }
        for(int i=0;i<6;i++) Tree(outside,new Vector3(-30+i*12,0,21));
        // A marked outdoor endpoint, not a different scene or transition portal.
        Cube("Assembly court",outside,new Vector3(0,.013f,-30),new Vector3(12,.026f,8),Mat("Assembly mint",new Color(.35f,.65f,.58f)),true);
        for(int side=-1;side<=1;side+=2) {
            Cube("Assembly border",outside,new Vector3(side*6,.04f,-30),new Vector3(.1f,.02f,8),white,false,false);
            Cube("Assembly border",outside,new Vector3(0,.04f,-30+side*4),new Vector3(12,.02f,.1f),white,false,false);
        }
        Cube("Assembly sign post",outside,new Vector3(0,1.5f,-34),new Vector3(.12f,3,.12f),dark);
        Sign("ASSEMBLY POINT\n室外集合点\nSTAY HERE / 在此等待",outside,new Vector3(0,2.4f,-33.9f),0,new Vector2(4,1.3f),green,.3f,Color.white);
        var assemblySign=Sign("ASSEMBLY POINT\n室外集合点\nSTAY HERE / 在此等待",outside,new Vector3(0,2.4f,-34.1f),180,new Vector2(4,1.3f),green,.3f,Color.white);
        SetString(assemblySign.gameObject.AddComponent<NotableCue>(),"cueId","OUTDOOR_ASSEMBLY_SIGN");
        var zone=Group("Outdoor assembly endpoint",zones); zone.position=new Vector3(0,1.4f,-30);
        var col=zone.gameObject.AddComponent<BoxCollider>(); col.size=new Vector3(11,2.8f,7); col.isTrigger=true;
        var geo=zone.gameObject.AddComponent<GeoFence>(); Set(geo,"requiredCollider",body); SetFloat(geo,"dwellSeconds",2);
        var endpoint=zone.gameObject.AddComponent<SafeZoneEndpoint>(); Set(endpoint,"zone",geo);
        var outcome=zone.gameObject.AddComponent<AssemblyOutcomeController>(); Set(outcome,"endpoint",endpoint); Set(outcome,"completionZone",geo); Set(outcome,"resolution",resolution);
        Set(outcome,"health",systems.GetComponentInChildren<HealthTimeController>(true)); Set(outcome,"healthBar",systems.GetComponentInChildren<HealthTimeBar>(true)); Set(outcome,"audioManager",systems.GetComponentInChildren<Jiao.HealthTime.AudioManager>(true));
        for(int z=-17;z>=-25;z-=4) FloorArrow(outside,new Vector3(0,.03f,z),180,green);
        // Landscape and a small sports court enrich the campus without blocking evacuation.
        Cube("Sports court",outside,new Vector3(40,-.02f,-10),new Vector3(16,.08f,28),Mat("Court terracotta",new Color(.57f,.33f,.25f)));
        for(int side=-1;side<=1;side+=2) {
            Cube("Court sideline",outside,new Vector3(40+side*7,.03f,-10),new Vector3(.08f,.01f,26),white,false,false);
            Cube("Court baseline",outside,new Vector3(40,.03f,-10+side*13),new Vector3(14,.01f,.08f),white,false,false);
        }
        Cube("Court centre line",outside,new Vector3(40,.03f,-10),new Vector3(14,.01f,.08f),white,false,false);
        foreach(int end in new[]{-1,1}) {
            Cube("Basketball pole",outside,new Vector3(40,1.65f,-10+end*12),new Vector3(.12f,3.3f,.12f),dark);
            Cube("Basketball backboard",outside,new Vector3(40,3.1f,-10+end*11.8f),new Vector3(1.7f,1,.12f),white);
            Cube("Basketball rim",outside,new Vector3(40,2.75f,-10+end*11.4f),new Vector3(.5f,.035f,.5f),brass,false,false);
        }
        // Paving joints, landscape edging and bike racks add human-scale detail.
        for(int x=-28;x<=28;x+=4) Cube("Paving joint",outside,new Vector3(x,.006f,-24),new Vector3(.018f,.01f,28),Mat("Paving joint",new Color(.51f,.52f,.48f)),false,false);
        for(int z=-38;z<=-10;z+=4) Cube("Paving joint",outside,new Vector3(0,.006f,z),new Vector3(58,.01f,.018f),Mat("Paving joint",new Color(.51f,.52f,.48f)),false,false);
        for(int i=0;i<5;i++) {
            Vector3 p=new Vector3(-20+i*1.2f,0,-12);
            Cube("Bike rack crossbar",outside,p+Vector3.up*.8f,new Vector3(.8f,.055f,.055f),dark);
            foreach(int side in new[]{-1,1}) Cube("Bike rack leg",outside,p+new Vector3(side*.4f,.4f,0),new Vector3(.055f,.8f,.055f),dark);
        }
    }

    private static void WireExperience()
    {
        var coordinator=new GameObject("Campus Experience"); coordinator.transform.SetParent(systems,false);
        var experience=coordinator.AddComponent<CampusExperience>(); Set(experience,"trackedHead",head);
        var ui=systems.GetComponentInChildren<SystemUIView>(true);
        var so=new SerializedObject(ui);
        var gameplay=so.FindProperty("gameplayUIRoot").objectReferenceValue as GameObject;
        var victory=so.FindProperty("victoryPanel").objectReferenceValue as GameObject;
        if(gameplay!=null) {
            var label=UItext("Campus Floor HUD",gameplay.transform,new Vector2(640,65),21);
            label.text="4F  |  HARBOURVIEW CAMPUS\n教学楼 · 请选择绿色疏散路线";
            // The inherited health canvas uses metres, unlike the pixel-sized outcome canvases.
            label.rectTransform.localScale=Vector3.one*.002f;
            label.rectTransform.anchoredPosition=new Vector2(0,.24f); Set(experience,"floorLabel",label);
        }
        if(victory!=null) {
            var panel=victory.GetComponent<RectTransform>(); panel.sizeDelta=new Vector2(840,660);
            foreach(var image in victory.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if(image.name=="Background") {
                    image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);
                    image.rectTransform.anchoredPosition=Vector2.zero; image.rectTransform.sizeDelta=new Vector2(840,660);
                }
            var title=victory.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Title");
            title.fontSize=52; title.rectTransform.anchoredPosition=new Vector2(0,235); title.rectTransform.sizeDelta=new Vector2(760,125);
            var buttons=victory.GetComponentsInChildren<VRMenuButton>(true);
            for(int b=0;b<buttons.Length;b++) {
                var rect=buttons[b].GetComponent<RectTransform>(); rect.anchoredPosition=new Vector2(0,-100-b*105);
            }
            var label=UItext("Learning Review",victory.transform,new Vector2(760,200),28);
            label.rectTransform.anchoredPosition=new Vector2(0,60); Set(experience,"debriefLabel",label);
        }
        else throw new InvalidOperationException("Complete victory panel is required.");
        foreach(var anchor in systems.GetComponentsInChildren<WorldSpaceUIAnchor>(true)) {
            var setting=new SerializedObject(anchor);
            if(!setting.FindProperty("followHead").boolValue) {
                setting.FindProperty("headLocalOffset").vector3Value=new Vector3(0,-.08f,1.2f); setting.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        // Reuse an existing team's warning clip as a quiet delayed alarm cue.
        var alarm=coordinator.AddComponent<AudioSource>(); alarm.playOnAwake=false; alarm.loop=true; alarm.volume=.14f;
        alarm.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Resolve("Assets/Scripts/HealthTimeBar_Test/low_health_warning.wav")); Set(experience,"alarm",alarm);
        if(systems.GetComponentInChildren<JourneyDirector>(true)==null) coordinator.AddComponent<JourneyDirector>();
    }

    private static void SafetyStation(Transform parent,Vector3 p,bool physicalTowel)
    {
        Cube("Safety cabinet",parent,p+Vector3.up*.55f,new Vector3(1.1f,1.1f,.45f),white);
        Prop("fire",parent,p+new Vector3(.32f,.25f,.27f),.62f,0);
        var towel=Cube("Protective towel pickup",parent,p+new Vector3(-.27f,1.15f,0),new Vector3(.35f,.07f,.22f),Mat("Towel cyan",new Color(.29f,.69f,.75f)));
        var pickup=towel.AddComponent<TowelPickup>();
        Set(pickup,"pickupSound",AssetDatabase.LoadAssetAtPath<AudioClip>(Resolve("Assets/Scripts/HealthTimeBar_Test/towel_pickup.wav")));
        Sign("SAFETY STATION / 安全教学站\nTOWEL: TRIGGER TO COLLECT",parent,p+new Vector3(0,2.2f,-.03f),180,new Vector2(2.8f,.7f),teal,.16f);
        if(physicalTowel) {
            var carry=Cube("Carry and throw towel",parent,p+new Vector3(.27f,1.17f,0),new Vector3(.25f,.07f,.18f),white);
            carry.isStatic=false;
            carry.AddComponent<Rigidbody>(); carry.AddComponent<PickableTowel>();
        }
    }

    private static void SmokeZone(Transform parent,Vector3 p,Vector3 size)
    {
        var zone=Group("Smoke training zone",parent); zone.position=p;
        var collider=zone.gameObject.AddComponent<BoxCollider>(); collider.size=size; collider.isTrigger=true;
        zone.gameObject.AddComponent<Jiao.HealthTime.SmokeZone>();
        var smoke=new GameObject("Lightweight smoke particles"); smoke.transform.SetParent(zone,false);
        var ps=smoke.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.startLifetime=6; main.startSpeed=.12f; main.startSize=1.2f; main.startColor=new Color(.3f,.34f,.36f,.18f); main.maxParticles=36;
        main.simulationSpace=ParticleSystemSimulationSpace.World; var emission=ps.emission; emission.rateOverTime=5;
        var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=size*.75f;
        var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=Mat("Smoke",new Color(.4f,.43f,.45f,.14f),true);
    }

    private static void MapBoard(Transform parent,Vector3 p,int level)
    {
        var board=Group("Evacuation map "+level+"F",parent); board.position=p;
        Cube("Map backboard",board,p,new Vector3(2.4f,1.45f,.06f),wood,false,false);
        Sign($"{level}F  EVACUATION MAP / 疏散图\n← A  ═══ CORRIDOR ═══  B →\n二层：避难区 · 一层：室外集合点\n2F REFUGE · 1F OUTDOOR ASSEMBLY\n楼梯 A 受阻时，请选择 B / USE B IF A IS BLOCKED",board,p+new Vector3(0,0,-.04f),0,new Vector2(2.28f,1.32f),teal,.125f,Color.white);
        var cue=board.gameObject.AddComponent<NotableCue>(); SetString(cue,"cueId",$"F{level}_EVACUATION_MAP");
    }

    private static void Bookshelf(Transform root,Vector3 p)
    {
        Cube("Bookcase back",root,p+Vector3.up*1.1f,new Vector3(1.9f,2.2f,.3f),wood);
        for(int shelf=0;shelf<4;shelf++) {
            Cube("Shelf",root,p+new Vector3(0,.35f+shelf*.48f,-.2f),new Vector3(1.9f,.04f,.4f),dark,false,false);
            for(int book=0;book<9;book++) Cube("Books",root,p+new Vector3(-.8f+book*.2f,.53f+shelf*.48f,-.27f),new Vector3(.12f,.3f,.2f),book%2==0?teal:cream,false,false);
        }
    }
    private static void Bench(Transform parent,Vector3 p)
    {
        Cube("Timber bench",parent,p+Vector3.up*.48f,new Vector3(1.7f,.12f,.5f),wood);
        foreach(int side in new[]{-1,1}) Cube("Bench leg",parent,p+new Vector3(side*.6f,.22f,0),new Vector3(.08f,.45f,.42f),dark);
        Cube("Bench back",parent,p+new Vector3(0,.85f,-.24f),new Vector3(1.7f,.45f,.06f),wood);
    }
    private static void Planter(Transform parent,Vector3 p,float size)
    {
        Cube("Planter",parent,p+Vector3.up*size*.45f,new Vector3(size,size*.9f,size),cream);
        for(int i=0;i<3;i++) {
            var foliage=Primitive("Indoor foliage",PrimitiveType.Sphere,parent,p+new Vector3((i-1)*size*.2f,size*(1.15f+i*.16f),0),new Vector3(size*.7f,size*.8f,size*.7f),green,false);
        }
    }
    private static void Tree(Transform parent,Vector3 p)
    {
        Primitive("Tree trunk",PrimitiveType.Cylinder,parent,p+Vector3.up*1.7f,new Vector3(.4f,1.7f,.4f),wood,true);
        Primitive("Tree canopy",PrimitiveType.Sphere,parent,p+Vector3.up*4,new Vector3(4,4,4),green,false);
        Primitive("Tree canopy secondary",PrimitiveType.Sphere,parent,p+new Vector3(1.2f,4.8f,.5f),new Vector3(3,3,3),Mat("Leaf sage",new Color(.3f,.48f,.31f)),false);
    }
    private static void Rail(Transform parent,Vector3 start,Vector3 end)
    {
        var bar=Cube("Stair handrail",parent,(start+end)*.5f,new Vector3(.065f,.065f,Vector3.Distance(start,end)),dark);
        bar.transform.rotation=Quaternion.LookRotation(end-start);
        foreach(float t in new[]{0,.5f,1}) {
            Vector3 p=Vector3.Lerp(start,end,t);
            Cube("Handrail post",parent,p-Vector3.up*.45f,new Vector3(.05f,.9f,.05f),dark);
        }
    }
    private static void FloorArrow(Transform parent,Vector3 p,float yaw,Material material)
    {
        var group=Group("Evacuation direction arrow",parent); group.position=p; group.rotation=Quaternion.Euler(0,yaw,0);
        var stem=Cube("Arrow stem",group,p,new Vector3(.12f,.01f,.65f),material,false,false); stem.transform.localPosition=Vector3.zero; stem.transform.localRotation=Quaternion.identity;
        foreach(int side in new[]{-1,1}) {
            var wing=Cube("Arrow head",group,p,new Vector3(.12f,.01f,.46f),material,false,false);
            wing.transform.localPosition=new Vector3(side*.14f,0,.23f); wing.transform.localRotation=Quaternion.Euler(0,-side*45,0);
        }
    }
    private static void Prop(string name,Transform parent,Vector3 p,float height,float yaw)
    {
        string path=Resolve("Assets/school basic prefab/Prefabs/props/"+name+".prefab");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) { Debug.LogWarning("Optional school prop missing: "+path); return; }
        var prop=Prefab(path,parent); prop.transform.localRotation=Quaternion.Euler(0,yaw,0);
        var b=BoundsOf(prop); prop.transform.localScale*=height/Mathf.Max(.01f,b.size.y); b=BoundsOf(prop);
        prop.transform.position+=p-new Vector3(b.center.x,b.min.y,b.center.z);
        foreach(var script in prop.GetComponentsInChildren<MonoBehaviour>(true)) if(script!=null) UnityEngine.Object.DestroyImmediate(script);
        // Use one bounds collider instead of detailed imported mesh colliders.
        foreach(var c in prop.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        var collider=prop.AddComponent<BoxCollider>(); b=BoundsOf(prop);
        collider.center=prop.transform.InverseTransformPoint(b.center); collider.size=new Vector3(b.size.x/prop.transform.lossyScale.x,b.size.y/prop.transform.lossyScale.y,b.size.z/prop.transform.lossyScale.z);
        ConvertPropMaterials(prop);
    }
    private static void ConvertPropMaterials(GameObject prop)
    {
        foreach(var renderer in prop.GetComponentsInChildren<Renderer>(true)) {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++) {
                var original=mats[i]; if(original==null) { mats[i]=cream; continue; }
                if(!propMaterials.TryGetValue(original,out var converted)) {
                    string path="Assets/Art/Campus/Materials/Imported_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original))+"_"+original.name.Replace('/','_')+".mat";
                    converted=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(converted==null) {
                        converted=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        converted.SetColor("_BaseColor",original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):(original.HasProperty("_Color")?original.color:Color.white));
                        Texture texture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):(original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null);
                        converted.SetTexture("_BaseMap",texture); converted.SetFloat("_Smoothness",.25f); AssetDatabase.CreateAsset(converted,path);
                    }
                    propMaterials[original]=converted;
                }
                mats[i]=converted;
            }
            var meshFilter=renderer.GetComponent<MeshFilter>();
            if(meshFilter!=null && meshFilter.sharedMesh!=null && mats.Length>meshFilter.sharedMesh.subMeshCount) mats=mats.Take(meshFilter.sharedMesh.subMeshCount).ToArray();
            renderer.sharedMaterials=mats;
        }
    }

    private static Transform Sign(string text,Transform parent,Vector3 p,float yaw,Vector2 size,Material background,float textSize,Color? color=null)
    {
        var root=Group("Sign "+text.Split('\n')[0],parent); root.position=p; root.rotation=Quaternion.Euler(0,yaw,0);
        var panel=Cube("Sign backing",root,p,new Vector3(size.x+.06f,size.y+.04f,.035f),background,false,false);
        panel.transform.localPosition=Vector3.zero; panel.transform.localRotation=Quaternion.identity;
        var label=new GameObject("Bilingual label"); label.transform.SetParent(root,false); label.transform.localPosition=new Vector3(0,0,-.025f);
        var tmp=label.AddComponent<TextMeshPro>(); tmp.font=font; tmp.text=text; tmp.color=color??Color.white;
        tmp.fontSize=textSize*10; tmp.alignment=TextAlignmentOptions.Center; tmp.enableWordWrapping=true;
        tmp.rectTransform.sizeDelta=size; tmp.enableAutoSizing=false;
        return root;
    }
    private static TextMeshProUGUI UItext(string name,Transform parent,Vector2 size,float fontSize)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
        var tmp=go.AddComponent<TextMeshProUGUI>(); tmp.font=font; tmp.fontSize=fontSize; tmp.color=Color.white;
        tmp.rectTransform.sizeDelta=size; tmp.alignment=TextAlignmentOptions.Center; tmp.raycastTarget=false; return tmp;
    }
    private static void InitMaterials()
    {
        cream=Mat("Warm limestone",new Color(.86f,.84f,.77f)); teal=Mat("Deep teal",new Color(.075f,.24f,.27f));
        wood=Mat("Oak",new Color(.51f,.32f,.18f)); floor=Mat("Terrazzo",new Color(.66f,.70f,.69f)); dark=Mat("Charcoal",new Color(.1f,.14f,.16f));
        green=Mat("Safety green",new Color(.11f,.44f,.28f)); red=Mat("Caution red",new Color(.63f,.17f,.12f)); brass=Mat("Brass",new Color(.76f,.61f,.33f));
        glass=Mat("Window glass",new Color(.56f,.78f,.85f,.26f),true); grass=Mat("Campus grass",new Color(.35f,.46f,.31f)); paving=Mat("Plaza paving",new Color(.68f,.67f,.61f));
        white=Mat("Light diffuser",new Color(.94f,.94f,.87f)); white.EnableKeyword("_EMISSION"); white.SetColor("_EmissionColor",new Color(.65f,.68f,.62f));
    }
    private static Material Mat(string name,Color color,bool transparent=false)
    {
        string path="Assets/Art/Campus/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
        mat.color=color; mat.enableInstancing=true; mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",transparent?.65f:.24f);
        if(transparent) { mat.SetFloat("_Surface",1); mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite",0); mat.SetFloat("_Cull",0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue=3000; }
        EditorUtility.SetDirty(mat); return mat;
    }
    private static void SetupLighting()
    {
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Resolve("Assets/New Universal Render Pipeline Asset.asset"));
        if(pipeline!=null) {
            SetInt(pipeline,"m_AdditionalLightsRenderingMode",(int)LightRenderingMode.PerPixel);
            var settings=new SerializedObject(pipeline); settings.FindProperty("m_SoftShadowsSupported").boolValue=true; settings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.msaaSampleCount=4; pipeline.shadowDistance=40;
            EditorUtility.SetDirty(pipeline);
        }
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.51f,.58f,.64f); RenderSettings.ambientEquatorColor=new Color(.38f,.40f,.38f); RenderSettings.ambientGroundColor=new Color(.19f,.21f,.22f); RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.76f,.83f,.84f); RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogStartDistance=70; RenderSettings.fogEndDistance=145;
        var sun=new GameObject("06 Daylight"); sun.transform.rotation=Quaternion.Euler(48,-35,0); var light=sun.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.25f; light.color=new Color(1,.91f,.78f); light.shadows=LightShadows.Soft;
        var data=sun.AddComponent<UniversalAdditionalLightData>(); data.usePipelineSettings=true; RenderSettings.sun=light;
    }
    private static void FillLight(Transform parent,Vector3 position,float range,float intensity)
    {
        var go=new GameObject("Warm interior fill (no shadows)"); go.transform.SetParent(parent,false); go.transform.position=position;
        var light=go.AddComponent<Light>(); light.type=LightType.Point; light.range=range; light.intensity=intensity; light.color=new Color(1,.92f,.79f); light.shadows=LightShadows.None;
        go.AddComponent<UniversalAdditionalLightData>();
    }
    private static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 scale,Material mat,bool walkable=false,bool collider=true)
    {
        var go=Primitive(name,PrimitiveType.Cube,parent,p,scale,mat,collider); if(walkable) go.AddComponent<TeleportArea>(); return go;
    }
    private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 scale,Material mat,bool collider)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,true); go.transform.position=p; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.isStatic=true; return go;
    }
    private static Transform Group(string name,Transform parent=null) { var go=new GameObject(name); if(parent!=null) go.transform.SetParent(parent,false); return go.transform; }
    private static GameObject Prefab(string path,Transform parent) { var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(asset==null) throw new InvalidOperationException("Missing prefab: "+path); return (GameObject)PrefabUtility.InstantiatePrefab(asset,parent); }
    private static Bounds BoundsOf(GameObject go) { var renderers=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponent<TMP_Text>()==null).ToArray(); if(renderers.Length==0) throw new InvalidOperationException("No geometry: "+go.name); var b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds); return b; }
    public static void Folder(string path) { if(AssetDatabase.IsValidFolder(path)) return; string parent=Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
    public static string Resolve(string original) { if(File.Exists(original)) return original; return CampusProjectOrganizer.Remap(original); }
    public static void Set(UnityEngine.Object target,string key,UnityEngine.Object value) { var so=new SerializedObject(target); var property=so.FindProperty(key); if(property==null) throw new InvalidOperationException(target.GetType()+"."+key); property.objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static void SetInt(UnityEngine.Object target,string key,int value) { var so=new SerializedObject(target); so.FindProperty(key).intValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static void SetString(UnityEngine.Object target,string key,string value) { var so=new SerializedObject(target); so.FindProperty(key).stringValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static void SetFloat(UnityEngine.Object target,string key,float value) { var so=new SerializedObject(target); so.FindProperty(key).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(UnityEngine.Object target,string key,UnityEngine.Object[] values) { var so=new SerializedObject(target); var p=so.FindProperty(key); p.arraySize=values.Length; for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void ConfigureMainMenu()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity");
        foreach(var nav in UnityEngine.Object.FindObjectsOfType<SystemUINavigation>(true)) SetString(nav,"gameplayEntrySceneName","CampusTraining");
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Campus/Validate Integrated Scene")]
    public static void Validate()
    {
        if(SceneManager.GetActiveScene().path!=ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        if(missing!=0) throw new InvalidOperationException("Missing scripts: "+missing);
        var windows=UnityEngine.Object.FindObjectsOfType<RuntimeFireWindow>(true); var lifts=UnityEngine.Object.FindObjectsOfType<FireSafetyElevator>(true);
        if(windows.Select(w=>w.transform.position).Distinct().Count()!=78) throw new InvalidOperationException("Window prefab placements were not persisted.");
        if(windows.Length<60 || lifts.Length!=4 || UnityEngine.Object.FindObjectsOfType<CampusRefugeCall>(true).Length!=1 || UnityEngine.Object.FindObjectsOfType<AssemblyOutcomeController>(true).Length!=1) throw new InvalidOperationException("Incomplete interaction coverage.");
        if(UnityEngine.Object.FindObjectsOfType<HealthTimeController>(true).Length!=1 || UnityEngine.Object.FindObjectsOfType<ResolutionCoordinator>(true).Length!=1) throw new InvalidOperationException("Duplicate shared systems.");
        if(all.Any(t=>PrefabUtility.GetPrefabAssetType(t.gameObject)==PrefabAssetType.MissingAsset)) throw new InvalidOperationException("Missing prefab source.");
        foreach(var support in UnityEngine.Object.FindObjectsOfType<FireSafetyPrefabSupport>(true))
            if(support.WarningFont==null || support.RayMaterial==null) throw new InvalidOperationException("Prefab resources missing.");
        if(Camera.main==null || UnityEngine.Object.FindObjectsOfType<CampusLocomotion>(true).Length!=1 || UnityEngine.Object.FindObjectsOfType<CampusHandInteractor>(true).Length!=2 || UnityEngine.Object.FindObjectsOfType<VRTeleporter>(true).Length!=2 || UnityEngine.Object.FindObjectsOfType<VRFireSafetyInteraction>(true).Length!=0) throw new InvalidOperationException("Contact/teleport locomotion integration incomplete.");
        foreach(var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
            if(text.font==null) throw new InvalidOperationException("Missing sign font.");
        var ui=new SerializedObject(UnityEngine.Object.FindObjectOfType<SystemUIView>());
        if(ui.FindProperty("victoryPanel").objectReferenceValue==null || ui.FindProperty("gameOverPanel").objectReferenceValue==null) throw new InvalidOperationException("Outcome panels are unbound.");
        var experience=new SerializedObject(UnityEngine.Object.FindObjectOfType<CampusExperience>());
        if(experience.FindProperty("debriefLabel").objectReferenceValue==null) throw new InvalidOperationException("Learning review is unbound.");
        string report=$"Saved scene: {ScenePath}\nFloors: 4 (3.8m/storey)\nWindows: {windows.Length}\nElevators: {lifts.Length}\nTeaching/service rooms: 32\nStair towers: 2; 120 actual steps\nRefuge intercom: 1; outdoor assembly endpoint: 1\nMissing scripts: {missing}\nObjects: {all.Length}\n";
        Directory.CreateDirectory("Documentation"); File.WriteAllText("Documentation/CampusValidation.txt",report); Debug.Log("CAMPUS_VALIDATION_COMPLETE\n"+report);
        File.WriteAllLines("Documentation/AssetIndex.tsv",new[]{"GUID\tPath"}.Concat(AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")).OrderBy(p=>p).Select(p=>AssetDatabase.AssetPathToGUID(p)+"\t"+p)));
    }
}
