using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireSafetyPrefabSupport))]
public sealed class RuntimeFireWindow : MonoBehaviour
{
    [SerializeField] private Mesh readableWindowMesh;
    [SerializeField] private bool physicalHandInteraction;
    private static readonly Dictionary<Collider, RuntimeFireWindow> Panes = new Dictionary<Collider, RuntimeFireWindow>();
    private Mesh wallMesh, paneMesh;
    private Material glassMaterial;
    private Transform pivot, pane;
    private BoxCollider paneCollider;
    private BoxCollider openWindowTarget;
    private Bounds glassBounds;
    private bool open, busy;
    private float angle;
    private CampusHingeInteractable physicalHinge;
    public Transform HighlightTarget => physicalHandInteraction ? pane : open ? transform : pane;
    public bool IsOpen => physicalHinge != null ? physicalHinge.Angle > .1f : open;
    public float Reach { get; private set; }
    public bool UsesPhysicalHands => physicalHandInteraction;

    private void Awake()
    {
        Mesh source = readableWindowMesh != null ? readableWindowMesh : Resources.Load<Mesh>("FireWindowMesh");
        if (source == null)
        {
            Debug.LogError("FireWindowMesh cache is missing. Exit Play mode and allow the editor to generate it.");
            return;
        }
        Build(source, GetComponent<MeshFilter>());
    }

    public static bool TryFind(Collider collider, out RuntimeFireWindow window)
    {
        if (Panes.TryGetValue(collider, out window)) return true;
        window = collider.GetComponentInParent<RuntimeFireWindow>();
        return window != null && window.IsOpen;
    }

