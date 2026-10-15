using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public static partial class CampusPreview
{
    public static void RenderMainMenuForeground()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity");
        var canvas=UnityEngine.Object.FindObjectsOfType<Canvas>(true).First(c=>c.renderMode==RenderMode.WorldSpace);
        if(canvas.GetComponentInParent<CampusUIForeground>()==null) throw new InvalidOperationException("Main-menu foreground configuration is missing.");
        foreach(var c in UnityEngine.Object.FindObjectsOfType<Camera>(true)) c.enabled=false;
        var go=new GameObject("Temporary main menu QA camera"); var camera=go.AddComponent<Camera>();
        go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camera.fieldOfView=80; camera.nearClipPlane=.05f; camera.farClipPlane=150;
        Vector3 p=canvas.transform.position-canvas.transform.forward*1.2f;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="Temporary main-menu QA wall";
        wall.transform.SetPositionAndRotation(p+canvas.transform.forward*.25f,canvas.transform.rotation); wall.transform.localScale=new Vector3(8,8,.04f);
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor",new Color(.04f,.07f,.10f)); wall.GetComponent<Renderer>().sharedMaterial=material;
        canvas.enabled=false; Shot(camera,"UIWall_MainMenuBaseline",p,canvas.transform.position);
        canvas.enabled=true; Shot(camera,"UIWall_MainMenu",p,canvas.transform.position);
        RequirePixelDifference("UIWall_MainMenuBaseline","UIWall_MainMenu",new RectInt(150,100,1140,700),2500,"main menu at a close wall");
        UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(material);
        Debug.Log("MAIN_MENU_FOREGROUND_RENDER_PASS");
    }
    // Real GPU rendering with an opaque wall only 25 cm from the head. Material names
    // and canvas sorting alone cannot prove that glyphs survive depth occlusion.
    private static void RenderUIOcclusion(Camera camera,GameObject systems,Transform head,SystemUIView ui)
    {
        foreach(string name in new[]{"Campus/Foreground UI","TextMeshPro/Distance Field Overlay"})
        {
            var shader=Shader.Find(name);
            if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Foreground shader failed: "+name);
        }
        var presenter=systems.GetComponentInChildren<CampusMessagePresenter>(true);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="Temporary UI occlusion QA wall";
        wall.transform.localScale=new Vector3(8,8,.04f);
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor",new Color(.04f,.07f,.10f));
        wall.GetComponent<Renderer>().sharedMaterial=material;
        Quaternion originalRotation=head.rotation;
        var canvases=systems.GetComponentsInChildren<Canvas>(true);
        var pause=systems.GetComponentInChildren<SystemUIPauseController>(true);
        var pausePanel=(GameObject)typeof(SystemUIPauseController).GetField("pausePanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pause);
        try
        {
            foreach(int yaw in new[]{0,90,180,270})
            {
                head.rotation=Quaternion.Euler(0,yaw,0); camera.transform.SetPositionAndRotation(head.position,head.rotation);
                wall.transform.SetPositionAndRotation(head.position+head.forward*.25f,head.rotation);
                ui.ShowGameplay(); PlaceUI(systems); presenter.Show(CampusMessagePresenter.WindowWarning);
                var panel=(Transform)typeof(CampusMessagePresenter).GetField("panel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
                panel.gameObject.SetActive(false);
                foreach(var canvas in canvases) canvas.enabled=false;
                string baseline="UIWall_Baseline_"+yaw; Shot(camera,baseline,head.position,head.position+head.forward);
                foreach(var canvas in canvases) canvas.enabled=true;
                string hud="UIWall_HUD_"+yaw; Shot(camera,hud,head.position,head.position+head.forward);
                RequirePixelDifference(baseline,hud,new RectInt(350,140,300,210),800,"minimap after yaw "+yaw);
                RequirePixelDifference(baseline,hud,new RectInt(450,540,540,190),800,"health/floor HUD after yaw "+yaw);
                foreach(var canvas in canvases) canvas.enabled=false;
                panel.gameObject.SetActive(true);
                string warning="UIWall_Warning_"+yaw; Shot(camera,warning,head.position,head.position+head.forward);
                RequirePixelDifference(baseline,warning,new RectInt(100,250,1240,350),800,"bilingual warning after yaw "+yaw);
                foreach(var canvas in canvases) canvas.enabled=true;
                string combined="UIWall_Combined_"+yaw; Shot(camera,combined,head.position,head.position+head.forward);
                panel.gameObject.SetActive(false);
                if(yaw==0)
                {
                    foreach(string outcome in new[]{"Pause","GameOver","Victory"})
                    {
                        // Pause belongs to its separate controller, not SystemUIView.
                        // ShowPause is Play-mode only: activate that exact panel for editor rendering.
                        pausePanel.SetActive(false);
                        if(outcome=="Pause") { ui.ShowGameMenu(); pausePanel.SetActive(true); }
                        else typeof(SystemUIView).GetMethod("Show"+outcome).Invoke(ui,null);
                        PlaceUI(systems);
                        string result="UIWall_"+outcome; Shot(camera,result,head.position,head.position+head.forward);
                        RequirePixelDifference(baseline,result,new RectInt(200,180,1040,600),2500,outcome+" menu at a close wall");
                    }
                }
            }
            Debug.Log("UI_OCCLUSION_RENDER_PASS (4 head directions; HUD, minimap, bilingual warning and 3 menus)");
        }
        finally
        {
            foreach(var canvas in canvases) canvas.enabled=true;
            pausePanel.SetActive(false);
            head.rotation=originalRotation; ui.ShowGameplay(); PlaceUI(systems);
            UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(material);
        }
    }
    private static void RequirePixelDifference(string baseline,string result,RectInt region,int minimum,string label)
    {
        var a=new Texture2D(2,2); var b=new Texture2D(2,2);
        a.LoadImage(File.ReadAllBytes("Documentation/Previews/"+baseline+".png"));
        b.LoadImage(File.ReadAllBytes("Documentation/Previews/"+result+".png"));
        var pa=a.GetPixels32(); var pb=b.GetPixels32(); int changed=0;
        for(int y=region.yMin;y<region.yMax;y++) for(int x=region.xMin;x<region.xMax;x++)
        {
            int i=y*a.width+x;
            if(Math.Abs(pa[i].r-pb[i].r)+Math.Abs(pa[i].g-pb[i].g)+Math.Abs(pa[i].b-pb[i].b)>30) changed++;
        }
        UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
        if(changed<minimum) throw new InvalidOperationException("UI hidden by wall: "+label+" ("+changed+" visible pixels)");
        Debug.Log("UI_WALL_PIXEL_PASS "+label+" pixels="+changed);
    }
}
