using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CampusSceneBuilder
{
    [MenuItem("Tools/Campus/Refine teaching, fire and physical interaction")]
    public static void ApplyRefinementUpdate()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup="Assets/Scenes/Legacy/CampusTraining_v10_PreRefinement_Backup.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup)) throw new IOException("Cannot back up campus scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        InitMaterials(); RepairStairWindowPrefab(); ConfigureBuildingSmoke(); ExtendReadingRoomFire();
        ConfigureGuidedLesson();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save refined campus.");
        AssetDatabase.SaveAssets(); Validate(); Debug.Log("CAMPUS_REFINEMENT_UPDATE_COMPLETE");
    }
    private static Material ObjectiveMaterial()
    {
        var material=Mat("Objective cyan luminous",new Color(.04f,.86f,1));
        material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",new Color(0,1.1f,1.6f));
        EditorUtility.SetDirty(material); return material;
    }
    private static void ConfigureLessonDestination(CampusLessonDirector lesson)
    {
        var label=UnityEngine.Object.FindObjectsOfType<TextMeshPro>(true).Single(t=>t.text=="411 · 卫生间");
        label.color=Color.white;
        var plate=label.transform.parent.GetComponentInChildren<MeshRenderer>();
        plate.sharedMaterial=Mat("411 objective door plate",new Color(.025f,.16f,.19f));
        plate.sharedMaterial.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(plate.sharedMaterial);
        var old=label.transform.parent.Find("Objective glowing border");
        if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var halo=Group("Objective glowing border",label.transform.parent);
        foreach(int side in new[]{-1,1})
        {
            var rail=Cube("Objective border",halo,Vector3.zero,new Vector3(1.66f,.034f,.025f),TeachingMaterial("Objective cyan beacon",new Color(.1f,1,1)),false,false);
            rail.GetComponent<Renderer>().sortingOrder=31000;
            rail.isStatic=false; rail.transform.localPosition=new Vector3(0,side*.182f,-.045f);
            var end=Cube("Objective border",halo,Vector3.zero,new Vector3(.034f,.39f,.025f),TeachingMaterial("Objective cyan beacon",new Color(.1f,1,1)),false,false);
            end.GetComponent<Renderer>().sortingOrder=31000;
            end.isStatic=false; end.transform.localPosition=new Vector3(side*.817f,0,-.045f);
        }
        halo.gameObject.SetActive(false);
        var target=label.transform.parent.GetComponent<CampusDestinationHighlight>() ?? label.transform.parent.gameObject.AddComponent<CampusDestinationHighlight>();
        Set(target,"plate",plate); Set(target,"label",label); Set(target,"halo",halo.gameObject); Set(lesson,"destination",target);
        Set(target,"highlightedText",AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Campus/Materials/Teaching label overlay.mat"));
    }
    private static void RepairStairWindowPrefab()
    {
        var root=PrefabUtility.LoadPrefabContents(StairWindowPath);
        try
        {
            var hinge=root.GetComponent<CampusStairWindow>().Hinge;
            // Movable geometry must not be frozen into Unity's static batches.
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) { t.gameObject.isStatic=false; EditorUtility.SetDirty(t.gameObject); }
            var body=hinge.GetComponent<Rigidbody>(); if(body==null) body=hinge.gameObject.AddComponent<Rigidbody>();
            body.isKinematic=true; body.useGravity=false; body.interpolation=RigidbodyInterpolation.None;
            var handle=hinge.transform.Find("Window handle");
            handle.localPosition=new Vector3(-2.30f,-.50f,.16f); handle.localScale=new Vector3(.045f,.24f,.14f);
            PrefabUtility.SaveAsPrefabAsset(root,StairWindowPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach(var window in UnityEngine.Object.FindObjectsOfType<CampusStairWindow>(true))
        {
            foreach(var t in window.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic=false;
            var handle=window.Hinge.transform.Find("Window handle");
            handle.localPosition=new Vector3(-2.30f,-.50f,.16f); handle.localScale=new Vector3(.045f,.24f,.14f);
            var body=window.Hinge.GetComponent<Rigidbody>(); if(body==null) body=window.Hinge.gameObject.AddComponent<Rigidbody>();
            body.isKinematic=true; body.useGravity=false;
        }
        foreach(var fixedBar in UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t=>t.name=="Tower mullion").ToArray())
            UnityEngine.Object.DestroyImmediate(fixedBar.gameObject);
    }
    private static void ExtendReadingRoomFire()
    {
        var origin=UnityEngine.Object.FindObjectsOfType<Transform>(true).Single(t=>t.name=="4F reading room fire origin");
        var previous=origin.Find("Fire progression to doorway");
        if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var front=Group("Fire progression to doorway",origin);
        // Flames on the interior furniture, then at the doorway threshold visible from 401.
        foreach(var p in new[]{new Vector3(-5.6f,11.4f,-4.5f),new Vector3(-3.5f,11.4f,-3.3f),new Vector3(-2.2f,11.4f,-1.7f)})
            FireAndSmoke(front,p);
        foreach(var ps in origin.GetComponentsInChildren<ParticleSystem>(true))
        {
            if(ps.name=="Rising textured smoke") { TuneSmoke(ps,true,true); continue; }
            if(ps.name!="Orange flame tongues" && ps.name!="Hot flame core") continue;
            bool outer=ps.name=="Orange flame tongues";
            var main=ps.main; main.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.45f);
            main.startSize=new ParticleSystem.MinMaxCurve(outer?.65f:.35f,outer?1.5f:.75f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.6f); main.startRotation=new ParticleSystem.MinMaxCurve(-.18f,.18f);
            main.maxParticles=70; main.prewarm=true;
            var size=ps.sizeOverLifetime; size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.6f),new Keyframe(.35f,1.2f),new Keyframe(1,.35f)));
            var sheet=ps.textureSheetAnimation; sheet.startFrame=new ParticleSystem.MinMaxCurve(0,1);
            var noise=ps.noise; noise.quality=ParticleSystemNoiseQuality.Medium; noise.strength=.19f; noise.frequency=.75f;
        }
    }
    // Bake into proxy coordinates, including the left glove's negative-X mirror.
    // Copying only position/rotation loses that mirror and gives two right hands.
    private static Mesh BakeHandInProxySpace(CampusHandInteractor proxy,SkinnedMeshRenderer source,string name)
    {
        var mesh=new Mesh(); source.BakeMesh(mesh,false);
        Matrix4x4 matrix=proxy.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
        var vertices=mesh.vertices; var normals=mesh.normals; var tangents=mesh.tangents;
        var normalMatrix=matrix.inverse.transpose; float sign=matrix.determinant<0?-1:1;
        for(int i=0;i<vertices.Length;i++)
        {
            vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
            if(normals.Length>i) normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
            if(tangents.Length>i) { Vector3 t=matrix.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)).normalized; tangents[i]=new Vector4(t.x,t.y,t.z,tangents[i].w*sign); }
        }
        mesh.vertices=vertices; mesh.normals=normals; mesh.tangents=tangents;
        if(sign<0) for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var triangles=mesh.GetTriangles(sub);
            for(int i=0;i<triangles.Length;i+=3) { int swap=triangles[i]; triangles[i]=triangles[i+1]; triangles[i+1]=swap; }
            mesh.SetTriangles(triangles,sub);
        }
        mesh.RecalculateBounds(); Folder("Assets/Art/Campus/Meshes");
        string path="Assets/Art/Campus/Meshes/"+name+".asset"; var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(stored==null) { mesh.name=name; AssetDatabase.CreateAsset(mesh,path); stored=mesh; }
        else { EditorUtility.CopySerialized(mesh,stored); EditorUtility.SetDirty(stored); UnityEngine.Object.DestroyImmediate(mesh); }
        return stored;
    }
    private static void CreateControllerTeaching(Transform controls,CampusLessonAnimation animation,Material shell,Material ink,Material blue,Material yellow,TMP_Text caption)
    {
        var hands=UnityEngine.Object.FindObjectsOfType<CampusHandInteractor>(true).OrderBy(h=>h.InputSource==Valve.VR.SteamVR_Input_Sources.LeftHand?0:1).ToArray();
        controls.localScale=Vector3.one*1.2f;
        var glove=TeachingMaterial("Teaching textured glove",Color.white);
        glove.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SteamVR/Models/Materials/vr_glove_color.jpg"));
        EditorUtility.SetDirty(glove);
        for(int i=0;i<2;i++)
        {
            bool left=i==0; var root=Group(left?"3D left Touch controller":"3D right Touch controller",controls);
            root.localPosition=new Vector3(left?-.15f:.15f,0,0); root.localRotation=Quaternion.Euler(-25,0,left?-12:12);
            root.localScale=Vector3.one*1.35f;
            Set(animation,left?"leftController":"rightController",root);
            TeachingSphere("Rounded controller grip",root,new Vector3(0,.025f,.017f),new Vector3(.067f,.16f,.072f),shell);
            TeachingSphere("Rounded controller shell",root,new Vector3(0,.098f,0),new Vector3(.117f,.057f,.107f),shell);
            TeachingSphere("Black controller face",root,new Vector3(0,.117f,0),new Vector3(.105f,.012f,.096f),ink);
            var stick=TeachingSphere("3D joystick cap",root,new Vector3(-.024f,.126f,-.014f),new Vector3(.029f,.016f,.029f),yellow);
            if(left) Set(animation,"stick",stick);
            var trigger=TeachingSphere("3D index trigger",root,new Vector3(0,.065f,-.052f),new Vector3(.035f,.045f,.028f),yellow);
            Set(animation,left?"leftTrigger":"trigger",trigger);
            TeachingSphere("Grip key (unused)",root,new Vector3(left?.033f:-.033f,.015f,.012f),new Vector3(.012f,.060f,.028f),ink);
            foreach(int key in new[]{0,1})
            {
                var button=TeachingSphere("3D "+(left?(key==0?"X":"Y"):(key==0?"A":"B"))+" button",root,new Vector3(.024f,.129f,key==0?-.019f:.014f),Vector3.one*.022f,key==0 && !left?yellow:blue);
                if(!left && key==0) Set(animation,"confirmButton",button);
                var label=TeachingLabel(left?(key==0?"X":"Y"):(key==0?"A":"B"),root,new Vector3(.024f,.142f,key==0?-.019f:.014f),.016f,new Vector2(.030f,.028f));
                label.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            TeachingSphere("System menu button",root,new Vector3(0,.128f,.031f),Vector3.one*.016f,ink);
            var visual=hands[i].GetComponent<CampusHandVisual>();
            var source=hands[i].GetComponentInChildren<SkinnedMeshRenderer>(true);
            visual.SetGesture(false,false,false); visual.Tick(1); var rest=BakeHandInProxySpace(hands[i],source,"Teaching glove "+i+" relaxed");
            visual.SetGesture(true,false,false); visual.Tick(1); var fist=BakeHandInProxySpace(hands[i],source,"Teaching glove "+i+" fist");
            visual.SetGesture(false,false,false); visual.Tick(1);
            var hand=new GameObject(left?"Teaching left glove":"Teaching right glove"); hand.transform.SetParent(root,false);
            hand.transform.localPosition=new Vector3(0,-.025f,-.025f); hand.transform.localRotation=Quaternion.Euler(-75,0,left?180:0);
            hand.AddComponent<MeshFilter>().sharedMesh=rest;
            var renderer=hand.AddComponent<MeshRenderer>(); renderer.sharedMaterial=glove; renderer.sortingOrder=31000;
            var teaching=hand.AddComponent<CampusTeachingHand>(); teaching.Configure(rest,fist); Set(animation,left?"leftHand":"rightHand",teaching);
        }
        var door=Group("Animated demo door pivot",controls); door.localPosition=new Vector3(.32f,0,.04f);
        TeachingCube("Demo door",door,new Vector3(.11f,.04f,0),new Vector3(.22f,.28f,.022f),blue); Set(animation,"door",door);
        Set(animation,"actionLabel",caption);
    }
    private static TextMeshPro TeachingLabel(string value,Transform parent,Vector3 position,float size,Vector2 rect)
    {
        var go=new GameObject("Teaching label "+value); go.transform.SetParent(parent,false); go.transform.localPosition=position;
        var label=go.AddComponent<TextMeshPro>(); label.font=font; label.text=value; label.fontSize=size*10;
        label.alignment=TextAlignmentOptions.Center; label.rectTransform.sizeDelta=rect; label.enableWordWrapping=true;
        var material=new Material(font.material); material.shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader");
        string path="Assets/Art/Campus/Materials/Teaching label overlay.mat";
        var stored=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(stored==null) { AssetDatabase.CreateAsset(material,path); stored=material; } else UnityEngine.Object.DestroyImmediate(material);
        label.fontSharedMaterial=stored; label.GetComponent<MeshRenderer>().sortingOrder=31001; return label;
    }
}
