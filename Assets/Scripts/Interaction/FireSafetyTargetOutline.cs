using UnityEngine;
using UnityEngine.Rendering;

public sealed class FireSafetyTargetOutline : MonoBehaviour
{
    private static readonly int[,] EdgeIndices =
    {
        { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 },
        { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 },
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
    };

    private readonly LineRenderer[] edgeLines = new LineRenderer[12];
    private Transform target;
    private Material outlineMaterial;
    private Color outlineColor;
    private float outlineWidth;
    private bool isVisible;

    public void Show(Transform newTarget, Color color, float width)
    {
        if (newTarget == null)
        {
            Hide();
            return;
        }

        target = newTarget;
        outlineColor = color;
        outlineWidth = Mathf.Max(0.001f, width);
        EnsureEdgeLines();
        UpdateEdgePositions();

        for (int i = 0; i < edgeLines.Length; i++)
            edgeLines[i].enabled = true;

        isVisible = true;
    }

    public void Hide()
    {
        isVisible = false;

        for (int i = 0; i < edgeLines.Length; i++)
        {
            if (edgeLines[i] != null)
                edgeLines[i].enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (isVisible && target != null)
            UpdateEdgePositions();
    }

    private void EnsureEdgeLines()
    {
        if (edgeLines[0] != null)
            return;

        outlineMaterial = CreateOutlineMaterial();

        for (int i = 0; i < edgeLines.Length; i++)
        {
            GameObject edgeObject = new GameObject("Edge " + i);
            edgeObject.transform.SetParent(transform, false);

            LineRenderer line = edgeObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = outlineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.material = outlineMaterial;
            line.startColor = outlineColor;
            line.endColor = outlineColor;
            line.enabled = false;
            edgeLines[i] = line;
        }
    }

    private void UpdateEdgePositions()
    {
        if (target == null)
            return;

        Vector3[] corners = GetTargetCorners();
        for (int i = 0; i < edgeLines.Length; i++)
        {
            LineRenderer line = edgeLines[i];
            if (line == null)
                continue;

            line.widthMultiplier = outlineWidth;
            line.startColor = outlineColor;
            line.endColor = outlineColor;
            line.SetPosition(0, corners[EdgeIndices[i, 0]]);
            line.SetPosition(1, corners[EdgeIndices[i, 1]]);
        }
    }

    private Vector3[] GetTargetCorners()
    {
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
            return GetLocalBoundsCorners(target, targetRenderer.localBounds);

        BoxCollider boxCollider = target.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Bounds localBounds = new Bounds(boxCollider.center, boxCollider.size);
            return GetLocalBoundsCorners(target, localBounds);
        }

        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
            return GetWorldBoundsCorners(targetCollider.bounds);

        return GetWorldBoundsCorners(new Bounds(target.position, Vector3.one * 0.1f));
    }

    private static Vector3[] GetLocalBoundsCorners(Transform owner, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            owner.TransformPoint(new Vector3(min.x, min.y, min.z)),
            owner.TransformPoint(new Vector3(max.x, min.y, min.z)),
            owner.TransformPoint(new Vector3(min.x, max.y, min.z)),
            owner.TransformPoint(new Vector3(max.x, max.y, min.z)),
            owner.TransformPoint(new Vector3(min.x, min.y, max.z)),
            owner.TransformPoint(new Vector3(max.x, min.y, max.z)),
            owner.TransformPoint(new Vector3(min.x, max.y, max.z)),
            owner.TransformPoint(new Vector3(max.x, max.y, max.z))
        };
    }

    private static Vector3[] GetWorldBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private Material CreateOutlineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = "Fire Safety Outline Material (Runtime)";
        material.color = outlineColor;

        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private void OnDestroy()
    {
        if (outlineMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(outlineMaterial);
        else
            DestroyImmediate(outlineMaterial);
    }
}
