using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CampusSceneBuilder
{
    [MenuItem("Tools/Campus/Remove Corridor Extinguishers (keeps other objects)")]
    public static void RemoveCorridorExtinguishers()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup="Assets/Scenes/Legacy/CampusTraining_v5_PreExtinguisherRemoval_Backup.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup)) throw new IOException("Cannot back up campus scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var details=GameObject.Find("06 Campus Environmental Details");
        var targets=details!=null?details.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Wall-mounted extinguisher detail").ToArray():Array.Empty<Transform>();
        foreach(var target in targets) UnityEngine.Object.DestroyImmediate(target.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save corridor extinguisher removal.");
        AssetDatabase.SaveAssets(); Validate();
        Debug.Log("CORRIDOR_EXTINGUISHERS_REMOVED count="+targets.Length+"; other objects and interactions retained");
    }
    [MenuItem("Tools/Campus/Apply Natural Smoke and Foreground UI (keeps scene layout)")]
    public static void ApplyPresentationUpdate()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string backup="Assets/Scenes/Legacy/CampusTraining_v4_PrePresentation_Backup.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup)) throw new IOException("Cannot back up campus scene.");
        EditorSceneManager.OpenScene(ScenePath);
        ConfigureComfortAndSmoke();
        RecordGeneratedPrefabOverrides(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new IOException("Cannot save campus presentation update.");
        ConfigureMainMenuForeground();
        AssetDatabase.SaveAssets(); Validate();
        Debug.Log("CAMPUS_PRESENTATION_UPDATE_COMPLETE");
    }
    private static void ConfigureMainMenuForeground()
    {
        var menuScene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity",OpenSceneMode.Additive);
        try
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Campus/Shaders/CampusForegroundUI.shader");
            var fontShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader");
            foreach(var canvas in menuScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Where(c=>c.renderMode==RenderMode.WorldSpace))
            {
                var root=canvas.GetComponentInParent<CampusUIForeground>() ?? canvas.gameObject.AddComponent<CampusUIForeground>();
                root.Configure(shader,fontShader);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            }
            EditorSceneManager.MarkSceneDirty(menuScene);
            if(!EditorSceneManager.SaveScene(menuScene)) throw new IOException("Cannot save main-menu foreground configuration.");
        }
        finally { EditorSceneManager.CloseScene(menuScene,true); }
    }
    private static void ConfigurePresentationAndEnvironment()
    {
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Campus/Shaders/CampusForegroundUI.shader");
        var fontShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader");
        if(shader==null || fontShader==null) throw new InvalidOperationException("Foreground UI shader is missing.");
        var player=GameObject.Find("05 Player & Shared Systems");
        foreach(var canvas in player.GetComponentsInChildren<Canvas>(true))
        {
            if(canvas.GetComponentInParent<CampusUIForeground>()!=null) continue;
            canvas.gameObject.AddComponent<CampusUIForeground>().Configure(shader,fontShader);
        }
        foreach(var foreground in player.GetComponentsInChildren<CampusUIForeground>(true)) foreground.Configure(shader,fontShader);
        Set(player.GetComponentInChildren<CampusMessagePresenter>(true),"foregroundShader",shader);
        Set(player.GetComponentInChildren<CampusMessagePresenter>(true),"foregroundFontShader",fontShader);
        const string promptPath="Assets/Prefabs/Campus/HealthPrompt.prefab";
        var prompt=PrefabUtility.LoadPrefabContents(promptPath);
        try
        {
            (prompt.GetComponent<CampusUIForeground>() ?? prompt.AddComponent<CampusUIForeground>()).Configure(shader,fontShader);
            PrefabUtility.SaveAsPrefabAsset(prompt,promptPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prompt); }
        EnrichCampusEnvironment();
        UpgradeRestroomWaterAndTowels();
        ConfigureGuidedLesson();
    }
    private static void RestoreOriginalDebrisPosition()
    {
        var pile=UnityEngine.Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t=>t.name=="3F Stair A collapsed storage pile");
        if(pile==null) return;
        // Undo only our last revision's relocation; do not reposition a user's custom pile.
        if(Vector3.Distance(pile.position,new Vector3(-25.1f,9.5f,4.82f))<.02f && Mathf.Abs(pile.localScale.x-.5f)<.01f)
        {
            pile.position=Vector3.zero; pile.localScale=Vector3.one;
            foreach(Transform child in pile) child.position+=new Vector3(-21.55f,7.6f,1.05f);
        }
        // The real cabinet/carton colliders stay; never add an invisible full-width wall.
    }
    private static void EnrichCampusEnvironment()
    {
        var previous=GameObject.Find("06 Campus Environmental Details");
        if(previous!=null) UnityEngine.Object.DestroyImmediate(previous);
        var root=Group("06 Campus Environmental Details");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Resolve("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset"));
        var paper=Mat("Warm paper",new Color(.93f,.9f,.8f));
        var blue=Mat("Notebook blue",new Color(.13f,.27f,.38f));
        var foliage=Mat("Plant leaves",new Color(.19f,.32f,.18f));
        var rubber=Mat("Bicycle rubber",new Color(.05f,.055f,.06f));
        for(int level=0;level<4;level++)
        {
            float y=level*Storey; var storey=Group((level+1)+"F environment details",root);
            foreach(int side in new[]{-1,1})
            {
                for(int room=0;room<4;room++)
                {
                    // The lift bank is recessed and the ground-floor lobby is an opening,
                    // not a wall to mount a rail on.
                    if(side<0 && (room==2 || (level==0 && room==1))) continue;
                    float cx=-15+room*10;
                    Cube("Corridor wall protection rail",storey,new Vector3(cx-1.45f,y+.95f,side*1.94f),new Vector3(7.1f,.09f,.045f),teal,false,false);
                    Cube("Corridor wall protection rail",storey,new Vector3(cx+4.25f,y+.95f,side*1.94f),new Vector3(1.5f,.09f,.045f),teal,false,false);
                }
                foreach(int x in new[]{-10,10})
                {
                    // Keep alarm call points; corridor extinguishers are intentionally omitted.
                    Cube("Alarm call point",storey,new Vector3(x+.3f,y+1.25f,side*1.88f),new Vector3(.13f,.16f,.08f),red,false,false);
                }
            }
            var bench=Group("Corridor waiting bench",storey);
            Bench(bench,Vector3.zero);
            bench.SetPositionAndRotation(new Vector3(-5,y,1.42f),Quaternion.Euler(0,180,0));
            Cube("Water station cabinet",storey,new Vector3(-8,y+.48f,1.60f),new Vector3(.48f,.96f,.30f),white);
            Cube("Water station recess",storey,new Vector3(-8,y+.7f,1.43f),new Vector3(.35f,.35f,.02f),dark,false,false);
            Cube("Water station taps",storey,new Vector3(-8,y+.78f,1.38f),new Vector3(.23f,.06f,.05f),brass,false,false);
            foreach(int s in new[]{-1,1})
            {
                var bin=Group("Recycling station",storey); Vector3 p=new Vector3(11+s*.25f,y+.3f,1.60f);
                DetailCylinder("Recycling bin",bin,p,new Vector3(.35f,.3f,.35f),s<0?blue:green);
                DetailCylinder("Recycling bin lid",bin,p+Vector3.up*.32f,new Vector3(.36f,.025f,.36f),dark);
            }
            ClockDetail(storey,new Vector3(0,y+2.82f,1.82f));
            foreach(int x in new[]{-10,10}) DetailCylinder("Ceiling smoke detector detail",storey,new Vector3(x,y+3.43f,0),new Vector3(.17f,.035f,.17f),white);
            foreach(int side in new[]{-1,1}) for(int room=0;room<4;room++)
            {
                float cx=-15+room*10;
                if(side<0 && room!=3) continue;
                Vector3 desk=new Vector3(cx-2.8f,y+.80f,side*3.1f);
                Prop("a laptop",storey,desk+new Vector3(-.30f,0,0),.22f,side>0?180:0);
                NotebookDetail(storey,desk+new Vector3(.43f,0,0),blue,paper);
                var penCup=DetailCylinder("Desk pen cup",storey,desk+new Vector3(.65f,.06f,.23f),new Vector3(.08f,.06f,.08f),dark);
                for(int i=0;i<4;i++) Cube("Pencil",storey,penCup.position+new Vector3((i-1.5f)*.01f,.065f,0),new Vector3(.007f,.13f,.007f),wood,false,false);
                if(side<0)
                {
                    foreach(int sample in new[]{-1,1})
                    {
                        Vector3 p=desk+new Vector3(.1f,.1f,sample*.23f);
                        DetailCylinder("Laboratory sample vessel",storey,p,new Vector3(.07f,.10f,.07f),glass);
                        DetailCylinder("Coloured laboratory sample",storey,p-Vector3.up*.03f,new Vector3(.06f,.045f,.06f),sample<0?teal:brass);
                    }
                }
            }
            if(level>=2)
            {
                foreach(int side in new[]{-1,1})
                {
                    Vector3 table=new Vector3(-5+side*1.55f,y+.80f,-5.2f);
                    NotebookDetail(storey,table+new Vector3(.25f,0,0),wood,paper);
                    Cube("Open book left page",storey,table+new Vector3(-.28f,.013f,0),new Vector3(.22f,.02f,.28f),paper,false,false);
                    Cube("Open book right page",storey,table+new Vector3(-.04f,.013f,0),new Vector3(.22f,.02f,.28f),paper,false,false);
                    var lamp=Group("Reading desk lamp",storey); Vector3 p=table+new Vector3(-.48f,0,.4f);
                    DetailCylinder("Desk lamp base",lamp,p,new Vector3(.18f,.018f,.18f),dark);
                    Cube("Desk lamp stem",lamp,p+Vector3.up*.17f,new Vector3(.018f,.32f,.018f),brass,false,false);
                    Cube("Desk lamp shade",lamp,p+new Vector3(.05f,.34f,0),new Vector3(.22f,.06f,.13f),teal,false,false);
                    Cube("Desk lamp diffuser",lamp,p+new Vector3(.05f,.305f,0),new Vector3(.19f,.008f,.10f),white,false,false);
                }
            }
            Cube("Restroom hand dryer",storey,new Vector3(-19.8f,y+1.2f,-4.7f),new Vector3(.15f,.32f,.26f),white,false,false);
            Cube("Restroom paper dispenser",storey,new Vector3(-19.8f,y+1.3f,-5.15f),new Vector3(.13f,.28f,.23f),dark,false,false);
            Cube("Restroom floor mat",storey,new Vector3(-16.8f,y+.012f,-4.4f),new Vector3(1.35f,.02f,.65f),teal,false,false);
            var mop=Group("Restroom cleaning equipment",storey); Vector3 bucket=new Vector3(-19.15f,y+.18f,-9.15f);
            DetailCylinder("Cleaning bucket",mop,bucket,new Vector3(.32f,.18f,.32f),blue);
            var handle=Cube("Mop handle",mop,bucket+new Vector3(.07f,.45f,.03f),new Vector3(.025f,.92f,.025f),wood,false,false); handle.transform.rotation=Quaternion.Euler(0,0,-8);
        }
        var outdoors=Group("Campus outdoor details",root);
        for(int i=0;i<3;i++) BicycleDetail(outdoors,new Vector3(-19.5f+i*1.6f,.05f,-12),rubber);
        foreach(int side in new[]{-1,1}) foreach(int z in new[]{-17,-21,-25})
        {
            Vector3 p=new Vector3(side*5.4f,.35f,z);
            Cube("Path bollard",outdoors,p,new Vector3(.14f,.7f,.14f),dark);
            Cube("Bollard light diffuser",outdoors,p+Vector3.up*.29f,new Vector3(.15f,.13f,.15f),white,false,false);
        }
        Prop("bus",outdoors,new Vector3(22,0,-27),2.7f,90);
        foreach(int side in new[]{-1,1}) for(int i=0;i<4;i++)
        {
            Vector3 p=new Vector3(side*7,0,-17-i*3);
            var bed=Cube("Low landscape edging",outdoors,p+Vector3.up*.12f,new Vector3(1.4f,.24f,.65f),paving);
            for(int leaf=0;leaf<3;leaf++)
            {
                var bush=GameObject.CreatePrimitive(PrimitiveType.Sphere); bush.name="Landscaped shrub"; bush.transform.SetParent(outdoors,false);
                bush.transform.position=p+new Vector3((leaf-1)*.32f,.34f,0); bush.transform.localScale=new Vector3(.55f,.5f,.45f);
                bush.GetComponent<Renderer>().sharedMaterial=foliage; UnityEngine.Object.DestroyImmediate(bush.GetComponent<Collider>()); bush.isStatic=true;
            }
        }
    }
    private static Transform DetailCylinder(string name,Transform parent,Vector3 p,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetParent(parent,false);
        go.transform.position=p; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.isStatic=true; return go.transform;
    }
    private static void NotebookDetail(Transform root,Vector3 p,Material cover,Material paper)
    {
        var book=Group("Desk notebook",root);
        Cube("Notebook cover",book,p,new Vector3(.28f,.025f,.34f),cover,false,false);
        Cube("Notebook pages",book,p+Vector3.up*.02f,new Vector3(.25f,.02f,.31f),paper,false,false);
        Cube("Notebook binding",book,p+new Vector3(-.12f,.025f,0),new Vector3(.018f,.025f,.32f),dark,false,false);
    }
    private static void ClockDetail(Transform root,Vector3 p)
    {
        var clock=Group("Corridor analogue clock",root);
        var face=DetailCylinder("Clock face",clock,p,new Vector3(.4f,.015f,.4f),white); face.rotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;
            var tick=Cube("Clock hour mark",clock,p+new Vector3(Mathf.Sin(a)*.16f,Mathf.Cos(a)*.16f,-.018f),new Vector3(.018f,.04f,.008f),dark,false,false);
            tick.transform.rotation=Quaternion.Euler(0,0,-i*30);
        }
        Cube("Clock minute hand",clock,p+new Vector3(.035f,.063f,-.025f),new Vector3(.012f,.145f,.008f),dark,false,false).transform.rotation=Quaternion.Euler(0,0,-30);
        Cube("Clock hour hand",clock,p+new Vector3(0,.04f,-.03f),new Vector3(.018f,.085f,.008f),dark,false,false);
    }
    private static void BicycleDetail(Transform root,Vector3 p,Material rubber)
    {
        var bike=Group("Parked bicycle",root);
        foreach(int end in new[]{-1,1})
        {
            var wheel=DetailCylinder("Bicycle wheel",bike,p+new Vector3(0,.3f,end*.52f),new Vector3(.56f,.025f,.56f),rubber); wheel.rotation=Quaternion.Euler(0,0,90);
            var hub=DetailCylinder("Bicycle wheel hub",bike,wheel.position,new Vector3(.42f,.028f,.42f),dark); hub.rotation=wheel.rotation;
        }
        Cube("Bicycle top tube",bike,p+new Vector3(0,.65f,0),new Vector3(.04f,.04f,.66f),teal,false,false);
        foreach(int end in new[]{-1,1})
        {
            var fork=Cube("Bicycle fork",bike,p+new Vector3(0,.5f,end*.40f),new Vector3(.035f,.48f,.035f),teal,false,false); fork.transform.rotation=Quaternion.Euler(end*22,0,0);
        }
        Cube("Bicycle seat",bike,p+new Vector3(0,.79f,-.28f),new Vector3(.17f,.05f,.22f),dark,false,false);
        Cube("Bicycle handlebars",bike,p+new Vector3(0,.86f,.35f),new Vector3(.45f,.035f,.035f),dark,false,false);
    }
}
