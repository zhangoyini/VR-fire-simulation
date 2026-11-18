using Harbourview.Journey;
using UnityEngine;
using System.Collections.Generic;

// Talk first, then roll along the player's committed teleport route.
[DefaultExecutionOrder(31), RequireComponent(typeof(BoxCollider))]
public sealed class CampusRescueStudent : MonoBehaviour, IInteractable
{
    public enum State { Waiting, Following, Delivered }
    public static bool Rescued { get; private set; }

    [SerializeField] private Transform followTarget;
    [SerializeField] private Collider refugeVolume;
    [SerializeField, Min(.5f)] private float followSpeed = 2.2f;
    [SerializeField, Range(.8f, 1.5f)] private float stoppingDistance = 1.1f;
    [Header("Following Doorway Clearance / 跟随模式门口容差")]
    [Tooltip("Clearance forgiveness per side in metres. 0.05 allows 5 cm.")]
    [SerializeField, Range(0f, .1f)] private float doorwayTolerance = .05f;
    [SerializeField, Min(.2f)] private float dwellSeconds = 1f;

    public State Current { get; private set; } = State.Waiting;
    internal bool AssistanceStarted => started;
    public bool IsFollowing => isActiveAndEnabled && Current == State.Following && !Blocked;
    public float PlayerClearanceRadius => .2f - Mathf.Clamp(doorwayTolerance, 0, .1f);

    private ResolutionCoordinator resolution;
    private BoxCollider bodyCollider;
    private CampusRescueDialogue dialogue;
    private CharacterController assistingBody;
    private float originalBodyRadius;
    private readonly List<Vector3> destinations = new List<Vector3>();
    public bool DialogueVisible => dialogue?.Visible ?? false;
    private CampusRescueAssistanceView assistanceView;
    [SerializeField] private Vector3[] assistanceRoute = {
        new Vector3(-2.2f, 3.8f, 0),
        new Vector3(-2.2f, 3.8f, -3.4f),
        new Vector3(-5, 3.8f, -6)
    };
    private float dwell;
    private float spawnY;
    private bool started;
    private float blockedSeconds;
    private bool recoveryAttempted, reversing;
    private Vector3 reverseTarget;
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

    private bool InTalkingRange => followTarget != null &&
        Mathf.Abs(followTarget.position.y - spawnY - 1.3f) < 1.3f &&
        Vector3.ProjectOnPlane(followTarget.position - transform.position, Vector3.up).sqrMagnitude < 2.5f * 2.5f;

    private bool CanSeeClassmate()
    {
        if (!InTalkingRange) return false;
        Vector3 offset = transform.position + Vector3.up * 1.2f - followTarget.position;
        if (Vector3.Angle(followTarget.forward, offset) > 60f) return false;
        return !Physics.Raycast(followTarget.position, offset.normalized, out var hit,
            offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    public void OnHoverEnter() { if (!Blocked && CanSeeClassmate()) OpenDialogue(); }
    public void OnHoverExit() { }
    // Shared hand interaction uses this entry point; handles no longer gate the task.
    public bool CanPush(Transform hand) => hand != null && !Blocked && Current == State.Waiting && InTalkingRange;
    public void OnInteractStart(Transform hand) { if (CanPush(hand)) OpenDialogue(); }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { }

    private void OpenDialogue()
    {
        if (Current != State.Waiting || Blocked || followTarget == null) return;
        if (dialogue == null) dialogue = new CampusRescueDialogue(followTarget, ReplyFollowMe);
        dialogue.Show(true);
    }

    public void ReplyFollowMe()
    {
        if (Blocked || Current != State.Waiting || !DialogueVisible || !InTalkingRange) return;
        Current = State.Following;
        ApplyPlayerClearance();
        dialogue.Show(false);
        destinations.Clear();
        ClearRecovery();
        dwell = 0;
        started = true;
        SessionLogger.Write("RESCUE_STARTED");
        if (assistanceView == null)
            assistanceView = gameObject.AddComponent<CampusRescueAssistanceView>();
        assistanceView.Begin(followTarget, assistanceRoute);
    }

    public bool TryGetFollowPosition(Vector3 fromFeet, Vector3 toFeet, out Vector3 position, out Quaternion rotation)
    {
        position = toFeet;
        position.y = spawnY;
        rotation = transform.rotation;
        if (!IsFollowing || followTarget == null || Mathf.Abs(fromFeet.y - spawnY) > .2f ||
            Mathf.Abs(toFeet.y - spawnY) > .2f) return false;
        // Validate from the last queued corner so rapid teleports cannot cut across walls.
        Vector3 from = destinations.Count > 0 ? destinations[destinations.Count - 1] : transform.position;
        // A player who backtracks can lead a blocked chair out of its old route.
        if ((reversing || recoveryAttempted || blockedSeconds > 0) &&
            TryPlanTravel(transform.position, position, transform.rotation, out rotation)) return true;
        return TryPlanTravel(from, position, transform.rotation, out rotation);
    }

    private Quaternion TravelFacing(Vector3 travel, Quaternion current)
    {
        if (travel.sqrMagnitude < .0001f) return current;
        // Normal following always faces the destination. Only blocked recovery may reverse.
        return Quaternion.LookRotation(travel, Vector3.up);
    }

    private bool TryPlanTravel(Vector3 from, Vector3 to, Quaternion current, out Quaternion facing)
    {
        facing = TravelFacing(to - from, current);
        return CanTravel(from, to, facing);
    }

    private bool CanTurnAt(Vector3 position, Quaternion from, Quaternion to)
    {
        int turns = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(from, to) / 5f));
        for (int i = 0; i <= turns; i++)
            if (!HasClearBody(position, Quaternion.Slerp(from, to, (float)i / turns))) return false;
        return true;
    }

