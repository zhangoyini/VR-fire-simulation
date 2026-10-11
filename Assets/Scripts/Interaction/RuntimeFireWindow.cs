using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FireSafetyPrefabSupport))]
public sealed class RuntimeFireWindow : MonoBehaviour
{
    [SerializeField] private Mesh readableWindowMesh;
    [SerializeField, Min(0.01f)] private float maxPushDistance = 0.35f;
    [SerializeField, Range(1f, 170f)] private float maxOpenAngle = 85f;
    private static readonly Dictionary<Collider, RuntimeFireWindow> Panes = new Dictionary<Collider, RuntimeFireWindow>();
    private Mesh wallMesh, paneMesh;
    private Material glassMaterial;
    private Transform pivot, pane;
    private BoxCollider paneCollider;
    private BoxCollider openWindowTarget;
    private Bounds glassBounds;
    private bool open, pushing;
    private float angle;
    private float openingAmount;
    private float openingSign;
    private float pushStartAmount;
    private Vector3 pushStartPosition;
    private Vector3 outwardNormal;

    public Transform HighlightTarget => pane != null ? pane : transform;
    public bool IsOpen => open;
    public bool IsPushing => pushing;
    public float OpeningAmount => openingAmount;
    public float LastPushDelta { get; private set; }
    public float Reach { get; private set; }

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

        // The real window hinges along its upper edge, so the handle/bottom edge
        // moves away from the wall when the user pushes it.
        pivot = new GameObject("Window top hinge (runtime)").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pivot.gameObject, gameObject.scene);
        pivot.position = transform.TransformPoint(new Vector3(
            glassBounds.center.x,
            glassBounds.max.y,
            glassBounds.center.z));
        pivot.rotation = transform.rotation;
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
        ApplyOpening(0f);
        // Retire the old collision-triggered warning for this window.
        var reminder = GetComponent<Window_Reminder>();
        if (reminder != null) reminder.enabled = false;
        foreach (TMPro.TMP_Text text in GetComponentsInChildren<TMPro.TMP_Text>(true)) text.gameObject.SetActive(false);
    }

    public bool CanInteract(Vector3 viewer)
    {
        if (pane == null || !isActiveAndEnabled) return false;
        Vector3 nearest = transform.TransformPoint(glassBounds.ClosestPoint(transform.InverseTransformPoint(viewer)));
        return Vector3.Distance(viewer, nearest) <= Reach;
    }

    /// <summary>
    /// Locks this window to the controller for the duration of a trigger hold.
    /// The sign is based on which side of the window the hand is on.
    /// </summary>
    public bool BeginPush(Vector3 handPosition, Vector3 viewerPosition)
    {
        if (pane == null || !isActiveAndEnabled)
            return false;

        Vector3 planePoint = transform.TransformPoint(glassBounds.center);
        float side = Vector3.Dot(handPosition - planePoint, transform.forward);
        if (Mathf.Abs(side) < 0.01f)
            side = Vector3.Dot(viewerPosition - planePoint, transform.forward);
        if (Mathf.Abs(side) < 0.01f)
            side = 1f;

        // Keep the existing opening direction when closing an already open
        // window. This prevents the hinge from changing sides mid-interaction.
        if (openingAmount <= 0.001f || Mathf.Abs(openingSign) < 0.01f)
            openingSign = Mathf.Sign(side);

        outwardNormal = transform.forward * Mathf.Sign(side);
        pushStartPosition = handPosition;
        pushStartAmount = openingAmount;
        LastPushDelta = 0f;
        pushing = true;
        return true;
    }

    /// <summary>
    /// Applies controller displacement along the window normal. Moving toward
    /// the wall opens the window; moving back closes it. Releasing the trigger
    /// leaves the window at the current opening amount.
    /// </summary>
    public float UpdatePush(Vector3 handPosition)
    {
        if (!pushing || pane == null)
            return openingAmount;

        float previousAmount = openingAmount;
        float inwardDistance = -Vector3.Dot(handPosition - pushStartPosition, outwardNormal);
        openingAmount = Mathf.Clamp01(pushStartAmount + inwardDistance / Mathf.Max(0.01f, maxPushDistance));
        ApplyOpening(openingAmount);
        LastPushDelta = openingAmount - previousAmount;
        return openingAmount;
    }

    public void EndPush()
    {
        pushing = false;
        LastPushDelta = 0f;
        if (openingAmount <= 0.001f)
        {
            openingAmount = 0f;
            openingSign = 0f;
        }
    }

    private void ApplyOpening(float amount)
    {
        openingAmount = Mathf.Clamp01(amount);
        angle = openingSign * maxOpenAngle * openingAmount;
        if (pivot != null)
            pivot.rotation = transform.rotation * Quaternion.Euler(angle, 0f, 0f);
        open = openingAmount > 0.001f;
        if (openWindowTarget != null)
            openWindowTarget.enabled = open;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        open = pushing = false;
        openingAmount = 0f;
        openingSign = 0f;
        LastPushDelta = 0f;
        angle = 0f;
        if (openWindowTarget != null) openWindowTarget.enabled = false;
        if (pivot != null)
        {
            pivot.rotation = transform.rotation;
            pivot.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (pivot != null)
        {
            pivot.gameObject.SetActive(true);
            ApplyOpening(openingAmount);
        }
    }
    private void OnDestroy()
    {
        if (paneCollider != null) Panes.Remove(paneCollider);
        if (pivot != null) Destroy(pivot.gameObject);
        if (wallMesh != null) Destroy(wallMesh);
        if (paneMesh != null) Destroy(paneMesh);
        if (glassMaterial != null) Destroy(glassMaterial);
    }
}
