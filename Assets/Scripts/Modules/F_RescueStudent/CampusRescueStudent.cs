using Harbourview.Journey;
using UnityEngine;

// Optional side quest (plan B): press the trigger near the student to signal/greet,
// then lead; the student follows the player to the 2F refuge. This records and reports
// only — it never gates the refuge intercom or the victory outcome.
[RequireComponent(typeof(BoxCollider))]
public sealed class CampusRescueStudent : MonoBehaviour, IInteractable
{
    public enum State { Waiting, Following, Delivered }

    public static bool Rescued { get; private set; }

    [SerializeField] private Transform followTarget;
    [SerializeField] private Collider refugeVolume;
    [SerializeField, Min(0.5f)] private float followSpeed = 1.4f;
    [SerializeField, Min(0.3f)] private float stopDistance = 0.8f;
    [SerializeField, Min(0.3f)] private float followOffset = 0.75f;
    [SerializeField, Min(0.2f)] private float dwellSeconds = 1f;
    [SerializeField] private string greetHint =
        "按扳机打招呼，带他去二层避难区\nPress trigger to signal; lead them to the 2F refuge";
    [SerializeField] private string leadText =
        "已接应，跟我走，前往二层避难区\nFollow me to the 2F refuge";
    [SerializeField] private string doneText =
        "同学已安全送达避难区\nStudent delivered to the 2F refuge";

    public State Current { get; private set; } = State.Waiting;

    private ResolutionCoordinator resolution;
    private float dwell;
    private float spawnY;
    private bool greeted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Rescued = false;

    private void Awake()
    {
        spawnY = transform.position.y;
        resolution = FindObjectOfType<ResolutionCoordinator>();

        if (followTarget == null)
        {
            var loco = FindObjectOfType<CampusLocomotion>();
            if (loco != null) followTarget = loco.transform;
        }

        if (refugeVolume == null)
        {
            var zone = GameObject.Find("2F Refuge Occupancy");
            if (zone != null) refugeVolume = zone.GetComponent<Collider>();
        }

        // The hand interactor only reads non-trigger contact colliders.
        var col = GetComponent<BoxCollider>();
        col.isTrigger = false;
        if (col.size == Vector3.one) col.size = new Vector3(0.78f, 1.34f, 1.05f);
        if (col.center == Vector3.zero) col.center = new Vector3(0f, 0.67f, 0f);
    }

    private bool Blocked =>
        Time.timeScale == 0f
        || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None)
        || (CampusLessonDirector.Instance != null && !CampusLessonDirector.Instance.ExerciseStarted);

    public void OnHoverEnter()
    {
        if (Current == State.Waiting && !Blocked && !greeted)
            CampusMessagePresenter.Instance?.Show(greetHint);
    }

    public void OnHoverExit() { }

    public void OnInteractStart(Transform hand)
    {
        if (Current != State.Waiting || Blocked) return;
        greeted = true;
        Current = State.Following;
        dwell = 0f;
        SessionLogger.Write("RESCUE_STARTED");
        CampusMessagePresenter.Instance?.Show(leadText);
    }

    public void OnInteract(Transform hand) { }

    public void OnInteractEnd(Transform hand) { }

    private void Update()
    {
        if (Current != State.Following || Blocked) return;

        if (followTarget != null)
        {
            Vector3 dest = followTarget.position - followTarget.forward * followOffset;
            dest.y = spawnY;
            Vector3 to = dest - transform.position;
            to.y = 0f;
            if (to.magnitude > stopDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, followSpeed * Time.deltaTime);
                Quaternion wanted = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 6f * Time.deltaTime);
            }
        }

        if (refugeVolume != null && refugeVolume.bounds.Contains(transform.position))
        {
            dwell += Time.deltaTime;
            if (dwell >= dwellSeconds) Deliver();
        }
        else
        {
            dwell = 0f;
        }
    }

    private void Deliver()
    {
        Current = State.Delivered;
        Rescued = true;
        dwell = 0f;
        SessionLogger.Write("RESCUE_COMPLETED");
        CampusMessagePresenter.Instance?.Show(doneText);
    }

    // Used by restart / test harnesses.
    public void ResetQuest()
    {
        Current = State.Waiting;
        Rescued = false;
        greeted = false;
        dwell = 0f;
    }
}
