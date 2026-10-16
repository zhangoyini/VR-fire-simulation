using System.IO;
using UnityEditor;
using UnityEngine;

// Builds a primitive-based, low-poly wheelchair + seated student prefab and
// provides one-click placement for the 2F rescue side quest. No external art.
// Menu:
//   Tools/Campus/Rescue Student/1. Build Rescue Student Prefab
//   Tools/Campus/Place Rescue Student (2F corridor)
public static class CampusRescueStudentSetup
{
    private const string RootName = "Rescue — Student (2F corridor)";
    private const string PrefabPath = "Assets/Prefabs/Campus/RescueStudent.prefab";
    private const string MatFolder = "Assets/Prefabs/Campus/RescueStudentMats";
    private static readonly Vector3 PlacePosition = new Vector3(0f, 3.8f, 0f);

    // ---------------------------------------------------------------------
    // Menus
    // ---------------------------------------------------------------------

    [MenuItem("Tools/Campus/Rescue Student/1. Build Rescue Student Prefab")]
    public static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Prefabs/Campus");
        EnsureFolder(MatFolder);

        var frame = MakeMat("M_Wheelchair_Frame", Hex("#39404A"));
        var tire  = MakeMat("M_Wheelchair_Tire",  Hex("#17181A"));
        var metal = MakeMat("M_Wheelchair_Metal", Hex("#9DA6B0"));
        var seat  = MakeMat("M_Wheelchair_Seat",  Hex("#2E71B8"));
        var shirt = MakeMat("M_Student_Shirt",    Hex("#E9EEF3"));
        var skin  = MakeMat("M_Student_Skin",     Hex("#E8B890"));
        var pants = MakeMat("M_Student_Pants",    Hex("#33415C"));
        var dark  = MakeMat("M_Student_Dark",     Hex("#23202C"));

        var root = new GameObject("RescueStudent");
        BuildModel(root.transform, frame, tire, metal, seat, shirt, skin, pants, dark);

