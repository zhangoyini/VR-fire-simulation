using Harbourview.Journey;
using UnityEngine;
using Valve.VR;

// One short trigger press at each rear handle toggles following.
[DefaultExecutionOrder(31), RequireComponent(typeof(BoxCollider))]
public sealed class CampusRescueStudent : MonoBehaviour, IInteractable
{
    public enum State { Waiting, Following, Delivered }
    public static bool Rescued { get; private set; }

    [SerializeField] private Transform followTarget;
    [SerializeField] private Collider refugeVolume;
    [SerializeField, Min(.5f)] private float followSpeed = 2.2f;
    [SerializeField, Min(.8f)] private float followOffset = 1.05f;
    [SerializeField, Min(.2f)] private float dwellSeconds = 1f;
    [SerializeField] private string pushHint =
        "双手靠近轮椅后方左右推把，同时各按一次扳机开始推行\nPress both triggers once at the rear handles to start";
    [SerializeField] private string pushText =
        "轮椅正在前方跟随；再次双手点击推把可停止\nWheelchair follows ahead; press both handles again to stop";
    [SerializeField] private string stopText =
        "已停止推行；双手再点击推把可继续\nStopped; press both handles again to continue";
    [SerializeField] private string doneText =
        "同学已安全送达避难区\nStudent delivered to the 2F refuge";

    public State Current { get; private set; } = State.Waiting;
    public bool IsFollowing => Current == State.Following && !Blocked;

    private ResolutionCoordinator resolution;
    private Transform pendingHand;
    private BoxCollider bodyCollider;
    private float pendingAt;
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
    }

    private bool Blocked => Time.timeScale == 0f ||
        (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None) ||
        (CampusLessonDirector.Instance != null && !CampusLessonDirector.Instance.ExerciseStarted);

    public void OnHoverEnter()
    {
        if (!Blocked && Current != State.Delivered)
            CampusMessagePresenter.Instance?.Show(Current == State.Waiting ? pushHint : pushText);
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
        return Vector3.Distance(transform.InverseTransformPoint(hand.position),
            new Vector3(side * .22f, .96f, -.30f)) <= .32f;
    }

    public void OnInteractStart(Transform hand)
    {
        if (!CanPush(hand)) return;
        if (pendingHand != null && pendingHand != hand && Time.unscaledTime - pendingAt <= .5f)
        {
            pendingHand = null;
            if (Current == State.Waiting)
            {
                Current = State.Following;
                dwell = 0f;
                if (!started) { started = true; SessionLogger.Write("RESCUE_STARTED"); }
                CampusMessagePresenter.Instance?.Show(pushText);
            }
            else
            {
                Current = State.Waiting;
                dwell = 0f;
                CampusMessagePresenter.Instance?.Show(stopText);
            }
        }
        else { pendingHand = hand; pendingAt = Time.unscaledTime; }
    }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { }

    private Vector3 Forward
    {
        get
        {
            Vector3 forward = followTarget != null ? followTarget.forward : transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > .01f ? forward.normalized : transform.forward;
        }
    }

    public bool TryGetFollowPosition(Vector3 fromFeet, Vector3 toFeet, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;
        if (!IsFollowing || followTarget == null || Mathf.Abs(fromFeet.y - spawnY) > .2f ||
            Mathf.Abs(toFeet.y - spawnY) > .2f) return false;
        Vector3 forward = Forward;
        position = toFeet + forward * followOffset;
        position.y = spawnY;
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        return CanPlace(position, rotation);
    }

    private bool CanPlace(Vector3 position, Quaternion rotation)
    {
        bool floor = false;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * .25f, Vector3.down,
            rayHits, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (rayHits[i].collider.GetComponentInParent<TeleportArea>() != null &&
                Mathf.Abs(rayHits[i].point.y - spawnY) <= .12f) { floor = true; break; }
        if (!floor) return false;

        Vector3 half = new Vector3(.44f, .58f, .55f);
        Vector3 travel = position - transform.position;
        float length = travel.magnitude;
        if (length > .001f)
        {
            count = Physics.BoxCastNonAlloc(transform.position + Vector3.up * .69f, half,
                travel / length, rayHits, transform.rotation, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == rayHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!IgnoreCollision(rayHits[i].collider)) return false;
        }
        count = Physics.OverlapBoxNonAlloc(position + Vector3.up * .69f, half, overlaps, rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++)
            if (!IgnoreCollision(overlaps[i])) return false;
        return true;
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
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (!IsFollowing || followTarget == null) return;
        Vector3 desired = followTarget.position + Forward * followOffset;
        desired.y = spawnY;
        Vector3 next = Vector3.MoveTowards(transform.position, desired, followSpeed * Time.deltaTime);
        Quaternion facing = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(Forward, Vector3.up), 180f * Time.deltaTime);
        if (((next - transform.position).sqrMagnitude > .000025f ||
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
        pendingHand = null;
        Rescued = true;
        dwell = 0f;
        SessionLogger.Write("RESCUE_COMPLETED");
        CampusMessagePresenter.Instance?.Show(doneText);
    }

    public void ResetQuest()
    {
        Current = State.Waiting;
        Rescued = false;
        pendingHand = null;
        started = false;
        dwell = 0f;
    }
}
