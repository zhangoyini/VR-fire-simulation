#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Harbourview.EditorTools
{
    /// <summary>
    /// First-time project bootstrap.  Run "Harbourview / Apply URP Settings" after opening the
    /// project in Unity Hub, and "Harbourview / Check Project Setup" whenever dependencies change.
    /// </summary>
    public static class HarbourviewProjectSetup
    {
        [MenuItem("Harbourview/Apply URP Settings", false, 1)]
        public static void ApplyUrpSettings()
        {
            RenderPipelineAsset asset = FindRenderPipelineAsset();
            if (asset == null)
            {
                Debug.LogError("[Harbourview] No Universal Render Pipeline Asset found under Assets/. " +
                               "Check that com.unity.render-pipelines.universal 14.0.12 is listed in Packages/manifest.json.");
                return;
            }

            // The quality levels inherit this asset whenever they leave it set to None,
            // so there is no need to touch every quality entry.
            GraphicsSettings.renderPipelineAsset = asset;
            Debug.Log("[Harbourview] Render pipeline set to " + asset.name + " (" + AssetDatabase.GetAssetPath(asset) + ")");
        }

        [MenuItem("Harbourview/Check Project Setup", false, 2)]
        public static void CheckSetup()
        {
            var report = new StringBuilder();
            report.AppendLine("[Harbourview] Project setup report");

            RenderPipelineAsset asset = FindRenderPipelineAsset();
            bool urpInstalled = Shader.Find("Universal Render Pipeline/Lit") != null;
            bool urpAssigned = GraphicsSettings.renderPipelineAsset != null;

            report.Append(Line("URP package present", urpInstalled));
            report.Append(Line("URP asset in Assets/", asset != null));
            report.Append(Line("GraphicsSettings uses URP", urpAssigned));

            bool inputSystem = System.Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem") != null;
            bool xri = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XROrigin, Unity.XR.Interaction.Toolkit") != null;
            bool openXrManagement = System.Type.GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management") != null;

            report.Append(Line("Input System package (required by XRI)", inputSystem));
            report.Append(Line("XR Interaction Toolkit", xri));
            report.Append(Line("XR Plug-in Management", openXrManagement));

            bool scene = AssetDatabase.LoadAssetAtPath<Object>("Assets/_Project/Scenes/AssemblyArea_1F.unity") != null;
            report.Append(Line("Assembly area scene generated", scene));

            if (inputSystem && !UsesNewInputSystem())
            {
                report.AppendLine("  -> Player > Active Input Handling must be set to Input System Package (New) for XRI to work.");
            }

            if (openXrManagement)
            {
                report.AppendLine("  -> OpenXR checklist: add the Meta Quest Support interaction profile before building to Quest 3.");
            }

            Debug.Log(report.ToString());
        }

        private static bool UsesNewInputSystem()
        {
#if ENABLE_INPUT_SYSTEM
            return true;
#else
            return false;
#endif
        }

        private static string Line(string label, bool ok)
        {
            return "  [" + (ok ? "OK" : "MISSING") + "] " + label + "\n";
        }

        private static RenderPipelineAsset FindRenderPipelineAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
        }
    }
}
#endif
