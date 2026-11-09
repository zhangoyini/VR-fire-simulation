using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Phase = CampusLessonDirector.Phase;

// World-space task outlines remain occluded by the building and leave contact materials alone.
internal sealed class CampusObjectiveHighlights
{
    private static readonly Color Cyan = new Color(.12f, .95f, 1f);
    private static readonly Color Amber = new Color(1f, .56f, .12f);

    private readonly GameObject root;
    private readonly Transform head;
    private readonly Material objectiveMaterial, warningMaterial;
    private readonly LineRenderer primary, secondary, obstacle;
    private readonly TextMeshPro eastStairLabel;
    private readonly Renderer classroomBar, debris, refugeSign, callButton, assemblySign;
    private readonly Renderer[] exitBars, pushGrips;
    private readonly CampusRescueStudent student;

    internal CampusObjectiveHighlights(Transform parent, Transform head, TMP_FontAsset font,
        CampusHingeInteractable classroomDoor, Transform exitDoor, CampusRescueStudent student)
    {
        this.head = head;
        this.student = student;
        root = new GameObject("Exercise objective highlights");
        root.transform.SetParent(parent, false);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        objectiveMaterial = new Material(shader) { color = Cyan };
        warningMaterial = new Material(shader) { color = Amber };
        primary = CreateOutline("Primary objective", objectiveMaterial);
        secondary = CreateOutline("Second handle", objectiveMaterial);
        obstacle = CreateOutline("Stair obstacle", warningMaterial);

        classroomBar = classroomDoor.transform.Find("Push bar")?.GetComponent<Renderer>();
        exitBars = FindParts(exitDoor, "Panic bar");
        pushGrips = FindParts(student != null ? student.transform : null, "Push grip");
        debris = GameObject.Find("3F Stair A collapsed storage pile")?.transform
            .Find("Toppled storage cabinet")?.GetComponent<Renderer>();
        refugeSign = SignBacking(GameObject.Find("Sign 2F REFUGE  /  二层避难区")?.transform);
        callButton = GameObject.Find("Intercom button")?.GetComponent<Renderer>();

        var assemblyRoot = GameObject.Find("Assembly sign post")?.transform.parent;
        // The front-facing plaque is the one nearer the building entrance.
        Transform frontSign = null;
        if (assemblyRoot != null)
            foreach (Transform child in assemblyRoot)
                if (child.name == "Sign ASSEMBLY POINT" && (frontSign == null || child.position.z > frontSign.position.z))
                    frontSign = child;
        assemblySign = SignBacking(frontSign);

        var eastLanding = GameObject.Find("Stair B — continuous physical stairs")?.transform.Find("Full landing 3F");
        if (eastLanding != null)
        {
            var label = new GameObject("3F Stair B objective sign", typeof(RectTransform));
            label.transform.SetParent(root.transform, false);
            label.transform.position = eastLanding.position + new Vector3(-2.65f, 2.05f, 0);
            eastStairLabel = label.AddComponent<TextMeshPro>();
            eastStairLabel.font = font;
            eastStairLabel.fontSize = 2f;
            eastStairLabel.rectTransform.sizeDelta = new Vector2(2.5f, .5f);
            eastStairLabel.alignment = TextAlignmentOptions.Center;
            eastStairLabel.color = Cyan;
            eastStairLabel.text = "B 楼梯 / Stair B";
        }
        Hide();
    }

