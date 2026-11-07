using Harbourview.Journey;
using UnityEngine;
using Valve.VR;

// One click per hand at the handles starts assistance; a fresh click on the chair stops it.
[DefaultExecutionOrder(31), RequireComponent(typeof(BoxCollider))]
public sealed class CampusRescueStudent : MonoBehaviour, IInteractable
{
    public enum State { Waiting, Following, Delivered }
    public static bool Rescued { get; private set; }

    [SerializeField] private Transform followTarget;
    [SerializeField] private Collider refugeVolume;
    [SerializeField, Min(.5f)] private float followSpeed = 2.2f;
    [SerializeField, Range(.3f, .6f)] private float followOffset = .5f;
    [SerializeField, Range(.65f, .8f)] private float sideOffset = .7f;
    [Header("Assisting Doorway Clearance / 协助模式门口容差")]
    [Tooltip("Forgiveness per side in metres while assisting. 0 restores strict checks; 0.05 allows 5 cm. 协助时每侧的容差（米），0 为严格检查，0.05 为 5 厘米。")]
    [InspectorName("Doorway Tolerance / 门口容差")]
    [SerializeField, Range(0f, .1f)] private float doorwayTolerance = .05f;
    [SerializeField, Min(.2f)] private float dwellSeconds = 1f;
    [SerializeField] private string pushHint =
        "双手放在左右推把上，各按一次扳机开始协助\nPlace both hands on the handles and press each trigger once to help";
    [SerializeField] private string stopText =
        "已退出协助，双手回到推把各按一次扳机可继续\nAssistance stopped. Press both triggers once at the handles to resume.";
    [SerializeField] private string doneText =
        "同学已安全送达避难区\nStudent delivered to the 2F refuge";

    public State Current { get; private set; } = State.Waiting;
    public bool IsFollowing => isActiveAndEnabled && Current == State.Following && !Blocked;
    public float PlayerClearanceRadius => .2f - Mathf.Clamp(doorwayTolerance, 0, .1f);

    private ResolutionCoordinator resolution;
    private BoxCollider bodyCollider;
    private float lastToggleAt = float.NegativeInfinity;
    private Transform pendingHand;
    private float pendingAt;
    private Vector3 travelForward;
    private Vector3 lastPlayerFeet;
    private Vector3 entrySideStep;
    private bool aligningAlongside;
    private bool hasTravelled;
    private CharacterController assistingBody;
    private float originalBodyRadius;
    private CampusRescueAssistanceView assistanceView;
    [SerializeField] private Vector3[] assistanceRoute = {
        new Vector3(-2.2f, 3.8f, 0),
        new Vector3(-2.2f, 3.8f, -3.4f),
        new Vector3(-5, 3.8f, -6)
    };
    private float dwell;
    private float spawnY;
    private bool started;
    private readonly RaycastHit[] rayHits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Rescued = false;

    private void Awake()
    {
        spawnY = transform.position.y;
        resolution = FindObjectOfType<ResolutionCoordinator>();
        if (followTarget == null)
        {
            var locomotion = FindObjectOfType<CampusLocomotion>();
            followTarget = locomotion != null && locomotion.TrackedHead != null
                ? locomotion.TrackedHead : Camera.main != null ? Camera.main.transform : null;
        }
        if (refugeVolume == null)
        {
            var zone = GameObject.Find("2F Refuge Occupancy");
            if (zone != null) refugeVolume = zone.GetComponent<Collider>();
        }
        bodyCollider = GetComponent<BoxCollider>();
        bodyCollider.isTrigger = false;
        if (bodyCollider.size == Vector3.one) bodyCollider.size = new Vector3(.88f, 1.4f, 1.16f);
        if (bodyCollider.center == Vector3.zero) bodyCollider.center = new Vector3(0f, .67f, 0f);
        travelForward = transform.forward;
    }

    private bool Blocked => Time.timeScale == 0f ||
        (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None) ||
        (CampusLessonDirector.Instance != null && !CampusLessonDirector.Instance.ExerciseStarted);

    public void OnHoverEnter()
    {
        if (!Blocked && Current == State.Waiting)
            CampusMessagePresenter.Instance?.Show(pushHint);
    }
    public void OnHoverExit() { }

    public bool CanPush(Transform hand)
    {
        if (Current == State.Delivered || Blocked || hand == null) return false;
        var interactor = hand.GetComponent<CampusHandInteractor>();
        if (interactor == null) return false;
        float side = interactor.InputSource == SteamVR_Input_Sources.LeftHand ? -1f :
            interactor.InputSource == SteamVR_Input_Sources.RightHand ? 1f : 0f;
        if (side == 0f) return false;
        Vector3 local = transform.InverseTransformPoint(interactor.ContactPoint);
        bool atHandle = local.x * side > .04f &&
            Vector3.Distance(local, new Vector3(side * .22f, .96f, -.30f)) <= .32f;
        if (Current == State.Following)
            atHandle |= Vector3.Distance(local, new Vector3(-side * .22f, .96f, -.30f)) <= .32f;
        return atHandle || (Current == State.Following && PointsAtChair(hand));
    }