        // Kinematic moving collider: the hand interactor needs a non-trigger box,
        // and the student moves at runtime.
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.78f, 1.34f, 1.05f);
        col.center = new Vector3(0f, 0.67f, 0f);
        col.isTrigger = false;

        root.AddComponent<CampusRescueStudent>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RescueStudent] Prefab saved at " + PrefabPath);
        return prefab;
    }

    [MenuItem("Tools/Campus/Place Rescue Student (2F corridor)")]
    public static void Place()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();

        var existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place Rescue Student");
        instance.name = RootName;
        instance.transform.position = PlacePosition;
        instance.transform.rotation = Quaternion.identity;

        var student = instance.GetComponent<CampusRescueStudent>();
        var serial = new SerializedObject(student);
        var loco = Object.FindObjectOfType<CampusLocomotion>();
        if (loco != null) serial.FindProperty("followTarget").objectReferenceValue = loco.transform;
        var refuge = GameObject.Find("2F Refuge Occupancy");
        if (refuge != null) serial.FindProperty("refugeVolume").objectReferenceValue = refuge.GetComponent<Collider>();
        serial.ApplyModifiedProperties();

        Selection.activeGameObject = instance;
        EditorUtility.SetDirty(instance);
        Debug.Log("[RescueStudent] Placed at " + PlacePosition + ". Nudge X/Z in Scene view if the corridor spot differs.");
    }

    // ---------------------------------------------------------------------
    // Model construction (model faces +Z, origin at floor level)
    // ---------------------------------------------------------------------

    private static void BuildModel(Transform root, Material frame, Material tire, Material metal,
        Material seat, Material shirt, Material skin, Material pants, Material dark)
    {
        var chair = Group("Wheelchair", root);
        var student = Group("Student", root);
        Quaternion wheelAxis = Quaternion.Euler(0f, 0f, 90f); // cylinder axis -> X

        // ---- symmetric parts ----
        foreach (var sx in new[] { -1f, 1f })
        {
            float x = sx * 0.30f;

            // Rear wheel: tire + hub + handrim + cross spokes
            Part("Rear tire", PrimitiveType.Cylinder, chair, new Vector3(x, 0.30f, -0.05f),
                new Vector3(0.30f, 0.05f, 0.30f), tire, wheelAxis);
            Part("Rear hub", PrimitiveType.Cylinder, chair, new Vector3(x, 0.30f, -0.05f),
                new Vector3(0.13f, 0.055f, 0.13f), metal, wheelAxis);
            Part("Handrim", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.345f, 0.30f, -0.05f),
                new Vector3(0.26f, 0.02f, 0.26f), metal, wheelAxis);
            Part("Spoke A", PrimitiveType.Cube, chair, new Vector3(x, 0.30f, -0.05f),
                new Vector3(0.028f, 0.50f, 0.028f), metal);
            Part("Spoke B", PrimitiveType.Cube, chair, new Vector3(x, 0.30f, -0.05f),
                new Vector3(0.028f, 0.50f, 0.028f), metal, Quaternion.Euler(90f, 0f, 0f));

            // Front caster
            Part("Caster", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.07f, 0.34f),
                new Vector3(0.07f, 0.04f, 0.07f), tire, wheelAxis);
            Part("Caster stem", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.12f, 0.32f),
                new Vector3(0.025f, 0.08f, 0.025f), metal);

            // Frame: lower rail from rear axle down to front caster
            Part("Lower rail", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.24f, 0.20f, 0.14f),
                new Vector3(0.035f, 0.44f, 0.035f), frame, Quaternion.Euler(61f, 0f, 0f));
            Part("Seat support", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.39f, 0.20f),
                new Vector3(0.035f, 0.22f, 0.035f), frame);
            Part("Back post", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.61f, -0.10f),
                new Vector3(0.035f, 0.64f, 0.035f), frame);
            Part("Push handle", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.93f, -0.17f),
                new Vector3(0.035f, 0.18f, 0.035f), frame, Quaternion.Euler(90f, 0f, 0f));

            // Armrest
            Part("Armrest", PrimitiveType.Cube, chair, new Vector3(sx * 0.27f, 0.63f, 0.05f),
                new Vector3(0.06f, 0.04f, 0.40f), frame);
            Part("Armrest support", PrimitiveType.Cube, chair, new Vector3(sx * 0.25f, 0.55f, 0.20f),
                new Vector3(0.04f, 0.16f, 0.04f), frame);

            // Footrest
            Part("Footrest hanger", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.15f, 0.32f, 0.36f),
                new Vector3(0.03f, 0.36f, 0.03f), frame, Quaternion.Euler(24f, 0f, 0f));
            Part("Footplate", PrimitiveType.Cube, chair, new Vector3(sx * 0.15f, 0.15f, 0.47f),
                new Vector3(0.16f, 0.03f, 0.22f), frame);

            // ---- student: arms ----
            Part("Upper arm", PrimitiveType.Capsule, student, new Vector3(sx * 0.22f, 0.88f, 0.02f),
                new Vector3(0.09f, 0.17f, 0.09f), shirt, Quaternion.Euler(22f, 0f, 0f));
            Part("Forearm", PrimitiveType.Capsule, student, new Vector3(sx * 0.23f, 0.67f, 0.13f),
                new Vector3(0.08f, 0.14f, 0.08f), shirt, Quaternion.Euler(82f, 0f, 0f));
            Part("Hand", PrimitiveType.Sphere, student, new Vector3(sx * 0.23f, 0.62f, 0.21f),
                new Vector3(0.055f, 0.055f, 0.055f), skin);

            // ---- student: legs ----
            Part("Thigh", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.47f, 0.26f),
                new Vector3(0.13f, 0.11f, 0.28f), pants);
            Part("Shin", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.30f, 0.37f),
                new Vector3(0.12f, 0.30f, 0.12f), pants);
            Part("Shoe", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.145f, 0.46f),
                new Vector3(0.13f, 0.09f, 0.24f), dark);
        }

        // ---- central chair parts ----
        Part("Axle", PrimitiveType.Cylinder, chair, new Vector3(0f, 0.30f, -0.05f),
            new Vector3(0.04f, 0.62f, 0.04f), metal, wheelAxis);
        Part("Seat", PrimitiveType.Cube, chair, new Vector3(0f, 0.50f, 0.05f),
            new Vector3(0.46f, 0.06f, 0.44f), seat);
        Part("Backrest", PrimitiveType.Cube, chair, new Vector3(0f, 0.73f, -0.11f),
            new Vector3(0.46f, 0.44f, 0.04f), seat, Quaternion.Euler(-8f, 0f, 0f));

        // ---- student: body ----
        Part("Pelvis", PrimitiveType.Cube, student, new Vector3(0f, 0.56f, 0.02f),
            new Vector3(0.34f, 0.12f, 0.30f), pants);
        Part("Torso", PrimitiveType.Capsule, student, new Vector3(0f, 0.84f, -0.01f),
            new Vector3(0.36f, 0.18f, 0.24f), shirt, Quaternion.Euler(-6f, 0f, 0f));
        Part("Head", PrimitiveType.Sphere, student, new Vector3(0f, 1.20f, 0.00f),
            new Vector3(0.14f, 0.16f, 0.14f), skin);
        Part("Hair", PrimitiveType.Sphere, student, new Vector3(0f, 1.255f, -0.02f),
            new Vector3(0.148f, 0.085f, 0.148f), dark);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Part(string name, PrimitiveType type, Transform parent,
        Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = rot ?? Quaternion.identity;
        t.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static Material MakeMat(string name, Color color)
    {
        var path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
