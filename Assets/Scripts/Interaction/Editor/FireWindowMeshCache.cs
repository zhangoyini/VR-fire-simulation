using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Generate a readable copy without changing the FBX importer or scene assets.
[InitializeOnLoad]
public sealed class FireWindowMeshCache : IPreprocessBuildWithReport
{
    private const string PrefabPath = "Assets/Stair Prefab/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab";
    private const string CachePath = "Assets/Scripts/Interaction/Resources/FireWindowMesh.asset";
    public int callbackOrder => 0;

    static FireWindowMeshCache()
    {
        EditorApplication.delayCall += EnsureCache;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) EnsureCache();
        };
    }

    public void OnPreprocessBuild(BuildReport report) => EnsureCache();

    private static void EnsureCache()
    {
        if (EditorApplication.isPlaying) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Mesh source = prefab.GetComponent<MeshFilter>().sharedMesh;
        if (source == null) throw new BuildFailedException("Window mesh is missing.");
        // Unity permits accessing non-readable imported meshes in edit mode.
        var copy = new Mesh { name = "FireWindowMesh", indexFormat = source.indexFormat };
        copy.vertices = source.vertices;
        copy.normals = source.normals;
        copy.tangents = source.tangents;
        copy.uv = source.uv;
        copy.uv2 = source.uv2;
        copy.colors = source.colors;
        copy.subMeshCount = source.subMeshCount;
        for (int i = 0; i < source.subMeshCount; i++)
            copy.SetTriangles(source.GetTriangles(i), i);
        copy.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Interaction/Resources"))
            AssetDatabase.CreateFolder("Assets/Scripts/Interaction", "Resources");
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(CachePath);
        if (existing == null) AssetDatabase.CreateAsset(copy, CachePath);
        else
        {
            EditorUtility.CopySerialized(copy, existing);
            Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssets();
    }
}
