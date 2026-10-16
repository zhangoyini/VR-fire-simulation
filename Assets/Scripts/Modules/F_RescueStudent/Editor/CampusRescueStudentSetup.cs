using System.IO;
using UnityEditor;
using UnityEngine;

// Builds a stylized wheelchair and seated student, then places the 2F side quest.
// Menu:
//   Tools/Campus/Rescue Student/1. Build Rescue Student Prefab
//   Tools/Campus/Place Rescue Student (2F corridor)
public static class CampusRescueStudentSetup
{
    private const string RootName = "Rescue — Student (2F corridor)";
    private const string PrefabPath = "Assets/Prefabs/Campus/RescueStudent.prefab";
    private const string MatFolder = "Assets/Prefabs/Campus/RescueStudentMats";
    private const string MeshFolder = "Assets/Prefabs/Campus/RescueStudentMeshes";
    private static readonly Vector3 PlacePosition = new Vector3(0f, 3.8f, 0f);

    // ---------------------------------------------------------------------
    // Menus
    // ---------------------------------------------------------------------

    [MenuItem("Tools/Campus/Rescue Student/1. Build Rescue Student Prefab")]
    public static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Prefabs/Campus");
        EnsureFolder(MatFolder);
        EnsureFolder(MeshFolder);

        var frame = MakeMat("M_Wheelchair_Frame", Hex("#39404A"));
        var tire  = MakeMat("M_Wheelchair_Tire",  Hex("#17181A"));
        var metal = MakeMat("M_Wheelchair_Metal", Hex("#9DA6B0"));
        var seat  = MakeMat("M_Wheelchair_Seat",  Hex("#2E71B8"));
        var shirt = MakeMat("M_Student_Shirt",    Hex("#E9EEF3"));
        var skin  = MakeMat("M_Student_Skin",     Hex("#E8B890"));
        var pants = MakeMat("M_Student_Pants",    Hex("#33415C"));
        var dark  = MakeMat("M_Student_Dark",     Hex("#23202C"));
        var jacket = MakeMat("M_Student_Jacket",  Hex("#315E7E"));
        var accent = MakeMat("M_Student_Accent",  Hex("#F2B553"));

        var root = new GameObject("RescueStudent");
        BuildModel(root.transform, frame, tire, metal, seat, shirt, skin, pants, dark, jacket, accent);

