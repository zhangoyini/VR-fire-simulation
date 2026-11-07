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

    private static void StartAssistance(CampusRescueStudent student, CampusHandInteractor right, CampusHandInteractor left)
    {
        right.transform.position = student.transform.TransformPoint(new Vector3(.22f, .96f, -.3f));
        left.transform.position = student.transform.TransformPoint(new Vector3(-.22f, .96f, -.3f));
        Set(student, "lastToggleAt", float.NegativeInfinity);
        Physics.SyncTransforms();
        student.OnInteractStart(right.transform);
        student.OnInteractStart(left.transform);
    }

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
            hand.transform.position = student.transform.TransformPoint(new Vector3(-.22f, .96f, -.3f));
            Check(!student.CanPush(hand.transform), "A right hand at the left handle cannot count toward activation");
            hand.transform.position = student.transform.TransformPoint(new Vector3(.22f, .96f, -.3f));
            left.transform.position = student.transform.TransformPoint(new Vector3(-.22f, .96f, -.3f));
            Physics.SyncTransforms();
            hand.RefreshContact();
            hand.ProcessGrip(true, true, false);
            hand.ProcessGrip(false, false, true);
            Check(!student.IsFollowing, "One trigger press alone does not enter assistance");
            student.OnInteractStart(hand.transform);
            Check(!student.IsFollowing, "Repeated presses from the same hand cannot replace the second hand");
            hand.transform.position = head.position;
            student.OnInteractStart(left.transform);
            Check(!student.IsFollowing, "Both hands must still be at their matching handles when activation completes");
            hand.transform.position = student.transform.TransformPoint(new Vector3(.22f, .96f, -.3f));
            Set(student, "pendingHand", null);
            student.OnInteractStart(hand.transform);
            Set(student, "pendingAt", Time.unscaledTime - 2f);
            student.OnInteractStart(left.transform);
            Check(!student.IsFollowing, "An old first-hand click cannot activate assistance later");
            Set(student, "pendingHand", null);
            hand.ProcessGrip(true, true, false);
            hand.ProcessGrip(false, false, true);
            left.RefreshContact();
            left.ProcessGrip(true, true, false);
            left.ProcessGrip(false, false, true);
            Check(student.IsFollowing, "One press from each hand enters assistance and releasing both keeps it active");
            Check(Mathf.Abs(movement.Body.radius - student.PlayerClearanceRadius) < .001f,
                "Assisting player collision radius matches the forgiving teleport checks");
            student.OnInteractStart(hand.transform);
            Check(student.IsFollowing, "Simultaneous or bouncing trigger presses cannot immediately exit assistance");
            var view = student.GetComponent<CampusRescueAssistanceView>();
            var status = head.GetComponentInChildren<TextMeshPro>();
            Check(status != null && status.text.Contains("协助") && status.text.Contains("Helping") &&
                status.text.Contains("再按一次扳机") && status.text.Contains("press trigger again"),
                "Persistent bilingual status includes the exit reminder");
            Check(status.font != null && status.font.name.Contains("Noto"),
                "Assistance HUD reuses the campus Simplified Chinese and English font");
            Check(student.TryGetFollowPosition(rig.transform.position, rig.transform.position,
                out var settled, out var settledRotation), "Initial viewing gap has clear wheelchair space");
            Vector3 entryStart = student.transform.position;
            var entrySide = (Vector3)typeof(CampusRescueStudent)
                .GetField("entrySideStep", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(student);
            var chairBody = student.GetComponent<BoxCollider>();
            bool entryClear = true;
            for (int step = 0; step <= 40; step++)
            {
                student.transform.position = step <= 20 ?
                    Vector3.Lerp(entryStart, entrySide, step / 20f) :
                    Vector3.Lerp(entrySide, settled, (step - 20) / 20f);
                Physics.SyncTransforms();
                Vector3 bodyPoint = rig.transform.position + Vector3.up * .7f;
                if (Vector3.ProjectOnPlane(chairBody.ClosestPoint(bodyPoint) - bodyPoint, Vector3.up).magnitude < .23f)
                    entryClear = false;
            }
            student.transform.position = entryStart;
            Physics.SyncTransforms();
            Check(entryClear, "Entry alignment moves around the player's body instead of through it");
            student.FollowTeleport(settled, settledRotation);
            Vector3 chairBefore = student.transform.position;
            Quaternion facingBefore = student.transform.rotation;
            head.localRotation = Quaternion.Euler(0, 90, 0);
            Call(student, "Update");
            Check(student.transform.position == chairBefore && student.transform.rotation == facingBefore,
                "Looking sideways leaves the wheelchair position and orientation stable");
            head.localPosition += new Vector3(.12f, 0, .12f);
            Call(student, "Update");
            Check(student.transform.position == chairBefore && student.transform.rotation == facingBefore,
                "Leaning toward the wheelchair or door does not move the chair away");
            head.localPosition = Vector3.up * 1.65f;
            head.localRotation = Quaternion.Euler(0, -90, 0);
            Vector3 feet = rig.transform.position;
            Check(student.TryGetFollowPosition(feet, new Vector3(-.65f, 3.8f, 0), out var chair, out var rotation),
                "Wheelchair can accompany a teleport along the 2F corridor");
            Check(Vector3.Distance(chair, new Vector3(-1.15f, 3.8f, .7f)) < .01f,
                "Wheelchair stays alongside at 70 cm to the right and 50 cm forward");
            Check(head.localPosition == Vector3.up * 1.65f,
                "Assistance preserves the player's tracked eye height");
            Vector3 nearHandle = student.transform.TransformPoint(new Vector3(-.22f, .96f, -.3f));
            Vector3 shoulder = rig.transform.position + Vector3.up * 1.3f;
            Check(Vector3.Distance(nearHandle, shoulder) < .65f,
                "The nearer wheelchair handle stays within a 65 cm arm reach");
            hand.transform.position = nearHandle;
            Check(student.CanPush(hand.transform),
                "Either hand can use the nearer handle to exit after two-hand activation");
            student.ShowTeleportPreview(feet, new Vector3(-.65f, 3.8f, 0), true);
            var outline = UnityEngine.Object.FindObjectsOfType<LineRenderer>()
                .Single(line => line.name == "Wheelchair landing outline");
            Check(outline.enabled && Vector3.Distance(outline.transform.position, chair + Vector3.up * .04f) < .001f,
                "Wheelchair footprint previews the same position used by teleport");
            student.ShowTeleportPreview(feet, new Vector3(-.65f, 3.8f, 0), false);
            Check(status.text.Contains("空地") && status.text.Contains("Choose a clear spot") &&
                outline.startColor.r > outline.startColor.b,
                "Blocked aims show a red footprint and concise bilingual guidance");
            Check(!student.TryGetFollowPosition(feet, new Vector3(0, 0, 0), out _, out _),
                "Assisting teleports cannot take the wheelchair onto a different floor");
            Check(view.GetComponentsInChildren<Collider>().Length == 1,
                "Assistance feedback adds no new colliders to the wheelchair");

            // Exercise the real teleporter's coupled commit and preview through its normal arc.
            hand.transform.position = feet + new Vector3(-.3f, 1.1f, 0);
            bool aimed = false;
            for (int pitch = 15; pitch <= 65; pitch += 5)
            {
                hand.transform.rotation = Quaternion.Euler(pitch, -90, 0);
                Physics.SyncTransforms();
                Call(teleport, "RenderTeleportArc");
                if (teleport.HasValidTarget) { aimed = true; break; }
            }
            Check(aimed && outline.enabled, "Normal teleport aiming displays player and wheelchair destinations together");
            Call(teleport, "ExecuteTeleport");
            Check(Vector3.Distance(student.transform.position, outline.transform.position - Vector3.up * .04f) < .001f,
                "Player and wheelchair arrive at their previewed destinations in the same teleport");
            Call(teleport, "CancelAim");
            Check(!outline.enabled, "Ending teleport aim clears the wheelchair footprint");

            // Restore the authored start and check the corridor/doorway approach in the actual scene.
            student.ResetQuest();
            student.transform.SetPositionAndRotation(Vector3.up * 3.8f, Quaternion.Euler(0, -90, 0));
            rig.transform.position = feet;
            StartAssistance(student, hand, left);
            Vector3[] hops = {
                new Vector3(-.65f, 3.8f, 0),
                new Vector3(-1.7f, 3.8f, 0),
                new Vector3(-1.7f, 3.8f, -.5f),
                new Vector3(-1.7f, 3.8f, -.9f),
                new Vector3(-1.7f, 3.8f, -1.6f),
                new Vector3(-1.7f, 3.8f, -3.7f)
            };
            var refugeDoor = UnityEngine.Object.FindObjectsOfType<CampusHingeInteractable>()
                .OrderBy(door => Vector3.Distance(door.transform.position, new Vector3(-2.2f, 3.8f, -2))).First();
            Call(refugeDoor, "Awake");
            for (int i = 0; i < hops.Length; i++)
            {
                Physics.SyncTransforms();
                if (i == 4)
                {
                    Check(!student.TryGetFollowPosition(rig.transform.position, hops[i], out _, out _),
                        "Closed refuge door blocks wheelchair travel");
                    hand.transform.position = rig.transform.position + new Vector3(0, 1.1f, -.65f);
                    Physics.SyncTransforms();
                    hand.RefreshContact();
                    hand.ProcessGrip(true, true, false);
                    Check(refugeDoor.IsHeldBy(hand.transform),
                        "Player can reach the closed refuge door from the valid assisting approach");
                    hand.transform.position += Vector3.back * .35f;
                    hand.ProcessGrip(false, true, false);
                    hand.ProcessGrip(false, false, true);
                    Check(Mathf.Abs(refugeDoor.Angle) >= 80 && student.IsFollowing,
                        "A normal hand push opens the door while assistance stays active");
                    Physics.SyncTransforms();
                    CheckDoorwayTolerance(student, teleport, rig.transform.position);
                }
                bool clear = student.TryGetFollowPosition(rig.transform.position, hops[i], out chair, out rotation);
                if (!clear)
                {
                    Vector3 delta = chair - student.transform.position;
                    foreach (var hit in Physics.BoxCastAll(student.transform.position + Vector3.up * .69f,
                        new Vector3(.44f, .58f, .55f), delta.normalized, rotation, delta.magnitude,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        Debug.Log("RESCUE_BLOCKER hop " + i + ": " + hit.collider.name + " at " + hit.point);
                    foreach (var hit in Physics.OverlapBox(chair + Vector3.up * .69f,
                        new Vector3(.44f, .58f, .55f), rotation))
                        Debug.Log("RESCUE_ENDPOINT hop " + i + ": " + hit.name);
                }
                Check(clear,
                    "Wheelchair clears refuge approach hop " + (i + 1));
                rig.transform.position = hops[i];
                student.FollowTeleport(chair, rotation);
            }
            Set(student, "dwell", 2f);
            Call(student, "Update");
            Check(student.Current == CampusRescueStudent.State.Delivered && CampusRescueStudent.Rescued,
                "Delivering the wheelchair completes the rescue and exits assistance");
            Check(Mathf.Abs(movement.Body.radius - originalPlayerRadius) < .001f,
                "Delivery restores the player's original collision radius");
            Check(!status.gameObject.activeSelf && !outline.enabled, "Delivery hides the assistance HUD and preview");

            student.ResetQuest();
            student.transform.SetPositionAndRotation(Vector3.up * 3.8f, Quaternion.Euler(0, -90, 0));
            rig.transform.position = feet;
            StartAssistance(student, hand, left);
            hand.transform.position = feet + Vector3.up * .95f;
            hand.transform.LookAt(student.transform.position + Vector3.up * .8f);
            Physics.SyncTransforms();
            Check(student.PointsAtChair(hand.transform), "Player can target the wheelchair from the viewing gap");
            Set(student, "lastToggleAt", float.NegativeInfinity);
            hand.RefreshContact();
            hand.ProcessGrip(true, true, false);
            hand.ProcessGrip(false, false, true);
            Check(student.Current == CampusRescueStudent.State.Waiting, "A fresh targeted trigger press exits assistance");
            Check(Mathf.Abs(movement.Body.radius - originalPlayerRadius) < .001f,
                "Exiting assistance restores normal player clearance");
            StartAssistance(student, hand, left);
            Time.timeScale = 0;
            Call(student, "Update");
            Check(!student.IsFollowing && !status.gameObject.activeSelf, "Pause hides assistance feedback and freezes movement");
            Time.timeScale = 1;
            Call(student, "Update");
            Check(student.IsFollowing && status.gameObject.activeSelf, "Resuming preserves the assisting mode");

            var previewFeet = new Vector3(-.65f, 3.8f, 0);
            Check(student.TryGetFollowPosition(rig.transform.position, previewFeet, out chair, out rotation),
                "Assisting view retains a clear gap after movement");
            rig.transform.position = previewFeet;
            student.FollowTeleport(chair, rotation);
            hand.transform.position = head.position;
            hand.transform.rotation = Quaternion.identity;
            hand.RefreshContact();
            left.transform.position = head.position;
            left.transform.rotation = Quaternion.identity;
            left.RefreshContact();
            Call(view, "Update");
            head.localRotation = Quaternion.Euler(20, -65, 0);
            status.ForceMeshUpdate();
            status.GetComponent<CampusUIForeground>().Refresh();
            Capture(camera);
            File.WriteAllLines("Logs/rescue-assistance-checks.txt", results);
            Debug.Log("RESCUE_ASSISTANCE_CHECKS_PASS");
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

    private static void CheckDoorwayTolerance(CampusRescueStudent student, VRTeleporter teleport, Vector3 feet)
    {
        var standing = typeof(VRTeleporter).GetMethod("HasStandingClearance", BindingFlags.NonPublic | BindingFlags.Instance);
        var path = typeof(VRTeleporter).GetMethod("HasClearTravelPath", BindingFlags.NonPublic | BindingFlags.Instance);
        var chairPlacement = typeof(CampusRescueStudent).GetMethod("CanPlace", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 edge = new Vector3(-1.63f, 3.8f, -2.1f);
        Vector3 chairEdge = new Vector3(-2.58f, 3.8f, -2.1f);
        Quaternion chairFacing = Quaternion.Euler(0, 180, 0);
        Set(student, "doorwayTolerance", 0f);
        bool strictPlayer = (bool)standing.Invoke(teleport, new object[] { edge }) &&
            (bool)path.Invoke(teleport, new object[] { edge });
        bool strictChair = (bool)chairPlacement.Invoke(student, new object[] { chairEdge, chairFacing });
        Set(student, "doorwayTolerance", .05f);
        Check(!strictPlayer && (bool)standing.Invoke(teleport, new object[] { edge }) &&
            (bool)path.Invoke(teleport, new object[] { edge }) &&
            student.TryGetFollowPosition(feet, edge, out _, out _),
            "5 cm tolerance accepts a player destination grazing the open doorway that strict checks reject");
        Check(!strictChair && (bool)chairPlacement.Invoke(student, new object[] { chairEdge, chairFacing }),
            "The same Inspector tolerance also eases wheelchair clearance at the door jamb");
        Call(student, "ApplyPlayerClearance");
        Check(Mathf.Abs(student.PlayerClearanceRadius - .15f) < .001f,
            "Inspector tolerance uses metres with 5 cm of clearance forgiveness per side");
        Set(student, "doorwayTolerance", .1f);
        Check(!(bool)standing.Invoke(teleport, new object[] { new Vector3(-3.6f, 3.8f, -2f) }),
            "Even maximum doorway tolerance rejects a player inside the solid wall");
        Set(student, "doorwayTolerance", 0f);
        Call(student, "ApplyPlayerClearance");
        Check(Mathf.Abs(student.PlayerClearanceRadius - .2f) < .001f,
            "Zero tolerance restores the original strict teleport clearance");
        Set(student, "doorwayTolerance", .05f);
        Call(student, "ApplyPlayerClearance");
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
