using UnityEngine;
using UnityEngine.Rendering;

// Per-eye screen-coordinate shading on a camera-attached quad; no camera FOV or tracking changes.
public sealed class CampusComfortVignette : MonoBehaviour
{
    [SerializeField] private Shader vignetteShader;
    [SerializeField, Range(0, 1)] private float walkingStrength = .75f;
    private Material material;
    private Mesh mesh;
    private MeshRenderer overlay;
    private float demand, snapPulse;
    public float Strength { get; private set; }
    public bool IsVisible => overlay != null && overlay.enabled;
    public void Configure(Shader shader) { vignetteShader = shader; }
    private void Awake()
    {
        if (vignetteShader == null) vignetteShader = Shader.Find("Campus/Comfort Vignette");
        if (vignetteShader == null) { Debug.LogError("Comfort vignette shader is missing.", this); return; }
        material = new Material(vignetteShader);
        var go = new GameObject("Comfort peripheral overlay (runtime)");
        go.transform.SetParent(transform, false);
        var camera = GetComponent<Camera>();
        go.transform.localPosition = Vector3.forward * Mathf.Max(.35f, camera != null ? camera.nearClipPlane * 1.1f : .35f);
        mesh = new Mesh { name = "Comfort full-view quad" };
        mesh.vertices = new[] { new Vector3(-3,-3,0), new Vector3(-3,3,0), new Vector3(3,3,0), new Vector3(3,-3,0) };
        mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        overlay = go.AddComponent<MeshRenderer>(); overlay.sharedMaterial = material;
        overlay.shadowCastingMode = ShadowCastingMode.Off; overlay.receiveShadows = false;
        overlay.lightProbeUsage = LightProbeUsage.Off; overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlay.enabled = false;
    }
    public void ReportMotion(float fraction, bool snapped)
    { demand = Mathf.Clamp01(fraction) * walkingStrength; if (snapped) snapPulse = .14f; }
    public void ResetMotion() { demand = snapPulse = 0; }
    private void LateUpdate() { Tick(Time.unscaledDeltaTime); }
    public void Tick(float dt)
    {
        if (Time.timeScale == 0) ResetMotion();
        float target = snapPulse > 0 ? Mathf.Max(.85f, demand) : demand;
        snapPulse = Mathf.Max(0, snapPulse - dt);
        Strength = Mathf.MoveTowards(Strength, target, dt * 5);
        if (material != null) material.SetFloat("_Strength", Strength);
        if (overlay != null) overlay.enabled = Strength > .001f;
    }
    private void OnDisable()
    { ResetMotion(); Strength = 0; if (overlay != null) overlay.enabled = false; }
    private void OnDestroy()
    { if (overlay != null) Destroy(overlay.gameObject); if (material != null) Destroy(material); if (mesh != null) Destroy(mesh); }
}
