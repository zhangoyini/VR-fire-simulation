using UnityEngine;
using Valve.VR;

// Additional menu input only; tracking, contact input and teleport remain unchanged.
[DefaultExecutionOrder(100)]
public class VRMenuRayPointer : MonoBehaviour
{
    [SerializeField] private Transform trackedController;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private VRMenuButton[] menuButtons;
    [SerializeField, Range(0.1f, 10f)] private float maxDistance = 4f;
    [SerializeField, Range(-45f, 45f)] private float downwardPitchDegrees = 18f;
    [SerializeField] private Shader laserShader;
    [SerializeField] private bool useCampusInput;
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.RightHand;

    private SteamVR_Action_Boolean grabAction;
    private SteamVR_Action_Boolean teleportAction;
    private LineRenderer laser;
    private Material laserMaterial;
    private LineRenderer reticle;
    private Material reticleMaterial;
    private bool previousTrigger, wasMenuVisible;
    public VRMenuButton CurrentTarget { get; private set; }
    public bool IsVisible => laser != null && laser.enabled;

    private void Awake()
    {
        grabAction = SteamVR_Input.GetBooleanAction("GrabPinch");
        teleportAction = SteamVR_Input.GetBooleanAction("Teleport");
        Shader shader = laserShader;
        if (shader == null)
        {
            Debug.LogWarning("[VRMenuRayPointer] Laser shader unavailable.", this);
            return;
        }

        var visual = new GameObject("Menu Laser");
        visual.transform.SetParent(transform, false);
        laser = visual.AddComponent<LineRenderer>();
        laserMaterial = new Material(shader);
        laserMaterial.renderQueue = 5000;
        laserMaterial.SetInt("_ZTest", (int)(useCampusInput ? UnityEngine.Rendering.CompareFunction.Always : UnityEngine.Rendering.CompareFunction.LessEqual));
        laser.sharedMaterial = laserMaterial;
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.startWidth = laser.endWidth = 0.003f;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;
        laser.sortingOrder = 32767;
        laser.enabled = false;

        var marker = new GameObject("Menu Target Ring");
        marker.transform.SetParent(transform, false);
        reticle = marker.AddComponent<LineRenderer>();
        reticleMaterial = new Material(laserMaterial);
        // Only the small marker overlays UI; the beam still respects world depth.
        reticleMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        reticle.sharedMaterial = reticleMaterial;
        reticle.useWorldSpace = true;
        reticle.loop = true;
        reticle.positionCount = 16;
        reticle.startWidth = reticle.endWidth = 0.002f;
        reticle.startColor = reticle.endColor = Color.green;
        reticle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        reticle.receiveShadows = false;
        reticle.sortingOrder = 32767;
        reticle.enabled = false;
    }

    private void LateUpdate()
    {
        ProcessInput(useCampusInput ? CampusControllerInput.Trigger(inputSource)
            : grabAction != null && grabAction.GetState(inputSource));
    }

    // Uses unscaled frame input, including when the simulation is paused.
    public void ProcessInput(bool triggerHeld)
    {
        bool menuVisible = false;
        if (menuButtons != null)
            foreach (VRMenuButton button in menuButtons)
                if (button != null && button.isActiveAndEnabled) menuVisible = true;

        bool show = trackedController != null && menuVisible
            && (useCampusInput || teleportAction == null || !teleportAction.GetState(inputSource));
        var pose=trackedController!=null?trackedController.GetComponent<CampusTrackedHandPose>():null;
        if(useCampusInput && CampusControllerInput.HardwarePresent && pose!=null && !pose.PhysicalTrackingValid) show=false;
        bool triggerDown=show && wasMenuVisible && triggerHeld && !previousTrigger;
        previousTrigger=triggerHeld;
        wasMenuVisible=show;
        CurrentTarget=null;
        if (laser != null) laser.enabled = show;
        if (reticle != null) reticle.enabled = false;
        if (!show) return;

        Vector3 origin = trackedController.position;
        // Positive local X pitch turns +Z toward -Y, without changing tracking.
        Quaternion rayRotation = trackedController.rotation * Quaternion.Euler(downwardPitchDegrees, 0f, 0f);
        Vector3 direction = rayRotation * Vector3.forward;
        Vector3 end = origin + direction * maxDistance;
        VRMenuButton target = null;
        float closest = maxDistance;
        Physics.SyncTransforms();

        // Only explicitly registered menu buttons act. Legacy scenes retain solid
        // occlusion; campus foreground menus remain reachable beside world walls.
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, maxDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            VRMenuButton candidate = hit.collider.GetComponentInParent<VRMenuButton>();
            bool valid = candidate != null && candidate.isActiveAndEnabled
                && System.Array.IndexOf(menuButtons, candidate) >= 0;
            // Campus menus render in the foreground, including beside walls. Their
            // dedicated ray hits only registered menu buttons, never world objects.
            if(useCampusInput && !valid) continue;
            if (!valid && playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
            if (hit.collider.isTrigger && !valid) continue;
            if (hit.distance > closest) continue;
            closest = hit.distance;
            end = hit.point;
            target = valid ? candidate : null;
        }

        if (laser != null)
        {
            laser.SetPosition(0, origin);
            laser.SetPosition(1, end);
            laser.startColor = laser.endColor = target != null ? Color.green : Color.cyan;
        }
        if (reticle != null && target != null)
        {
            reticle.enabled = true;
            for (int i = 0; i < reticle.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / reticle.positionCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.008f;
                reticle.SetPosition(i, end + rayRotation * offset);
            }
        }
        CurrentTarget=target;
        if (target != null && triggerDown)
            target.OnInteractStart(trackedController);
    }

    private void OnDisable()
    {
        previousTrigger=true;
        wasMenuVisible=false;
        CurrentTarget=null;
        if (laser != null) laser.enabled = false;
        if (reticle != null) reticle.enabled = false;
    }

    private void OnDestroy()
    {
        if (laser != null) ReleaseVisual(laser.gameObject);
        ReleaseVisual(laserMaterial);
        if (reticle != null) ReleaseVisual(reticle.gameObject);
        ReleaseVisual(reticleMaterial);
    }

    private static void ReleaseVisual(Object value)
    {
        if(value==null) return;
        if(Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
