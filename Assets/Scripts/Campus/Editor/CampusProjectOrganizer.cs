using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// AssetDatabase moves keep .meta GUIDs and therefore all prefab/scene/material references.
public static class CampusProjectOrganizer
{
    private static readonly string[,] Moves = {
        {"Assets/Baic Asset", "Assets/ThirdParty/Art/BasicAssets"},
        {"Assets/Elevator Exit sign prefab", "Assets/ThirdParty/Art/OfficeBuilding"},
        {"Assets/school basic prefab", "Assets/ThirdParty/Art/School"},
        {"Assets/Stair Prefab", "Assets/ThirdParty/Art/Interiors"},
        {"Assets/Window Prefab", "Assets/ThirdParty/Art/Windows"},
        {"Assets/Red light prefab", "Assets/ThirdParty/Art/EmergencyLighting"},
        {"Assets/enviromet effect", "Assets/ThirdParty/Art/Effects"},
        {"Assets/prefab", "Assets/Prefabs/Environment"},
        {"Assets/Playground Prefab", "Assets/Prefabs/LegacyAssembly"},
        {"Assets/HealthTimeBar prefab", "Assets/Prefabs/UI"},
        {"Assets/Scripts/Interaction2", "Assets/Scripts/Health"},
        {"Assets/Scripts/Testing", "Assets/Scripts/Legacy"},
        {"Assets/Scenes/1F/Scripts", "Assets/Scripts/Journey"},
        {"Assets/Scenes/1F/Art", "Assets/Art/LegacyAssembly"},
        {"Assets/Scripts/HealthTimeBar_Test/HealthTimeBar_Test.unity", "Assets/Scenes/Tests/HealthTimeBar_Test.unity"},
        {"Assets/Scripts/HealthTimeBar_Test/low_health_warning.wav", "Assets/Audio/Health/low_health_warning.wav"},
        {"Assets/Scripts/HealthTimeBar_Test/towel_pickup.wav", "Assets/Audio/Health/towel_pickup.wav"},
        {"Assets/Scripts/HealthTimeBar_Test/TowelPreviewMat.mat", "Assets/Art/LegacyAssembly/Materials/TowelPreviewMat.mat"},
        {"Assets/Scenes/whole_floor.unity", "Assets/Scenes/Legacy/whole_floor.unity"},
        {"Assets/Scenes/SampleScene.unity", "Assets/Scenes/Legacy/SampleScene.unity"},
        {"Assets/Scenes/Yining.unity", "Assets/Scenes/Legacy/Yining.unity"},
        {"Assets/Scenes/1F/Scenes/AssemblyArea_1F.unity", "Assets/Scenes/Legacy/AssemblyArea_1F.unity"},
        {"Assets/Scenes/SystemUI/SystemUITest.unity", "Assets/Scenes/Tests/SystemUITest.unity"},
        {"Assets/Test", "Assets/Legacy/TestAssets"},
        {"Assets/NewBehaviourScript.cs", "Assets/Scripts/Legacy/NewBehaviourScript.cs"},
        {"Assets/New Universal Render Pipeline Asset.asset", "Assets/Settings/Rendering/CampusURP.asset"},
        {"Assets/New Universal Render Pipeline Asset_Renderer.asset", "Assets/Settings/Rendering/CampusRenderer.asset"},
        {"Assets/UniversalRenderPipelineGlobalSettings.asset", "Assets/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset"}
    };

    public static string Remap(string path)
    {
        for(int i=0;i<Moves.GetLength(0);i++)
            if(path==Moves[i,0] || path.StartsWith(Moves[i,0]+"/",StringComparison.Ordinal)) return Moves[i,1]+path.Substring(Moves[i,0].Length);
        return path;
    }

    [MenuItem("Tools/Campus/Organize Project Assets")]
    public static void Run()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RemoveRetiredStairComponents();
        for(int i=0;i<Moves.GetLength(0);i++) {
            string source=Moves[i,0],target=Moves[i,1];
            if(!File.Exists(source) && !AssetDatabase.IsValidFolder(source)) continue;
            CampusSceneBuilder.Folder(Path.GetDirectoryName(target).Replace('\\','/'));
            string error=AssetDatabase.MoveAsset(source,target);
            if(!string.IsNullOrEmpty(error)) throw new InvalidOperationException(source+": "+error);
        }
        // Update editor tools' string paths (serialized references were already preserved by GUID).
        foreach(string path in AssetDatabase.FindAssets("t:MonoScript",new[]{"Assets/Scripts"}).Select(AssetDatabase.GUIDToAssetPath)) {
            if(path.Contains("/Campus/")) continue; // builder deliberately supports old/new asset layout
            string original=File.ReadAllText(path),updated=original;
            for(int i=0;i<Moves.GetLength(0);i++) updated=updated.Replace(Moves[i,0],Moves[i,1]);
            updated=updated.Replace("Assets/1F/Scenes/AssemblyArea_1F.unity","Assets/Scenes/Legacy/AssemblyArea_1F.unity");
            updated=updated.Replace("Assets/1F/Art","Assets/Art/LegacyAssembly");
            updated=updated.Replace("\"whole_floor\"","\"CampusTraining\"");
            if(updated!=original) File.WriteAllText(path,updated);
        }
        foreach(string path in new[]{"Assets/Scripts/Interaction/StairTeleportTrigger.cs","Assets/Scripts/Managers/FloorManager.cs"})
            if(File.Exists(path)) AssetDatabase.DeleteAsset(path);
        foreach(string folder in new[]{"Assets/Scripts/HealthTimeBar_Test","Assets/Scripts/Managers","Assets/Scenes/1F/Scenes","Assets/Scenes/1F","Assets/Scenes/SystemUI"})
            if(Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
        AssetDatabase.SaveAssets();
        // Keep an index of *all* assets, including vendor content not used in the new scene.
        Directory.CreateDirectory("Documentation");
        File.WriteAllLines("Documentation/AssetIndex.tsv",new[]{"GUID\tPath"}.Concat(AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")).OrderBy(p=>p).Select(p=>AssetDatabase.AssetPathToGUID(p)+"\t"+p)));
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("CAMPUS_ORGANIZE_COMPLETE — GUIDs preserved; obsolete stair transition components removed.");
    }

    private static void RemoveRetiredStairComponents()
    {
        // Remove references first; otherwise deleting scripts would leave missing components in archives.
        foreach(string path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath)) {
            var scene=EditorSceneManager.OpenScene(path); bool changed=false;
            foreach(var root in scene.GetRootGameObjects()) foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if(component!=null && (component.GetType().Name=="StairTeleportTrigger" || component.GetType().Name=="FloorManager")) {
                    UnityEngine.Object.DestroyImmediate(component); changed=true;
                }
            foreach(var nav in UnityEngine.Object.FindObjectsOfType<SystemUINavigation>(true)) { CampusSceneBuilder.SetString(nav,"gameplayEntrySceneName","CampusTraining"); changed=true; }
            if(changed) EditorSceneManager.SaveScene(scene);
        }
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/prefab"}).Select(AssetDatabase.GUIDToAssetPath)) {
            string yaml=File.ReadAllText(path);
            string[] retired={AssetDatabase.AssetPathToGUID("Assets/Scripts/Interaction/StairTeleportTrigger.cs"),AssetDatabase.AssetPathToGUID("Assets/Scripts/Managers/FloorManager.cs")};
            if(!retired.Any(g=>!string.IsNullOrEmpty(g) && yaml.Contains(g))) continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component!=null && (component.GetType().Name=="StairTeleportTrigger" || component.GetType().Name=="FloorManager")) UnityEngine.Object.DestroyImmediate(component);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
