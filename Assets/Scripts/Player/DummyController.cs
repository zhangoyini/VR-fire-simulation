using UnityEngine;
using Valve.VR;

public class DummyController : MonoBehaviour
{
    [SerializeField] private GameObject controller;
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any;

    private GameObject holdingHand;
    private GameObject pushingHand;
    private GameObject buttonPressHand;

    private SteamVR_Action_Boolean grabAction;
    private IInteractable currentInteractable;
    private IInteractable heldInteractable;
    private bool pushingDoor;
    private readonly System.Collections.Generic.List<IInteractable> nearbyInteractables =
        new System.Collections.Generic.List<IInteractable>();
    private readonly System.Collections.Generic.Dictionary<IInteractable, int> colliderCounts =
        new System.Collections.Generic.Dictionary<IInteractable, int>();

    private void Awake()
    {
        grabAction = SteamVR_Input.GetBooleanAction("GrabPinch");

        if (controller != null)
        {
            // Replace the tracked device mesh with a hand model while keeping the
            // controller pose as the source for this interaction proxy.
            Transform controllerModel = controller.transform.Find("Model");
            if (controllerModel != null)
                controllerModel.gameObject.SetActive(false);
        }

        BuildHandVisual();
    }

    private void Update()
    {
        if (controller != null)
        {
            transform.position = controller.transform.position;
            transform.rotation = controller.transform.rotation;
        }

        if (grabAction == null)
            return;

        UpdateHandGesture(grabAction.GetState(inputSource),
            pushingDoor || VRFireSafetyInteraction.IsWindowPushGestureActive,
            VRFireSafetyInteraction.IsElevatorButtonPressGestureActive);

        if (grabAction.GetStateDown(inputSource) && currentInteractable != null)
        {
            heldInteractable = currentInteractable;
            pushingDoor = heldInteractable is Door_Rotation || heldInteractable is DoorInteraction;
            heldInteractable.OnInteractStart(transform);
        }

        if (heldInteractable != null && grabAction.GetState(inputSource))
        {
            heldInteractable.OnInteract(transform);
        }

        if (heldInteractable != null && grabAction.GetStateUp(inputSource))
        {
            EndHeldInteraction();
            heldInteractable = null;
            pushingDoor = false;
        }
    }

