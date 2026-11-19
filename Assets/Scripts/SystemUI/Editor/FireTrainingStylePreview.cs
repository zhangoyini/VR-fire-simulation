using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FireTrainingStylePreview
{
    public static void CaptureHealthStates()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HealthTimeCanvas.prefab");
        float[] values = { .78f, .45f, .18f, .45f };
        string[] icons = { "icon_health", "icon_warning", "icon_cross", "icon_protection" };
        for (int i = 0; i < values.Length; i++)
        {
            var root = Object.Instantiate(prefab);
            root.transform.SetPositionAndRotation(new Vector3(0, .525f - i * .35f, 2), Quaternion.identity);
            root.transform.localScale = Vector3.one;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var bar = root.GetComponentInChildren<Jiao.HealthTime.HealthTimeBar>(true);
            bar.ApplyArtwork();
            // Edit-mode previews do not call Start, which normally hides this
            // legacy low-health flash overlay before gameplay begins.
            bar.StopWarning();
            bar.SendMessage("SetTowelProtection", i == 3);
            bar.SendMessage("OnHealthChanged", values[i]);
            if (bar.CurrentStateIcon != icons[i]) throw new System.InvalidOperationException("Incorrect health state icon: " + i);
            var number = root.transform.Find("HealthTimeSlider/Health value");
            if (number == null || number.GetComponent<TMPro.TMP_Text>().text != Mathf.RoundToInt(values[i] * 100).ToString())
                throw new System.InvalidOperationException("Health value does not match live percentage.");
            if (i == 3)
            {
                bar.SendMessage("SetTowelProtection", false);
                if (bar.CurrentStateIcon != "icon_warning") throw new System.InvalidOperationException("Protection off must restore the health threshold icon.");
                bar.SendMessage("SetTowelProtection", true);
            }
        }
        Canvas.ForceUpdateCanvases();
        foreach (var text in Object.FindObjectsOfType<TMPro.TMP_Text>())
        { text.ForceMeshUpdate(); if (text.isTextOverflowing) throw new System.InvalidOperationException("Health value overflow."); }
        var camera = new GameObject("Health states camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1);
        camera.orthographic = true; camera.orthographicSize = .85f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 1100, 24); camera.targetTexture = rt;
        camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/HealthHUD_FourStates.png", png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("HEALTH_HUD_FOUR_STATES_PASS: health/warning/danger/protection icons, live values and protection-off restoration.");
    }
    public static void CaptureCompactWarning()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Campus/CampusTraining.unity");
        var head = Object.FindObjectOfType<CampusLocomotion>(true).TrackedHead;
        foreach (var anchor in Object.FindObjectsOfType<WorldSpaceUIAnchor>()) anchor.SendMessage("Place");
        Canvas.ForceUpdateCanvases();
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var group = CampusCompactWarning.Create(head, font, out var label);
        var placement = group.GetComponent<CampusCompactWarning>();
        string[] warnings = { CampusMessagePresenter.SmokeWarning, CampusMessagePresenter.SmokeRules,
            CampusMessagePresenter.ElevatorWarning, CampusMessagePresenter.WindowWarning, CampusMessagePresenter.RescueStairWarning };
        foreach (var copy in warnings)
        {
            placement.SetMessage(copy); Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
            if (label.isTextOverflowing) throw new System.InvalidOperationException("Compact warning overflow: " + copy);
        }
        placement.Place();
        var slider = Object.FindObjectOfType<Jiao.HealthTime.HealthTimeBar>(true).GetComponentInChildren<Slider>(true);
        var healthCorners = new Vector3[4]; ((RectTransform)slider.transform).GetWorldCorners(healthCorners);
        var warningCorners = new Vector3[4]; group.GetComponentInChildren<Image>().rectTransform.GetWorldCorners(warningCorners);
        if (head.InverseTransformPoint(warningCorners[1]).y > head.InverseTransformPoint(healthCorners[0]).y - .03f)
            throw new System.InvalidOperationException("Warning must leave a gap below the health slider.");
        if (Vector3.Distance(warningCorners[0], warningCorners[3]) > .75f)
            throw new System.InvalidOperationException("Warning is wider than the compact target.");
        foreach (var foreground in Object.FindObjectsOfType<CampusUIForeground>()) foreground.Refresh();
        foreach (var camera in Object.FindObjectsOfType<Camera>()) camera.enabled = false;
        var shot = new GameObject("Compact warning preview camera").AddComponent<Camera>();
        shot.transform.SetPositionAndRotation(head.position, head.rotation);
        shot.fieldOfView = 75; shot.nearClipPlane = .01f;
        var rt = new RenderTexture(1440, 900, 24); shot.targetTexture = rt;
        shot.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/Warning_BelowHealth.png", png.EncodeToPNG());
        foreach (var copy in new[] { CampusMessagePresenter.ElevatorWarning, CampusMessagePresenter.WindowWarning })
        {
            placement.SetMessage(copy); Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
            shot.Render(); RenderTexture.active = rt;
            png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
            File.WriteAllBytes("Documentation/Previews/Warning_" +
                (copy == CampusMessagePresenter.ElevatorWarning ? "NoElevator" : "NoJump") + ".png", png.EncodeToPNG());
        }
        RenderTexture.active = null; shot.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        var windowTest = new GameObject("First window opening check");
        var box = windowTest.AddComponent<BoxCollider>(); box.center = new Vector3(-.5f, 0, 0); box.size = new Vector3(1, 1, .1f);
        var hinge = windowTest.AddComponent<CampusHingeInteractable>(); hinge.Configure(windowTest.transform, 1, true);
        var hand = new GameObject("Window test hand").transform; hand.position = new Vector3(-1, 0, .05f);
        Physics.SyncTransforms(); hinge.OnInteractStart(hand);
        hand.position = Quaternion.AngleAxis(20, Vector3.up) * hand.position;
        hinge.OnInteract(hand);
        bool warned = (bool)typeof(CampusHingeInteractable).GetField("warnedThisOpening", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(hinge);
        if (hinge.Angle <= .1f || !hinge.IsHeld || !warned)
            throw new System.InvalidOperationException("First opening must warn while still held, without a second grip.");
        Object.DestroyImmediate(hand.gameObject); Object.DestroyImmediate(windowTest);
        Debug.Log("FIRST_WINDOW_OPEN_WARNING_PASS");
        Debug.Log("COMPACT_WARNING_LAYOUT_PASS: five bilingual warnings fit; card is below health with a gap and under 0.75m wide.");
    }
    public static void CaptureTaskNotice()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var parent = new GameObject("Task notice preview");
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var group = FireTrainingTaskNotice.Create(parent.transform, font, out var label);
        var source = File.ReadAllText("Assets/Scripts/Modules/E_LessonTasksUI/CampusExerciseTasks.cs");
        var matches = System.Text.RegularExpressions.Regex.Matches(source,
            @"(?:QueueNotice|Announce)\(""((?:\\.|[^""\\])*)""");
        if (matches.Count == 0) throw new System.InvalidOperationException("No live task notification copy found.");
        string longest = "";
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string copy = System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value);
            label.text = copy;
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            if (label.isTextOverflowing) throw new System.InvalidOperationException("Task notification overflow: " + copy);
            if (copy.Length > longest.Length) longest = copy;
        }
        label.text = longest;
        Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
        var camera = new GameObject("Task notification camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, .09f, 0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/TaskNotice_MainScreen.png", png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("TASK_NOTICE_LAYOUT_PASS: " + matches.Count + " live bilingual notices fit the screen panel.");
    }
    public static void CaptureTaskCards()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Task cards preview", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.transform.localScale = Vector3.one * .0018f;
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var cards = new CampusTaskCardList(root.transform, font);
        cards.SetEntries(new[] {
            new CampusTaskCardList.Entry("主任务 · 到操场集合", "Main · Reach the assembly area"),
            new CampusTaskCardList.Entry("1 · 到 411 寻找毛巾", "1 · Find a towel in room 411", true),
            new CampusTaskCardList.Entry("2 · 打湿毛巾并下到三楼", "2 · Wet towel, reach 3F", true),
            new CampusTaskCardList.Entry("3 · 前往 B 楼梯", "3 · Reach Stair B", true),
            new CampusTaskCardList.Entry("可选 · 护送同学到二楼避难区", "Optional · Escort to 2F refuge"),
            new CampusTaskCardList.Entry("4 · 找到出口并走到室外", "4 · Find exit, go outside") });
        cards.Show(1);
        Canvas.ForceUpdateCanvases();
        foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) throw new System.InvalidOperationException("Task card text overflow: " + text.text);
        }
        int count = 0;
        foreach (var image in root.GetComponentsInChildren<Image>())
            if (image.name == "Completion check") count++;
        if (count != 3) throw new System.InvalidOperationException("Completed cards must show exactly three checks.");
        var camera = new GameObject("Task card preview camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1.45f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/TaskCards_AllTasks.png", png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("TASK_CARDS_LAYOUT_PASS: six bilingual cards, three completion checks, no text overflow.");
    }
    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity");
        FireTrainingSceneStyle.Apply(scene);
        var canvas = GameObject.Find("MainMenuCanvas").GetComponent<Canvas>();
        foreach (var button in Object.FindObjectsOfType<VRMenuButton>(true))
            FireTrainingUIStyle.Apply(button.GetComponent<Image>(), "buttons/button_" + (button.name.Contains("Start") ? "primary" : "secondary") + "_normal");
        Canvas.ForceUpdateCanvases();
        var go = new GameObject("UI artwork preview camera");
        var camera = go.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(canvas.transform.position - canvas.transform.forward * 1.6f, canvas.transform.rotation);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .20f, .24f);
        camera.nearClipPlane = .01f;
        camera.fieldOfView = 60;
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/HandDrawn_MainMenu.png", png.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(png);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        Debug.Log("HAND_DRAWN_UI_PREVIEW_COMPLETE");
    }
}