    private void Build(Mesh source, MeshFilter filter)
    {
        var renderer = filter.GetComponent<MeshRenderer>();
        Material[] materials = renderer.sharedMaterials;
        int glass = System.Array.FindIndex(materials, m => m != null && m.name.ToLowerInvariant().Contains("glass"));
        if (glass < 0 || glass >= source.subMeshCount)
        {
            Debug.LogError("Cannot identify window glass material.", this);
            return;
        }
        int[] triangles = source.GetTriangles(glass);
        Vector3[] vertices = source.vertices;
        if (triangles.Length == 0) return;
        glassBounds = new Bounds(vertices[triangles[0]], Vector3.zero);
        foreach (int index in triangles) glassBounds.Encapsulate(vertices[index]);

        wallMesh = Instantiate(source);
        wallMesh.name = "Window wall (runtime)";
        wallMesh.SetTriangles(new int[0], glass);
        filter.sharedMesh = wallMesh;
        // A convex collider would fill the window opening again.
        foreach (Collider collider in GetComponents<Collider>()) collider.enabled = false;
        var wallCollider = gameObject.AddComponent<MeshCollider>();
        wallCollider.sharedMesh = wallMesh;
        wallCollider.convex = false;

        // Ray-only target in the original opening, including when the glass has swung away.
        // A trigger avoids reintroducing a solid, invisible wall across the open window.
        openWindowTarget = gameObject.AddComponent<BoxCollider>();
        openWindowTarget.center = glassBounds.center;
        Vector3 openingSize = glassBounds.size;
        openingSize.z = Mathf.Max(openingSize.z, 0.025f);
        openWindowTarget.size = openingSize;
        openWindowTarget.isTrigger = true;
        openWindowTarget.enabled = false;

        pivot = new GameObject("Window right hinge (runtime)").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pivot.gameObject, gameObject.scene);
        pivot.position = transform.TransformPoint(glassBounds.center);
        pivot.rotation = Quaternion.LookRotation(transform.forward, transform.up);
        pane = new GameObject("Opening glass (runtime)").transform;
        pane.SetParent(pivot, false);
        pane.gameObject.layer = gameObject.layer;
        // Compact mesh: its bounds must contain only glass, not unused wall vertices.
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        Vector3[] sourceNormals = source.normals;
        Vector2[] sourceUV = source.uv;
        var remap = new Dictionary<int, int>();
        foreach (int index in triangles)
        {
            if (!remap.TryGetValue(index, out int mapped))
            {
                mapped = positions.Count;
                remap.Add(index, mapped);
                positions.Add(pivot.InverseTransformPoint(transform.TransformPoint(vertices[index])));
                Vector3 normal = sourceNormals.Length == vertices.Length ? sourceNormals[index] : Vector3.forward;
                normals.Add(pivot.InverseTransformDirection(transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normal).normalized));
                uvs.Add(sourceUV.Length == vertices.Length ? sourceUV[index] : Vector2.zero);
            }
            indices.Add(mapped);
        }
        paneMesh = new Mesh { name = "Window glass (runtime)", indexFormat = source.indexFormat };
        paneMesh.SetVertices(positions);
        paneMesh.SetNormals(normals);
        paneMesh.SetUVs(0, uvs);
        paneMesh.SetTriangles(indices, 0);
        paneMesh.RecalculateBounds();
        paneMesh.RecalculateTangents();
        pane.gameObject.AddComponent<MeshFilter>().sharedMesh = paneMesh;
        glassMaterial = new Material(materials[glass]);
        // Opening exposes the back of the original glass surface.
        if (glassMaterial.HasProperty("_Cull")) glassMaterial.SetInt("_Cull", 0);
        pane.gameObject.AddComponent<MeshRenderer>().sharedMaterial = glassMaterial;
        paneCollider = pane.gameObject.AddComponent<BoxCollider>();
        paneCollider.center = paneMesh.bounds.center;
        Vector3 size = paneMesh.bounds.size;
        size.z = Mathf.Max(size.z, 0.025f);
        paneCollider.size = size;
        Panes[paneCollider] = this;
        Reach = Mathf.Clamp(size.x * 0.75f, 1.5f, 2f);
        if (physicalHandInteraction)
        {
            Vector3 hinge = pane.TransformPoint(paneMesh.bounds.center - Vector3.right * paneMesh.bounds.extents.x);
            Vector3 panePosition = pane.position;
            pivot.position = hinge; pane.position = panePosition;
            // Parent in world-preserving mode: tracked hand displacement now drives this hinge.
            // Remain unscaled in world space: the original wall has non-uniform scale.
            // Parenting the rotating glass under it would shear the pane during rotation.
            pivot.SetParent(null, true);
            physicalHinge = pivot.gameObject.AddComponent<CampusHingeInteractable>();
            physicalHinge.Configure(pivot, 1, true);
        }
        // Retire the old collision-triggered warning for this window.
        var reminder = GetComponent<Window_Reminder>();
        if (reminder != null) reminder.enabled = false;
        foreach (TMPro.TMP_Text text in GetComponentsInChildren<TMPro.TMP_Text>(true)) text.gameObject.SetActive(false);
    }

    public bool CanInteract(Vector3 viewer)
    {
        if (busy || pane == null || !isActiveAndEnabled) return false;
        Vector3 nearest = transform.TransformPoint(glassBounds.ClosestPoint(transform.InverseTransformPoint(viewer)));
        return Vector3.Distance(viewer, nearest) <= Reach;
    }

    public void Open(Vector3 viewer)
    {
        if (physicalHandInteraction) return;
        if (!CanInteract(viewer) || open) return;
        // Viewed from +Z, screen-right is local -X; from -Z it is +X.
        float side = Vector3.Dot(viewer - pivot.position, pivot.forward) >= 0 ? 1f : -1f;
        Vector3 rightEdge = paneMesh.bounds.center + Vector3.right * (-side * paneMesh.bounds.extents.x);
        Vector3 hinge = pane.TransformPoint(rightEdge);
        Vector3 panePosition = pane.position;
        pivot.position = hinge;
        pane.position = panePosition;
        StartCoroutine(RotateTo(90f, true));
    }

    public void WarnAndClose()
    {
        if (!open || busy) return;
        StartCoroutine(CloseAfterWarning());
    }

    private IEnumerator CloseAfterWarning()
    {
        busy = true;
        openWindowTarget.enabled = false;
        yield return new WaitForSeconds(2f);
        yield return RotateTo(0f, false);
    }

    private IEnumerator RotateTo(float target, bool opened)
    {
        busy = true;
        Quaternion baseRotation = Quaternion.LookRotation(transform.forward, transform.up);
        float start = angle;
        for (float elapsed = 0; elapsed < 0.65f; elapsed += Time.deltaTime)
        {
            angle = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, elapsed / 0.65f));
            pivot.rotation = baseRotation * Quaternion.Euler(0, angle, 0);
            yield return null;
        }
        angle = target;
        pivot.rotation = baseRotation * Quaternion.Euler(0, angle, 0);
        open = opened;
        busy = false;
        openWindowTarget.enabled = opened;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        busy = open = false;
        angle = 0;
        if (physicalHinge != null) physicalHinge.SetAngle(0);
        if (openWindowTarget != null) openWindowTarget.enabled = false;
        if (pivot != null)
        {
            pivot.rotation = Quaternion.LookRotation(transform.forward, transform.up);
            pivot.gameObject.SetActive(false);
        }
    }

    private void OnEnable() { if (pivot != null) pivot.gameObject.SetActive(true); }
    private void OnDestroy()
    {
        if (paneCollider != null) Panes.Remove(paneCollider);
        if (pivot != null) Destroy(pivot.gameObject);
        if (wallMesh != null) Destroy(wallMesh);
        if (paneMesh != null) Destroy(paneMesh);
        if (glassMaterial != null) Destroy(glassMaterial);
    }
}