    public bool PointsAtChair(Transform hand)
    {
        if (!IsFollowing || hand == null) return false;
        Vector3 origin = hand.GetComponent<CampusHandInteractor>()?.ContactPoint ?? hand.position;
        return Physics.Raycast(origin, hand.forward, out var hit, 3f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            (hit.transform == transform || hit.transform.IsChildOf(transform));
    }

    public void OnInteractStart(Transform hand)
    {
        if (!CanPush(hand) || Time.unscaledTime - lastToggleAt < .35f) return;
        if (Current == State.Waiting)
        {
            bool bothReady = pendingHand != null && pendingHand != hand &&
                Time.unscaledTime - pendingAt <= 1.5f && CanPush(pendingHand) &&
                pendingHand.GetComponent<CampusHandInteractor>().InputSource !=
                    hand.GetComponent<CampusHandInteractor>().InputSource;
            if (!bothReady)
            {
                pendingHand = hand;
                pendingAt = Time.unscaledTime;
                return;
            }
        }
        pendingHand = null;
        lastToggleAt = Time.unscaledTime;
        dwell = 0f;
        if (Current == State.Waiting)
        {
            Current = State.Following;
            ApplyPlayerClearance();
            travelForward = transform.forward;
            // Bring the chair alongside the player while keeping its handles reachable.
            hasTravelled = true;
            lastPlayerFeet = followTarget != null ? followTarget.position : transform.position;
            lastPlayerFeet.y = spawnY;
            Vector3 right = Vector3.Cross(Vector3.up, travelForward);
            float currentSide = Vector3.Dot(transform.position - lastPlayerFeet, right);
            entrySideStep = transform.position + right * (Mathf.Clamp(sideOffset, .65f, .8f) - currentSide);
            aligningAlongside = true;
            if (!started) { started = true; SessionLogger.Write("RESCUE_STARTED"); }
            if (CampusMessagePresenter.Instance != null &&
                (CampusMessagePresenter.Instance.CurrentMessage == pushHint ||
                 CampusMessagePresenter.Instance.CurrentMessage == stopText))
                CampusMessagePresenter.Instance.Hide();
            if (assistanceView == null)
                assistanceView = gameObject.AddComponent<CampusRescueAssistanceView>();
            assistanceView.Begin(followTarget, assistanceRoute);
        }
        else
        {
            Current = State.Waiting;
            RestorePlayerClearance();
            assistanceView?.Hide();
            CampusMessagePresenter.Instance?.Show(stopText);
        }
    }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { }

    public bool TryGetFollowPosition(Vector3 fromFeet, Vector3 toFeet, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        if (!IsFollowing || followTarget == null || Mathf.Abs(fromFeet.y - spawnY) > .2f ||
            Mathf.Abs(toFeet.y - spawnY) > .2f) return false;
        Vector3 movement = Vector3.ProjectOnPlane(toFeet - fromFeet, Vector3.up);
        Vector3 forward = movement.sqrMagnitude > .01f ? movement.normalized : travelForward;
        position = AssistancePosition(toFeet, forward);
        position.y = spawnY;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        return CanPlace(position, rotation);
    }

    private Vector3 AssistancePosition(Vector3 feet, Vector3 forward)
    {
        // Clamp earlier saved forward offsets too, so existing scene instances get the reach fix.
        return feet + forward * Mathf.Clamp(followOffset, .3f, .6f) +
            Vector3.Cross(Vector3.up, forward) * Mathf.Clamp(sideOffset, .65f, .8f);
    }

    private bool CanPlace(Vector3 position, Quaternion rotation)
    {
        bool floor = false;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * .25f, Vector3.down,
            rayHits, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (rayHits[i].collider.GetComponentInParent<TeleportArea>() is TeleportArea area && area.isActiveAndEnabled &&
                Mathf.Abs(rayHits[i].point.y - spawnY) <= .12f) { floor = true; break; }
        if (!floor) return false;

        Vector3 half = WheelchairClearanceHalfSize;
        Vector3 travel = position - transform.position;
        float length = travel.magnitude;
        // Check intermediate orientations as well as the final pose at a corner.
        int turns = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(transform.rotation, rotation) / 15f));
        for (int step = 0; step <= turns; step++)
        {
            Quaternion facing = Quaternion.Slerp(transform.rotation, rotation, (float)step / turns);
            if (!HasClearBody(transform.position, facing) || !HasClearBody(position, facing)) return false;
            if (length > .001f)
            {
                count = Physics.BoxCastNonAlloc(transform.position + Vector3.up * .69f, half,
                    travel / length, rayHits, facing, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == rayHits.Length) return false;
                for (int i = 0; i < count; i++)
                    if (!IgnoreCollision(rayHits[i].collider)) return false;
            }
        }
        return true;
    }

    private bool HasClearBody(Vector3 position, Quaternion rotation)
    {
        int count = Physics.OverlapBoxNonAlloc(position + Vector3.up * .69f,
            WheelchairClearanceHalfSize, overlaps, rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++)
            if (!IgnoreCollision(overlaps[i])) return false;
        return true;
    }

    private Vector3 WheelchairClearanceHalfSize
    {
        get
        {
            float tolerance = Mathf.Clamp(doorwayTolerance, 0, .1f);
            return new Vector3(.44f - tolerance, .58f, .55f - tolerance);
        }
    }

    private void ApplyPlayerClearance()
    {
        if (assistingBody == null && followTarget != null)
        {
            assistingBody = followTarget.GetComponentInParent<CharacterController>();
            if (assistingBody != null) originalBodyRadius = assistingBody.radius;
        }
        if (assistingBody != null)
            assistingBody.radius = Mathf.Min(originalBodyRadius, PlayerClearanceRadius);
    }

    private void RestorePlayerClearance()
    {
        if (assistingBody != null) assistingBody.radius = originalBodyRadius;
        assistingBody = null;
    }

    private bool IgnoreCollision(Collider hit)
    {
        if (hit == bodyCollider || hit.transform.IsChildOf(transform)) return true;
        return hit.GetComponentInParent<CampusLocomotion>() != null ||
            hit.GetComponentInParent<CampusTowelInteractable>()?.IsHeld == true ||
            hit.GetComponentInParent<CampusPracticeBook>()?.IsHeld == true;
    }

    public void FollowTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsFollowing) return;
        transform.SetPositionAndRotation(position, rotation);
        travelForward = rotation * Vector3.forward;
        aligningAlongside = false;
        hasTravelled = true;
        if (followTarget != null)
        {
            lastPlayerFeet = followTarget.position;
            lastPlayerFeet.y = spawnY;
        }
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (Blocked) pendingHand = null;
        if (!IsFollowing || followTarget == null) { assistanceView?.SetVisible(false); return; }
        ApplyPlayerClearance();
        assistanceView?.SetVisible(true);
        // Anchor between teleports so leaning toward a handle or door cannot move it away.
        // Move sideways first on entry, keeping the chair out of the player's body.
        if (aligningAlongside && Vector3.Distance(transform.position, entrySideStep) < .02f)
            aligningAlongside = false;
        Vector3 desired = aligningAlongside ? entrySideStep : AssistancePosition(lastPlayerFeet, travelForward);
        desired.y = spawnY;
        Vector3 next = Vector3.MoveTowards(transform.position, desired, followSpeed * Time.deltaTime);
        Quaternion facing = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(travelForward, Vector3.up), 180f * Time.deltaTime);
        if (hasTravelled && ((next - transform.position).sqrMagnitude > .000025f ||
             Quaternion.Angle(transform.rotation, facing) > .5f) && CanPlace(next, facing))
        {
            transform.SetPositionAndRotation(next, facing);
            Physics.SyncTransforms();
        }

        if (refugeVolume != null && refugeVolume.bounds.Contains(transform.position + Vector3.up * .7f))
        {
            dwell += Time.deltaTime;
            if (dwell >= dwellSeconds) Deliver();
        }
        else dwell = 0f;
    }

    private void Deliver()
    {
        Current = State.Delivered;
        RestorePlayerClearance();
        assistanceView?.Hide();
        Rescued = true;
        dwell = 0f;
        SessionLogger.Write("RESCUE_COMPLETED");
        CampusMessagePresenter.Instance?.Show(doneText);
    }

    public void ResetQuest()
    {
        Current = State.Waiting;
        RestorePlayerClearance();
        pendingHand = null;
        Rescued = false;
        assistanceView?.Hide();
        lastToggleAt = float.NegativeInfinity;
        started = false;
        hasTravelled = false;
        aligningAlongside = false;
        dwell = 0f;
    }

    public void ShowTeleportPreview(Vector3 fromFeet, Vector3 toFeet, bool playerAllowed)
    {
        if (!IsFollowing || assistanceView == null) return;
        bool chairAllowed = TryGetFollowPosition(fromFeet, toFeet, out var position, out var rotation);
        assistanceView.ShowPreview(position, rotation, playerAllowed && chairAllowed);
    }

    public void HideTeleportPreview() => assistanceView?.HidePreview();
    private void OnDisable()
    {
        pendingHand = null;
        RestorePlayerClearance();
        assistanceView?.Hide();
    }
}
