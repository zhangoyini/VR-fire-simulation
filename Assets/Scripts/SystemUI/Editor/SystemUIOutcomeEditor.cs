#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Presentation previews only; excluded from builds and never decide win/lose conditions.
[InitializeOnLoad]
public static class SystemUIOutcomeEditor
{
    private const string ScenePath = "Assets/Scenes/Tests/SystemUITest.unity";
    private static SystemUIView previewView;

    static SystemUIOutcomeEditor()
    {
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            if (previewView != null && previewView.gameObject.scene == scene)
                RestoreGameplay();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                RestoreGameplay();
        };
        AssemblyReloadEvents.beforeAssemblyReload += RestoreGameplay;
    }

    [MenuItem("Tools/System UI/Preview/Gameplay")]
    public static void PreviewGameplay() => Preview(0);

    [MenuItem("Tools/System UI/Preview/Game Over")]
    public static void PreviewGameOver() => Preview(1);

    [MenuItem("Tools/System UI/Preview/Victory")]
    public static void PreviewVictory() => Preview(2);

    // Can also be run with Unity -executeMethod for verification without VR hardware.
    [MenuItem("Tools/System UI/Validate Outcome Panels")]
    public static void ValidateSavedPanels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Validate outside Play Mode.");

        const string fontPath = "Assets/Fonts/NotoSansCJKsc-Regular SDF.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null || !font.TryAddCharacters("游戏结束训练完成重新开始主菜单", out string missing))
            throw new System.InvalidOperationException("Required Chinese font/glyphs are unavailable.");
        foreach (Texture2D texture in font.atlasTextures)
            if (texture != null) EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject canvas = null;
            GameObject root = null;
            foreach (GameObject item in scene.GetRootGameObjects())
            {
                if (item.name == "SystemUICanvas") canvas = item;
                if (item.name == "SystemUIRoot") root = item;
            }
            if (canvas == null || root == null || root.GetComponents<SystemUINavigation>().Length != 1)
                throw new System.InvalidOperationException("Expected existing UI roots and one navigation component.");
            SystemUINavigation navigation = root.GetComponent<SystemUINavigation>();
            var settings = new SerializedObject(navigation);
            if (settings.FindProperty("gameplayEntrySceneName").stringValue != "CampusTraining" ||
                settings.FindProperty("mainMenuSceneName").stringValue != "MainMenu")
                throw new System.InvalidOperationException("Incorrect navigation scene configuration.");

            if (!canvas.transform.Find("GameplayPanel").gameObject.activeSelf)
                throw new System.InvalidOperationException("Gameplay must be active by default.");
            foreach (string panelName in new[] { "GameOverPanel", "VictoryPanel" })
            {
                Transform panel = canvas.transform.Find(panelName);
                if (panel == null || panel.gameObject.activeSelf)
                    throw new System.InvalidOperationException("Outcome must be inactive: " + panelName);
                foreach (TextMeshProUGUI text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (text.font != font)
                        throw new System.InvalidOperationException("Incorrect font: " + text.name);
                foreach (string buttonName in new[] { "RestartButton", "MainMenuButton" })
                {
                    Button button = panel.Find(buttonName).GetComponent<Button>();
                    string method = buttonName == "RestartButton" ? "RestartGame" : "ReturnToMainMenu";
                    if (button.onClick.GetPersistentEventCount() != 1 ||
                        button.onClick.GetPersistentTarget(0) != navigation ||
                        button.onClick.GetPersistentMethodName(0) != method)
                        throw new System.InvalidOperationException("Incorrect button binding: " + panelName + "/" + buttonName);
                }
            }
            Debug.Log("[System UI] VALIDATION PASSED: bilingual fonts, four navigation bindings, " +
                "one navigation component, and gameplay default states.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void Preview(int panel)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[System UI] Open SystemUITest to preview its panels.");
            return;
        }

        GameObject root = null;
        GameObject canvas = null;
        foreach (GameObject item in scene.GetRootGameObjects())
        {
            if (item.name == "SystemUIRoot") root = item;
            if (item.name == "SystemUICanvas") canvas = item;
        }
        SystemUIView view = root == null ? null : root.GetComponent<SystemUIView>();
        if (view == null || canvas == null)
        {
            Debug.LogWarning("[System UI] The existing SystemUIView or canvas is missing.");
            return;
        }

        string name = panel == 0 ? "GameplayPanel" : panel == 1 ? "GameOverPanel" : "VictoryPanel";
        Transform target = canvas.transform.Find(name);
        if (target == null)
        {
            Debug.LogWarning("[System UI] Missing preview panel: " + name);
            return;
        }

        if (!EditorApplication.isPlaying)
            previewView = view;
        if (panel == 0) view.ShowGameplay();
        else if (panel == 1) view.ShowGameOver();
        else view.ShowVictory();

        // Frame the UI in Scene view without moving any runtime camera or VR rig.
        Selection.activeGameObject = target.gameObject;
        if (!EditorApplication.isPlaying && SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[System UI] Previewing " + name + ". Edit-mode previews restore gameplay " +
            "before saving or entering Play Mode.");
    }

    private static void RestoreGameplay()
    {
        if (previewView != null)
            previewView.ShowGameplay();
        previewView = null;
    }
}
#endif