    private bool TryPlanLeg(Vector3 from, Vector3 to, Quaternion current, out Quaternion facing) =>
        TryPlanTravel(from, to, current, out facing) && CanTurnAt(from, current, facing);

    private void ClearRecovery()
    {
        blockedSeconds = 0;
        recoveryAttempted = reversing = false;
    }

    private void BeginReverseRecovery(Vector3 desired)
    {
        recoveryAttempted = true;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 fallback = transform.position;
        // Search only a small local manoeuvre. Every leg and turn must clear solid geometry.
        foreach (float back in new[] { .6f, 1f })
        {
            Vector3 behind = transform.position - forward * back;
            if (!CanTravel(transform.position, behind, transform.rotation)) continue;
            if (fallback == transform.position) fallback = behind;
            foreach (float width in new[] { .8f, 1.15f, 1.5f })
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 beside = behind + right * width * side;
                Vector3 past = desired + right * width * side;
                if (!TryPlanLeg(behind, beside, transform.rotation, out var first) ||
                    !TryPlanLeg(beside, past, first, out var second) ||
                    !TryPlanLeg(past, desired, second, out _)) continue;
                destinations.Insert(0, past);
                destinations.Insert(0, beside);
                reverseTarget = behind;
                reversing = true;
                return;
            }
        }
        // If no detour fits, back into clear space once and let the player choose another route.
        // Do not oscillate endlessly against the same obstacle.
        if (fallback != transform.position)
        {
            reverseTarget = fallback;
            reversing = true;
        }
    }

    private bool HasFloor(Vector3 position)
    {
        int count = Physics.RaycastNonAlloc(position + Vector3.up * .25f, Vector3.down,
            rayHits, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (rayHits[i].collider.GetComponentInParent<TeleportArea>() is TeleportArea area &&
                area.isActiveAndEnabled && Mathf.Abs(rayHits[i].point.y - spawnY) <= .12f) return true;
        return false;
    }

    private bool CanTravel(Vector3 from, Vector3 to, Quaternion rotation)
    {
        Vector3 travel = to - from;
        float length = travel.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / .2f));
        for (int i = 0; i <= steps; i++)
            if (!HasFloor(Vector3.Lerp(from, to, (float)i / steps))) return false;
        if (!HasClearBody(from, rotation) || !HasClearBody(to, rotation)) return false;
        if (length < .001f) return true;
        int count = Physics.BoxCastNonAlloc(from + Vector3.up * .69f, WheelchairClearanceHalfSize,
            travel / length, rayHits, rotation, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == rayHits.Length) return false;
        for (int i = 0; i < count; i++) if (!IgnoreCollision(rayHits[i].collider)) return false;
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
        if (assistingBody != null) assistingBody.radius = Mathf.Min(originalBodyRadius, PlayerClearanceRadius);
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

    // Called after a successful player teleport. Never relocates the chair instantly.
    public void FollowTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsFollowing) return;
        position.y = spawnY;
        if ((reversing || recoveryAttempted || blockedSeconds > 0) &&
            TryPlanTravel(transform.position, position, transform.rotation, out _))
        {
            destinations.Clear();
            ClearRecovery();
        }
        recoveryAttempted = false;
        if (destinations.Count == 0 || Vector3.Distance(destinations[destinations.Count - 1], position) > .05f)
            destinations.Add(position);
    }

    private void Update()
    {
        if (Current == State.Waiting)
        {
            if (!Blocked && CanSeeClassmate()) OpenDialogue();
            else dialogue?.Show(false);
        }
        if (!IsFollowing || followTarget == null) { assistanceView?.SetVisible(false); return; }
        ApplyPlayerClearance();
        assistanceView?.SetVisible(true);
        TickFollowing(Time.deltaTime);
    }

    // Deterministic time step for hardware-free movement checks.
    public void TickFollowing(float dt)
    {
        if (!IsFollowing || followTarget == null || dt <= 0) return;
        if (reversing)
        {
            Vector3 next = Vector3.MoveTowards(transform.position, reverseTarget, Mathf.Min(followSpeed, 1f) * dt);
            if (CanTravel(transform.position, next, transform.rotation))
            {
                transform.position = next;
                Physics.SyncTransforms();
            }
            else reversing = false;
            if (Vector3.Distance(transform.position, reverseTarget) < .02f) reversing = false;
            blockedSeconds = 0;
            return;
        }
        if (destinations.Count > 0)
        {
            Vector3 desired = destinations[0];
            Vector3 travel = desired - transform.position;
            float distance = travel.magnitude;
            float gap = destinations.Count == 1 ? stoppingDistance : 0f;
            if (distance > gap + .01f)
            {
                Quaternion targetFacing = TravelFacing(travel, transform.rotation);
                Quaternion facing = Quaternion.RotateTowards(transform.rotation, targetFacing, 180f * dt);
                bool canTurn = CanTurnAt(transform.position, transform.rotation, facing);
                bool turning = Quaternion.Angle(transform.rotation, facing) > .01f;
                if (canTurn && turning)
                {
                    transform.rotation = facing;
                    Physics.SyncTransforms();
                    blockedSeconds = 0;
                }
                Vector3 next = Vector3.MoveTowards(transform.position, desired,
                    Mathf.Min(followSpeed * dt, distance - gap));
                // Turn toward the player before rolling, so a destination behind the chair
                // cannot cause ordinary following to look like backwards driving.
                if (canTurn && Quaternion.Angle(facing, targetFacing) < 5f && CanTravel(transform.position, next, facing))
                {
                    transform.SetPositionAndRotation(next, facing);
                    Physics.SyncTransforms();
                    blockedSeconds = 0;
                }
                else if (!canTurn || !turning)
                {
                    blockedSeconds += dt;
                    if (blockedSeconds >= .35f && !recoveryAttempted) BeginReverseRecovery(desired);
                }
            }
            if (destinations.Count > 1 && Vector3.Distance(transform.position, desired) < .02f)
                destinations.RemoveAt(0);
        }
        if (refugeVolume != null && refugeVolume.bounds.Contains(transform.position + Vector3.up * .7f))
        {
            dwell += dt;
            if (dwell >= dwellSeconds) Deliver();
        }
        else dwell = 0;
    }

    private void Deliver()
    {
        Current = State.Delivered;
        ClearRecovery();
        RestorePlayerClearance();
        destinations.Clear();
        assistanceView?.Hide();
        Rescued = true;
        dwell = 0;
        SessionLogger.Write("RESCUE_DELIVERED");
        CampusLessonDirector.Instance?.StudentDelivered();
        CampusMessagePresenter.Instance?.Show("同学已安全到达避难区，救援完成。\nClassmate reached the refuge safely. Rescue complete.");
    }

    public void ResetQuest()
    {
        Current = State.Waiting;
        ClearRecovery();
        RestorePlayerClearance();
        Rescued = false;
        dialogue?.Show(false);
        assistanceView?.Hide();
        destinations.Clear();
        started = false;
        dwell = 0;
    }

    private void OnDisable() { RestorePlayerClearance(); dialogue?.Show(false); assistanceView?.Hide(); }
    private void OnDestroy() => dialogue?.Dispose();
}