        // Kinematic moving collider: the hand interactor needs a non-trigger box,
        // and the student moves at runtime.
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.88f, 1.4f, 1.16f);
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
        var refuge = GameObject.Find("2F Refuge Occupancy");
        if (refuge != null) serial.FindProperty("refugeVolume").objectReferenceValue = refuge.GetComponent<Collider>();
        serial.ApplyModifiedProperties();
        instance.transform.rotation = Quaternion.Euler(0, -90, 0); // face the 2F refuge; push handles face the B-stair arrival

        Selection.activeGameObject = instance;
        EditorUtility.SetDirty(instance);
        Debug.Log("[RescueStudent] Placed at " + PlacePosition + ". Nudge X/Z in Scene view if the corridor spot differs.");
    }

    // ---------------------------------------------------------------------
    // Model construction (model faces +Z, origin at floor level)
    // ---------------------------------------------------------------------

    private static void BuildModel(Transform root, Material frame, Material tire, Material metal,
        Material seat, Material shirt, Material skin, Material pants, Material dark, Material jacket, Material accent)
    {
        var chair = Group("Wheelchair", root);
        var student = Group("Student", root);
        Quaternion wheelAxis = Quaternion.Euler(0f, 0f, 90f); // cylinder axis -> X
        var rearRing = RingMesh("RearWheelRing", .275f, .035f, 24, 8);
        var handRing = RingMesh("HandRimRing", .232f, .009f, 24, 6);
        var casterRing = RingMesh("CasterRing", .051f, .016f, 16, 6);

        // ---- symmetric parts ----
        foreach (var sx in new[] { -1f, 1f })
        {
            float x = sx * 0.30f;

            // Open wheels have real curved tyres and visible spokes, rather than solid discs.
            MeshPart("Rear tyre", rearRing, chair, new Vector3(x, .31f, -.05f), tire, wheelAxis);
            MeshPart("Handrim", handRing, chair, new Vector3(sx * .355f, .31f, -.05f), metal, wheelAxis);
            Part("Rear hub", PrimitiveType.Cylinder, chair, new Vector3(x, .31f, -.05f),
                new Vector3(.08f, .025f, .08f), metal, wheelAxis);
            for (int spoke = 0; spoke < 8; spoke++)
            {
                float a = spoke * Mathf.PI / 4f;
                Vector3 hub = new Vector3(x, .31f, -.05f);
                Vector3 rim = hub + new Vector3(0, Mathf.Cos(a) * .245f, Mathf.Sin(a) * .245f);
                Rod("Spoke", chair, hub, rim, .010f, metal);
            }

            // Front caster
            MeshPart("Front caster", casterRing, chair, new Vector3(sx * .22f, .067f, .34f), tire, wheelAxis);
            Part("Caster stem", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.12f, 0.32f),
                new Vector3(0.025f, 0.08f, 0.025f), metal);

            // Frame: lower rail from rear axle down to front caster
            Part("Lower rail", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.24f, 0.20f, 0.14f),
                new Vector3(0.035f, 0.44f, 0.035f), frame, Quaternion.Euler(61f, 0f, 0f));
            Part("Seat support", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.39f, 0.20f),
                new Vector3(0.035f, 0.22f, 0.035f), frame);
            Part("Back post", PrimitiveType.Cylinder, chair, new Vector3(sx * 0.22f, 0.61f, -0.10f),
                new Vector3(0.035f, 0.64f, 0.035f), frame);
            Rod("Push handle stem", chair, new Vector3(sx * .22f, .89f, -.12f),
                new Vector3(sx * .22f, .96f, -.28f), .025f, metal);
            Part("Push grip", PrimitiveType.Capsule, chair, new Vector3(sx * .22f, .96f, -.30f),
                new Vector3(.055f, .12f, .055f), tire, Quaternion.Euler(90f, 0f, 0f));

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
            Part("Upper arm", PrimitiveType.Capsule, student, new Vector3(sx * .22f, .87f, .01f),
                new Vector3(.14f, .25f, .14f), jacket, Quaternion.Euler(20f, 0f, 0f));
            Part("Forearm", PrimitiveType.Capsule, student, new Vector3(sx * .23f, .70f, .15f),
                new Vector3(.12f, .23f, .12f), jacket, Quaternion.Euler(70f, 0f, 0f));
            Part("Hand", PrimitiveType.Sphere, student, new Vector3(sx * 0.23f, 0.62f, 0.21f),
                new Vector3(.10f, .075f, .12f), skin);

            // ---- student: legs ----
            Part("Thigh", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.47f, 0.26f),
                new Vector3(.17f, .14f, .36f), pants);
            Part("Shin", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.30f, 0.37f),
                new Vector3(.14f, .28f, .14f), pants);
            Part("Shoe", PrimitiveType.Cube, student, new Vector3(sx * 0.12f, 0.145f, 0.46f),
                new Vector3(.17f, .095f, .27f), dark);
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
        Part("Torso", PrimitiveType.Capsule, student, new Vector3(0f, .84f, -.01f),
            new Vector3(.37f, .24f, .28f), jacket, Quaternion.Euler(-6f, 0f, 0f));
        Part("Shirt front", PrimitiveType.Capsule, student, new Vector3(0f, .84f, .125f),
            new Vector3(.16f, .18f, .035f), shirt);
        Part("Jacket badge", PrimitiveType.Cube, student, new Vector3(.105f, .91f, .142f),
            new Vector3(.065f, .035f, .01f), accent);
        Part("Neck", PrimitiveType.Cylinder, student, new Vector3(0f, 1.07f, 0f),
            new Vector3(.075f, .055f, .075f), skin);
        Part("Head", PrimitiveType.Sphere, student, new Vector3(0f, 1.20f, 0.00f),
            new Vector3(.27f, .32f, .27f), skin);
        Part("Hair", PrimitiveType.Sphere, student, new Vector3(0f, 1.315f, -.02f),
            new Vector3(.29f, .13f, .29f), dark);
        foreach (float side in new[] { -1f, 1f })
        {
            Part("Eye", PrimitiveType.Sphere, student, new Vector3(side * .052f, 1.22f, .124f),
                new Vector3(.018f, .023f, .012f), dark);
            Part("Eyebrow", PrimitiveType.Cube, student, new Vector3(side * .052f, 1.257f, .121f),
                new Vector3(.047f, .008f, .012f), dark);
        }
        Part("Nose", PrimitiveType.Sphere, student, new Vector3(0f, 1.185f, .139f),
            new Vector3(.027f, .025f, .035f), skin);
        Rod("Smile", student, new Vector3(-.025f, 1.145f, .125f),
            new Vector3(.025f, 1.145f, .125f), .006f, dark);
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

    private static void Rod(string name, Transform parent, Vector3 from, Vector3 to, float diameter, Material mat)
    {
        var part = Part(name, PrimitiveType.Cylinder, parent, (from + to) * .5f,
            new Vector3(diameter, Vector3.Distance(from, to) * .5f, diameter), mat);
        part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
    }

    private static void MeshPart(string name, Mesh mesh, Transform parent, Vector3 position, Material mat, Quaternion rotation)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Mesh RingMesh(string name, float radius, float tube, int around, int across)
    {
        string path = MeshFolder + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = name };
        var vertices = new Vector3[(around + 1) * (across + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[around * across * 6];
        for (int a = 0; a <= around; a++)
        for (int b = 0; b <= across; b++)
        {
            float angle = a * Mathf.PI * 2 / around, cross = b * Mathf.PI * 2 / across;
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle), cb = Mathf.Cos(cross), sb = Mathf.Sin(cross);
            int i = a * (across + 1) + b;
            vertices[i] = new Vector3((radius + tube * cb) * ca, tube * sb, (radius + tube * cb) * sa);
            normals[i] = new Vector3(cb * ca, sb, cb * sa);
            uv[i] = new Vector2((float)a / around, (float)b / across);
        }
        int k = 0, stride = across + 1;
        for (int a = 0; a < around; a++)
        for (int b = 0; b < across; b++)
        {
            int i = a * stride + b;
            triangles[k++] = i; triangles[k++] = i + 1; triangles[k++] = i + stride;
            triangles[k++] = i + 1; triangles[k++] = i + stride + 1; triangles[k++] = i + stride;
        }
        mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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
