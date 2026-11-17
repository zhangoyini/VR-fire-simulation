using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harbourview.Journey;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

public static class CampusRescueChecks
{
    private static readonly List<string> results = new List<string>();
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add("PASS: " + description);
    }
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    public static void Run()
    {
        results.Clear();
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        Time.timeScale = 1;
        CampusTeamRuntime.Load();
        var originalPlayer = UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true);
        originalPlayer.transform.root.gameObject.SetActive(false);
        var student = UnityEngine.Object.FindObjectOfType<CampusRescueStudent>();
        Check(student != null, "Saved campus contains the rescue classmate");
        var logger = UnityEngine.Object.FindObjectOfType<SessionLogger>(true);
        if (logger != null)
        {
            Set(logger, "loggingEnabled", false);
            Call(logger, "Awake");
        }
        var rig = new GameObject("QA assisting player");
        var movement = rig.AddComponent<CampusLocomotion>();
        var head = new GameObject("QA assisting head").transform;
        head.SetParent(rig.transform, false);
        head.localPosition = Vector3.up * 1.65f;
        head.localRotation = Quaternion.Euler(0, -90, 0);
        head.gameObject.tag = "MainCamera";
        var camera = head.gameObject.AddComponent<Camera>();
        Set(movement, "trackedHead", head);
        Call(movement, "Awake");
        movement.Body.enabled = false;
        float originalPlayerRadius = movement.Body.radius;
        rig.transform.position = new Vector3(1, 3.8f, 0);
        Set(student, "followTarget", head);
        Call(student, "Awake");
        var presenter = UnityEngine.Object.FindObjectOfType<CampusMessagePresenter>(true);
        if (presenter != null) Call(presenter, "Awake");
        var handObject = new GameObject("QA assisting hand");
        var hand = handObject.AddComponent<CampusHandInteractor>();
        hand.ConfigureSource(SteamVR_Input_Sources.RightHand);
        var leftObject = new GameObject("QA left assisting hand");
        var left = leftObject.AddComponent<CampusHandInteractor>();
        left.ConfigureSource(SteamVR_Input_Sources.LeftHand);
        var teleport = handObject.AddComponent<VRTeleporter>();
        Set(teleport, "playerRoot", rig.transform);
        Set(teleport, "vrCamera", head);
        Set(teleport, "useCampusInput", true);
        Set(teleport, "maxTeleportDistance", 3f);
        Set(teleport, "maxHeightChange", 2f);
        Set(teleport, "teleportLayer", (LayerMask)~0);
        Call(teleport, "Awake");
        try
        {
            // Looking at a nearby classmate opens the conversation, without touching handles.
            head.LookAt(student.transform.position + Vector3.up * 1.2f);
            Call(student, "Update");
            Check(student.DialogueVisible && !student.IsFollowing, "Approach opens dialogue without starting escort");
            var conversation = head.Find("Classmate conversation");
            Check(conversation != null && conversation.localPosition.y < -.3f &&
                Mathf.Abs(conversation.localPosition.x) < .01f, "Conversation sits at bottom centre");
            var labels = conversation.GetComponentsInChildren<TMP_Text>();
            Check(labels.Any(t => t.text.Contains("不知道") && t.text.Contains("don't know")) &&
                labels.Any(t => t.text.Contains("跟我来") && t.text.Contains("Follow me")),
                "Dialogue and reply are both Simplified Chinese and English");
            Check(labels.All(t => t.font != null && t.font.name.Contains("Noto")), "Dialogue uses existing bilingual campus font");
            Canvas.ForceUpdateCanvases();
            foreach (var label in labels) label.ForceMeshUpdate();
            Capture(camera);
            student.OnInteractStart(hand.transform);
            Check(!student.IsFollowing, "Clicking the classmate does not bypass the reply");
            var reply = conversation.GetComponentInChildren<VRMenuButton>();
            reply.OnInteractStart(hand.transform);
            Check(student.IsFollowing && !student.DialogueVisible, "Selecting Follow me starts escort and closes dialogue");
            student.OnInteractStart(hand.transform);
            Check(student.IsFollowing, "A later chair click cannot cancel escort");
            Vector3 initial = student.transform.position;
            student.TickFollowing(.1f);
            Check(student.transform.position == initial, "Classmate waits for first teleport");
            Check(!student.TryGetFollowPosition(rig.transform.position, new Vector3(-2, 0, 0), out _, out _),
                "Wheelchair cannot follow onto another floor");

            var refugeDoor = UnityEngine.Object.FindObjectsOfType<CampusHingeInteractable>()
                .OrderBy(door => Vector3.Distance(door.transform.position, new Vector3(-2.2f, 3.8f, -2))).First();
            Call(refugeDoor, "Awake");
            Vector3[] hops = {
                new Vector3(-2.2f, 3.8f, 0),
                new Vector3(-2.2f, 3.8f, -1),
                new Vector3(-2.2f, 3.8f, -4.2f)
            };
            Vector3 previousFeet = rig.transform.position;
            for (int i = 0; i < hops.Length; i++)
            {
                if (i == 2)
                {
                    Check(!student.TryGetFollowPosition(previousFeet, hops[i], out _, out _),
                        "Closed refuge door blocks the follower route");
                    refugeDoor.SetAngle(-90f);
                    Physics.SyncTransforms();
                }
                Check(student.TryGetFollowPosition(previousFeet, hops[i], out var goal, out var facing),
                    "Saved scene permits follow route hop " + i);
                var before = student.transform.position;
                Set(teleport, "teleportTarget", hops[i]);
                Call(teleport, "ExecuteTeleport");
                Check(Vector3.Distance(rig.transform.position, hops[i]) < .01f, "Player teleport lands before follower movement");
                Check(student.transform.position == before, "Teleport queues movement without snapping the chair");
                previousFeet = hops[i];
                if (i == 0)
                {
                    Time.timeScale = 0;
                    student.TickFollowing(.1f);
                    Check(student.transform.position == before, "Pause stops follower movement");
                    Time.timeScale = 1;
                    head.localRotation = Quaternion.Euler(0, -90, 0);
                    student.TickFollowing(.02f);
                    Check(Vector3.Distance(student.transform.position, before) > 0 &&
                        Vector3.Distance(student.transform.position, before) <= .045f,
                        "Chair rolls at bounded speed even while player looks away");
                    // Queue the corner before the chair catches up to exercise rapid teleports.
                }
                else
                    for (int frame = 0; frame < 300 && student.IsFollowing; frame++) student.TickFollowing(.02f);
            }
            Check(student.Current == CampusRescueStudent.State.Delivered && CampusRescueStudent.Rescued,
                "Actual chair arrival inside the refuge completes delivery");
            student.ResetQuest();
            Check(student.Current == CampusRescueStudent.State.Waiting && !CampusRescueStudent.Rescued &&
                !student.DialogueVisible, "Reset clears escort, dialogue and completion");
            File.WriteAllLines("Logs/rescue-assistance-checks.txt", results);
            Debug.Log("RESCUE_CHECKS_PASS\n" + string.Join("\n", results));
        }
        catch (Exception exception)
        {
            File.WriteAllLines("Logs/rescue-assistance-checks.txt", results.Concat(new[] { "FAIL: " + exception }));
            throw;
        }
        finally
        {
            Time.timeScale = 1;
            UnityEngine.Object.DestroyImmediate(handObject);
            UnityEngine.Object.DestroyImmediate(leftObject);
            UnityEngine.Object.DestroyImmediate(student.gameObject);
            UnityEngine.Object.DestroyImmediate(rig);
        }
    }

    private static void Capture(Camera camera)
    {
        var target = new RenderTexture(1400, 1000, 24) { antiAliasing = 4 };
        var pixels = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/rescue-assistance-preview.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
