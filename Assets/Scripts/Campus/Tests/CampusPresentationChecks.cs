#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class CampusRevisionDriver
{
    private IEnumerator PresentationChecks()
    {
        var player=GameObject.Find("05 Player & Shared Systems");
        var roots=player.GetComponentsInChildren<CampusUIForeground>(true);
        Check(roots.Length>=2 && player.GetComponentsInChildren<Canvas>(true).All(c=>c.GetComponentInParent<CampusUIForeground>()!=null),"All player canvases, including inactive menus, use protected foreground presentation");
        foreach(var root in roots) root.Refresh(); Canvas.ForceUpdateCanvases();
        foreach(var graphic in player.GetComponentsInChildren<Graphic>(true).Where(g=>!(g is TextMeshPro)))
        {
            var modifier=graphic.GetComponent<CampusUIForegroundGraphic>();
            Check(modifier!=null,"Foreground modifier covers "+graphic.name);
            if(!graphic.gameObject.activeInHierarchy) continue; // Inactive modifiers deliberately return the base material.
            var material=graphic.materialForRendering;
            Check(material.renderQueue==4000 && (material.shader.name.EndsWith("Overlay") || material.shader.name=="Campus/Foreground UI"),"UI background/font ignores wall depth: "+graphic.name);
        }
        var presenter=CampusMessagePresenter.Instance; presenter.Show(CampusMessagePresenter.WindowWarning);
        var panel=Field<Transform>(presenter,"panel"); var text=panel.GetComponent<TextMeshPro>();
        Check(panel.GetComponent<CampusUIForeground>()!=null && text.fontSharedMaterial.renderQueue==4000 && text.fontSharedMaterial.shader.name.EndsWith("Overlay"),"Runtime bilingual warning uses native SDF overlay, not merely canvas sorting order (actual: "+text.fontSharedMaterial.shader.name+", queue="+text.fontSharedMaterial.renderQueue+", configured="+Field<Shader>(panel.GetComponent<CampusUIForeground>(),"fontShader")+")");
        Check(text.fontSharedMaterial!=text.font.material && Mathf.Approximately(panel.localPosition.z,1),"Warning keeps the one-metre distance without modifying the shared font asset");
        Check(player.GetComponentsInChildren<Canvas>(true).All(c=>c.renderMode==RenderMode.WorldSpace),"VR canvases stay world-space; none are converted to Screen Space Overlay");
        var prompt=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Campus/HealthPrompt.prefab");
        Check(prompt.GetComponent<CampusUIForeground>()!=null && Field<Shader>(prompt.GetComponent<CampusUIForeground>(),"fontShader")!=null,"The dynamically-spawned health/towel notice prefab retains its foreground shaders");
        var head=Camera.main.transform; var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name="QA close wall"; wall.transform.SetPositionAndRotation(head.position+head.forward*.22f,head.rotation); wall.transform.localScale=new Vector3(4,4,.04f); Physics.SyncTransforms();
        var ui=player.GetComponentInChildren<SystemUIView>(true); ui.ShowGameOver();
        foreach(var anchor in player.GetComponentsInChildren<WorldSpaceUIAnchor>())
        {
            anchor.SendMessage("Place"); var root=Field<Transform>(anchor,"presentationRoot");
            if(root!=null && !Field<bool>(anchor,"followHead")) Check(Vector3.Distance(head.position,root.position)>.9f,"Near-wall menus preserve their comfortable display distance: "+root.name);
        }
        ui.ShowGameplay(); Destroy(wall);
        var details=GameObject.Find("06 Campus Environmental Details");
        Check(!details.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Wall-mounted extinguisher detail" || t.name.StartsWith("Extinguisher ")),"Corridor extinguisher models and their decoration parts are removed");
        Check(details!=null && details.GetComponentsInChildren<MeshFilter>().Length>=350,"The scene contains hundreds of additional environmental mesh details");
        Check(details.GetComponentsInChildren<Transform>().Count(t=>t.name=="Water station cabinet")==4 && details.GetComponentsInChildren<Transform>().Count(t=>t.name=="Corridor analogue clock")==4,"Every floor has water facilities and a corridor clock");
        Check(details.GetComponentsInChildren<Transform>().Count(t=>t.name=="Parked bicycle")==3 && details.GetComponentsInChildren<Transform>().Count(t=>t.name=="Reading desk lamp")==4,"Outdoor bicycles and reading-room task lamps enrich the setting");
        var spread=FindObjectOfType<CampusSmokeSpread>(); spread.RoomDoor.SetAngle(60); spread.ResetSpread();
        Check(spread.Stages.All(s=>s.particles.velocityOverLifetime.x.mode==s.particles.velocityOverLifetime.y.mode && s.particles.velocityOverLifetime.y.mode==s.particles.velocityOverLifetime.z.mode),"Smoke velocity axes use compatible curve modes, without particle-system errors");
        var stage=spread.Stages.First(s=>s.floor==4 && s.particles.name.Contains("corridor")); Vector3 ceiling=stage.particles.transform.position;
        spread.Advance(80);
        Check(stage.particles.transform.position.y<ceiling.y-.4f,"The smoke layer gradually lowers as the ceiling fills");
        spread.ResetSpread(); Check(Vector3.Distance(stage.particles.transform.position,ceiling)<.001f,"Reset restores the initial high smoke layer and clears exposure");
        spread.RoomDoor.SetAngle(0);
        yield return null;
    }
}
#endif