    private void BuildHandVisual()
    {
        Shader iconShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (iconShader == null)
            iconShader = Shader.Find("Unlit/Color");

        Material outline = new Material(iconShader);
        Material fill = new Material(iconShader);
        if (fill.HasProperty("_Cull"))
            fill.SetFloat("_Cull", 0f);
        SetMaterialColor(outline, Color.black);
        SetMaterialColor(fill, Color.white);

        Vector2[] holdingShape =
        {
            new Vector2(-0.030f, -0.105f), new Vector2(-0.032f, -0.155f),
            new Vector2(0.032f, -0.155f), new Vector2(0.034f, -0.095f),
            new Vector2(0.061f, -0.072f), new Vector2(0.078f, -0.045f),
            new Vector2(0.081f, -0.024f), new Vector2(0.073f, -0.008f),
            new Vector2(0.060f, -0.006f), new Vector2(0.055f, 0.025f),
            new Vector2(0.044f, 0.041f), new Vector2(0.031f, 0.036f),
            new Vector2(0.025f, 0.049f), new Vector2(0.011f, 0.051f),
            new Vector2(0.001f, 0.037f), new Vector2(-0.006f, 0.050f),
            new Vector2(-0.020f, 0.048f), new Vector2(-0.031f, 0.030f),
            new Vector2(-0.041f, 0.043f), new Vector2(-0.055f, 0.036f),
            new Vector2(-0.061f, 0.017f), new Vector2(-0.057f, -0.006f),
            new Vector2(-0.070f, -0.026f), new Vector2(-0.064f, -0.055f),
            new Vector2(-0.050f, -0.083f)
        };

        Vector2[] openPalmShape =
        {
            new Vector2(-0.030f, -0.105f), new Vector2(-0.032f, -0.155f),
            new Vector2(0.032f, -0.155f), new Vector2(0.034f, -0.095f),
            new Vector2(0.060f, -0.070f), new Vector2(0.080f, -0.035f),
            new Vector2(0.124f, 0.020f), new Vector2(0.151f, 0.052f),
            new Vector2(0.160f, 0.066f), new Vector2(0.155f, 0.080f),
            new Vector2(0.140f, 0.080f), new Vector2(0.095f, 0.040f),
            new Vector2(0.075f, 0.020f), new Vector2(0.052f, 0.030f),
            new Vector2(0.052f, 0.120f), new Vector2(0.046f, 0.140f),
            new Vector2(0.032f, 0.140f), new Vector2(0.025f, 0.120f),
            new Vector2(0.025f, 0.040f), new Vector2(0.012f, 0.040f),
            new Vector2(0.012f, 0.150f), new Vector2(0.004f, 0.168f),
            new Vector2(-0.010f, 0.168f), new Vector2(-0.018f, 0.150f),
            new Vector2(-0.018f, 0.040f), new Vector2(-0.030f, 0.040f),
            new Vector2(-0.035f, 0.130f), new Vector2(-0.043f, 0.145f),
            new Vector2(-0.057f, 0.145f), new Vector2(-0.064f, 0.130f),
            new Vector2(-0.063f, 0.040f), new Vector2(-0.075f, 0.040f),
            new Vector2(-0.080f, 0.105f), new Vector2(-0.088f, 0.120f),
            new Vector2(-0.102f, 0.118f), new Vector2(-0.108f, 0.105f),
            new Vector2(-0.102f, 0.035f), new Vector2(-0.080f, -0.020f),
            new Vector2(-0.060f, -0.075f), new Vector2(-0.034f, -0.100f)
        };

        Vector2[] buttonPressShape =
        {
            new Vector2(-0.030f, -0.105f), new Vector2(-0.032f, -0.155f),
            new Vector2(0.032f, -0.155f), new Vector2(0.034f, -0.095f),
            new Vector2(0.065f, -0.055f), new Vector2(0.086f, -0.008f),
            new Vector2(0.083f, 0.012f), new Vector2(0.068f, 0.018f),
            new Vector2(0.052f, -0.008f), new Vector2(0.043f, -0.005f),
            new Vector2(0.043f, 0.050f), new Vector2(0.038f, 0.066f),
            new Vector2(0.024f, 0.066f), new Vector2(0.019f, 0.053f),
            new Vector2(0.018f, 0.023f), new Vector2(0.006f, 0.022f),
            new Vector2(0.005f, 0.133f), new Vector2(-0.002f, 0.150f),
            new Vector2(-0.016f, 0.150f), new Vector2(-0.024f, 0.134f),
            new Vector2(-0.024f, 0.012f), new Vector2(-0.038f, 0.050f),
            new Vector2(-0.052f, 0.067f), new Vector2(-0.066f, 0.060f),
            new Vector2(-0.067f, 0.044f), new Vector2(-0.048f, 0.002f),
            new Vector2(-0.055f, -0.045f)
        };

        holdingHand = CreateIconHand("Holding controller hand", holdingShape, outline, fill);
        holdingHand.transform.localRotation = Quaternion.Euler(0f, 60f, 60f);
        pushingHand = CreateIconHand("Open palm", openPalmShape, outline, fill);
        pushingHand.SetActive(false);
        buttonPressHand = CreateIconHand("Button press finger", buttonPressShape, outline, fill);
        buttonPressHand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        CreateIconDigitBump(buttonPressHand.transform, new Vector2(-0.060f, 0.012f),
            new Vector2(0.024f, 0.016f), outline, fill);
        LineRenderer buttonOutline = buttonPressHand.GetComponentInChildren<LineRenderer>();
        buttonOutline.startWidth = 0.011f;
        buttonOutline.endWidth = 0.011f;
        buttonPressHand.SetActive(false);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void CreateIconDigitBump(Transform parent, Vector2 center, Vector2 radius,
        Material outlineMaterial, Material fillMaterial)
    {
        const int segments = 16;
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = new Vector3(center.x, center.y, 0.003f);
        Vector3[] linePoints = new Vector3[segments];
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = center.x + Mathf.Cos(angle) * radius.x;
            float y = center.y + Mathf.Sin(angle) * radius.y;
            vertices[i + 1] = new Vector3(x, y, 0.003f);
            linePoints[i] = new Vector3(x, y, 0.005f);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        GameObject digit = new GameObject("Extra curled finger");
        digit.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = "Extra curled finger fill" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        digit.AddComponent<MeshFilter>().sharedMesh = mesh;
        digit.AddComponent<MeshRenderer>().sharedMaterial = fillMaterial;

        GameObject outline = new GameObject("Extra curled finger outline");
        outline.transform.SetParent(parent, false);
        LineRenderer line = outline.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = 0.011f;
        line.endWidth = 0.011f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sharedMaterial = outlineMaterial;
        line.SetPositions(linePoints);
    }

    private GameObject CreateIconHand(string handName, Vector2[] outlinePoints, Material outlineMaterial, Material fillMaterial)
    {
        GameObject hand = new GameObject(handName);
        hand.transform.SetParent(transform, false);
        hand.transform.localScale = Vector3.one * 0.65f;

        Mesh fillMesh = new Mesh { name = handName + " fill" };
        Vector3[] vertices = new Vector3[outlinePoints.Length];
        for (int i = 0; i < outlinePoints.Length; i++)
            vertices[i] = new Vector3(outlinePoints[i].x, outlinePoints[i].y, 0.001f);
        fillMesh.vertices = vertices;
        fillMesh.triangles = Triangulate(outlinePoints);
        fillMesh.RecalculateNormals();

        GameObject fillObject = new GameObject("White fill");
        fillObject.transform.SetParent(hand.transform, false);
        fillObject.AddComponent<MeshFilter>().sharedMesh = fillMesh;
        fillObject.AddComponent<MeshRenderer>().sharedMaterial = fillMaterial;

        GameObject outlineObject = new GameObject("Black outline");
        outlineObject.transform.SetParent(hand.transform, false);
        LineRenderer line = outlineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = outlinePoints.Length;
        line.startWidth = 0.009f;
        line.endWidth = 0.009f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sharedMaterial = outlineMaterial;
        for (int i = 0; i < outlinePoints.Length; i++)
            line.SetPosition(i, new Vector3(outlinePoints[i].x, outlinePoints[i].y, -0.001f));
        return hand;
    }

    private static int[] Triangulate(Vector2[] points)
    {
        System.Collections.Generic.List<int> remaining = new System.Collections.Generic.List<int>();
        for (int i = 0; i < points.Length; i++)
            remaining.Add(i);
        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        int guard = points.Length * points.Length;
        while (remaining.Count > 3 && guard-- > 0)
        {
            bool earFound = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                int current = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                if (Cross(points[previous], points[current], points[next]) <= 0f)
                    continue;

                bool containsPoint = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    int candidate = remaining[j];
                    if (candidate == previous || candidate == current || candidate == next)
                        continue;
                    if (PointInTriangle(points[candidate], points[previous], points[current], points[next]))
                    {
                        containsPoint = true;
                        break;
                    }
                }
                if (containsPoint)
                    continue;

                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(next);
                remaining.RemoveAt(i);
                earFound = true;
                break;
            }
            if (!earFound)
                break;
        }

