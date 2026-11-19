using UnityEditor;
using UnityEngine;

public sealed class FireTrainingTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/FireTrainingUI/")) return;
        Configure((TextureImporter)assetImporter);
    }
    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = importer.assetPath.Contains("/icons/") ? 256 : importer.assetPath.Contains("/buttons/") ? 512 : 2048;
    }
    public static void Reimport()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/FireTrainingUI" }))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            Configure(importer);
            importer.SaveAndReimport();
        }
    }
}
