using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CampusSceneBuilder
{
    private const string WashStationPath="Assets/Prefabs/Campus/RestroomWashStation.prefab";
    private const string CarryTowelPath="Assets/Prefabs/Campus/CarryTowel.prefab";
    [MenuItem("Tools/Campus/Apply Restroom Water and Held Towel (keeps other rooms)")]
    public static void ApplyRestroomUpdate()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup="Assets/Scenes/Legacy/CampusTraining_v6_PreWaterTowel_Backup.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup)) throw new IOException("Cannot back up campus scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath); UpgradeRestroomWaterAndTowels();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save restroom update.");
        AssetDatabase.SaveAssets(); Validate();
        var faucets=UnityEngine.Object.FindObjectsOfType<CampusFaucet>(true);
        var towels=UnityEngine.Object.FindObjectsOfType<CampusTowelInteractable>(true);
        if(faucets.Length!=8 || towels.Length!=4) throw new InvalidOperationException("Expected two taps and one dry towel per floor.");
        Debug.Log("CAMPUS_RESTROOM_UPDATE_COMPLETE faucets="+faucets.Length+" dryTowels="+towels.Length);
    }
    private static void UpgradeRestroomWaterAndTowels()
    {
        InitMaterials(); Folder("Assets/Art/Campus/Meshes"); Folder("Assets/Prefabs/Campus"); Folder("Assets/Audio/Campus");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Resolve("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset"));
        CreateCarryTowelPrefab(); CreateWashStationPrefab();
        var rooms=UnityEngine.Object.FindObjectsOfType<Transform>(true).Where(t=>t.name.Length==7 &&
            t.name[0]>='1' && t.name[0]<='4' && t.name.Substring(1)=="11 卫生间").ToArray();
        if(rooms.Length!=4) throw new InvalidOperationException("Expected restroom room roots 111, 211, 311 and 411.");
        foreach(var room in rooms)
        {
            // Only replace the known wash fixtures and old consumable towel; keep toilets,
            // stall doors, mirrors, soap, hand dryers, lighting and all other rooms intact.
            foreach(var child in room.Cast<Transform>().Where(t=>t.name=="Wash counter" || t.name.StartsWith("RR_Sink_01") || t.name=="Restroom wash station" ||
                t.name.StartsWith("Wet towel") || t.name=="Sign 湿毛巾" || t.GetComponent<CampusTowelInteractable>()!=null).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            int floorNumber=room.name[0]-'0';
            var station=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WashStationPath),room);
            station.name="Restroom wash station"; station.transform.position=new Vector3(-15,(floorNumber-1)*Storey,0);
        }
        var lesson=UnityEngine.Object.FindObjectOfType<CampusLessonDirector>();
        if(lesson!=null) Set(lesson,"targetTowel",UnityEngine.Object.FindObjectsOfType<CampusTowelInteractable>().OrderByDescending(t=>t.transform.position.y).First());
    }
    private static void CreateCarryTowelPrefab()
    {
        var dry=Mat("Dry cotton towel",new Color(.87f,.91f,.88f)); dry.SetFloat("_Smoothness",.08f);
        var wet=Mat("Wet cotton towel",new Color(.30f,.56f,.60f)); wet.SetFloat("_Smoothness",.32f);
        var root=new GameObject("Dry towel");
        try
        {
            var mesh=root.AddComponent<MeshFilter>(); mesh.sharedMesh=SaveFixtureMesh("Cotton towel",TowelMesh());
            var renderer=root.AddComponent<MeshRenderer>(); renderer.sharedMaterial=dry;
            var collider=root.AddComponent<BoxCollider>(); collider.size=new Vector3(.32f,.035f,.16f);
            var body=root.AddComponent<Rigidbody>(); body.mass=.12f; body.useGravity=true;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic; body.interpolation=RigidbodyInterpolation.Interpolate;
            body.drag=.5f; body.angularDrag=2;
            var friction=AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/Art/Campus/Materials/Towel friction.physicMaterial");
            if(friction==null) { friction=new PhysicMaterial("Towel friction"); AssetDatabase.CreateAsset(friction,"Assets/Art/Campus/Materials/Towel friction.physicMaterial"); }
            friction.dynamicFriction=.7f; friction.staticFriction=.8f; friction.bounciness=0; EditorUtility.SetDirty(friction); collider.sharedMaterial=friction;
            root.AddComponent<CampusTowelInteractable>().Configure(renderer,dry,wet);
            PrefabUtility.SaveAsPrefabAsset(root,CarryTowelPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void CreateWashStationPrefab()
    {
        var root=Group("Restroom wash station");
        try
        {
            var tile=Mat("Wash counter stone",new Color(.78f,.83f,.82f));
            var ceramic=Mat("Wash basin ceramic",new Color(.88f,.93f,.92f)); ceramic.SetFloat("_Smoothness",.7f);
            var chrome=Mat("Faucet chrome",new Color(.52f,.60f,.64f)); chrome.SetFloat("_Metallic",.82f); chrome.SetFloat("_Smoothness",.8f);
            var water=Mat("Running tap water",new Color(.65f,.9f,.96f,.4f),true); water.SetFloat("_Smoothness",.9f);
            // Leave actual holes for the bowls: no solid slab filling the basin interior.
            Cube("Wash counter back strip",root,new Vector3(-2.675f,.9f,-2.31f),new Vector3(3.55f,.08f,.24f),tile);
            Cube("Wash counter front strip",root,new Vector3(-2.675f,.9f,-2.97f),new Vector3(3.55f,.08f,.08f),tile);
            foreach(var segment in new[]{new Vector2(-4.33f,.24f),new Vector2(-3.225f,.33f),new Vector2(-1.57f,1.34f)})
                Cube("Wash counter solid section",root,new Vector3(segment.x,.9f,-2.68f),new Vector3(segment.y,.08f,.5f),tile);
            foreach(int side in new[]{-1,1}) Cube("Counter support",root,new Vector3(-2.675f+side*1.55f,.43f,-2.66f),new Vector3(.13f,.86f,.65f),tile);
            var basinMesh=SaveFixtureMesh("Open wash basin",BasinMesh());
            var surroundMesh=SaveFixtureMesh("Wash counter basin surround",BasinSurroundMesh());
            foreach(float x in new[]{-3.8f,-2.65f})
            {
                // A rectangular opening around an oval bowl leaves visible corner gaps.
                // Fill those corners with a solid ring, but leave the bowl interior open.
                var surround=new GameObject("Sealed oval basin surround"); surround.transform.SetParent(root,false);
                surround.transform.localPosition=new Vector3(x,0,-2.68f);
                surround.AddComponent<MeshFilter>().sharedMesh=surroundMesh; surround.AddComponent<MeshRenderer>().sharedMaterial=tile;
                surround.AddComponent<MeshCollider>().sharedMesh=surroundMesh; surround.isStatic=true;
                var basin=new GameObject("Open ceramic basin"); basin.transform.SetParent(root,false); basin.transform.localPosition=new Vector3(x,0,-2.68f);
                basin.AddComponent<MeshFilter>().sharedMesh=basinMesh; basin.AddComponent<MeshRenderer>().sharedMaterial=ceramic;
                basin.AddComponent<MeshCollider>().sharedMesh=basinMesh; basin.isStatic=true;
                FixtureCylinder("Basin drain",root,new Vector3(x,.705f,-2.68f),new Vector3(.065f,.008f,.065f),chrome);
                FixtureCylinder("Faucet upright",root,new Vector3(x,1.04f,-2.36f),new Vector3(.044f,.12f,.044f),chrome);
                FixtureCylinder("Faucet spout",root,new Vector3(x,1.155f,-2.515f),new Vector3(.04f,.155f,.04f),chrome).rotation=Quaternion.Euler(90,0,0);
                FixtureCylinder("Faucet nozzle",root,new Vector3(x,1.12f,-2.67f),new Vector3(.035f,.03f,.035f),chrome);
                var lever=Group("Faucet switch",root); lever.position=new Vector3(x,1.20f,-2.36f);
                var handle=Cube("Faucet switch handle",lever,lever.position+new Vector3(0,0,-.08f),new Vector3(.075f,.028f,.17f),chrome,false,false); handle.isStatic=false;
                var marker=Cube("Faucet blue indicator",lever,lever.position+new Vector3(0,.016f,-.145f),new Vector3(.035f,.008f,.015f),teal,false,false); marker.isStatic=false;
                var contact=lever.gameObject.AddComponent<BoxCollider>(); contact.center=new Vector3(0,0,-.07f); contact.size=new Vector3(.09f,.045f,.19f);
                var flow=Group("Running water and wetting volume",root); flow.position=new Vector3(x,.905f,-2.67f);
                FixtureCylinder("Visible water stream",flow,new Vector3(x,.905f,-2.67f),new Vector3(.012f,.18f,.012f),water);
                var volume=flow.gameObject.AddComponent<BoxCollider>(); volume.isTrigger=true; volume.size=new Vector3(.14f,.36f,.16f);
                CreateWaterDroplets(flow,new Vector3(x,1.085f,-2.67f),water);
                var audio=lever.gameObject.AddComponent<AudioSource>(); audio.playOnAwake=false; audio.loop=true; audio.spatialBlend=1; audio.volume=.18f;
                audio.minDistance=.3f; audio.maxDistance=4; audio.rolloffMode=AudioRolloffMode.Logarithmic; audio.clip=CreateRunningWaterAudio();
                lever.gameObject.AddComponent<CampusFaucet>().Configure(flow.gameObject,volume,audio);
                Cube("Wash mirror task light",root,new Vector3(x,2.02f,-2.18f),new Vector3(.85f,.045f,.07f),white,false,false);
            }
            FillLight(root,new Vector3(-2.8f,1.85f,-3.0f),3,1.6f);
            var towel=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CarryTowelPath),root);
            towel.name="Dry towel"; towel.transform.position=new Vector3(-1.4f,.966f,-2.62f);
            Sign("毛巾 / TOWEL",root,new Vector3(-1.4f,1.22f,-2.14f),0,new Vector2(.95f,.18f),teal,.075f);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,WashStationPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }
    private static Transform FixtureCylinder(string name,Transform parent,Vector3 p,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetParent(parent,false);
        go.transform.position=p; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
    }
    private static void CreateWaterDroplets(Transform parent,Vector3 p,Material material)
    {
        var go=new GameObject("Water droplets"); go.transform.SetParent(parent,false); go.transform.position=p; go.transform.rotation=Quaternion.Euler(90,0,0);
        var particles=go.AddComponent<ParticleSystem>(); particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main; main.startLifetime=new ParticleSystem.MinMaxCurve(.14f,.28f); main.startSpeed=new ParticleSystem.MinMaxCurve(.45f,.8f);
        main.startSize=new ParticleSystem.MinMaxCurve(.004f,.011f); main.maxParticles=32; main.gravityModifier=.4f; main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=particles.emission; emission.rateOverTime=65;
        var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=4; shape.radius=.006f;
        var renderer=go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
    }
    private static AudioClip CreateRunningWaterAudio()
    {
        const string path="Assets/Audio/Campus/running_water.wav";
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path); if(clip!=null) return clip;
        const int sampleRate=22050,frames=22050; var samples=new short[frames]; var random=new System.Random(681);
        float filtered=0;
        for(int i=0;i<frames;i++)
        {
            filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.28f);
            float envelope=.70f+.17f*Mathf.Sin(i*2*Mathf.PI/frames*7);
            // Crossfade the ends to silence to avoid an audible loop discontinuity.
            float fade=Mathf.Min(1,Mathf.Min(i,frames-1-i)/220f);
            samples[i]=(short)(filtered*envelope*fade*12000);
        }
        using(var stream=File.Create(path)) using(var writer=new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+frames*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate*2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frames*2); foreach(var value in samples) writer.Write(value);
        }
        AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static Mesh SaveFixtureMesh(string name,Mesh generated)
    {
        string path="Assets/Art/Campus/Meshes/"+name+".asset"; var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(stored==null) { generated.name=name; AssetDatabase.CreateAsset(generated,path); return generated; }
        EditorUtility.CopySerialized(generated,stored); stored.name=name; EditorUtility.SetDirty(stored); UnityEngine.Object.DestroyImmediate(generated); return stored;
    }
    private static Mesh BasinMesh()
    {
        const int segments=40; var radii=new[]{new Vector2(.45f,.29f),new Vector2(.39f,.24f),new Vector2(.36f,.22f),new Vector2(.28f,.17f),new Vector2(.16f,.10f),new Vector2(.025f,.025f)};
        var heights=new[]{.945f,.945f,.91f,.8f,.72f,.70f}; var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
        for(int ring=0;ring<radii.Length;ring++) for(int i=0;i<=segments;i++)
        {
            float a=i*2*Mathf.PI/segments; float x=Mathf.Cos(a)*radii[ring].x,z=Mathf.Sin(a)*radii[ring].y;
            vertices.Add(new Vector3(x,heights[ring],z)); uv.Add(new Vector2(x+.5f,z+.5f));
            if(ring==radii.Length-1 || i==segments) continue;
            int outer=ring*(segments+1)+i,inner=outer+segments+1;
            triangles.AddRange(new[]{outer,inner,outer+1,outer+1,inner,inner+1});
        }
        var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static Mesh TowelMesh()
    {
        const int nx=24,nz=18; var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
        for(int side=0;side<2;side++) for(int z=0;z<=nz;z++) for(int x=0;x<=nx;x++)
        {
            float px=(float)x/nx*.32f-.16f,pz=(float)z/nz*.16f-.08f;
            float fold=Mathf.Sin(px*57)*Mathf.Cos(pz*35)*.0025f;
            vertices.Add(new Vector3(px,(side==0?.010f:-.010f)+fold,pz)); uv.Add(new Vector2((float)x/nx,(float)z/nz));
            if(x==nx || z==nz) continue;
            int i=side*(nx+1)*(nz+1)+z*(nx+1)+x;
            triangles.AddRange(side==0?new[]{i,i+nx+1,i+1,i+1,i+nx+1,i+nx+2}:new[]{i,i+1,i+nx+1,i+1,i+nx+2,i+nx+1});
        }
        // Close the edges so the folded cloth has thickness even at grazing angles.
        int layer=(nx+1)*(nz+1);
        for(int x=0;x<nx;x++) { ClothEdge(triangles,x+1,x,layer); int i=nz*(nx+1)+x; ClothEdge(triangles,i,i+1,layer); }
        for(int z=0;z<nz;z++) { int i=z*(nx+1); ClothEdge(triangles,i,i+nx+1,layer); ClothEdge(triangles,i+nx+nx+1,i+nx,layer); }
        var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static Mesh BasinSurroundMesh()
    {
        const int segments=40; var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
        for(int ring=0;ring<4;ring++) for(int i=0;i<=segments;i++)
        {
            float a=i*2*Mathf.PI/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);
            // All four rectangle corners occur at the diagonal samples; do not cut
            // them off with a polygon that only samples rays on the long sides.
            float factor=1/Mathf.Max(Mathf.Abs(c),Mathf.Abs(s));
            bool inner=ring%2==1;
            float x=inner?c*.395f:c*.41f*factor,z=inner?s*.245f:s*.25f*factor;
            vertices.Add(new Vector3(x,ring<2?.94f:.86f,z)); uv.Add(new Vector2(x+.5f,z+.5f));
        }
        int n=segments+1;
        for(int i=0;i<segments;i++)
        {
            triangles.AddRange(new[]{i,i+n,i+1,i+1,i+n,i+n+1}); // Top.
            triangles.AddRange(new[]{i+2*n,i+2*n+1,i+3*n,i+2*n+1,i+3*n+1,i+3*n}); // Bottom.
            triangles.AddRange(new[]{i,i+1,i+2*n,i+1,i+2*n+1,i+2*n}); // Outside.
            triangles.AddRange(new[]{i+n,i+3*n,i+n+1,i+n+1,i+3*n,i+3*n+1}); // Bowl opening.
        }
        var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static void ClothEdge(List<int> triangles,int a,int b,int layer) { triangles.AddRange(new[]{a,b,a+layer,b,b+layer,a+layer}); }
}