        if (remaining.Count == 3)
        {
            triangles.Add(remaining[0]);
            triangles.Add(remaining[1]);
            triangles.Add(remaining[2]);
        }
        return triangles.ToArray();
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Cross(a, b, p);
        float bc = Cross(b, c, p);
        float ca = Cross(c, a, p);
        return ab >= 0f && bc >= 0f && ca >= 0f;
    }

    private void UpdateHandGesture(bool gripping, bool doorPush, bool buttonPress)
    {
        if (holdingHand == null || pushingHand == null || buttonPressHand == null)
            return;
        bool showButtonPress = buttonPress && gripping;
        bool showOpenPalm = doorPush && gripping && !showButtonPress;
        holdingHand.SetActive(!showOpenPalm && !showButtonPress);
        pushingHand.SetActive(showOpenPalm);
        buttonPressHand.SetActive(showButtonPress);
    }

    private void OnTriggerEnter(Collider other)
    {
        IInteractable interactable = FindInteractable(other);

        if (interactable != null)
        {
            int count;
            colliderCounts.TryGetValue(interactable, out count);
            colliderCounts[interactable] = count + 1;

            if (count == 0)
            {
                nearbyInteractables.Add(interactable);
                SetCurrentInteractable(interactable);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IInteractable interactable = FindInteractable(other);

        if (interactable == null)
            return;

        int count;
        if (!colliderCounts.TryGetValue(interactable, out count))
            return;

        if (count > 1)
        {
            colliderCounts[interactable] = count - 1;
            return;
        }

        colliderCounts.Remove(interactable);
        nearbyInteractables.Remove(interactable);
        if (interactable == currentInteractable)
        {
            currentInteractable.OnHoverExit();
            currentInteractable = null;

            // Fall back to the most recently entered object still in range.
            if (nearbyInteractables.Count > 0)
                SetCurrentInteractable(nearbyInteractables[nearbyInteractables.Count - 1]);
        }
    }

    private static IInteractable FindInteractable(Collider other)
    {
        // Interactable scripts are often placed on a parent while the collider is on a child.
        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable interactable)
                return interactable;
        }

        return null;
    }

    private void SetCurrentInteractable(IInteractable interactable)
    {
        if (currentInteractable == interactable)
            return;

        if (currentInteractable != null)
            currentInteractable.OnHoverExit();

        currentInteractable = interactable;
        currentInteractable.OnHoverEnter();
    }

    private void EndHeldInteraction()
    {
        if (heldInteractable == null)
            return;

        heldInteractable.OnInteractEnd(transform);
    }

    private void OnDisable()
    {
        if (heldInteractable != null)
        {
            EndHeldInteraction();
            heldInteractable = null;
        }
        pushingDoor = false;

        if (currentInteractable != null)
            currentInteractable.OnHoverExit();

        currentInteractable = null;
        nearbyInteractables.Clear();
        colliderCounts.Clear();
    }
}
