using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

[DefaultExecutionOrder(29), RequireComponent(typeof(LineRenderer))]
public class VRTeleporter : MonoBehaviour
{
    [Header("SteamVR Input")]
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any;
    [SerializeField] private SteamVR_Action_Boolean teleportAction;

    [Header("Player Setup")]
    [SerializeField] private Transform playerRoot; 
    [SerializeField] private Transform vrCamera;   

    [Header("Arc Settings")]
    [SerializeField, Min(1)] private int resolution = 40;
    [SerializeField] private float initialVelocity = 8f; 
    [SerializeField] private LayerMask teleportLayer;    
    [Tooltip("0 preserves legacy unrestricted height. Campus stairs use 2 m to prevent skipping floors/window drops.")]
    [SerializeField, Min(0)] private float maxHeightChange;
    [Tooltip("Maximum 3D distance from the floor below the head to the destination; 0 keeps legacy range.")]
    [SerializeField, Min(0)] private float maxTeleportDistance;
    [SerializeField] private bool useCampusInput;

    private LineRenderer lineRenderer;
    private Vector3 teleportTarget;
    private bool isValidLocationFound;
    private bool isAimingTeleport;
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private static VRTeleporter aimingHand;
    private static int lastTeleportFrame = -1;
    private bool previousHeld;
    private CampusLocomotion locomotion;
    private ResolutionCoordinator resolutionCoordinator;
    private CampusSmokeExposure smokeExposure;
    private CampusTrackedHandPose[] trackedHands;
    private MaterialPropertyBlock arcColour;
    public float MaxTeleportDistance => maxTeleportDistance;
    public SteamVR_Input_Sources InputSource => inputSource;
    public bool IsAiming => isAimingTeleport;
    public bool HasValidTarget => isValidLocationFound;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAimOwner() { aimingHand=null; lastTeleportFrame=-1; }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false; 

        if (teleportAction == null)
            teleportAction = SteamVR_Input.GetBooleanAction("Teleport");

        locomotion=playerRoot!=null?playerRoot.GetComponent<CampusLocomotion>():null;
        resolutionCoordinator=FindObjectOfType<ResolutionCoordinator>();
        smokeExposure=FindObjectOfType<CampusSmokeExposure>();
        trackedHands=FindObjectsOfType<CampusTrackedHandPose>();

