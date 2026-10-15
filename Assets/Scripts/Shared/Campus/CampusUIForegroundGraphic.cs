using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Material modifier runs after UGUI's stencil modifier: preserve masks and font atlas.
// Per-graphic copies never change shared project materials or world room/EXIT signs.
[DisallowMultipleComponent]
public sealed class CampusUIForegroundGraphic : MonoBehaviour, IMaterialModifier
{
    [SerializeField] private Shader imageShader;
    [SerializeField] private Shader fontShader;
    private Material source, foreground;
    public void Configure(Shader shader,Shader sdfShader=null) { if(imageShader==shader && fontShader==sdfShader) return; imageShader=shader; fontShader=sdfShader; GetComponent<Graphic>()?.SetMaterialDirty(); }
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || baseMaterial==null) return baseMaterial;
        if(baseMaterial==foreground) return foreground;
        if (source!=baseMaterial || foreground==null)
        {
            DisposeMaterial();
            source=baseMaterial; foreground=new Material(baseMaterial) { name=baseMaterial.name+" (VR foreground)",hideFlags=HideFlags.DontSave };
        }
        // Fonts keep their own SDF shader and all TMP outline/fallback/stencil properties.
        // The project's TMP shaders use unity_GUIZTestMode, NOT a _ZTestMode property.
        // Their native Overlay variant keeps the SDF atlas, outlines, masks and glyph metrics.
        if((baseMaterial.shader.name.Contains("Distance Field") || foreground.HasProperty("_GradientScale")) && fontShader!=null) foreground.shader=fontShader;
        else if(foreground.HasProperty("_ZTestMode")) foreground.SetFloat("_ZTestMode",(float)CompareFunction.Always);
        else if(imageShader!=null) foreground.shader=imageShader;
        foreground.renderQueue=4000;
        return foreground;
    }
    private void OnEnable() { GetComponent<Graphic>()?.SetMaterialDirty(); }
    private void OnDisable() { GetComponent<Graphic>()?.SetMaterialDirty(); }
    private void OnDestroy() { DisposeMaterial(); }
    private void DisposeMaterial() { if(foreground==null) return; if(Application.isPlaying) Destroy(foreground); else DestroyImmediate(foreground); foreground=null; }
}
