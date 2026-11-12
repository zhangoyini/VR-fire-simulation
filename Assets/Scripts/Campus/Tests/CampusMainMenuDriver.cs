#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

public sealed class CampusMainMenuDriver : MonoBehaviour
{
    private string report = "";
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Campus.MenuQA", false))
                new GameObject("Main menu checks").AddComponent<CampusMainMenuDriver>();
        };
    }

    private void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Guard()); }
    private IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object next;
            try
            {
                if (!checks.MoveNext()) { Finish(0); yield break; }
                next = checks.Current;
            }
            catch (Exception exception) { report += exception; Finish(1); yield break; }
            yield return next;
        }
    }
    private void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        report += "PASS: " + label + "\n";
    }
    private void Finish(int code)
    {
        SessionState.SetBool("Campus.MenuQA", false);
        File.WriteAllText("Documentation/MainMenuChecks.txt", report);
        Debug.Log(code == 0 ? "MAIN_MENU_CHECKS_PASS" : "MAIN_MENU_CHECKS_FAIL\n" + report);
        EditorApplication.Exit(code);
    }

    private IEnumerator Checks()
    {
        yield return null;
        yield return null;
        var hands = FindObjectsOfType<CampusHandVisual>();
        Check(hands.Length == 2, "MainMenu contains both articulated hands after repeated setup");
        var meshes = hands.Select(h => h.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh).ToArray();
        var materials = hands.Select(h => h.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial).ToArray();
        foreach (var hand in hands)
        {
            Check(hand.Bones.Length == 31 && Field<bool>(hand, "followTriggerInput"),
                "Menu hand has all finger bones and follows its own trigger");
            Quaternion relaxed = hand.Bones[7].localRotation;
            hand.SetGesture(true, false, false);
            hand.Tick(1);
            Check(Quaternion.Angle(relaxed, hand.Bones[7].localRotation) > 1,
                "The detailed hand curls when its trigger gesture is pressed");
            hand.SetGesture(false, false, false);
            hand.Tick(1);
            Check(Quaternion.Angle(relaxed, hand.Bones[7].localRotation) < .1f,
                "Releasing restores the relaxed menu hand");
        }
        var hint = GameObject.Find("MainMenuCanvas/Trigger confirmation hint").GetComponent<TMP_Text>();
        hint.ForceMeshUpdate();
        Check(hint.text.Contains("按下扳机确认") && !hint.isTextOverflowing && !hint.raycastTarget,
            "The bilingual trigger-confirmation hint fits and leaves menu input clear");
        var ray = FindObjectsOfType<VRMenuRayPointer>().First(r =>
            Field<SteamVR_Input_Sources>(r, "inputSource") == SteamVR_Input_Sources.RightHand);
        var controller = Field<Transform>(ray, "trackedController");
        var start = GameObject.Find("MainMenuCanvas/StartButton").GetComponent<VRMenuButton>();
        var camera = Field<Transform>(controller.GetComponent<CampusTrackedHandPose>(), "trackedHead");
        controller.position = camera.position + camera.forward * .1f;
        controller.rotation = Quaternion.LookRotation(start.GetComponent<Collider>().bounds.center - controller.position) *
            Quaternion.Euler(-Field<float>(ray, "downwardPitchDegrees"), 0, 0);
        ray.ProcessInput(false);
        Check(ray.CurrentTarget == start, "The normal menu ray targets Start");
        ray.ProcessInput(true);
        float deadline = Time.realtimeSinceStartup + 45;
        while (SceneManager.GetActiveScene().name != "CampusTraining" && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(SceneManager.GetActiveScene().name == "CampusTraining", "A trigger press on Start loads the real training scene");
        yield return null;
        var trainingHands = FindObjectsOfType<CampusHandVisual>();
        Check(trainingHands.Length == 2 && trainingHands.All(h =>
            meshes.Contains(h.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh) &&
            materials.Contains(h.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial)),
            "MainMenu and CampusTraining use the same hand meshes and materials");
        Check(CampusLessonDirector.Instance != null && CampusLessonDirector.Instance.IsPractice,
            "The actual MainMenu entry starts the protected classroom tutorial");
    }
}
#endif