        if (!useCampusInput && vrCamera != null && vrCamera.GetComponent<VRControlTutorial>() == null)
            vrCamera.gameObject.AddComponent<VRControlTutorial>();
    }

    private void Update()
    {
        if(useCampusInput) ProcessInput(CampusControllerInput.Teleport(inputSource));
    }
    private void LateUpdate()
    { if(!useCampusInput) ProcessInput(teleportAction!=null && teleportAction.GetState(inputSource)); }

    // Public for deterministic input regression checks without headset hardware.
    public void ProcessInput(bool teleportHeld)
    {
        var pose=GetComponentInParent<CampusTrackedHandPose>();
        bool blocked=playerRoot==null || Time.timeScale==0 ||
            (locomotion!=null && locomotion.ScriptedLocked) ||
            (CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.MovementLocked) ||
            (resolutionCoordinator!=null && resolutionCoordinator.CurrentOutcome!=ResolutionCoordinator.Outcome.None) ||
            (useCampusInput && pose!=null && CampusControllerInput.HardwarePresent && !pose.PhysicalTrackingValid);
        if(blocked) { CancelAim(); previousHeld=teleportHeld; return; }
        if (teleportHeld && !previousHeld && aimingHand==null && lastTeleportFrame!=Time.frameCount)
        {
            aimingHand=this;
            isAimingTeleport = true;
        }
        if(teleportHeld && isAimingTeleport)
        {
            Physics.SyncTransforms();
            RenderTeleportArc();
        }
        else if (isAimingTeleport)
        {
            // Detect release from the held state, then use the latest controller pose.
            Physics.SyncTransforms(); RenderTeleportArc();

            if (isValidLocationFound)
            {
                ExecuteTeleport(); lastTeleportFrame=Time.frameCount;
            }

            CancelAim();
        }
        previousHeld=teleportHeld;
    }

    private void CancelAim()
    {
        if(lineRenderer!=null) lineRenderer.enabled=false;
        isValidLocationFound=isAimingTeleport=false;
        if(aimingHand==this) aimingHand=null;
    }
    private void OnDisable() { CancelAim(); previousHeld=true; }

    private void RenderTeleportArc()
    {
        lineRenderer.enabled = true;
        List<Vector3> arcPoints = new List<Vector3>();

        Vector3 currentPosition = transform.position;
        Vector3 currentVelocity = GetLaunchVelocity();

        arcPoints.Add(currentPosition);
        isValidLocationFound = false;

        float timeStep = 0.05f;
        for (int i = 0; i < resolution; i++)
        {
            Vector3 nextPosition = currentPosition + currentVelocity * timeStep + 0.5f * Physics.gravity * timeStep * timeStep;
            Vector3 directVector = nextPosition - currentPosition;

            
            if (TryGetFirstBlockingHit(currentPosition, directVector.normalized, directVector.magnitude, out RaycastHit hit))
            {
                arcPoints.Add(hit.point);

                if (TryResolveLanding(hit,out Vector3 landing))
                {
                    teleportTarget = landing;
                    arcPoints[arcPoints.Count-1]=landing;
                    isValidLocationFound = true;
                }

                // Any first solid hit stops the arc. Only marked floor hits are valid targets.
                break;
            }

            
            currentPosition = nextPosition;
            currentVelocity += Physics.gravity * timeStep;
            arcPoints.Add(currentPosition);
        }

        
        lineRenderer.positionCount = arcPoints.Count;
        lineRenderer.SetPositions(arcPoints.ToArray());

        lineRenderer.startColor = isValidLocationFound ? Color.green : Color.red;
        lineRenderer.endColor = isValidLocationFound ? Color.green : Color.red;
        // URP Unlit ignores vertex tint on some line materials; set per-renderer tint too.
        if(arcColour==null) arcColour=new MaterialPropertyBlock();
        arcColour.SetColor("_BaseColor",lineRenderer.startColor);
        arcColour.SetColor("_Color",lineRenderer.startColor);
        lineRenderer.SetPropertyBlock(arcColour);
    }

    private Vector3 GetLaunchVelocity()
    {
        float speed=initialVelocity;
        if(maxTeleportDistance>0 && playerRoot!=null)
        {
            Vector3 feet=vrCamera!=null?vrCamera.position:playerRoot.position;
            feet.y=playerRoot.position.y;
            float handOffset=Vector3.ProjectOnPlane(transform.position-feet,Vector3.up).magnitude;
            // Keep a level aim inside the range at the current hand height.
            // Speed is capped; the launch direction always follows the controller.
            float availableRange=Mathf.Max(.05f,maxTeleportDistance-handOffset-.1f);
            float drop=Mathf.Max(.1f,transform.position.y-feet.y);
            float fallTime=Mathf.Sqrt(2*drop/Mathf.Max(.01f,-Physics.gravity.y));
            speed=Mathf.Min(speed,availableRange/fallTime);
        }
        return transform.forward*speed;
    }

    private bool TryGetFirstBlockingHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit firstHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool foundHit = false;
        firstHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = raycastHits[i].collider;
            if (candidate == null)
                continue;

            Transform hitTransform = candidate.transform;
            if (IsPlayerCollider(candidate)) continue;

            if (!foundHit || raycastHits[i].distance < firstHit.distance)
            {
                firstHit = raycastHits[i];
                foundHit = true;
            }
        }

        return foundHit;
    }

    private void ExecuteTeleport()
    {
        Vector3 cameraOffset = vrCamera!=null?vrCamera.position-playerRoot.position:Vector3.zero;
        cameraOffset.y = 0; // Keep the player safely locked to floor level height
        Vector3 destination=teleportTarget-cameraOffset;
        Vector3 delta=destination-playerRoot.position;
        // Evaluate protection before translating the head and the held cloth together.
        if(useCampusInput && smokeExposure!=null && vrCamera!=null)
            smokeExposure.RecordTeleport(vrCamera.position,vrCamera.position+delta);
        var body=playerRoot.GetComponent<CharacterController>();
        bool restoreBody=body!=null && body.enabled;
        if(restoreBody) body.enabled=false;
        playerRoot.position=destination;
        if(trackedHands!=null) foreach(var hand in trackedHands)
            if(hand!=null && hand.TrackingSpace==playerRoot) hand.FollowRigTranslation(delta);
        if(restoreBody) body.enabled=true;
        Physics.SyncTransforms();
    }

    public bool IsWithinRange(Vector3 point)
    {
        if(playerRoot==null) return false;
        Vector3 feet=vrCamera!=null?vrCamera.position:playerRoot.position; feet.y=playerRoot.position.y;
        return (maxTeleportDistance<=0 || Vector3.Distance(feet,point)<=maxTeleportDistance+.0001f) &&
            (maxHeightChange<=0 || Mathf.Abs(point.y-playerRoot.position.y)<=maxHeightChange);
    }

    private bool IsPlayerCollider(Collider candidate)
    {
        Transform t=candidate.transform;
        if(t==transform || t.IsChildOf(transform) || (playerRoot!=null && (t==playerRoot || t.IsChildOf(playerRoot)))) return true;
        var hand=candidate.GetComponentInParent<CampusTrackedHandPose>();
        if(hand!=null && hand.TrackingSpace==playerRoot) return true;
        var towel=candidate.GetComponentInParent<CampusTowelInteractable>();
        if(towel!=null && towel.IsHeld) return true;
        var book=candidate.GetComponentInParent<CampusPracticeBook>();
        return book!=null && book.IsHeld;
    }

    private bool HasClearTravelPath(Vector3 point)
    {
        if(!useCampusInput) return true;
        Vector3 feet=vrCamera!=null?vrCamera.position:playerRoot.position; feet.y=playerRoot.position.y;
        Vector3 delta=point-feet; float distance=delta.magnitude;
        if(distance<.001f) return true;
        float height=vrCamera!=null?Mathf.Clamp(vrCamera.position.y-playerRoot.position.y,1.2f,2.1f):1.7f;
        foreach(var hit in Physics.CapsuleCastAll(feet+Vector3.up*.42f,feet+Vector3.up*(height-.2f),.2f,
            delta/distance,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            if(!IsPlayerCollider(hit.collider)) return false;
        return true;
    }

    private bool IsWalkableHit(RaycastHit hit)
    {
        var area=hit.collider.GetComponentInParent<TeleportArea>();
        return (teleportLayer.value & (1<<hit.collider.gameObject.layer))!=0 && hit.normal.y>=.65f &&
            area!=null && area.isActiveAndEnabled;
    }

    private bool TryResolveLanding(RaycastHit originalHit,out Vector3 landing)
    {
        Vector3 requested=originalHit.point;
        landing=requested;
        bool floor=IsWalkableHit(originalHit);
        if(floor && IsWithinRange(requested) && HasStandingClearance(requested) && HasClearTravelPath(requested)) return true;
        if(!useCampusInput || originalHit.normal.y<.65f) return false;
        // Small edge tolerance: show the corrected endpoint, and validate every
        // candidate against the same floor, body, distance and wall rules.
        for(int ring=1;ring<=2;ring++) for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI*.25f;
            Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.15f);
            // A ray grazing a desk's top edge may still have clear floor beside it.
            if(!TryGetFirstBlockingHit(requested+offset+Vector3.up*.08f,Vector3.down,floor?.16f:1.1f,out var hit) ||
                !IsWalkableHit(hit) || !IsWithinRange(hit.point) || !HasStandingClearance(hit.point) ||
                !HasClearTravelPath(hit.point)) continue;
            landing=hit.point; return true;
        }
        return false;
    }

    private bool HasStandingClearance(Vector3 point)
    {
        float height = vrCamera != null ? Mathf.Clamp(vrCamera.position.y - playerRoot.position.y, 1.2f, 2.1f) : 1.7f;
        // Leave foot-level clearance for a 0.19m stair riser; still reject walls/low ceilings.
        Collider[] overlaps = Physics.OverlapCapsule(point + Vector3.up * .42f,
            point + Vector3.up * (height - .2f), .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider c in overlaps)
        {
            if (IsPlayerCollider(c)) continue;
            return false;
        }
        return true;
    }
}
