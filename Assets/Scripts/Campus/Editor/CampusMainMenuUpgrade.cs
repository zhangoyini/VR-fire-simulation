using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

public static partial class CampusSceneBuilder
{
    public static void RunMainMenuChecks()
    {
        ApplyMainMenuHandsUpdate();
        ApplyMainMenuHandsUpdate();
        foreach (var script in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            if ((script.GetType().Namespace ?? "").StartsWith("Valve.VR")) script.enabled = false;
        SessionState.SetBool("Campus.MenuQA", true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Campus/Update Main Menu Hands and Input Hint")]
    public static void ApplyMainMenuHandsUpdate()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity");
        ConfigureMainMenuHandsAndHint();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save main menu.");
        AssetDatabase.SaveAssets();
        Debug.Log("MAIN_MENU_HANDS_UPDATE_COMPLETE");
    }

    private static void ConfigureMainMenuHandsAndHint()
    {
        var right = UnityEngine.Object.FindObjectsOfType<DummyController>(true).Single(d =>
            new SerializedObject(d).FindProperty("inputSource").intValue == (int)SteamVR_Input_Sources.RightHand);
        var rightRay = UnityEngine.Object.FindObjectOfType<VRMenuRayPointer>(true);
        var canvas = GameObject.Find("MainMenuCanvas").transform;
        var tracking = new SerializedObject(right.GetComponent<CampusTrackedHandPose>());
        var head = (Transform)tracking.FindProperty("trackedHead").objectReferenceValue;
        var rig = (Transform)tracking.FindProperty("trackingSpace").objectReferenceValue;
        var leftController = rig.GetComponentsInChildren<SteamVR_Behaviour_Pose>(true)
            .Single(p => p.inputSource == SteamVR_Input_Sources.LeftHand).transform;
        var left = UnityEngine.Object.FindObjectsOfType<DummyController>(true).FirstOrDefault(d =>
            new SerializedObject(d).FindProperty("inputSource").intValue == (int)SteamVR_Input_Sources.LeftHand);
        if (left == null)
        {
            left = new GameObject("LeftControllerDummy").AddComponent<DummyController>();
            Set(left, "controller", leftController.gameObject);
            SetInt(left, "inputSource", (int)SteamVR_Input_Sources.LeftHand);
            var pose = left.gameObject.AddComponent<CampusTrackedHandPose>();
            Set(pose, "trackingSpace", rig);
            Set(pose, "trackedController", leftController);
            Set(pose, "trackedHead", head);
            SetInt(pose, "inputSource", (int)SteamVR_Input_Sources.LeftHand);
            var ray = left.gameObject.AddComponent<VRMenuRayPointer>();
            EditorUtility.CopySerialized(rightRay, ray);
            Set(ray, "trackedController", left.transform);
            SetInt(ray, "inputSource", (int)SteamVR_Input_Sources.LeftHand);
        }
        ConfigureDetailedHands();
        foreach (var dummy in new[] { left, right })
        {
            var visual = new SerializedObject(dummy.GetComponent<CampusHandVisual>());
            visual.FindProperty("followTriggerInput").boolValue = true;
            visual.ApplyModifiedPropertiesWithoutUndo();
            var controller = (GameObject)new SerializedObject(dummy).FindProperty("controller").objectReferenceValue;
            controller.transform.Find("Model")?.gameObject.SetActive(false);
        }

        var hint = canvas.Find("Trigger confirmation hint")?.GetComponent<TextMeshProUGUI>();
        if (hint == null)
        {
            var label = new GameObject("Trigger confirmation hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvas, false);
            hint = label.GetComponent<TextMeshProUGUI>();
        }
        hint.font = canvas.Find("Title").GetComponent<TMP_Text>().font;
        hint.fontSize = hint.fontSizeMax = 36;
        hint.fontSizeMin = 30;
        hint.enableAutoSizing = true;
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        hint.rectTransform.anchoredPosition = new Vector2(0, -305);
        hint.rectTransform.sizeDelta = new Vector2(1200, 100);
        hint.text = "用手柄射线指向选项，按下扳机确认。\n<size=80%>Point at an option with the controller ray, then press the trigger to confirm.</size>";
    }
}
