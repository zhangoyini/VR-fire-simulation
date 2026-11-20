using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class RescueDialoguePreview
{
    public static void Capture()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var head = new GameObject("Preview head").transform;
        var type = typeof(CampusLessonDirector).Assembly.GetType("CampusRescueDialogue");
        Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { head, new UnityEngine.Events.UnityAction(() => {}) }, null);
        var root = head.Find("Classmate conversation").gameObject;
        root.SetActive(true);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.font = font;
        Canvas.ForceUpdateCanvases();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate();
            if (label.isTextOverflowing) throw new Exception("Dialogue text overflow: " + label.text);
        }
        var image = root.GetComponentInChildren<Image>();
        if (image.sprite == null || !image.sprite.name.Contains("lesson_card_compact"))
            throw new Exception("Missing lesson card background: " + image.sprite);
        if (File.ReadAllText("Assets/Scripts/Modules/E_LessonTasksUI/CampusExerciseTasks.cs").Contains("Announce(\"<b>可选任务"))
            throw new Exception("Optional task popup still published");
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f,.20f,.24f);
        var rt = new RenderTexture(1440,900,24);
        camera.targetTexture = rt;
        camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(1440,900,TextureFormat.RGB24,false);
        png.ReadPixels(new Rect(0,0,1440,900),0,0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/RescueDialogue_Compact.png",png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(png); UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log("RESCUE_DIALOGUE_PREVIEW_PASS: text fits, compact artwork present, optional popup removed.");
    }
}