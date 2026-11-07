using UnityEngine;
using UnityEngine.Rendering;

// World-space feedback owned by one teleporter. It has no physics colliders.
public sealed class TeleportDestinationMarker : MonoBehaviour
{
    private const int RingSegments = 48;
    private const float FadeSeconds = .25f;
    private static readonly Color ValidColor = new Color(.15f, .8f, 1f);
    private static readonly Color InvalidColor = new Color(1f, .25f, .2f);
    private LineRenderer ring, arrow, crossA, crossB;
    private bool valid, fading;
    private float fadeStarted;
    private float currentRadius;

    public static Color TargetColor(bool allowed) => allowed ? ValidColor : InvalidColor;

    private void Awake()
    {
        EnsureLines();
        Hide();
    }

    private void EnsureLines()
    {
        if (ring != null) return;
        var material = Resources.Load<Material>("TeleportDestinationMarker");
        ring = MakeLine("Landing ring", material, true);
        arrow = MakeLine("Facing arrow", material, false);
        crossA = MakeLine("Invalid cross A", material, false);
        crossB = MakeLine("Invalid cross B", material, false);
    }

    private LineRenderer MakeLine(string label, Material material, bool loop)
    {
        var child = new GameObject(label);
        child.layer = Physics.IgnoreRaycastLayer;
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = loop;
        line.alignment = LineAlignment.TransformZ;
        // LineAlignment.TransformZ expects the ribbon normal along the local Z axis.
        child.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        return line;
    }

    public void Show(Vector3 point, Vector3 normal, Vector3 facing, bool allowed, bool stairTread)
    {
        EnsureLines();
        fading = false;
        valid = allowed;
        Vector3 forward = Vector3.ProjectOnPlane(facing, normal);
        if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(Vector3.right, normal);
        // Restroom tiles are a 16 mm decorative layer above the walkable collider.
        transform.SetPositionAndRotation(point + normal * .025f, Quaternion.LookRotation(forward, normal));
        float radius = stairTread ? .095f : .22f;
        if (currentRadius != radius) SetGeometry(radius, stairTread ? .01f : .02f);
        ring.enabled = true;
        arrow.enabled = allowed;
        crossA.enabled = crossB.enabled = !allowed;
        Tick(Time.unscaledTime);
    }

    private void SetGeometry(float radius, float width)
    {
        currentRadius = radius;
        ring.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = 2 * Mathf.PI * i / RingSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
        // The child XY plane maps to the ground plane, with -Y pointing forward.
        arrow.positionCount = 5;
        arrow.SetPositions(new[] {
            new Vector3(0, radius * .35f, 0),
            new Vector3(0, -radius * .6f, 0),
            new Vector3(-radius * .3f, -radius * .2f, 0),
            new Vector3(0, -radius * .6f, 0),
            new Vector3(radius * .3f, -radius * .2f, 0)
        });
        float crossSize = radius * .45f;
        crossA.positionCount = crossB.positionCount = 2;
        crossA.SetPositions(new[] { new Vector3(-crossSize, -crossSize, 0), new Vector3(crossSize, crossSize, 0) });
        crossB.SetPositions(new[] { new Vector3(-crossSize, crossSize, 0), new Vector3(crossSize, -crossSize, 0) });
        foreach (var line in new[] { ring, arrow, crossA, crossB })
            line.startWidth = line.endWidth = width;
    }

    public void FinishTeleport()
    {
        fading = true;
        fadeStarted = Time.unscaledTime;
    }

    public void HideAim()
    {
        if (!fading) Hide();
    }

    public void Hide()
    {
        fading = false;
        if (ring == null) return;
        ring.enabled = arrow.enabled = crossA.enabled = crossB.enabled = false;
    }

    private void Update()
    {
        if (Time.timeScale == 0) Hide();
        else Tick(Time.unscaledTime);
    }

    private void Tick(float now)
    {
        if (!ring.enabled) return;
        float opacity = fading ? 1 - Mathf.Clamp01((now - fadeStarted) / FadeSeconds) :
            .8f + .2f * Mathf.Sin(now * 2 * Mathf.PI);
        if (opacity <= 0) { Hide(); return; }
        Color color = TargetColor(valid);
        color.a = opacity;
        ring.startColor = ring.endColor = color;
        crossA.startColor = crossA.endColor = color;
        crossB.startColor = crossB.endColor = color;
        Color arrowColor = new Color(.8f, .95f, 1f, opacity);
        arrow.startColor = arrow.endColor = arrowColor;
    }
}
