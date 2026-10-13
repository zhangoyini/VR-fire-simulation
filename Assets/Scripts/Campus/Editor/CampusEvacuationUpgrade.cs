using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Valve.VR;

public static partial class CampusSceneBuilder
{
    private const string StairWindowPath = "Assets/Prefabs/Campus/StairWindow.prefab";

    [MenuItem("Tools/Campus/Apply Evacuation Update (keeps scene layout)")]
    public static void ApplyEvacuationUpdate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup = "Assets/Scenes/Legacy/CampusTraining_v3_PreEvacuation_Backup.unity";
        if (!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath, backup)) throw new IOException("Cannot back up campus scene.");
        EditorSceneManager.OpenScene(ScenePath);
        ConfigureComfortAndSmoke();
        RecordGeneratedPrefabOverrides();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new IOException("Cannot save evacuation update.");
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("CAMPUS_EVACUATION_UPDATE_COMPLETE (existing layout retained)");
    }

    private static void ConfigureEvacuationUpdate()
    {
        InitMaterials();
        ConfigureNearestExits();
        ConfigureDetailedHands();
        ConfigureStairWindows();
        ConfigureBuildingSmoke();
        ConfigurePresentationAndEnvironment();
    }

    private static void ConfigureNearestExits()
    {
        foreach (var label in UnityEngine.Object.FindObjectsOfType<TextMeshPro>(true).ToArray())
        {
            if (label.text.Contains("STAIR A / 楼梯") || label.text.Contains("STAIR B / 楼梯") || label.text == "封闭 / CLOSED")
                UnityEngine.Object.DestroyImmediate(label.transform.parent.gameObject);
        }
        foreach (var cue in UnityEngine.Object.FindObjectsOfType<CampusRouteCue>(true)) UnityEngine.Object.DestroyImmediate(cue.gameObject);
        var blockage = UnityEngine.Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Debris physical blockage");
        if (blockage != null) UnityEngine.Object.DestroyImmediate(blockage.gameObject);
        RestoreOriginalDebrisPosition();
        foreach (var arrow in UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "Unambiguous exit arrow"))
        {
            var sign = arrow.parent;
            if (Mathf.Abs(sign.position.z) > 2 || Mathf.Abs(sign.position.x)>20) continue; // Preserve lobby/outdoor exits.
            Vector3 desired = sign.position.x <= 0 ? Vector3.left : Vector3.right;
            bool right = Vector3.Dot(sign.right, desired) > 0;
            arrow.localRotation = Quaternion.Euler(0,0,right ? 0 : 180);
        }
        foreach (var arrow in UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "Evacuation direction arrow"))
            if (Mathf.Abs(arrow.position.z) < 1) arrow.rotation = Quaternion.Euler(0,arrow.position.x <= 0 ? -90 : 90,0);
        foreach (var label in UnityEngine.Object.FindObjectsOfType<TextMeshPro>(true).Where(t=>t.text=="EXIT → LOBBY / 出口通往大堂").ToArray())
        {
            var old=label.transform.parent; Vector3 p=old.position; Transform parent=old.parent;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            ExitSign(parent,p,180,p.x>0);
        }
        foreach (var mini in UnityEngine.Object.FindObjectsOfType<CampusMinimap>(true))
        {
            var key = mini.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Map key");
            if (key != null) key.text = "● YOU / 位置   A / B → EXIT";
            foreach (var stair in mini.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i => i.name.StartsWith("Stair ")))
                stair.color = new Color(.18f,.7f,.4f);
        }
    }

    private static void ConfigureDetailedHands()
    {
        var material = Mat("Detailed VR glove", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SteamVR/Models/Materials/vr_glove_color.jpg"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SteamVR/Models/Materials/vr_glove_normal.png"));
        material.SetFloat("_BumpScale",.65f); material.SetFloat("_Smoothness",.22f); material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        const string poses = "Assets/SteamVR/InteractionSystem/Poses/";
        foreach (var dummy in UnityEngine.Object.FindObjectsOfType<DummyController>(true))
        {
            var previous = dummy.transform.Find("Detailed tracked glove");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var source = (SteamVR_Input_Sources)new SerializedObject(dummy).FindProperty("inputSource").intValue;
            string side = source == SteamVR_Input_Sources.LeftHand ? "left" : "right";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SteamVR/Prefabs/vr_glove_"+side+"_model_slim.prefab");
            if (asset == null) throw new InvalidOperationException("Missing bundled glove model: "+side);
            var model = UnityEngine.Object.Instantiate(asset, dummy.transform, false);
            model.name = "Detailed tracked glove";
            var skeleton = model.GetComponent<SteamVR_Behaviour_Skeleton>();
            Transform skeletonRoot = skeleton.skeletonRoot;
            // Remove SDK auto-pose/Animator writers so they cannot fight the controller proxy.
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            model.transform.localPosition = new Vector3(0,-.035f,-.035f);
            model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            { renderer.enabled=true; renderer.sharedMaterial = material; renderer.updateWhenOffscreen = true; renderer.quality = SkinQuality.Bone4; }
            var visual = dummy.GetComponent<CampusHandVisual>() ?? dummy.gameObject.AddComponent<CampusHandVisual>();
            visual.Configure(skeletonRoot.GetComponentsInChildren<Transform>(true), source,
                AssetDatabase.LoadAssetAtPath<SteamVR_Skeleton_Pose>(poses+"fallback_relaxed.asset"),
                AssetDatabase.LoadAssetAtPath<SteamVR_Skeleton_Pose>(poses+"fallback_fist.asset"),
                AssetDatabase.LoadAssetAtPath<SteamVR_Skeleton_Pose>(poses+"fallback_point.asset"));
        }
    }

    private static void ConfigureStairWindows()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StairWindowPath);
        if (asset == null)
        {
            var root = Group("StairWindow");
            var pivot = Group("Right-side outward hinge",root); pivot.localPosition = new Vector3(1.4f,0,0);
            var leaf = Cube("Opening glass pane",pivot,Vector3.zero,new Vector3(2.8f,2.6f,.08f),glass);
            leaf.transform.localPosition = new Vector3(-1.4f,0,0);
            foreach (int side in new[]{-1,1})
            {
                var upright = Cube("Opening frame upright",pivot,Vector3.zero,new Vector3(.065f,2.6f,.1f),dark);
                upright.transform.localPosition = new Vector3(-1.4f+side*1.365f,0,0);
                var rail = Cube("Opening frame rail",pivot,Vector3.zero,new Vector3(2.8f,.065f,.1f),dark);
                rail.transform.localPosition = new Vector3(-1.4f,side*1.265f,0);
            }
            var handle = Cube("Window handle",pivot,Vector3.zero,new Vector3(.035f,.22f,.09f),brass);
            handle.transform.localPosition = new Vector3(-2.65f,0,.08f);
            var hinge = pivot.gameObject.AddComponent<CampusHingeInteractable>(); hinge.Configure(pivot,-1,true);
            root.gameObject.AddComponent<CampusStairWindow>().Configure(hinge);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,StairWindowPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(StairWindowPath);
        }
        foreach (var old in UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "Tower window").ToArray())
        {
            var window = (GameObject)PrefabUtility.InstantiatePrefab(asset, old.parent);
            window.name = "Interactive stair window"; window.transform.SetPositionAndRotation(old.position,Quaternion.identity);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }

    private static void ConfigureBuildingSmoke()
    {
        var origin = UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "4F reading room fire origin");
        var room = origin.parent;
        var door = room.GetComponentInChildren<CampusHingeInteractable>(true);
        var zones = GameObject.Find("04 Training Zones").transform;
        foreach (var old in zones.GetComponentsInChildren<CampusSmokeSpread>(true).ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root = Group("Building progressive smoke — 4F to 1F",zones);
        var stages = new List<CampusSmokeSpread.Stage>();
        var ceiling = room.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Ceiling smoke layer");
        TuneSmoke(ceiling,false,true);
        stages.Add(new CampusSmokeSpread.Stage { particles=ceiling,exposure=FlowVolume(root,"4F reading room exposure",new Vector3(-5,13.2f,-6),new Vector3(9.3f,3.6f,7.7f)),roomClock=true,floor=4,fillSeconds=14,emissionRate=14,minimumSize=1,maximumSize=1.9f });
        var portal = FlowParticles(root,"4F doorway rolling plume",new Vector3(-2.2f,11.6f,-1.55f),new Vector3(1.15f,.25f,.35f),true);
        TuneSmoke(portal,false,false);
        stages.Add(new CampusSmokeSpread.Stage { particles=portal,exposure=FlowVolume(root,"4F doorway exposure",new Vector3(-2.2f,12.85f,-1.3f),new Vector3(1.5f,2.5f,1.2f)),floor=4,fillSeconds=8,emissionRate=9,minimumSize=.65f,maximumSize=1.15f });
        foreach (int x in new[]{-2,2,-6,6,-10,10,-14,14,-18,18})
        {
            AddSmokeStage(stages,root,4,"4F corridor x="+x,new Vector3(x,13.8f,0),new Vector3(3.5f,.35f,1.25f),
                new Vector3(3.96f,2.7f,3.6f),3+Mathf.Abs(x+2.2f)/1.6f,14,12,true);
        }
        for (int floor = 4; floor >= 1; floor--)
        {
            float y = (floor-1)*Storey;
            // Same spread/settling parameters in both directions. Only actual path length differs.
            float eastArrival = 9+25.2f/1.6f+(4-floor)*18, westArrival = 9+20.8f/1.6f+(4-floor)*18;
            foreach (int side in new[]{-1,1})
            {
                float x = side*23, arrival = side>0 ? eastArrival : westArrival;
                AddSmokeStage(stages,root,floor,floor+"F stair "+(side>0?"B":"A")+" landing",new Vector3(x,y+2.8f,0),new Vector3(4,.25f,1.2f),new Vector3(5.6f,3.1f,2.15f),arrival,16,10,true);
                if (floor == 4) continue;
                AddSmokeStage(stages,root,floor,"Descending stair plume "+floor+".B"+side,new Vector3(x+1.45f,y+3.15f,2.45f),new Vector3(1.7f,.3f,2.1f),new Vector3(2.1f,2.1f,2.9f),arrival-18,14,9,true);
                AddSmokeStage(stages,root,floor,"Descending stair plume "+floor+".A"+side,new Vector3(x-1.45f,y+1.8f,2.45f),new Vector3(1.7f,.3f,2.1f),new Vector3(2.1f,2.1f,2.9f),arrival-9,14,9,true);
            }
            if (floor == 4) continue;
            foreach (int x in new[]{-18,-14,-10,-6,-2,2,6,10,14,18})
            {
                float arrival = Mathf.Min(eastArrival+(23-x)/1.6f,westArrival+(x+23)/1.6f);
                AddSmokeStage(stages,root,floor,floor+"F corridor x="+x,new Vector3(x,y+2.8f,0),new Vector3(3.5f,.25f,1.25f),new Vector3(3.96f,2.7f,3.6f),arrival,18,10,true);
            }
        }
        root.gameObject.AddComponent<CampusSmokeSpread>().Configure(door,portal,stages.ToArray());
        foreach (var plume in origin.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name == "Rising textured smoke")) TuneSmoke(plume,true,true);
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null) { pipeline.supportsCameraDepthTexture = true; EditorUtility.SetDirty(pipeline); }
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Campus/Materials/Textured smoke.mat");
        material.SetFloat("_SoftParticlesEnabled",1); material.SetFloat("_SoftParticlesNearFadeDistance",0); material.SetFloat("_SoftParticlesFarFadeDistance",.8f);
        material.SetVector("_SoftParticleFadeParams",new Vector4(0,1.25f,0,0)); material.SetOverrideTag("RenderType","Transparent");
        material.EnableKeyword("_SOFTPARTICLES_ON"); EditorUtility.SetDirty(material);
    }

    private static void AddSmokeStage(List<CampusSmokeSpread.Stage> stages,Transform root,int floor,string name,Vector3 p,Vector3 shape,Vector3 volume,float delay,float fill,float rate,bool dense)
    {
        var particles = FlowParticles(root,name,p,shape,false); TuneSmoke(particles,false,dense);
        var centre = p; centre.y = (floor-1)*Storey+1.7f;
        // Stair flight volumes follow their height, not the flat corridor floor.
        if (name.StartsWith("Descending")) centre = p-Vector3.up*.65f;
        stages.Add(new CampusSmokeSpread.Stage { particles=particles,exposure=FlowVolume(root,name+" exposure",centre,volume),floor=floor,delay=delay,fillSeconds=fill,emissionRate=rate,minimumSize=dense?.95f:.8f,maximumSize=dense?1.8f:1.55f,settlingDepth=name.Contains("corridor")?.55f:name.Contains("landing")?.35f:0 });
        var velocity=particles.velocityOverLifetime;
        if(name.Contains("corridor"))
        {
            float drift=Mathf.Sign(p.x+(floor==4?2.2f:0))*(floor==4?.09f:-.07f);
            velocity.x=new ParticleSystem.MinMaxCurve(drift,drift); // Match the Y/Z TwoConstants mode.
        }
    }
    private static void TuneSmoke(ParticleSystem particles,bool rising,bool dense)
    {
        var main=particles.main; main.startLifetime=new ParticleSystem.MinMaxCurve(7,10); main.maxParticles=rising?85:particles.name=="Ceiling smoke layer"?100:dense?36:28;
        main.prewarm=false; main.startSpeed=0; main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(.2f,.21f,.22f,dense?.76f:.54f),new Color(.39f,.4f,.41f,dense?.70f:.48f));
        var noise=particles.noise; noise.enabled=true; noise.quality=ParticleSystemNoiseQuality.Medium; noise.strength=.18f; noise.frequency=.28f; noise.scrollSpeed=.30f; noise.damping=true;
        var velocity=particles.velocityOverLifetime; velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=new ParticleSystem.MinMaxCurve(-.05f,.05f); velocity.z=new ParticleSystem.MinMaxCurve(-.04f,.04f);
        velocity.y=new ParticleSystem.MinMaxCurve(rising?.25f:.02f,rising?.4f:.055f);
        var renderer=particles.GetComponent<ParticleSystemRenderer>(); renderer.sortMode=ParticleSystemSortMode.Distance;
        renderer.minParticleSize=0; renderer.maxParticleSize=.7f;
        var collision=particles.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.World; collision.quality=ParticleSystemCollisionQuality.Low; collision.collidesWith=~0;
        collision.dampen=1; collision.bounce=0; collision.radiusScale=.18f;
    }
}
