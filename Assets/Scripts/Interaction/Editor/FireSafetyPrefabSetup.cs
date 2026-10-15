using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FireSafetyPrefabSetup
{
    public const string ElevatorPath = "Assets/Prefabs/Environment/elevator(button))+wu.prefab";
    public const string WindowPath = "Assets/ThirdParty/Art/Interiors/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab";
    private const string ResourcesPath = "Assets/Scripts/Interaction/Resources/";

    [MenuItem("Tools/Fire Safety/Configure and validate interaction prefabs")]
    public static void Configure()
    {
        FireWindowMeshCache.EnsureCache();
        Font font = AssetDatabase.LoadAssetAtPath<Font>(ResourcesPath + "NotoSansCJKsc-Regular.otf");
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ResourcesPath + "FireWindowMesh.asset");
        if (font == null || mesh == null || !mesh.isReadable)
            throw new InvalidOperationException("Missing readable window mesh or warning font.");
        string materialPath = ResourcesPath + "FireSafetyRay.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader is missing.");
            material = new Material(shader) { name = "FireSafetyRay" };
            material.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        ConfigureOne(ElevatorPath, font, material, mesh, false);
        ConfigureOne(WindowPath, font, material, mesh, true);
        AssetDatabase.SaveAssets();
        Validate();
    }

    private static void ConfigureOne(string path, Font font, Material material, Mesh mesh, bool window)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var support = root.GetComponent<FireSafetyPrefabSupport>() ?? root.AddComponent<FireSafetyPrefabSupport>();
            SetReference(support, "warningFont", font);
            SetReference(support, "rayMaterial", material);
            if (window)
            {
                var behaviour = root.GetComponent<RuntimeFireWindow>() ?? root.AddComponent<RuntimeFireWindow>();
                SetReference(behaviour, "readableWindowMesh", mesh);
                var legacy = root.GetComponent<Window_Reminder>();
                if (legacy != null) legacy.enabled = false;
                foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    text.gameObject.SetActive(false);
            }
            else
            {
                var behaviour = root.GetComponent<FireSafetyElevator>() ?? root.AddComponent<FireSafetyElevator>();
                var colliders = root.GetComponentsInChildren<Collider>(true);
                SetReference(behaviour, "upButton", colliders.Single(c => c.name == "up"));
                SetReference(behaviour, "downButton", colliders.Single(c => c.name == "down"));
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Fire Safety/Validate interaction prefabs")]
    public static void Validate()
    {
        foreach (string path in new[] { ElevatorPath, WindowPath })
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var support = asset.GetComponent<FireSafetyPrefabSupport>();
            if (support == null || support.WarningFont == null || support.RayMaterial == null)
                throw new InvalidOperationException("Prefab dependencies are incomplete: " + path);
            if (asset.GetComponents<FireSafetyPrefabSupport>().Length != 1)
                throw new InvalidOperationException("Duplicate prefab support: " + path);
            if (path == WindowPath)
            {
                var component = asset.GetComponent<RuntimeFireWindow>();
                if (component == null) throw new InvalidOperationException("Window interaction is missing.");
                var mesh = new SerializedObject(component).FindProperty("readableWindowMesh").objectReferenceValue as Mesh;
                if (mesh == null || !mesh.isReadable) throw new InvalidOperationException("Window mesh is not readable.");
            }
            else
            {
                var component = asset.GetComponent<FireSafetyElevator>();
                if (component == null) throw new InvalidOperationException("Elevator interaction is missing.");
                var serialized = new SerializedObject(component);
                foreach (string field in new[] { "upButton", "downButton" })
                {
                    var button = serialized.FindProperty(field).objectReferenceValue as Collider;
                    if (button == null || !button.transform.IsChildOf(asset.transform))
                        throw new InvalidOperationException("Button reference is not inside the prefab: " + field);
                }
            }
            // Instantiate twice to check local reference remapping and name independence.
            for (int i = 0; i < 2; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(asset);
                try
                {
                    instance.name = "Renamed interaction test " + i;
                    var elevator = instance.GetComponent<FireSafetyElevator>();
                    if (elevator == null) continue;
                    foreach (string field in new[] { "upButton", "downButton" })
                    {
                        var button = (Collider)new SerializedObject(elevator).FindProperty(field).objectReferenceValue;
                        button.name = "Renamed button";
                        if (!button.transform.IsChildOf(instance.transform) ||
                            !elevator.TryGetButton(button, button.transform.position, out _) ||
                            elevator.TryGetButton(button, button.transform.position + Vector3.right * 3f, out _))
                            throw new InvalidOperationException("Instanced elevator binding/range validation failed.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }
        Debug.Log("FIRE_SAFETY_PREFABS_VALIDATED: both prefabs, dependencies, cloned button references, rename independence and elevator range.");
    }
}