    internal void Show(Phase phase, CampusExerciseTasks tasks, bool inRestroom,
        CampusTowelInteractable towel, CampusFaucet faucet)
    {
        root.SetActive(true);
        primary.enabled = secondary.enabled = obstacle.enabled = false;
        bool showEastStair = false;

        switch (tasks.Floor)
        {
            case 4:
                if (phase == Phase.OpenDoor) DrawFlat(primary, classroomBar, 9f, .11f);
                else if (((phase == Phase.FindTowel && inRestroom) || phase == Phase.WetTowel) && !towel.IsHeld)
                    Draw(primary, towel.Surface.bounds, 3f);
                else if (phase == Phase.WetTowel && !towel.IsWet && faucet != null)
                {
                    if (faucet.IsOn && faucet.WaterVolume != null)
                        Draw(primary, faucet.WaterVolume.bounds, 3f);
                    else if (faucet.TryGetComponent<Collider>(out var lever))
                        Draw(primary, lever.bounds, 3f);
                }
                break;
            case 3:
                if (!tasks.DetourActive) break;
                Draw(obstacle, debris, 7f);
                if (eastStairLabel == null) break;
                Vector3 offset = eastStairLabel.transform.position - head.position;
                if (offset.sqrMagnitude < 48f * 48f && offset.sqrMagnitude > .01f)
                {
                    eastStairLabel.transform.rotation = Quaternion.LookRotation(offset, Vector3.up);
                    showEastStair = true;
                }
                break;
            case 2:
                if (!tasks.RescueActive || student == null) break;
                if (student.Current == CampusRescueStudent.State.Waiting) DrawPair(pushGrips, 5f);
                else if (student.Current == CampusRescueStudent.State.Following) DrawFlat(primary, refugeSign, 30f);
                else Draw(primary, callButton, 8f);
                break;
            case 1:
                if (tasks.ExitActive)
                {
                    if (exitBars.Length > 0) DrawFlat(primary, exitBars[0], 14f, .11f);
                    if (exitBars.Length > 1) DrawFlat(secondary, exitBars[1], 14f, .11f);
                }
                else if (tasks.AssemblyApproach) DrawFlat(primary, assemblySign, 40f);
                break;
        }
        if (eastStairLabel != null) eastStairLabel.gameObject.SetActive(showEastStair);
    }

    internal void Hide() => root.SetActive(false);

    internal void Dispose()
    {
        Object.Destroy(root);
        Object.Destroy(objectiveMaterial);
        Object.Destroy(warningMaterial);
    }

    private LineRenderer CreateOutline(string name, Material material)
    {
        var node = new GameObject(name);
        node.transform.SetParent(root.transform, false);
        var line = node.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 4;
        line.useWorldSpace = true;
        line.loop = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private static Renderer[] FindParts(Transform parent, string name) => parent == null
        ? System.Array.Empty<Renderer>()
        : parent.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.name == name).ToArray();

    private static Renderer SignBacking(Transform sign) => sign?.Find("Sign backing")?.GetComponent<Renderer>();

    private void DrawPair(Renderer[] parts, float range)
    {
        if (parts.Length > 0) Draw(primary, parts[0], range);
        if (parts.Length > 1) Draw(secondary, parts[1], range);
    }

    private void Draw(LineRenderer line, Renderer part, float range)
    {
        if (part != null && part.enabled && part.gameObject.activeInHierarchy) Draw(line, part.bounds, range);
    }

    private void Draw(LineRenderer line, Bounds bounds, float range)
    {
        Vector3 toHead = head.position - bounds.center;
        float distance = toHead.magnitude;
        if (distance < .05f || distance > range) return;
        Vector3 normal = toHead / distance;
        Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
        if (right == Vector3.zero) right = head.right;
        Vector3 up = Vector3.Cross(normal, right);
        Vector3 center = bounds.center + normal * (ProjectedExtent(bounds.extents, normal) + .025f);
        Vector3 x = right * (ProjectedExtent(bounds.extents, right) + .035f);
        Vector3 y = up * (ProjectedExtent(bounds.extents, up) + .035f);
        SetOutline(line, center, x, y, distance);
    }

    private void DrawFlat(LineRenderer line, Renderer part, float range, float surfacePadding = .04f)
    {
        if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) return;
        Transform plate = part.transform;
        Vector3 toHead = head.position - plate.position;
        float distance = toHead.magnitude;
        if (distance > range) return;
        Vector3 halfSize = plate.lossyScale * .5f;
        float face = Mathf.Sign(Vector3.Dot(toHead, plate.forward));
        Vector3 center = plate.position + plate.forward * (face * (halfSize.z + surfacePadding));
        SetOutline(line, center, plate.right * (halfSize.x + .045f), plate.up * (halfSize.y + .045f), distance);
    }

    private static void SetOutline(LineRenderer line, Vector3 center, Vector3 x, Vector3 y, float distance)
    {
        line.widthMultiplier = Mathf.Clamp(distance * .006f, .012f, .075f);
        line.SetPosition(0, center - x - y);
        line.SetPosition(1, center + x - y);
        line.SetPosition(2, center + x + y);
        line.SetPosition(3, center - x + y);
        line.enabled = true;
    }

    private static float ProjectedExtent(Vector3 extents, Vector3 axis) =>
        extents.x * Mathf.Abs(axis.x) + extents.y * Mathf.Abs(axis.y) + extents.z * Mathf.Abs(axis.z);
}
