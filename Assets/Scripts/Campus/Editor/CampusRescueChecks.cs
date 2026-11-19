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
            var resolve = typeof(VRTeleporter).GetMethod("TryResolveLanding", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(Physics.Raycast(new Vector3(21.55f, 4.05f, 1.24f), Vector3.down, out var stairHit, .2f),
                "Saved 2F stairs provide a real warning target");
            presenter.Hide();
            resolve.Invoke(teleport, new object[] { stairHit, Vector3.zero });
            Check(!presenter.IsVisible, "Stair rescue warning stays off before escort starts");
            student.OnInteractStart(hand.transform);
            Check(!student.IsFollowing, "Clicking the classmate does not bypass the reply");
            var reply = conversation.GetComponentInChildren<VRMenuButton>();
            reply.OnInteractStart(hand.transform);
            Check(student.IsFollowing && !student.DialogueVisible, "Selecting Follow me starts escort and closes dialogue");
            var guidance = head.Find("Assisting classmate reminder").GetComponent<CampusRescueNoticeCard>();
            Check(guidance.transform.localPosition == new Vector3(0, 0, 1.3f) &&
                guidance.GetComponentInChildren<UnityEngine.UI.Image>().color.a >= .95f,
                "Refuge guidance is centred on an opaque mission-style card");
            Canvas.ForceUpdateCanvases();
            guidance.Label.ForceMeshUpdate();
            Check(!guidance.Label.isTextOverflowing, "Bilingual guidance fits inside its mission card");
            Capture(camera, "Logs/refuge-guidance-card.png");
            Check(!(bool)resolve.Invoke(teleport, new object[] { stairHit, Vector3.zero }) &&
                presenter.IsVisible && presenter.CurrentMessage == CampusMessagePresenter.RescueStairWarning,
                "Aiming at stairs during escort blocks landing and explains the 2F refuge objective in both languages");
            student.GetComponent<CampusRescueAssistanceView>().SetVisible(true);
            var warning = head.Find("Refuge stair warning").GetComponent<CampusRescueNoticeCard>();
            Check(warning.gameObject.activeSelf && !guidance.gameObject.activeSelf &&
                warning.transform.localPosition == guidance.transform.localPosition,
                "Stair warning replaces guidance in the same centred card position");
            warning.GetComponent<CampusUIForeground>().Refresh();
            Call(warning, "LateUpdate");
            Check(warning.GetComponent<Canvas>().sortingOrder > 30000,
                "Warning remains in front of the task/map HUD after a foreground refresh");
            Canvas.ForceUpdateCanvases();
            warning.Label.ForceMeshUpdate();
            Check(!warning.Label.isTextOverflowing, "Bilingual stair warning fits inside its mission card");
            Capture(camera, "Logs/refuge-stair-warning-card.png");
            Time.timeScale = 0;
            Call(warning, "LateUpdate");
            Check(!warning.GetComponent<Canvas>().enabled, "Centred warning leaves the pause menu unobstructed");
            Time.timeScale = 1;
            Call(warning, "LateUpdate");
            Check(warning.GetComponent<Canvas>().enabled, "Warning card returns when gameplay resumes");
            presenter.Hide();
            student.GetComponent<CampusRescueAssistanceView>().SetVisible(true);
            Check(guidance.gameObject.activeSelf && !warning.gameObject.activeSelf,
                "Guidance returns after the stair warning is dismissed");
            Check(Physics.Raycast(new Vector3(-1.5f, 3.9f, 0), Vector3.down, out var corridorHit, .2f) &&
                (bool)resolve.Invoke(teleport, new object[] { corridorHit, Vector3.zero }) && !presenter.IsVisible,
                "Same-floor corridor teleport remains valid without a stair warning");
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
            presenter.Hide();
            resolve.Invoke(teleport, new object[] { stairHit, Vector3.zero });
            Check(!presenter.IsVisible, "Stair rescue warning stops after the classmate arrives");
            student.ResetQuest();
            Check(student.Current == CampusRescueStudent.State.Waiting && !CampusRescueStudent.Rescued &&
                !student.DialogueVisible, "Reset clears escort, dialogue and completion");
            CheckReverseRecovery(student, rig.transform, head);
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

    private static void CheckReverseRecovery(CampusRescueStudent student, Transform rig, Transform head)
    {
        Vector3 start = new Vector3(0, 3.8f, 0);
        Quaternion facing = Quaternion.Euler(0, -90, 0);
        student.transform.SetPositionAndRotation(start, facing);
        rig.position = start + Vector3.right;
        Call(student, "OpenDialogue");
        student.ReplyFollowMe();
        Check(student.TryGetFollowPosition(rig.position, start + Vector3.right * 2.6f, out var goal, out var rotation),
            "Player can choose a clear destination behind the wheelchair");
        rig.position = goal;
        student.FollowTeleport(goal, rotation);
        bool movedBackwards = false;
        for (int i = 0; i < 150; i++)
        {
            Vector3 previous = student.transform.position;
            student.TickFollowing(.02f);
            movedBackwards |= Vector3.Dot(student.transform.position - previous, student.transform.forward) < -.001f;
        }
        Check(student.transform.position.x > 1 &&
            Quaternion.Angle(student.transform.rotation, Quaternion.Euler(0, 90, 0)) < 5,
            "Classmate turns to face a player who teleports behind them and then follows forwards");
        Check(!movedBackwards, "Unobstructed following never drives backwards");

        student.ResetQuest();
        student.transform.SetPositionAndRotation(start, facing);
        rig.position = start + Vector3.right;
        Call(student, "OpenDialogue");
        student.ReplyFollowMe();
        goal = new Vector3(-3.3f, 3.8f, 0);
        Check(student.TryGetFollowPosition(rig.position, goal, out _, out _), "Furniture recovery starts with a valid route");
        rig.position = goal;
        student.FollowTeleport(goal, facing);
        var furniture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        furniture.name = "QA chair moved into follower route";
        furniture.transform.position = start + new Vector3(-1, .7f, 0);
        furniture.transform.localScale = new Vector3(.4f, 1.2f, .4f);
        Physics.SyncTransforms();
        try
        {
            bool reversed = false, crossedFurniture = false;
            var body = student.GetComponent<BoxCollider>();
            var obstacle = furniture.GetComponent<Collider>();
            for (int i = 0; i < 900; i++)
            {
                Vector3 previous = student.transform.position;
                student.TickFollowing(.02f);
                reversed |= Vector3.Dot(student.transform.position - previous, facing * Vector3.forward) < -.001f;
                crossedFurniture |= Physics.ComputePenetration(body, student.transform.position, student.transform.rotation,
                    obstacle, furniture.transform.position, furniture.transform.rotation, out _, out float depth) && depth > .06f;
            }
            Check(reversed, "Blocked classmate backs away from a chair before trying another approach");
            Check(!crossedFurniture, "Recovery respects solid furniture throughout the manoeuvre");
            Check(Vector3.Distance(student.transform.position, goal) < 1.2f,
                "Classmate resumes following after backing around furniture");
        }
        finally { UnityEngine.Object.DestroyImmediate(furniture); student.ResetQuest(); }
    }

    private static void Capture(Camera camera, string path = "Logs/rescue-assistance-preview.png")
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
            File.WriteAllBytes(path, pixels.EncodeToPNG());
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
