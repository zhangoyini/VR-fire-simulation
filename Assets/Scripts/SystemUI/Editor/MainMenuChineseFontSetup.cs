#if UNITY_EDITOR
using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

// Uses Unity's asset APIs rather than hand-writing TMP font serialization.
public static class MainMenuChineseFontSetup
{
    private const string SourcePath = "Assets/Fonts/NotoSansCJKsc-Regular.otf";
    private const string FontAssetPath = "Assets/Fonts/NotoSansCJKsc-Regular SDF.asset";
    private const string ScenePath = "Assets/Scenes/MainMenu/MainMenu.unity";

    [MenuItem("Tools/System UI/Configure Chinese Menu Font")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[System UI Font] Exit Play Mode before configuring the font.");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
        {
            Debug.LogWarning("[System UI Font] MainMenu has unsaved edits. Save it, then run " +
                "Tools > System UI > Configure Chinese Menu Font again.");
            return;
        }

        bool openedHere = false;
        try
        {
            TMP_FontAsset fontAsset = GetOrCreateFontAsset();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            GameObject canvas = Array.Find(scene.GetRootGameObjects(),
                root => root.name == "MainMenuCanvas");
            if (canvas == null)
                throw new InvalidOperationException("MainMenuCanvas was not found.");

            // Validate all targets before changing any scene component.
            TextMeshProUGUI title = GetText(canvas.transform, "Title");
            TextMeshProUGUI start = GetText(canvas.transform, "StartButton/Text");
            TextMeshProUGUI exit = GetText(canvas.transform, "ExitButton/Text");

            Apply(title, fontAsset,
                "<b>FIRE EVACUATION TRAINING</b>\n<size=40>火灾疏散训练</size>");
            Apply(start, fontAsset, "<b>START</b>\n<size=28>开始</size>");
            Apply(exit, fontAsset, "<b>EXIT</b>\n<size=28>退出</size>");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save MainMenu.");

            Debug.Log("[System UI Font] Chinese glyphs verified. Updated MainMenu Title, " +
                "StartButton/Text and ExitButton/Text using " + FontAssetPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        AssetDatabase.ImportAsset(SourcePath);
        var importer = AssetImporter.GetAtPath(SourcePath) as TrueTypeFontImporter;
        if (importer == null)
            throw new InvalidOperationException("Import the Noto source font first: " + SourcePath);
        if (!importer.includeFontData)
        {
            importer.includeFontData = true;
            importer.SaveAndReimport();
        }

        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (asset == null || asset.material == null || asset.atlasTextures[0] == null)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            TMP_FontAsset generated = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (generated == null)
                throw new InvalidOperationException("Unity could not create the Noto TMP font asset.");

            // Keep transient subassets alive across CreateAsset's import/reload.
            Material material = generated.material;
            Texture2D[] textures = generated.atlasTextures;
            if (asset == null)
            {
                asset = generated;
                AssetDatabase.CreateAsset(asset, FontAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(generated, asset);
                UnityEngine.Object.DestroyImmediate(generated);
            }
            asset.name = "NotoSansCJKsc-Regular SDF";
            material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);
            foreach (Texture2D texture in textures)
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            asset.material = material;
            asset.atlasTextures = textures;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        // Prewarm current and common future UI; dynamic multi-atlas supports later labels.
        var characters = new StringBuilder();
        for (int code = 32; code <= 126; code++)
            characters.Append((char)code);
        characters.Append("火灾消防疏散训练开始游戏退出结束失败胜利成功重新主菜单返回继续暂停设置确认取消剩余时间生命值");
        if (!asset.TryAddCharacters(characters.ToString(), out string missing))
            throw new InvalidOperationException("Noto TMP font is missing required characters: " + missing);

        foreach (Texture2D texture in asset.atlasTextures)
        {
            if (texture == null)
                continue;
            if (!AssetDatabase.Contains(texture))
            {
                texture.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            EditorUtility.SetDirty(texture);
        }
        EditorUtility.SetDirty(asset.material);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static TextMeshProUGUI GetText(Transform canvas, string path)
    {
        Transform target = canvas.Find(path);
        TextMeshProUGUI text = target == null ? null : target.GetComponent<TextMeshProUGUI>();
        if (text == null)
            throw new InvalidOperationException("Missing MainMenu TMP text: " + path);
        return text;
    }

    private static void Apply(TextMeshProUGUI text, TMP_FontAsset asset, string label)
    {
        text.font = asset;
        text.fontSharedMaterial = asset.material;
        text.text = label;
        text.ForceMeshUpdate(true, true);
        EditorUtility.SetDirty(text);
    }
}
#endif
