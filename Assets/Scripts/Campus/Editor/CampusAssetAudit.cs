using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CampusAssetAudit
{
    [Serializable] public class Audit { public int files; public SceneInfo[] scenes; public Prop[] props; }
    [Serializable] public class SceneInfo { public string path; public Root[] roots; }
    [Serializable] public class Root { public string name; public string[] scripts; }
    [Serializable] public class Prop { public string path; public Vector3 size; public Vector3 center; }
    public static void Run()
    {
        var scenes = new List<SceneInfo>();
        var sceneFolders = new[] { "Assets/Scenes", "Assets/Scripts/HealthTimeBar_Test" }.Where(AssetDatabase.IsValidFolder).ToArray();
        foreach (string path in AssetDatabase.FindAssets("t:Scene", sceneFolders).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            scenes.Add(new SceneInfo { path = path, roots = scene.GetRootGameObjects().Select(g => new Root {
                name = g.name, scripts = g.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null).Select(c => c.GetType().FullName).Distinct().ToArray()
            }).ToArray() });
        }
        var props = new List<Prop>();
        var prefabFolders = new[] { CampusSceneBuilder.Resolve("Assets/prefab"), CampusSceneBuilder.Resolve("Assets/school basic prefab/Prefabs/props") };
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", prefabFolders).Select(AssetDatabase.GUIDToAssetPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = asset.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<TMPro.TMP_Text>() == null).ToArray();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            props.Add(new Prop { path = path, size = b.size, center = b.center });
        }
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CampusAudit"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "campus-asset-audit.json"), JsonUtility.ToJson(new Audit { files = AssetDatabase.GetAllAssetPaths().Length, scenes = scenes.ToArray(), props = props.ToArray() }, true));
        Debug.Log("CAMPUS_AUDIT_COMPLETE");
    }
}
