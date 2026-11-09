using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiao.HealthTime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

public static class CampusTeleportChecks
{
    private static readonly List<string> results=new List<string>();
    private static void Check(bool value,string description)
    { if(!value) throw new InvalidOperationException(description); results.Add("PASS: "+description); }
    private static void Set(object target,string name,object value)
    { target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value); }
    private static void Call(object target,string method)
    { target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null); }
    private static void ResetOwner()
    { typeof(VRTeleporter).GetMethod("ResetAimOwner",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null); }

    // Batch entry point: upgrade and persist only the campus scene, then run isolated physics checks.
    public static void ApplyAndRun()
    { RunChecks(true); }

    public static void Run()
    { RunChecks(false); }

    public static void RunRestroomMarkerChecks()
    {
        results.Clear();
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        var tiles = UnityEngine.Object.FindObjectsOfType<MeshRenderer>()
            .Where(renderer => renderer.name == "Restroom tiled floor")
            .OrderBy(renderer => renderer.bounds.center.y).ToArray();
        Check(tiles.Length == 4, "Saved campus contains a tiled restroom on all four floors");
        var rig = new GameObject("QA restroom player");
        var head = new GameObject("QA restroom head").transform;
        head.SetParent(rig.transform, false);
        head.localPosition = Vector3.up * 1.65f;
        head.localRotation = Quaternion.Euler(0, 180, 0);
        var teleport = MakeTeleporter(rig.transform, head, SteamVR_Input_Sources.LeftHand);
        Set(teleport, "maxTeleportDistance", 3f);
        try
        {
            foreach (var tile in tiles)
            {
                var bounds = tile.bounds;
                Vector3 feet = bounds.center;
                feet.z += 2.8f;
                Check(Physics.Raycast(feet + Vector3.up * .1f, Vector3.down, out var floor, .3f),
                    "Restroom floor collider is present at " + tile.transform.parent.name);
                feet.y = floor.point.y;
                rig.transform.position = feet;
                teleport.transform.position = feet + Vector3.up * 1.1f;
                bool found = false;
                var arc = teleport.GetComponent<LineRenderer>();
                for (int pitch = 15; pitch <= 65; pitch += 5)
                {
                    teleport.transform.rotation = Quaternion.Euler(pitch, 180, 0);
                    Physics.SyncTransforms();
                    Call(teleport, "RenderTeleportArc");
                    Vector3 end = arc.GetPosition(arc.positionCount - 1);
                    if (teleport.HasValidTarget && end.z < feet.z - .4f && end.z > bounds.min.z &&
                        end.x > bounds.min.x && end.x < bounds.max.x)
                    { found = true; break; }
                }
                Check(found, "Teleport arc finds a valid destination inside " + tile.transform.parent.name);
                var marker = (TeleportDestinationMarker)typeof(VRTeleporter)
                    .GetField("destinationMarker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(teleport);
                Check(marker.transform.position.y > bounds.max.y + .005f,
                    "Ring and facing arrow sit above the visible tiles in " + tile.transform.parent.name);
                if (tile == tiles[tiles.Length - 1])
                {
                    arc.enabled = false;
                    CaptureRestroomMarker(head, marker);
                }
            }
            File.WriteAllLines("Logs/restroom-marker-checks.txt", results);
            Debug.Log("RESTROOM_MARKER_CHECKS_PASS");
        }
        catch (Exception exception)
        {
            File.WriteAllLines("Logs/restroom-marker-checks.txt", results.Concat(new[] { "FAIL: " + exception }));
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(teleport.gameObject);
            UnityEngine.Object.DestroyImmediate(rig);
        }
        RunDestinationMarkerChecks();
    }

    private static void CaptureRestroomMarker(Transform head, TeleportDestinationMarker marker)
    {
        var cameraObject = new GameObject("QA restroom camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = head.position;
        cameraObject.transform.LookAt(marker.transform.position);
        var target = new RenderTexture(1200, 800, 24) { antiAliasing = 4 };
        var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/restroom-marker-preview.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    // Focused marker checks can run independently of the existing desk-edge regression.
    public static void RunDestinationMarkerChecks()
    {
        results.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Time.timeScale = 1;
        ResetOwner();
        var rig = new GameObject("QA marker player");
        var head = new GameObject("QA marker head").transform;
        head.SetParent(rig.transform, false);
        head.localPosition = Vector3.up * 1.65f;
        head.localRotation = Quaternion.Euler(0, 65, 0);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * .05f;
        floor.transform.localScale = new Vector3(20, .1f, 20);
        floor.AddComponent<TeleportArea>();
        var teleport = MakeTeleporter(rig.transform, head, SteamVR_Input_Sources.LeftHand);
        var arc = teleport.GetComponent<LineRenderer>();
        Set(teleport, "maxTeleportDistance", 3f);
        try
        {
            Physics.SyncTransforms();
            teleport.ProcessInput(true);
            var marker = (TeleportDestinationMarker)typeof(VRTeleporter)
                .GetField("destinationMarker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(teleport);
            var ring = marker.transform.Find("Landing ring").GetComponent<LineRenderer>();
            var arrow = marker.transform.Find("Facing arrow").GetComponent<LineRenderer>();
            var cross = marker.transform.Find("Invalid cross A").GetComponent<LineRenderer>();
            Check(teleport.HasValidTarget && ring.enabled && arrow.enabled && !cross.enabled,
                "A valid aim shows the destination ring and facing arrow");
            Check(ring.sharedMaterial != null && ring.sharedMaterial.shader.name == "SystemUI/VRMenuLaser" &&
                ring.sharedMaterial.GetFloat("_ZTest") == 4 && ring.sharedMaterial.shader.isSupported,
                "Marker uses a bundled transparent stereo shader with normal depth testing");
            Check(marker.GetComponentsInChildren<Collider>().Length == 0,
                "Destination feedback adds no colliders that could intercept the teleport arc");
            Vector3 end = arc.GetPosition(arc.positionCount - 1);
            Check(Vector3.Distance(marker.transform.position, end + Vector3.up * .025f) < .001f,
                "Destination ring matches the validated arc endpoint");
            Vector3 tip = arrow.transform.TransformPoint(arrow.GetPosition(1)) - marker.transform.position;
            Check(Vector3.Dot(tip.normalized, Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized) > .999f,
                "Arrow geometry points in the actual arrival heading");
            var tick = typeof(TeleportDestinationMarker).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            tick.Invoke(marker, new object[] { 0f });
            float dim = ring.startColor.a;
            Vector3 centre = marker.transform.position;
            tick.Invoke(marker, new object[] { .25f });
            Check(ring.startColor.a > dim + .1f && marker.transform.position == centre,
                "Ring pulses in brightness while its position stays fixed");
            Quaternion heading = rig.transform.rotation;
            teleport.ProcessInput(false);
            Check(rig.transform.rotation == heading && ring.enabled && marker.transform.position == centre,
                "Release teleports without rotating the player or moving the world-space arrival marker");
            tick.Invoke(marker, new object[] { Time.unscaledTime + .3f });
            Check(!ring.enabled && !arrow.enabled, "Arrival feedback fades out after a quarter second");

            // Reproduce the existing successful floor-edge snap and inspect its visual endpoint.
            rig.transform.position = Vector3.zero;
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = new Vector3(1.5f, .85f, .35f);
            obstacle.transform.localScale = new Vector3(1, 1.7f, .4f);
            Physics.SyncTransforms();
            ResetOwner();
            teleport.ProcessInput(true);
            end = arc.GetPosition(arc.positionCount - 1);
            Check(teleport.HasValidTarget && end.z < -.01f &&
                Vector3.Distance(marker.transform.position, end + Vector3.up * .025f) < .001f,
                "An edge snap moves the marker to the corrected clear floor position");
            Time.timeScale = 0;
            teleport.ProcessInput(false);
            Check(!ring.enabled && !arrow.enabled && !cross.enabled, "Pause clears all destination feedback");
            Time.timeScale = 1;
            UnityEngine.Object.DestroyImmediate(obstacle);

            floor.GetComponent<TeleportArea>().enabled = false;
            Physics.SyncTransforms();
            ResetOwner();
            teleport.ProcessInput(true);
            Check(!teleport.HasValidTarget && ring.enabled && !arrow.enabled && cross.enabled &&
                ring.startColor.r > ring.startColor.b && arc.startColor.r > arc.startColor.b,
                "Blocked targets show a red crossed ring and a matching red arc");
            teleport.ProcessInput(false);
            Check(!ring.enabled && !cross.enabled, "Releasing an invalid aim clears its marker");
            floor.GetComponent<TeleportArea>().enabled = true;
            ResetOwner();
            teleport.ProcessInput(true);
            Call(teleport, "OnDisable");
            Check(!ring.enabled && !arrow.enabled, "Disabling a teleporter clears its marker");

            marker.Show(Vector3.zero, Vector3.up, Vector3.forward, true, true);
            float extent = ring.GetPosition(0).magnitude + ring.startWidth * .5f;
            Check(extent < .11f, "Compact stair ring fits inside a 28 cm tread");
            marker.Show(Vector3.zero, Vector3.up, Vector3.forward, true, false);
            Check(ring.GetPosition(0).magnitude > .2f, "Floor landings use the larger readable ring");
            Set(teleport, "resolution", 1);
            teleport.transform.rotation = Quaternion.Euler(-90, 0, 0);
            Physics.SyncTransforms();
            Call(teleport, "RenderTeleportArc");
            Check(!ring.enabled, "Aiming into empty space hides the destination marker");
            arc.enabled = false;
            RenderDestinationMarkerPreview(marker, floor.GetComponent<Renderer>());
            File.WriteAllLines("Logs/destination-marker-checks.txt", results);
            Debug.Log("DESTINATION_MARKER_CHECKS_PASS");
        }
        catch (Exception exception)
        {
            File.WriteAllLines("Logs/destination-marker-checks.txt", results.Concat(new[] { "FAIL: " + exception }));
            throw;
        }
        finally
        {
            Time.timeScale = 1;
            UnityEngine.Object.DestroyImmediate(teleport.gameObject);
            UnityEngine.Object.DestroyImmediate(rig);
            UnityEngine.Object.DestroyImmediate(floor);
        }
    }

    private static void RenderDestinationMarkerPreview(TeleportDestinationMarker marker, Renderer floor)
    {
        marker.Show(Vector3.zero, Vector3.up, Vector3.forward, true, false);
        var invalid = new GameObject("QA invalid preview").AddComponent<TeleportDestinationMarker>();
        invalid.Show(Vector3.right * .8f, Vector3.up, Vector3.forward, false, false);
        var compact = new GameObject("QA stair preview").AddComponent<TeleportDestinationMarker>();
        compact.Show(Vector3.left * .8f, Vector3.up, Vector3.forward, true, true);
        var cameraObject = new GameObject("QA marker preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(.25f, 1.35f, -1.7f);
        cameraObject.transform.LookAt(new Vector3(0, 0, .08f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .035f, .055f);
        camera.fieldOfView = 50;
        var originalMaterial = floor.sharedMaterial;
        var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        floorMaterial.SetColor("_BaseColor", new Color(.055f, .07f, .09f));
        floor.sharedMaterial = floorMaterial;
        var target = new RenderTexture(1200, 650, 24);
        target.antiAliasing = 4;
        var pixels = new Texture2D(1200, 650, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1200, 650), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/destination-marker-preview.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            floor.sharedMaterial = originalMaterial;
            UnityEngine.Object.DestroyImmediate(floorMaterial);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(invalid.gameObject);
            UnityEngine.Object.DestroyImmediate(compact.gameObject);
        }
    }

    private static void RunChecks(bool apply)
    {
        results.Clear();
        try
        {
            if(apply) CampusTeleportSetup.Apply();
            EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
            var configured=UnityEngine.Object.FindObjectsOfType<VRTeleporter>(true);
            Check(configured.Length==2 && configured.All(t=>t.MaxTeleportDistance==3),"Saved scene has two 3m teleporters");
            Check(configured.Select(t=>t.InputSource).Distinct().Count()==2,"Left/right hands have distinct input sources");
            var menuRays=UnityEngine.Object.FindObjectsOfType<VRMenuRayPointer>(true);
            Check(menuRays.Length==2 && menuRays.All(ray=>new SerializedObject(ray).FindProperty("menuButtons").arraySize==7),
                "Both hands have dedicated rays for all seven pause/outcome buttons");
            var configuredMovement=new SerializedObject(UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true));
            Check(((CampusLocomotion)configuredMovement.targetObject).gameObject.activeInHierarchy,"Saved Player and shared systems are enabled for direct testing");
            Check(configuredMovement.FindProperty("teleportOnly").boolValue && configuredMovement.FindProperty("footsteps").objectReferenceValue==null && configuredMovement.FindProperty("footstepClips").arraySize==0,"Saved scene uses teleport with no footsteps");
            Check(UnityEngine.Object.FindObjectsOfType<TeleportArea>(true).Length>=140,"Floors, stairs and outdoor evacuation paths are marked");
            var pause=new SerializedObject(UnityEngine.Object.FindObjectOfType<SystemUIPauseController>(true));
            Check(pause.FindProperty("teleport").objectReferenceValue!=null && pause.FindProperty("additionalTeleports").arraySize==1,"Pause controls both teleporters");
            CheckStairRoute(configured[0]);
            CheckStairAim(configured[0]);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RunIsolated();
            File.WriteAllLines("Documentation/TeleportChecks.txt",results.Concat(new[]{"Headset input, visual comfort and audio: require hardware verification."}));
            Debug.Log("CAMPUS_TELEPORT_CHECKS_PASS\n"+string.Join("\n",results));
        }
        catch(Exception exception)
        {
            File.WriteAllLines("Documentation/TeleportChecks.txt",results.Concat(new[]{"FAIL: "+exception}));
            Debug.LogException(exception); throw;
        }
    }

    private static void CheckStairRoute(VRTeleporter teleport)
    {
        var movement=UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true);
        // Activate only the unsaved test copy; the user's saved activation state is preserved.
        for(Transform parent=movement.transform;parent!=null;parent=parent.parent) parent.gameObject.SetActive(true);
        var rig=movement.transform; var body=movement.GetComponent<CharacterController>(); body.enabled=false;
        var head=(Transform)new SerializedObject(movement).FindProperty("trackedHead").objectReferenceValue;
        Vector3 offset=head.position-rig.position; offset.y=0;
        var resolve = typeof(VRTeleporter).GetMethod("TryResolveLanding", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector3 previous=new Vector3(24.45f,11.4f,.7f);
        var targets=new List<Vector3>();
        for(int floor=3;floor>=1;floor--)
        {
            float y=(floor-1)*3.8f;
            for(int step=9;step>=0;step--) targets.Add(new Vector3(24.45f,y+1.9f+(step+1)*.19f,3.76f-step*.28f));
            targets.Add(new Vector3(24.45f,y+1.9f,4.65f));
            targets.Add(new Vector3(23,y+1.9f,4.65f)); targets.Add(new Vector3(21.55f,y+1.9f,4.65f));
            for(int step=9;step>=0;step--) targets.Add(new Vector3(21.55f,y+(step+1)*.19f,1.24f+step*.28f));
            targets.Add(new Vector3(21.55f,y,.7f));
            if(floor>1) { targets.Add(new Vector3(23,y,.7f)); targets.Add(new Vector3(24.45f,y,.7f)); }
        }
        foreach(var target in targets)
        {
            rig.position=previous-offset; Physics.SyncTransforms();
            bool marked=Physics.Raycast(target+Vector3.up*.06f,Vector3.down,out var hit,.12f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider.GetComponent<TeleportArea>()!=null;
            Check(marked && (bool)resolve.Invoke(teleport, new object[] { hit, Vector3.zero }),"Stair B teleport route "+previous+" -> "+target);
            previous=target;
        }
        rig.position = new Vector3(21.55f, 0, .7f) - offset;
        Physics.SyncTransforms();
        // Aim into the vertical face of the fourth tread from the floor landing.
        var tread = Physics.Raycast(new Vector3(21.55f, .69f, 1.80f), Vector3.forward, out var riser, .3f);
        object[] args = { riser, Vector3.zero };
        Check(tread && (bool)resolve.Invoke(teleport, args) &&
            Mathf.Abs(((Vector3)args[1]).y - .76f) < .01f && ((Vector3)args[1]).z > riser.point.z + .05f,
            "A short multi-step hop snaps a riser hit inside the intended tread");

        // Both points are at similar heights and within range, on opposite sides of the void.
        rig.position = new Vector3(22.15f, 1.71f, 3.48f) - offset;
        Physics.SyncTransforms();
        var across = new Vector3(23.85f, 2.28f, 3.48f);
        var acrossHit = Physics.Raycast(across + Vector3.up * .06f, Vector3.down, out var otherFlight, .12f);
        var route = typeof(VRTeleporter).GetMethod("HasConnectedStairPath", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(acrossHit && teleport.IsWithinRange(across) &&
            !(bool)route.Invoke(teleport, new object[] { across, otherFlight.collider.GetComponent<TeleportArea>() }) &&
            !(bool)resolve.Invoke(teleport, new object[] { otherFlight, Vector3.zero }),
            "A nearby tread across the stairwell cannot bypass the connecting landing");
    }

    private static VRTeleporter MakeTeleporter(Transform rig,Transform head,SteamVR_Input_Sources source)
    {
        var go=new GameObject(source.ToString());
        go.transform.position=new Vector3(1.5f,1,0); go.transform.rotation=Quaternion.Euler(90,0,0);
        var teleport=go.AddComponent<VRTeleporter>();
        Set(teleport,"playerRoot",rig); Set(teleport,"vrCamera",head); Set(teleport,"inputSource",source);
        Set(teleport,"useCampusInput",true); Set(teleport,"maxTeleportDistance",2f); Set(teleport,"maxHeightChange",2f);
        Set(teleport,"teleportLayer",(LayerMask)~0); Call(teleport,"Awake"); return teleport;
    }

    private static void CheckStairAim(VRTeleporter teleport)
    {
        var settings=new SerializedObject(teleport);
        var rig=(Transform)settings.FindProperty("playerRoot").objectReferenceValue;
        var head=(Transform)settings.FindProperty("vrCamera").objectReferenceValue;
        Vector3 headOffset=head.position-rig.position; headOffset.y=0;
        Call(teleport,"Awake");
        foreach(bool ascending in new[]{true,false})
        {
            Vector3 feet=ascending?new Vector3(21.55f,0,.7f):new Vector3(24.45f,3.8f,.7f);
            rig.position=feet-headOffset;
            teleport.transform.position=feet+new Vector3(0,1.2f,.3f);
            bool reachable=false;
            for(int pitch=-40;pitch<=70;pitch+=2)
            {
                teleport.transform.rotation=Quaternion.Euler(pitch,0,0);
                Physics.SyncTransforms(); Call(teleport,"RenderTeleportArc");
                var arc=teleport.GetComponent<LineRenderer>(); Vector3 end=arc.GetPosition(arc.positionCount-1);
                if(teleport.HasValidTarget && (ascending?end.y>feet.y+.18f:end.y<feet.y-.18f)) { reachable=true; break; }
            }
            Check(reachable,"Controller-directed arc can aim at actual Stair B treads: "+(ascending?"up":"down"));
        }
        Call(teleport,"OnDisable");
    }

    private static void RunIsolated()
    {
        Time.timeScale=1; ResetOwner(); CampusSmokeVolume.Active.Clear();
        var rig=new GameObject("QA rig"); var body=rig.AddComponent<CharacterController>();
        body.height=1.65f; body.radius=.23f; body.center=Vector3.up*.825f;
        var head=new GameObject("QA head"); head.tag="MainCamera"; head.AddComponent<Camera>();
        head.transform.SetParent(rig.transform,false); head.transform.localPosition=Vector3.up*1.65f;
        var movement=rig.AddComponent<CampusLocomotion>(); Set(movement,"trackedHead",head.transform); Call(movement,"Awake");
        var health=new GameObject("QA health").AddComponent<HealthTimeController>();
        Set(health,"logEvents",false); Call(health,"Awake"); health.ResetHealth();
        var messages=new GameObject("QA warning").AddComponent<CampusMessagePresenter>(); Call(messages,"Awake");
        var exposure=new GameObject("QA exposure").AddComponent<CampusSmokeExposure>(); Set(exposure,"trackedHead",head.transform);
        var resolution=new GameObject("QA outcome").AddComponent<ResolutionCoordinator>();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="QA floor";
        floor.transform.position=Vector3.down*.05f; floor.transform.localScale=new Vector3(20,.1f,20); floor.AddComponent<TeleportArea>();
        var left=MakeTeleporter(rig.transform,head.transform,SteamVR_Input_Sources.LeftHand);
        var right=MakeTeleporter(rig.transform,head.transform,SteamVR_Input_Sources.RightHand);
        Check(left.IsWithinRange(Vector3.right*2) && !left.IsWithinRange(Vector3.right*2.001f),"2m accepted; 2.001m rejected");
        Check(!left.IsWithinRange(new Vector3(1.8f,1,0)),"Range includes vertical displacement on stairs");
        head.transform.localPosition+=Vector3.right*.35f;
        Check(left.IsWithinRange(Vector3.right*2.35f) && !left.IsWithinRange(Vector3.left*1.66f),"Distance is measured below the tracked head, including room-scale offset");
        head.transform.localPosition=Vector3.up*1.65f;
        Set(left,"maxTeleportDistance",3f);
        Check(left.IsWithinRange(Vector3.forward*3) && !left.IsWithinRange(Vector3.forward*3.001f),"Campus 3m boundary is enforced");
        Check(!left.IsWithinRange(new Vector3(0,2.01f,1)) && left.IsWithinRange(new Vector3(0,1.9f,2f)),"3m range retains the 2m height cap and permits shorter stair hops");
        foreach(float height in new[]{.9f,1.2f,1.5f})
        {
          float previousDistance=float.PositiveInfinity;
          foreach(float pitch in new[]{0f,15f,30f,60f})
          {
            left.transform.position=new Vector3(.22f,height,.4f);
            left.transform.rotation=Quaternion.Euler(pitch,0,0);
            ResetOwner(); left.ProcessInput(true);
            var arc=left.GetComponent<LineRenderer>();
            float landingDistance=Vector3.Distance(rig.transform.position,arc.GetPosition(arc.positionCount-1));
            Check(left.HasValidTarget && landingDistance<previousDistance &&
                (pitch==0?landingDistance>=2.7f && landingDistance<=3:landingDistance>.5f && landingDistance<3),
                "Aim reaches useful forward/near floor range at hand height "+height+" / downward pitch "+pitch);
            previousDistance=landingDistance;
            float highest=Enumerable.Range(0,arc.positionCount).Max(i=>arc.GetPosition(i).y);
            Check(highest<=height+.001f,"Level/downward controller aim never arches above the hand");
            Vector3 launch=(Vector3)typeof(VRTeleporter).GetMethod("GetLaunchVelocity",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(left,null);
            Check(Vector3.Dot(launch.normalized,left.transform.forward)>.9999f,"Launch velocity follows the controller direction");
            // Cancel without moving so all height/angle combinations share the same floor origin.
            Call(left,"OnDisable"); left.ProcessInput(false);
          }
        }
        Set(left,"maxTeleportDistance",2f);
        left.transform.position=new Vector3(1.5f,1,0); left.transform.rotation=Quaternion.Euler(90,0,0);
        Vector3 before=rig.transform.position;
        for(int i=0;i<10;i++) movement.Step(Vector2.up,1,.05f);
        Check(Vector3.ProjectOnPlane(rig.transform.position-before,Vector3.up).magnitude<.001f && rig.transform.rotation==Quaternion.identity,"Joystick input cannot slide or turn the rig");
        body.enabled=false; rig.transform.position=Vector3.zero; body.enabled=true; Physics.SyncTransforms();
        left.ProcessInput(true); Check(left.IsAiming && left.HasValidTarget,"Forward input acquires a valid marked floor");
        right.ProcessInput(true); Check(!right.IsAiming,"Second hand cannot take over an active aim");
        left.ProcessInput(false); Check(Mathf.Abs(rig.transform.position.x-1.5f)<.001f && body.enabled,"Release teleports and restores the CharacterController");
        right.ProcessInput(false); Check(Mathf.Abs(rig.transform.position.x-1.5f)<.001f,"Second-hand release cannot cause another teleport");
        ResetOwner(); right.ProcessInput(true); Check(right.IsAiming,"Right hand can independently aim"); right.enabled=false; Call(right,"OnDisable");
        Check(!right.IsAiming && !right.GetComponent<LineRenderer>().enabled,"Disabling a teleporter cancels its arc"); right.enabled=true;
        right.ProcessInput(true); Check(!right.IsAiming,"Re-enabling while the stick is held requires release before another aim"); right.ProcessInput(false);
        ResetOwner(); left.ProcessInput(true); Time.timeScale=0; left.ProcessInput(false);
        Check(!left.IsAiming && !left.GetComponent<LineRenderer>().enabled,"Pause cancels an aim without teleporting"); Time.timeScale=1;
        movement.SetScriptedLock(true); left.ProcessInput(true); Check(!left.IsAiming,"Scripted lesson lock blocks teleport"); movement.SetScriptedLock(false); left.ProcessInput(false);
        resolution.TryClaimGameOver(); right.ProcessInput(true); Check(!right.IsAiming,"Outcome blocks teleport"); resolution.ResetResolution(); right.ProcessInput(false);
        body.enabled=false; rig.transform.position=Vector3.zero; body.enabled=true;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(.75f,.85f,0); wall.transform.localScale=new Vector3(.1f,1.7f,2);
        ResetOwner(); left.ProcessInput(true); Check(!left.HasValidTarget,"A wall between the player and destination blocks teleport even when the arc starts beyond it"); left.ProcessInput(false); UnityEngine.Object.DestroyImmediate(wall);
        floor.GetComponent<TeleportArea>().enabled=false;
        // The marker must be present and enabled to authorize a target.
        ResetOwner(); left.ProcessInput(true); Check(!left.HasValidTarget,"Disabled teleport areas reject landing"); left.ProcessInput(false); floor.GetComponent<TeleportArea>().enabled=true;
        var cloud=new GameObject("QA smoke"); var volume=cloud.AddComponent<BoxCollider>(); volume.isTrigger=true;
        volume.size=new Vector3(20,4,4); cloud.transform.position=new Vector3(0,1.65f,0);
        var smoke=cloud.AddComponent<CampusSmokeVolume>(); Call(smoke,"OnEnable");
        exposure.Tick(false,0); exposure.Tick(true,0);
        Check(messages.IsVisible && messages.CurrentMessage==CampusMessagePresenter.SmokeWarning && health.HealthPercent==1,"Smoke entry warns immediately before the grace period expires");
        messages.Hide(); exposure.Tick(true,0); Check(!messages.IsVisible,"Remaining in smoke does not repeat the warning");
        exposure.Tick(false,0); exposure.Tick(true,0); Check(messages.IsVisible,"Leaving and re-entering smoke warns again");
        exposure.Tick(false,0); health.ResetHealth();
        for(int i=0;i<2;i++) exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-4/1.8f)<.001f && health.HealthPercent==1,"Two 2m teleports count 2.22s of smoke without premature damage");
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-6/1.8f)<.001f && Mathf.Abs(health.HealthPercent-(1-(6/1.8f-3)*2/100))<.0001f,"Third teleport crosses the 3s threshold and applies proportional damage");
        var grip=new GameObject("QA towel hand").transform; grip.SetParent(rig.transform,false);
        grip.position=head.transform.TransformPoint(new Vector3(0,-.1f,.065f));
        var cloth=new GameObject("QA towel"); cloth.AddComponent<Rigidbody>(); cloth.AddComponent<BoxCollider>().size=new Vector3(.2f,.15f,.02f);
        var towel=cloth.AddComponent<CampusTowelInteractable>(); Call(towel,"Awake"); Call(towel,"OnEnable");
        Set(towel,"<IsWet>k__BackingField",true); towel.OnInteractStart(grip); Physics.SyncTransforms();
        exposure.Tick(false,0); health.ResetHealth(); exposure.Tick(true,3);
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(exposure.IsProtected && Mathf.Abs(health.HealthPercent-(1-2/1.8f*.1f/100))<.0001f,"A wet towel covering the face limits equivalent teleport damage to 0.1 HP/s");
        towel.OnInteractEnd(grip); CampusTowelInteractable.Active.Remove(towel); UnityEngine.Object.DestroyImmediate(cloth);
        exposure.Tick(false,0); health.ResetHealth();
        var overlapping=UnityEngine.Object.Instantiate(cloud); var overlappingSmoke=overlapping.GetComponent<CampusSmokeVolume>(); Call(overlappingSmoke,"OnEnable");
        exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(Mathf.Abs(exposure.Exposure-2/1.8f)<.001f,"Overlapping smoke volumes count once");
        UnityEngine.Object.DestroyImmediate(overlapping); CampusSmokeVolume.Active.Remove(overlappingSmoke);
        exposure.Tick(false,0); volume.size=new Vector3(.02f,4,4); cloud.transform.position=new Vector3(1,1.65f,0);
        float hp=health.HealthPercent; exposure.RecordTeleport(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0));
        Check(exposure.Exposure==0 && health.HealthPercent==hp && messages.IsVisible,"Crossing a thin cloud warns and clean-air exit resets exposure");
        Check(smoke.SegmentInterval(new Vector3(0,1.65f,0),new Vector3(2,1.65f,0),out float a,out float b) && Mathf.Abs(b-a-.01f)<.0001f,"Thin smoke intersections are calculated exactly");
        exposure.Tick(false,0); messages.Hide(); Time.timeScale=0; exposure.Tick(true,10);
        Check(!messages.IsVisible && health.HealthPercent==hp,"Paused smoke processing causes no warning or damage"); Time.timeScale=1;
        results.Add("PASS: smoke warning uses existing five-second safety-message presentation");
        CheckMenuRays();
        CheckLandingTolerance();
    }

    private static void CheckLandingTolerance()
    {
        Vector3 origin=Vector3.right*1000;
        var rig=new GameObject("QA landing rig"); rig.transform.position=origin;
        var body=rig.AddComponent<CharacterController>(); body.stepOffset=.24f; body.enabled=false;
        var head=new GameObject("QA landing head").transform; head.SetParent(rig.transform,false); head.localPosition=Vector3.up*1.65f;
        var teleport=MakeTeleporter(rig.transform,head,SteamVR_Input_Sources.LeftHand); Set(teleport,"maxTeleportDistance",3f);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position=origin+Vector3.down*.05f; floor.transform.localScale=new Vector3(20,.1f,20);
        var floorParent=new GameObject("QA marked floor parent"); floor.transform.SetParent(floorParent.transform,true); floorParent.AddComponent<TeleportArea>();
        teleport.transform.position=origin+new Vector3(1.5f,1,0); teleport.transform.rotation=Quaternion.Euler(90,0,0);
        Physics.SyncTransforms(); Call(teleport,"RenderTeleportArc");
        Check(teleport.HasValidTarget,"A marked floor parent authorizes its child collider");
        var edge=GameObject.CreatePrimitive(PrimitiveType.Cube); edge.transform.position=origin+new Vector3(1.5f,.85f,.35f);
        edge.transform.localScale=new Vector3(1,1.7f,.4f);
        Physics.SyncTransforms(); Call(teleport,"RenderTeleportArc");
        var arc=teleport.GetComponent<LineRenderer>();
        Vector3 corrected=arc.GetPosition(arc.positionCount-1);
        Check(teleport.HasValidTarget && Vector3.Distance(corrected,origin+Vector3.right*1.5f)<=.301f && corrected.z<-.01f,
            "A slightly obstructed floor edge snaps to a visible, clear landing within 30cm");
        edge.transform.position=origin+new Vector3(1.5f,.4f,.5f); edge.transform.localScale=new Vector3(1,.8f,1);
        teleport.transform.position=origin+new Vector3(1.5f,1,.01f);
        Physics.SyncTransforms(); Call(teleport,"RenderTeleportArc");
        corrected=arc.GetPosition(arc.positionCount-1);
        Check(teleport.HasValidTarget && Mathf.Abs(corrected.y)<.01f && corrected.z<-.2f,
            "A ray grazing a desk edge resolves to clear adjacent floor instead of the desktop");
        teleport.transform.position=origin+new Vector3(1.5f,1,0);
        edge.transform.position=origin+new Vector3(.75f,.85f,0); edge.transform.localScale=new Vector3(.1f,1.7f,2);
        Physics.SyncTransforms(); Call(teleport,"RenderTeleportArc");
        Check(!teleport.HasValidTarget,"Landing tolerance and stair retry cannot bypass a wall");
        UnityEngine.Object.DestroyImmediate(edge);
        UnityEngine.Object.DestroyImmediate(teleport.gameObject); UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(floorParent);
    }

    private static void CheckMenuRays()
    {
        var rig=new GameObject("QA menu rig"); rig.transform.position=Vector3.up*100;
        var buttons=new List<VRMenuButton>();
        int clicks=0;
        foreach(string name in new[]{"Pause","Failure","Victory"})
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name="QA "+name;
            go.transform.SetParent(rig.transform,false); go.transform.localPosition=new Vector3(0,0,1);
            go.transform.localScale=new Vector3(.3f,.2f,.03f);
            var button=go.AddComponent<VRMenuButton>();
            var click=new UnityEngine.Events.UnityEvent(); click.AddListener(()=>clicks++); Set(button,"onClick",click);
            buttons.Add(button); go.SetActive(false);
        }
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=rig.transform.position+Vector3.forward*.5f;
        wall.transform.localScale=new Vector3(2,2,.1f);
        var resetClick=typeof(VRMenuButton).GetField("lastActivationFrame",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(var source in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
        {
            var hand=new GameObject("QA menu "+source); hand.transform.SetParent(rig.transform,false);
            var ray=hand.AddComponent<VRMenuRayPointer>();
            Set(ray,"trackedController",hand.transform); Set(ray,"playerRoot",rig.transform);
            Set(ray,"inputSource",source); Set(ray,"useCampusInput",true); Set(ray,"downwardPitchDegrees",0f);
            Set(ray,"menuButtons",buttons.ToArray());
            Set(ray,"laserShader",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/SystemUI/VRMenuLaser.shader")); Call(ray,"Awake");
            foreach(var button in buttons)
            {
                ray.ProcessInput(false); Check(!ray.IsVisible && ray.CurrentTarget==null,"Menu ray hidden during gameplay: "+source);
                Time.timeScale=button==buttons[0]?0:1;
                int before=clicks; button.gameObject.SetActive(true); resetClick.SetValue(null,-1);
                ray.ProcessInput(true);
                Check(ray.IsVisible && ray.CurrentTarget==button && clicks==before,"Menu opens without clicking an already held trigger: "+button.name+" / "+source);
                ray.ProcessInput(false); ray.ProcessInput(true);
                Check(clicks==before+1,"Fresh trigger clicks foreground menu beyond a wall while retaining rig-child buttons: "+button.name+" / "+source);
                resetClick.SetValue(null,-1); ray.ProcessInput(true);
                Check(clicks==before+1,"Held trigger cannot repeat menu activation");
                ray.enabled=false; Call(ray,"OnDisable"); ray.enabled=true; ray.ProcessInput(true);
                Check(clicks==before+1,"Re-enabled menu ray requires release before clicking");
                button.gameObject.SetActive(false); ray.ProcessInput(false);
                Check(!ray.IsVisible && ray.CurrentTarget==null,"Closing the menu hides its ray");
            }
            UnityEngine.Object.DestroyImmediate(hand);
        }
        Time.timeScale=1; UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(wall);
    }
}
