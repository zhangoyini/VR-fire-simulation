using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CampusSceneBuilder
{
    private const string ComfortShaderPath = "Assets/Scripts/Campus/Shaders/CampusComfortVignette.shader";

    [MenuItem("Tools/Campus/Apply Comfort and Smoke Update (keeps scene layout)")]
    public static void ApplyComfortUpdate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string backup = "Assets/Scenes/Legacy/CampusTraining_v2_PreComfort_Backup.unity";
        if (!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath, backup)) throw new IOException("Cannot back up the existing scene.");
        EditorSceneManager.OpenScene(ScenePath);
        ConfigureComfortAndSmoke();
        RecordGeneratedPrefabOverrides();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new IOException("Cannot save the updated scene.");
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("CAMPUS_COMFORT_UPDATE_COMPLETE (layout preserved)");
    }

    private static void ConfigureComfortAndSmoke()
    {
        var locomotion = UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true);
        if (locomotion == null) throw new InvalidOperationException("Campus locomotion is missing.");
        var cameraTransform = (Transform)new SerializedObject(locomotion).FindProperty("trackedHead").objectReferenceValue;
        if (cameraTransform == null) throw new InvalidOperationException("Tracked head is missing.");
        var vignette = cameraTransform.GetComponent<CampusComfortVignette>() ?? cameraTransform.gameObject.AddComponent<CampusComfortVignette>();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ComfortShaderPath);
        if (shader == null) throw new InvalidOperationException("Comfort shader is missing.");
        vignette.Configure(shader);
        Set(locomotion, "comfortVignette", vignette);
        SetFloat(locomotion, "walkingSpeed", 1.8f);
        SetFloat(locomotion, "acceleration", 6);
        foreach (var hand in UnityEngine.Object.FindObjectsOfType<CampusHandInteractor>(true)) SetFloat(hand, "contactDistance", .12f);
        foreach (var hinge in UnityEngine.Object.FindObjectsOfType<CampusHingeInteractable>(true)) SetFloat(hinge, "travelForFullOpening", .3f);

        foreach (var label in UnityEngine.Object.FindObjectsOfType<TextMeshPro>(true))
            if (label.text == "3F\nSTAIR A / 楼梯" || label.text == "4F\nSTAIR A / 楼梯")
                UnityEngine.Object.DestroyImmediate(label.transform.parent.gameObject);
        ConfigureReadingRoomSpread();
        ConfigureEvacuationUpdate();
    }

    private static void ConfigureReadingRoomSpread()
    {
        var origin = UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "4F reading room fire origin");
        var room = origin.parent;
        var doorway = room.GetComponentInChildren<CampusHingeInteractable>(true);
        if (doorway == null) throw new InvalidOperationException("Reading-room door hinge is missing.");
        var zones = GameObject.Find("04 Training Zones").transform;
        var oldProgression = zones.Find("4F progressive smoke flow");
        if (oldProgression != null) UnityEngine.Object.DestroyImmediate(oldProgression.gameObject);
        // Retire only the two fixed full-size zones from the preceding generated revision.
        foreach (var zone in zones.GetComponentsInChildren<CampusSmokeVolume>(true).ToArray())
            if (zone.name == "Smoke exposure volume" &&
                (Vector3.Distance(zone.transform.position, new Vector3(-5,13.2f,-6)) < .05f ||
                 Vector3.Distance(zone.transform.position, new Vector3(-5,13.55f,0)) < .05f))
                UnityEngine.Object.DestroyImmediate(zone.gameObject);
        var ceiling = room.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Ceiling smoke layer");
        var shape = ceiling.shape; shape.scale = new Vector3(5,.1f,3);
        ceiling.transform.position = new Vector3(-5,14.2f,-6.1f);

        var root = Group("4F progressive smoke flow", zones);
        var progression = new List<CampusSmokeSpread.Stage>();
        progression.Add(new CampusSmokeSpread.Stage { particles=ceiling, exposure=FlowVolume(root,"Reading room smoke",new Vector3(-5,13.2f,-6),new Vector3(9.3f,3.6f,7.7f)),roomClock=true,fillSeconds=12,emissionRate=10 });
        var portal = FlowParticles(root,"Smoke leaking through reading-room doorway",new Vector3(-2.2f,11.6f,-1.55f),new Vector3(.85f,.1f,.1f),true);
        progression.Add(new CampusSmokeSpread.Stage { particles=portal,exposure=FlowVolume(root,"Doorway smoke",new Vector3(-2.2f,12.85f,-1.3f),new Vector3(1.5f,2.5f,1.2f)),fillSeconds=6,emissionRate=8 });
        foreach (int x in new[] {-2,2,-6,6,-10,10,-14,14,-18,18})
        {
            var particles = FlowParticles(root,"Corridor smoke front x="+x,new Vector3(x,13.65f,0),new Vector3(2,.1f,.2f),false);
            var volume = FlowVolume(root,"Progressive corridor exposure x="+x,new Vector3(x,13.45f,0),new Vector3(3.96f,2.7f,3.6f));
            progression.Add(new CampusSmokeSpread.Stage {particles=particles,exposure=volume,delay=3+Mathf.Abs(x+2.2f)/.65f,fillSeconds=8,emissionRate=7});
        }
        root.gameObject.AddComponent<CampusSmokeSpread>().Configure(doorway,portal,progression.ToArray());
    }
    private static CampusSmokeVolume FlowVolume(Transform parent,string name,Vector3 p,Vector3 size)
    {
        var root=Group(name,parent); root.position=p;
        var box=root.gameObject.AddComponent<BoxCollider>(); box.size=size; box.isTrigger=true;
        var volume=root.gameObject.AddComponent<CampusSmokeVolume>(); volume.enabled=false; return volume;
    }
    private static ParticleSystem FlowParticles(Transform parent,string name,Vector3 p,Vector3 spread,bool portal)
    {
        var particles=SmokeParticles(parent,p,spread,false); particles.name=name;
        var main=particles.main; main.startLifetime=6; main.startSize=new ParticleSystem.MinMaxCurve(.65f,1.05f); main.maxParticles=36; main.prewarm=false; main.startSpeed=0;
        var velocity=particles.velocityOverLifetime; velocity.y=new ParticleSystem.MinMaxCurve(portal?.28f:.12f); velocity.x=new ParticleSystem.MinMaxCurve(0); velocity.z=new ParticleSystem.MinMaxCurve(0);
        var size=particles.sizeOverLifetime; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.6f));
        var collision=particles.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.World;
        collision.quality=ParticleSystemCollisionQuality.Low; collision.collidesWith=~0; collision.dampen=1; collision.bounce=0; collision.lifetimeLoss=0; collision.radiusScale=.3f;
        return particles;
    }
}
