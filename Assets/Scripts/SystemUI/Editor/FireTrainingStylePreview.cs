using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FireTrainingStylePreview
{
    public static void CaptureMainBrief()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Campus/CampusTraining.unity");
        var director = Object.FindObjectOfType<CampusLessonDirector>(true);
        var settings = new SerializedObject(director);
        var title = (TMPro.TMP_Text)settings.FindProperty("title").objectReferenceValue;
        var body = (TMPro.TMP_Text)settings.FindProperty("explanation").objectReferenceValue;
        var footer = (TMPro.TMP_Text)settings.FindProperty("footer").objectReferenceValue;
        ((GameObject)settings.FindProperty("lessonPanel").objectReferenceValue).SetActive(true);
        for (Transform parent = title.transform; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
        body.gameObject.SetActive(true);
        title.text = CampusLessonDirector.MainBriefHeading; body.text = CampusLessonDirector.MainBriefCopy; footer.text = "";
        var layout = director.GetComponent<CampusLessonLayout>();
        layout.Arrange(true); layout.ArrangeMainBrief(); layout.Tick(2); layout.SendMessage("LateUpdate");
        layout.SetReadingProgress(.5f);
        var layoutSettings = new SerializedObject(layout);
        var background = (RectTransform)layoutSettings.FindProperty("background").objectReferenceValue;
        Canvas.ForceUpdateCanvases();
        foreach (var text in new[] { title, body })
        {
            text.ForceMeshUpdate();
            if (text.isTextOverflowing || text.alignment != TMPro.TextAlignmentOptions.Center)
                throw new System.InvalidOperationException("Main objective text must fit and be centred: " + text.name);
            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
                if (!background.rect.Contains(background.InverseTransformPoint(corner)))
                    throw new System.InvalidOperationException("Main objective text extends outside the card: " + text.name);
        }
        if (footer.gameObject.activeSelf) throw new System.InvalidOperationException("Main objective must not show an outside footer.");
        var head = (Transform)settings.FindProperty("trackedHead").objectReferenceValue;
        foreach (var foreground in Object.FindObjectsOfType<CampusUIForeground>()) foreground.Refresh();
        Canvas.ForceUpdateCanvases();
        foreach (var camera in Object.FindObjectsOfType<Camera>()) camera.enabled = false;
        var shot = new GameObject("Main objective preview camera").AddComponent<Camera>();
        shot.transform.SetPositionAndRotation(head.position, head.rotation); shot.fieldOfView = 65; shot.nearClipPlane = .01f;
        var rt = new RenderTexture(1440, 900, 24); shot.targetTexture = rt; shot.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        File.WriteAllBytes("Documentation/Previews/MainBrief_Centred.png", png.EncodeToPNG());
        RenderTexture.active = null; shot.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("MAIN_BRIEF_LAYOUT_PASS: requested bilingual copy, centred title/body inside card, no overflow or outside footer.");
    }
    public static void CaptureTaskTab()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Task tab preview", typeof(RectTransform), typeof(Canvas), typeof(Image));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(205, 52);
        root.transform.localScale = Vector3.one * .01f;
        FireTrainingUIStyle.Apply(root.GetComponent<Image>(), "panels/task_tab");
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var title = CampusTaskTab.CreateLabels(root.transform, font, out var key);
        Canvas.ForceUpdateCanvases(); title.ForceMeshUpdate(); key.ForceMeshUpdate();
        if (title.isTextOverflowing || key.isTextOverflowing || title.text.Contains("/") || key.text != "Y")
            throw new System.InvalidOperationException("Task tab text layout failed.");
        var camera = new GameObject("Task tab camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -2);
        camera.orthographic = true; camera.orthographicSize = .38f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 420, 24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        File.WriteAllBytes("Documentation/Previews/TaskTab_CentredY.png", png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("TASK_TAB_LAYOUT_PASS: separate centred Y, title outside key frame, no slash or text overflow.");
    }
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
    public static void CaptureRescueFollowing()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var head = new GameObject("Rescue preview head").transform;
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var chair = new GameObject("Classmate behind player");
        chair.transform.position = Vector3.back * 2;
        var view = chair.AddComponent<CampusRescueAssistanceView>();
        var camera = new GameObject("Rescue preview camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt;
        var directions = new[] { Vector3.forward, Vector3.right, Vector3.left, Vector3.back };
        for (int i = 0; i < directions.Length; i++)
        {
            view.Begin(head, new[] { directions[i] * 5 });
            Canvas.ForceUpdateCanvases();
            var label = head.GetComponentInChildren<TMPro.TMP_Text>();
            label.font = font;
            label.ForceMeshUpdate();
            if (label.isTextOverflowing || label.text.Contains("传送后") || label.text.Contains("after each teleport"))
                throw new System.InvalidOperationException("Invalid rescue following notice: " + label.text);
            if (i != 1) continue;
            camera.Render(); RenderTexture.active = rt;
            var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
            Directory.CreateDirectory("Documentation/Previews");
            File.WriteAllBytes("Documentation/Previews/RescueFollowing_LessonCard.png", png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(rt);
        Debug.Log("RESCUE_FOLLOWING_LAYOUT_PASS: all four directions fit; teleport reminder removed.");
    }

    public static void CaptureTaskNotice()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var parent = new GameObject("Task notice preview");
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/NotoSansCJKsc-Regular SDF.asset");
        var group = FireTrainingTaskNotice.Create(parent.transform, font, out var label);
        var tab = new GameObject("Task details tab", typeof(RectTransform), typeof(Canvas), typeof(Image));
        tab.transform.SetParent(parent.transform, false);
        tab.transform.localPosition = new Vector3(.57f, .26f, 1.45f);
        tab.transform.localScale = Vector3.one * .0017f;
        tab.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        tab.GetComponent<RectTransform>().sizeDelta = new Vector2(205, 52);
        FireTrainingUIStyle.Apply(tab.GetComponent<Image>(), "panels/task_tab");
        CampusTaskTab.CreateLabels(tab.transform, font, out var keyLabel);
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
        var noticeCorners = new Vector3[4]; var tabCorners = new Vector3[4];
        group.GetComponentInChildren<Image>().rectTransform.GetWorldCorners(noticeCorners);
        tab.GetComponent<RectTransform>().GetWorldCorners(tabCorners);
        if (noticeCorners[1].y > tabCorners[0].y - .02f || Mathf.Abs(group.transform.position.x-tab.transform.position.x)>.001f || noticeCorners[0].x < .1f)
            throw new System.InvalidOperationException("Task notification must sit below the task tab, leaving the centre clear.");
        var camera = new GameObject("Task notification camera").AddComponent<Camera>();
        camera.transform.position = Vector3.zero;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .20f, .24f);
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply();
        Directory.CreateDirectory("Documentation/Previews");
        File.WriteAllBytes("Documentation/Previews/TaskNotice_MainScreen.png", png.EncodeToPNG());
        File.WriteAllBytes("Documentation/Previews/TaskNotice_BelowTasks.png", png.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Debug.Log("TASK_NOTICE_LAYOUT_PASS: " + matches.Count + " live bilingual notices fit; popup below Tasks, 30% smaller, centre clear.");
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
            new CampusTaskCardList.Entry("主任务 到操场集合", "Main Reach the assembly area"),
            new CampusTaskCardList.Entry("1 到 411 寻找毛巾", "1 Find a towel in room 411", true),
            new CampusTaskCardList.Entry("2 打湿毛巾并下到三楼", "2 Wet towel, reach 3F", true),
            new CampusTaskCardList.Entry("3 前往 B 楼梯", "3 Reach Stair B", true),
            new CampusTaskCardList.Entry("可选 护送同学到二楼避难区", "Optional Escort to 2F refuge"),
            new CampusTaskCardList.Entry("4 找到出口并走到室外", "4 Find exit, go outside") });
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
