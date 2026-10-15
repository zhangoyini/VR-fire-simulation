using System;
using System.IO;
using System.Linq;
using Harbourview.Journey;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

public static partial class CampusSceneBuilder
{
    private static void BackupPreviousCampus()
    {
        string backup = "Assets/Scenes/Legacy/CampusTraining_v1_Backup.unity";
        if (File.Exists(ScenePath) && !File.Exists(backup)) AssetDatabase.CopyAsset(ScenePath, backup);
    }
    private static void SetBool(UnityEngine.Object target, string field, bool value)
    { var so = new SerializedObject(target); so.FindProperty(field).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetVector(UnityEngine.Object target, string field, Vector3 value)
    { var so = new SerializedObject(target); so.FindProperty(field).vector3Value = value; so.ApplyModifiedPropertiesWithoutUndo(); }

    private static void BuildRevisedFloor(int level, Transform architecture, Transform interiors, Transform zones)
    {
        float y = level * Storey;
        var structure = Group($"{level+1}F Structure", architecture);
        var furnishing = Group($"{level+1}F Rooms", interiors);
        var accent = Mat("Floor " + (level+1) + " Accent", accents[level]);
        Cube("Continuous floor slab", structure, new Vector3(0,y-.12f,0), new Vector3(40.08f,.24f,20.08f), floor);
        // Upper floor slabs are ceilings; a continuous perimeter header fills their joints.
        foreach (int side in new[]{-1,1})
        {
            for(int w=0;w<10;w++)
            {
                if(level==0 && side==-1 && (w==4 || w==5)) continue;
                float x=-18+w*4;
                var window=Prefab(Resolve("Assets/Stair Prefab/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab"),structure);
                window.name=$"Wall_3m_Window_H {level+1}F {(side>0?"North":"South")} {w+1}";
                var mesh=window.GetComponent<MeshFilter>().sharedMesh;
                window.transform.rotation=Quaternion.Euler(0,side>0?180:0,0);
                window.transform.localScale=new Vector3(4.015f/mesh.bounds.size.x,3.6f/mesh.bounds.size.y,1.5f);
                var bounds=BoundsOf(window); window.transform.position+=new Vector3(x,y+1.8f,side*10)-bounds.center;
                var renderer=window.GetComponent<MeshRenderer>(); var materials=renderer.sharedMaterials;
                for(int m=0;m<materials.Length;m++) materials[m]=m==2?glass:(m==1?dark:cream);
                renderer.sharedMaterials=materials;
                SetBool(window.GetComponent<RuntimeFireWindow>(),"physicalHandInteraction",true);
            }
            Cube("Continuous facade header",structure,new Vector3(0,y+3.68f,side*10),new Vector3(40.12f,.26f,.6f),accent);
            Cube("Continuous facade sill",structure,new Vector3(0,y+.72f,side*10.22f),new Vector3(40.12f,.12f,.35f),teal);
            for(int room=0;room<4;room++)
            {
                float cx=-15+room*10;
                string type=side>0?"教室":room==0?"卫生间":room==1?(level==0?"门厅":level==1?"避难区":"阅览室"):room==2?"电梯":"实验室";
                var root=Group($"{level+1}{(side>0?'0':'1')}{room+1} {type}",furnishing);
                if(type=="电梯") { RevisedElevator(root,new Vector3(cx,y,-2.5f)); continue; }
                if(type=="门厅") { Bench(root,new Vector3(cx-1,y,-7)); continue; }
                RevisedPartition(root,cx,y,side,accent);
                Sign($"{level+1}{(side>0?'0':'1')}{room+1} · {type}",root,new Vector3(cx+2.8f,y+2.88f,side*1.88f),side>0?0:180,new Vector2(1.6f,.33f),teal,.13f);
                if(type=="卫生间") RevisedRestroom(root,cx,y,level);
                else if(type=="阅览室") RevisedLibrary(root,cx,y,level,zones);
                else if(type=="避难区") BuildRefuge(root,zones,new Vector3(cx,y,-6));
                else FurnishRoom(root,cx,y,side,type=="实验室"?"SCIENCE LAB":"CLASSROOM",level);
            }
            for(int x=-20;x<=20;x+=10)
                Cube("Sealed room divider",structure,new Vector3(x,y+1.8f,side*6),new Vector3(.22f,3.6f,8.12f),cream);
        }
        // One simple, consistently eastbound route; no competing map board or planter in its path.
        foreach(int x in new[]{-17,3,13})
        {
            ExitSign(furnishing,new Vector3(x,y+2.65f,1.86f),0,true);
            ExitSign(furnishing,new Vector3(x,y+2.65f,-1.86f),180,false);
        }
        foreach(int x in new[]{-16,-8,0,8,16}) FloorArrow(furnishing,new Vector3(x,y+.015f,0),90,green);
        for(int x=-18;x<=18;x+=6)
        {
            Cube("Ceiling housing",structure,new Vector3(x,y+3.53f,0),new Vector3(1.8f,.055f,.45f),dark,false,false);
            Cube("Ceiling diffuser",structure,new Vector3(x,y+3.49f,0),new Vector3(1.64f,.025f,.32f),white,false,false);
            if(x==-18 || x==0 || x==18) FillLight(structure,new Vector3(x,y+3,0),9,1.6f);
        }
        if(level==0)
        {
            // Actual open passage from corridor to main entrance, not a pair of lobby rooms.
            foreach(int side in new[]{-1,1}) Cube("Entrance wall infill",structure,new Vector3(side*3,1.8f,-10),new Vector3(2.1f,3.6f,.6f),cream);
            Cube("Entrance header",structure,new Vector3(0,3.16f,-10),new Vector3(4.1f,.9f,.6f),teal);
            var refugePartition=furnishing.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("112 "));
            if(refugePartition!=null) foreach(var t in refugePartition.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Corridor wall") || t.name=="Door lintel" || t.name=="Interactive classroom door").ToArray()) if(t!=null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            // A clearly defined 3.8m-wide lobby channel through the two south-centre bays.
            foreach(var t in structure.GetComponentsInChildren<Transform>().Where(t=>t.name=="Sealed room divider" && Mathf.Abs(t.position.x)<.1f && t.position.z<0).ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            Cube("Lobby west boundary",structure,new Vector3(-2.05f,1.8f,-6),new Vector3(.16f,3.6f,8),cream);
            ExitSign(furnishing,new Vector3(0,2.82f,-9.66f),180,false, true);
            BuildDoubleDoor(furnishing,new Vector3(0,0,-10));
        }
    }
    private static void RevisedPartition(Transform parent,float cx,float y,int side,Material accent)
    { Partition(parent,cx,y,side,accent); }

    private static void ExitSign(Transform parent,Vector3 p,float yaw,bool arrowRight,bool straight=false)
    {
        var sign=Sign("EXIT",parent,p,yaw,new Vector2(1.18f,.34f),green,.2f);
        sign.GetComponentInChildren<TextMeshPro>().rectTransform.anchoredPosition=new Vector2(-.23f,0);
        sign.GetComponentInChildren<TextMeshPro>().rectTransform.sizeDelta=new Vector2(.68f,.34f);
        // Solid geometric arrow: independent of font glyph coverage or language.
        var arrow=Group("Unambiguous exit arrow",sign); arrow.localPosition=new Vector3(.39f,0,-.046f);
        arrow.localRotation=Quaternion.Euler(0,0,straight?-90:arrowRight?0:180);
        var shaft=Cube("Direction shaft",arrow,Vector3.zero,new Vector3(.2f,.045f,.015f),white,false,false); shaft.transform.localPosition=Vector3.zero; shaft.transform.localRotation=Quaternion.identity;
        foreach(int s in new[]{-1,1}) { var wing=Cube("Direction head",arrow,Vector3.zero,new Vector3(.13f,.045f,.015f),white,false,false); wing.transform.localPosition=new Vector3(.075f,s*.033f,0); wing.transform.localRotation=Quaternion.Euler(0,0,-s*45); }
    }
    private static void RevisedElevator(Transform parent,Vector3 p)
    {
        float westWidth=p.y<.1f?1:3;
        Cube("Flush corridor lift wall west",parent,p+new Vector3(-2-westWidth*.5f,1.8f,0),new Vector3(westWidth,3.6f,.22f),cream);
        Cube("Flush corridor lift wall east",parent,p+new Vector3(3.5f,1.8f,0),new Vector3(3,3.6f,.22f),cream);
        var root=Group("Corridor elevator bank",parent); root.position=p;
        var steel=Mat("Brushed lift steel",new Color(.47f,.52f,.54f));
        Cube("Elevator shaft rear",root,p+new Vector3(0,1.8f,-1.55f),new Vector3(4,3.6f,.22f),cream);
        foreach(int side in new[]{-1,1})
        {
            Cube("Shaft side wall",root,p+new Vector3(side*2,1.8f,-.8f),new Vector3(.22f,3.6f,1.7f),cream);
            Cube("Brushed steel jamb",root,p+new Vector3(side*1.3f,1.37f,0),new Vector3(.14f,2.74f,.12f),steel);
            Cube("Closed lift door",root,p+new Vector3(side*.62f,1.35f,-.025f),new Vector3(1.22f,2.7f,.1f),steel);
            for(int groove=0;groove<8;groove++) Cube("Door brushing",root,p+new Vector3(side*.62f,.3f+groove*.29f,.033f),new Vector3(1.18f,.005f,.004f),Mat("Steel brushing",new Color(.4f,.44f,.46f)),false,false);
        }
        Cube("Elevator header",root,p+new Vector3(0,3.15f,0),new Vector3(4.1f,.9f,.22f),cream);
        Cube("Button steel plate",root,p+new Vector3(1.62f,1.25f,.02f),new Vector3(.23f,.55f,.08f),dark);
        var up=Cube("up",root,p+new Vector3(1.62f,1.4f,.09f),new Vector3(.14f,.14f,.035f),steel); up.AddComponent<CampusElevatorButton>();
        var down=Cube("down",root,p+new Vector3(1.62f,1.14f,.09f),new Vector3(.14f,.14f,.035f),steel); down.AddComponent<CampusElevatorButton>();
        Sign("▲",root,p+new Vector3(1.62f,1.4f,.125f),180,new Vector2(.11f,.1f),dark,.07f);
        Sign("▼",root,p+new Vector3(1.62f,1.14f,.125f),180,new Vector2(.11f,.1f),dark,.07f);
        Sign((Mathf.RoundToInt(p.y/3.8f)+1)+"F",root,p+new Vector3(0,2.93f,.13f),180,new Vector2(.58f,.25f),dark,.18f);
        Sign("电梯 / LIFT",root,p+new Vector3(0,3.37f,.13f),180,new Vector2(1.4f,.26f),teal,.13f);
        var safety=root.gameObject.AddComponent<FireSafetyElevator>(); Set(safety,"upButton",up.GetComponent<Collider>()); Set(safety,"downButton",down.GetComponent<Collider>());
        ConfigureSupport(root.GetComponent<FireSafetyPrefabSupport>());
        foreach(var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic=false;
        if(Mathf.Abs(p.y)<.1f) PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Prefabs/Campus/CorridorElevator.prefab");
    }
    private static void ConfigureSupport(FireSafetyPrefabSupport support)
    { Set(support,"warningFont",AssetDatabase.LoadAssetAtPath<Font>("Assets/Scripts/Interaction/Resources/NotoSansCJKsc-Regular.otf")); Set(support,"rayMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Interaction/Resources/FireSafetyRay.mat")); }

    private static void PlainTable(Transform root,Vector3 p,Vector2 size)
    {
        Cube("Reading tabletop — no plumbing",root,p+Vector3.up*.75f,new Vector3(size.x,.075f,size.y),wood);
        foreach(int x in new[]{-1,1}) foreach(int z in new[]{-1,1}) Cube("Table leg",root,p+new Vector3(x*(size.x*.5f-.12f),.35f,z*(size.y*.5f-.12f)),new Vector3(.06f,.7f,.06f),dark);
    }
    private static void RevisedLibrary(Transform root,float cx,float y,int level,Transform zones)
    {
        // Four wall cases plus six aisle cases; central 1.3m passage stays open.
        for(int i=0;i<4;i++) FacingBookshelf(root,new Vector3(cx-3.45f+i*2.25f,y,-9.08f),180);
        for(int row=0;row<3;row++) foreach(int side in new[]{-1,1}) FacingBookshelf(root,new Vector3(cx+side*3.55f,y,-8.1f+row*2),side<0?-90:90);
        foreach(int side in new[]{-1,1})
        {
            PlainTable(root,new Vector3(cx+side*1.55f,y,-5.2f),new Vector2(1.8f,1.2f));
            foreach(int seat in new[]{-1,1}) Prop("chair",root,new Vector3(cx+side*1.55f,y,-5.2f+seat*.88f),.92f,seat>0?180:0);
        }
        FillLight(root,new Vector3(cx,y+3.1f,-6),8,1.3f);
        if(level==3)
        {
            var firePoint=Group("4F reading room fire origin",root); firePoint.position=new Vector3(cx-3.5f,y+.15f,-7.45f);
            Cube("Charred lower bookcase",firePoint,firePoint.position+new Vector3(0,.4f,0),new Vector3(1.3f,.8f,.45f),Mat("Charred wood",new Color(.075f,.055f,.04f)));
            FireAndSmoke(firePoint,firePoint.position);
            SmokeVolume(zones,new Vector3(cx,y+1.8f,-6),new Vector3(9.3f,3.6f,7.7f));
            SmokeVolume(zones,new Vector3(-5,y+2.15f,0),new Vector3(14,3.25f,3.7f));
            SmokeParticles(root,new Vector3(cx,y+2.8f,-5.6f),new Vector3(7,.1f,5),true);
        }
    }
    private static void FacingBookshelf(Transform parent,Vector3 p,float yaw)
    { var bookcase=Group("Reading bookcase",parent); bookcase.position=p; Bookshelf(bookcase,p); bookcase.rotation=Quaternion.Euler(0,yaw,0); }
    private static void ImportedFixture(string path,Transform parent,Vector3 p,float targetHeight,float yaw=0)
    {
        var go=Prefab(path,parent); go.transform.rotation=Quaternion.Euler(0,yaw,0); var b=BoundsOf(go);
        go.transform.localScale*=targetHeight/Mathf.Max(.01f,b.size.y); b=BoundsOf(go); go.transform.position+=p-new Vector3(b.center.x,b.min.y,b.center.z); ConvertPropMaterials(go);
        foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true)) if(component!=null) UnityEngine.Object.DestroyImmediate(component);
        foreach(var collider in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        var box=go.AddComponent<BoxCollider>(); b=BoundsOf(go); box.center=go.transform.InverseTransformPoint(b.center);
        box.size=new Vector3(b.size.x/go.transform.lossyScale.x,b.size.y/go.transform.lossyScale.y,b.size.z/go.transform.lossyScale.z);
    }
    private static void RevisedRestroom(Transform root,float cx,float y,int level)
    {
        var tile=Mat("Restroom tile",new Color(.8f,.86f,.86f));
        Cube("Restroom tiled floor",root,new Vector3(cx,y+.008f,-6),new Vector3(9.7f,.016f,7.7f),tile,false,false);
        for(int cubicle=0;cubicle<3;cubicle++)
        {
            float x=cx-3+2*cubicle;
            ImportedFixture("Assets/ThirdParty/Art/BasicAssets/Apartment Kit/_Prefabs/Furniture/Bathroom/Toilet_Apt_01.prefab",root,new Vector3(x,y,-8.9f),.84f,180);
            Cube("Cubicle divider",root,new Vector3(x-1,y+1.1f,-8.1f),new Vector3(.08f,2.2f,2.65f),tile);
            foreach(int side in new[]{-1,1}) Cube("Cubicle front panel",root,new Vector3(x+side*.725f,y+1.1f,-6.8f),new Vector3(.55f,2.2f,.085f),tile);
            BuildDoor(root,new Vector3(x,y,-6.8f),180,.9f,2.15f,false);
            Primitive("Toilet paper roll",PrimitiveType.Cylinder,root,new Vector3(x-.75f,y+.8f,-7.8f),new Vector3(.12f,.08f,.12f),white,false);
        }
        Cube("Cubicle end divider",root,new Vector3(cx+2,y+1.1f,-8.1f),new Vector3(.08f,2.2f,2.65f),tile);
        for(int i=0;i<2;i++)
        {
            Cube("Mirror",root,new Vector3(cx-3.8f+i*1.12f,y+1.55f,-2.14f),new Vector3(.82f,.85f,.03f),Mat("Mirror glass",new Color(.65f,.78f,.81f)),false,false);
            Cube("Soap dispenser",root,new Vector3(cx-3.8f+i*1.12f,y+1.15f,-2.19f),new Vector3(.12f,.22f,.09f),white,false,false);
        }
        // The open basins, lever-controlled taps and reusable dry towel are installed
        // by UpgradeRestroomWaterAndTowels after the floor structure is built.
        FillLight(root,new Vector3(cx,y+3.1f,-6),8,1.5f);
    }
    private static void SmokeVolume(Transform parent,Vector3 p,Vector3 size)
    { var zone=Group("Smoke exposure volume",parent); zone.position=p; var box=zone.gameObject.AddComponent<BoxCollider>(); box.size=size; box.isTrigger=true; zone.gameObject.AddComponent<CampusSmokeVolume>(); }

    private static Material ParticleMaterial(string name,string texture,bool additive)
    {
        string path="Assets/Art/Campus/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(material,path); }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture)); material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Surface",1); material.SetFloat("_Blend",additive?2:0); material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend",(float)(additive?UnityEngine.Rendering.BlendMode.One:UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)); material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000; EditorUtility.SetDirty(material); return material;
    }
    private static ParticleSystem SmokeParticles(Transform parent,Vector3 p,Vector3 spread,bool ceiling)
    {
        var go=new GameObject(ceiling?"Ceiling smoke layer":"Rising textured smoke"); go.transform.SetParent(parent,false); go.transform.position=p;
        var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.startLifetime=ceiling?8:6; main.startSpeed=.35f; main.startSize=new ParticleSystem.MinMaxCurve(.8f,1.7f); main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2); main.maxParticles=ceiling?100:85; main.prewarm=true;
        main.startColor=new Color(.29f,.3f,.31f,.48f); main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var emission=ps.emission; emission.rateOverTime=ceiling?10:12;
        var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=spread;
        var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.Local; velocity.y=new ParticleSystem.MinMaxCurve(ceiling?.035f:.24f,ceiling?.035f:.24f); velocity.x=new ParticleSystem.MinMaxCurve(-.08f,.08f); velocity.z=new ParticleSystem.MinMaxCurve(-.08f,.08f);
        var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,1.8f));
        var colour=ps.colorOverLifetime; colour.enabled=true; var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.2f),new GradientAlphaKey(.25f,.8f),new GradientAlphaKey(0,1)}); colour.color=gradient;
        var sheet=ps.textureSheetAnimation; sheet.enabled=true; sheet.numTilesX=8; sheet.numTilesY=8; sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial=ParticleMaterial("Textured smoke","Assets/ThirdParty/Art/Effects/SmokeSquence_028_c.tga",false);
        return ps;
    }
    private static void FireAndSmoke(Transform parent,Vector3 p)
    {
        for(int layer=0;layer<2;layer++)
        {
            var go=new GameObject(layer==0?"Orange flame tongues":"Hot flame core"); go.transform.SetParent(parent,false); go.transform.position=p+Vector3.up*.25f;
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1.25f); main.startSpeed=new ParticleSystem.MinMaxCurve(.5f,1.4f); main.startSize=new ParticleSystem.MinMaxCurve(layer==0?.6f:.3f,layer==0?1.2f:.65f); main.startColor=layer==0?new Color(1,.53f,.12f,.8f):new Color(1,.88f,.5f,.7f); main.maxParticles=70; main.prewarm=true;
            var emission=ps.emission; emission.rateOverTime=layer==0?42:28;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=12; shape.radius=.42f; shape.rotation=new Vector3(-90,0,0);
            var colour=ps.colorOverLifetime; colour.enabled=true; var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.22f,.02f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.12f),new GradientAlphaKey(0,1)}); colour.color=gradient;
            var sheet=ps.textureSheetAnimation; sheet.enabled=true; sheet.numTilesX=4; sheet.numTilesY=4; sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
            var noise=ps.noise; noise.enabled=true; noise.strength=.13f; noise.frequency=.8f; noise.scrollSpeed=.6f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=ParticleMaterial("Animated flames","Assets/ThirdParty/Art/Effects/FireSquence_030_c.tga",true);
        }
        SmokeParticles(parent,p+Vector3.up*1.2f,new Vector3(.75f,.15f,.55f),false);
        var light=new GameObject("Fire glow"); light.transform.SetParent(parent,false); light.transform.position=p+Vector3.up*1.2f;
        var point=light.AddComponent<Light>(); point.type=LightType.Point; point.color=new Color(1,.42f,.12f); point.range=5; point.intensity=2.2f; point.shadows=LightShadows.None; light.AddComponent<CampusFireFlicker>();
    }
    private static void FinishStairConnections(Transform architecture)
    {
        foreach(int side in new[]{-1,1})
        {
            float x=side*20;
            Cube("Tower inside boundary — sealed joint",architecture,new Vector3(x,7.5f,3.4f),new Vector3(.25f,15.2f,4.7f),cream);
            Cube("Corridor end return — sealed joint",architecture,new Vector3(x,7.5f,-1.55f),new Vector3(.25f,15.2f,.95f),cream);
            for(int level=0;level<4;level++) Cube("Flush corridor landing join",architecture,new Vector3(x,level*Storey-.1f,0),new Vector3(.35f,.2f,2.2f),floor);
        }
        // Replace the toy red panel and unrelated stair fire with a believable toppled storage pile.
        foreach(var t in architecture.GetComponentsInChildren<Transform>().Where(t=>t.name=="3F A blocked training gate" || t.name.StartsWith("Sign ROUTE BLOCKED") || t.name=="Contained fire cue (blocked stair)").ToArray()) if(t!=null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        var pile=Group("3F Stair A collapsed storage pile",architecture);
        Vector3 p=new Vector3(-21.55f,7.6f,1.05f);
        var cabinet=Cube("Toppled storage cabinet",pile,p+new Vector3(0,.52f,.5f),new Vector3(2.15f,1.05f,.8f),wood); cabinet.transform.rotation=Quaternion.Euler(8,14,-12);
        for(int i=0;i<6;i++) { var box=Cube("Cardboard archive carton",pile,p+new Vector3((i%3-1)*.57f,.3f+i/3*.62f,i%2*.52f),new Vector3(.65f,.57f,.6f),Mat("Archive cardboard",new Color(.52f,.39f,.25f))); box.transform.rotation=Quaternion.Euler(i*3,i*17,i%2*12); }
        for(int i=0;i<4;i++) { var board=Cube("Broken shelf timber",pile,p+new Vector3(0,.75f+i*.14f,.32f),new Vector3(2.35f,.09f,.22f),wood); board.transform.rotation=Quaternion.Euler(5,i*14,-15+i*12); }
        Prop("chair",pile,p+new Vector3(.55f,.4f,.9f),.95f,120);
        var blocker=Group("Debris physical blockage",pile); blocker.position=p+new Vector3(0,1,.42f); var collider=blocker.gameObject.AddComponent<BoxCollider>(); collider.size=new Vector3(2.25f,2,1.25f);
        // Keep the warning in front of the tilted boards, rather than buried in the pile.
        Sign("封闭 / CLOSED",pile,p+new Vector3(0,1.7f,-.7f),0,new Vector2(1.65f,.4f),red,.16f);
    }
    private static void UpgradePlayerControls()
    {
        foreach(var old in systems.GetComponentsInChildren<VRTeleporter>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in systems.GetComponentsInChildren<VRMenuRayPointer>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in systems.GetComponentsInChildren<VRFireSafetyInteraction>(true)) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach(var old in systems.GetComponentsInChildren<LineRenderer>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in UnityEngine.Object.FindObjectsOfType<TeleportArea>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in systems.GetComponentsInChildren<VRControlTutorial>(true)) UnityEngine.Object.DestroyImmediate(old);
        foreach(var dummy in systems.GetComponentsInChildren<DummyController>(true))
        { SetBool(dummy,"contactInteractionOnly",true); var hand=dummy.gameObject.AddComponent<CampusHandInteractor>(); SetInt(hand,"inputSource",dummy.name.Contains("Left")?(int)SteamVR_Input_Sources.LeftHand:(int)SteamVR_Input_Sources.RightHand); }
        foreach(var door in UnityEngine.Object.FindObjectsOfType<DoorInteraction>(true))
        {
            var so=new SerializedObject(door); bool doubleDoor=so.FindProperty("doorType").enumValueIndex==1;
            string[] panels=doubleDoor?new[]{"leftDoorPanel","rightDoorPanel"}:new[]{"singleDoorPanel"};
            foreach(string name in panels) { var pivot=so.FindProperty(name).objectReferenceValue as Transform; if(pivot!=null) pivot.gameObject.AddComponent<CampusHingeInteractable>().Configure(pivot,name=="rightDoorPanel"?-1:1,false); }
            foreach(var trigger in door.GetComponentsInChildren<DoorProximityTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger.gameObject);
            var hint=so.FindProperty("interactionHint").objectReferenceValue as GameObject; if(hint!=null) UnityEngine.Object.DestroyImmediate(hint);
            UnityEngine.Object.DestroyImmediate(door);
        }
        var rig=head.GetComponentInParent<SteamVR_PlayArea>().gameObject;
        var bodyController=rig.AddComponent<CharacterController>(); bodyController.radius=.23f; bodyController.height=1.65f; bodyController.center=new Vector3(0,.825f,0); bodyController.stepOffset=.24f; bodyController.slopeLimit=48; bodyController.skinWidth=.025f;
        var locomotion=rig.AddComponent<CampusLocomotion>(); Set(locomotion,"trackedHead",head);
        var footAudio=rig.AddComponent<AudioSource>(); footAudio.playOnAwake=false; footAudio.spatialBlend=0; footAudio.volume=.5f; Set(locomotion,"footsteps",footAudio);
        SetArray(locomotion,"footstepClips",CreateFootsteps().Cast<UnityEngine.Object>().ToArray());
        var experience=systems.GetComponentInChildren<CampusExperience>(); var messages=experience.gameObject.AddComponent<CampusMessagePresenter>(); Set(messages,"font",font);
        Set(experience.gameObject.AddComponent<CampusSmokeExposure>(),"trackedHead",head);
        SetFloat(systems.GetComponentInChildren<HealthTimeController>(),"baseDrainPerSecond",0);
        var ui=systems.GetComponentInChildren<SystemUIView>(); var gameplay=new SerializedObject(ui).FindProperty("gameplayUIRoot").objectReferenceValue as GameObject;
        var healthAnchor=gameplay.GetComponent<WorldSpaceUIAnchor>(); SetVector(healthAnchor,"headLocalOffset",new Vector3(0,.27f,1));
        var floorLabel=new SerializedObject(experience).FindProperty("floorLabel").objectReferenceValue as TMP_Text; floorLabel.text="4F  ·  HEALTH / 血量";
        CreateMinimap(gameplay.transform);
        foreach(var script in systems.GetComponentsInChildren<MonoBehaviour>(true).Where(s=>s!=null && s.GetType().Name.Contains("SnapTurn")).ToArray()) UnityEngine.Object.DestroyImmediate(script);
    }
    private static AudioClip[] CreateFootsteps()
    {
        Folder("Assets/Audio/Campus"); var clips=new AudioClip[3];
        for(int clip=0;clip<3;clip++)
        {
            string path="Assets/Audio/Campus/Footstep_"+clip+".wav";
            if(!File.Exists(path))
            {
                int samples=11025; var random=new System.Random(150+clip);
                using(var writer=new BinaryWriter(File.Open(path,FileMode.Create)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+samples*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples*2);
                    float low=0; for(int i=0;i<samples;i++) { float t=i/44100f; low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.18f); float heel=Mathf.Exp(-t*32), sole=t>.048f?Mathf.Exp(-(t-.048f)*45):0; float value=(low*.5f+Mathf.Sin(t*(110+clip*7)*Mathf.PI*2)*.3f)*(heel+sole*.55f); writer.Write((short)(Mathf.Clamp(value,-1,1)*18000)); }
                }
                AssetDatabase.ImportAsset(path);
            }
            clips[clip]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return clips;
    }
    private static Image MapImage(string name,Transform parent,Vector2 p,Vector2 size,Color colour)
    { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var image=go.AddComponent<Image>(); image.color=colour; image.raycastTarget=false; image.rectTransform.sizeDelta=size; image.rectTransform.anchoredPosition=p; return image; }
    private static void CreateMinimap(Transform gameplay)
    {
        var map=new GameObject("Live minimap — lower left",typeof(RectTransform),typeof(Canvas)); map.transform.SetParent(gameplay,false); map.transform.localScale=Vector3.one*.002f; map.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var rect=map.GetComponent<RectTransform>(); rect.sizeDelta=new Vector2(268,160);
        var anchor=map.AddComponent<WorldSpaceUIAnchor>(); Set(anchor,"trackedHead",head); Set(anchor,"presentationRoot",map.transform); SetBool(anchor,"followHead",true); SetVector(anchor,"headLocalOffset",new Vector3(-.35f,-.3f,.82f));
        MapImage("Map background",map.transform,Vector2.zero,new Vector2(268,160),new Color(.03f,.09f,.1f,.93f));
        MapImage("Corridor",map.transform,Vector2.zero,new Vector2(190,18),new Color(.5f,.68f,.64f));
        foreach(int side in new[]{-1,1}) for(int room=0;room<4;room++) MapImage("Room",map.transform,new Vector2(-69+room*46,side*27),new Vector2(44,32),new Color(.22f,.35f,.38f));
        foreach(int side in new[]{-1,1}) MapImage(side>0?"Stair B":"Stair A",map.transform,new Vector2(side*106,12),new Vector2(25,35),side>0?new Color(.18f,.7f,.4f):new Color(.82f,.36f,.25f));
        var floorLabel=UItext("Map floor",map.transform,new Vector2(240,25),15); floorLabel.rectTransform.anchoredPosition=new Vector2(0,62);
        var a=UItext("A",map.transform,new Vector2(23,23),17); a.text="A"; a.rectTransform.anchoredPosition=new Vector2(-106,12);
        var b=UItext("B",map.transform,new Vector2(23,23),17); b.text="B"; b.rectTransform.anchoredPosition=new Vector2(106,12);
        var hint=UItext("Map key",map.transform,new Vector2(240,20),12); hint.text="● YOU / 位置   B → EXIT"; hint.rectTransform.anchoredPosition=new Vector2(0,-65);
        var player=MapImage("Player position",map.transform,Vector2.zero,new Vector2(7,7),new Color(1,.8f,.2f));
        var direction=MapImage("Player facing",player.transform,new Vector2(0,5),new Vector2(2,5),Color.white);
        var mini=map.AddComponent<CampusMinimap>(); Set(mini,"trackedHead",head); Set(mini,"playerDot",player.rectTransform); Set(mini,"floorText",floorLabel); mini.Refresh();
        // All geometry except labels and player belongs to a floor-plan group; ground level adds the plaza.
        var building=new GameObject("Building plan",typeof(RectTransform)); building.transform.SetParent(map.transform,false);
        foreach(var child in map.GetComponentsInChildren<Image>().Where(i=>i.name=="Corridor" || i.name=="Room" || i.name.StartsWith("Stair ")).ToArray()) child.transform.SetParent(building.transform,false);
        a.transform.SetParent(building.transform,false); b.transform.SetParent(building.transform,false);
        var outside=MapImage("Outdoor assembly",map.transform,new Vector2(0,-37.3f),new Vector2(55,18.7f),new Color(.13f,.47f,.32f)); outside.transform.SetSiblingIndex(1);
        Set(mini,"buildingPlan",building.GetComponent<RectTransform>()); Set(mini,"outdoorArea",outside.gameObject); Set(mini,"stairA",building.GetComponentsInChildren<Image>().First(i=>i.name=="Stair A")); mini.Refresh();
        player.transform.SetAsLastSibling();
    }
    private static void ConfigurePhysicalWindowAsset()
    {
        // Preserve drag-and-drop window functionality with the same hand-driven hinge mode.
        var asset=PrefabUtility.LoadPrefabContents(FireSafetyPrefabSetup.WindowPath);
        try {
            SetBool(asset.GetComponent<RuntimeFireWindow>(),"physicalHandInteraction",true);
            var renderer=asset.GetComponent<MeshRenderer>(); var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++) materials[i]=i==2?glass:i==1?dark:cream; renderer.sharedMaterials=materials;
            PrefabUtility.SaveAsPrefabAsset(asset,FireSafetyPrefabSetup.WindowPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(asset); }
    }
    private static void RecordGeneratedPrefabOverrides()
    {
        // Direct editor assignments need explicit overrides before the source prefab is reimported.
        foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            if(!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            foreach(var renderer in t.GetComponents<Renderer>()) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
    public static void GenerateWalkingActions()
    { SteamVR_Input.InitializeFile(true,false); SteamVR_Input_Generator.BeginGeneration(); Debug.Log("CAMPUS_WALK_ACTIONS_GENERATED"); }
}
