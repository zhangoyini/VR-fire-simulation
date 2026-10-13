using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// VR-compatible world-space foreground, not a Screen Space Overlay canvas.
// Late refresh includes inactive outcome panels and dynamically-created fallback glyph meshes.
[DefaultExecutionOrder(10000),DisallowMultipleComponent]
public sealed class CampusUIForeground : MonoBehaviour
{
    [SerializeField] private Shader imageShader;
    [SerializeField] private Shader fontShader;
    private readonly Dictionary<Material,Material> meshMaterials=new Dictionary<Material,Material>();
    private float nextRefresh;
    public void Configure(Shader shader,Shader sdfShader=null) { imageShader=shader; fontShader=sdfShader!=null?sdfShader:Shader.Find("TextMeshPro/Distance Field Overlay"); if(Application.isPlaying) Refresh(); }
    private void OnEnable() { if(Application.isPlaying) Refresh(); }
    private void LateUpdate()
    { if(Time.unscaledTime>=nextRefresh) { nextRefresh=Time.unscaledTime+.2f; Refresh(); } }
    public void Refresh()
    {
        foreach(var canvas in GetComponentsInChildren<Canvas>(true))
        { canvas.overrideSorting=true; canvas.sortingOrder=30000; }
        foreach(var graphic in GetComponentsInChildren<Graphic>(true))
        {
            // TextMeshPro (3D) renders via MeshRenderer, unlike TextMeshProUGUI.
            if(graphic is TextMeshPro) continue;
            var modifier=graphic.GetComponent<CampusUIForegroundGraphic>() ?? graphic.gameObject.AddComponent<CampusUIForegroundGraphic>();
            modifier.Configure(imageShader,fontShader);
        }
        foreach(var text in GetComponentsInChildren<TextMeshPro>(true))
            if(text.fontSharedMaterial!=null)
            { var material=ForegroundMaterial(text.fontSharedMaterial); if(text.fontSharedMaterial!=material) text.fontSharedMaterial=material; }
        foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++) if(materials[i]!=null) materials[i]=ForegroundMaterial(materials[i]);
            renderer.sharedMaterials=materials;
        }
    }
    private Material ForegroundMaterial(Material original)
    {
        // Copies are also keys, preventing a new copy of our own copy every refresh.
        if(!meshMaterials.TryGetValue(original,out var copy))
        {
            copy=new Material(original) { name=original.name+" (VR foreground)",hideFlags=HideFlags.DontSave };
            meshMaterials[original]=copy; meshMaterials[copy]=copy;
        }
        // AddComponent invokes OnEnable before Configure supplies the shader references.
        // Reapply to cached copies too, including fallback materials rebuilt by TMP.
        // Recognise native SDF variants by name as well; never treat a font atlas as a sprite.
        if((original.shader.name.Contains("Distance Field") || copy.HasProperty("_GradientScale")) && fontShader!=null) copy.shader=fontShader;
        else if(copy.HasProperty("_ZTestMode")) copy.SetFloat("_ZTestMode",(float)CompareFunction.Always);
        copy.renderQueue=4000; return copy;
    }
    private void OnDestroy()
    {
        var unique=new HashSet<Material>(meshMaterials.Values);
        foreach(var material in unique) if(material!=null) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        meshMaterials.Clear();
    }
}
