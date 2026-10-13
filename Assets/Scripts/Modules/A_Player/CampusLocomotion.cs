using UnityEngine;
using Valve.VR;

[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(CharacterController))]
public sealed class CampusLocomotion : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField, Min(.1f)] private float walkingSpeed = 1.8f;
    [SerializeField, Min(.1f)] private float acceleration = 6f;
    [SerializeField] private CampusComfortVignette comfortVignette;
    [SerializeField] private AudioSource footsteps;
    [SerializeField] private AudioClip[] footstepClips;
    private CharacterController body;
    private SteamVR_Action_Vector2 move;
    private float verticalSpeed, stepDistance, currentSpeed;
    private Vector3 walkingForward;
    private bool headingLatched;
    private int nextClip;
    private ResolutionCoordinator resolution;
    private bool scriptedLock,bodyWasEnabled;
    public CharacterController Body => body;
    public bool ScriptedLocked => scriptedLock;
    public void SetScriptedLock(bool value)
    {
        if(body==null) body=GetComponent<CharacterController>();
        if(scriptedLock==value) return;
        scriptedLock=value; currentSpeed=verticalSpeed=0; headingLatched=false; comfortVignette?.ResetMotion();
        if(value) { bodyWasEnabled=body.enabled; body.enabled=false; }
        else body.enabled=bodyWasEnabled;
    }
    private void Awake()
    {
        body = GetComponent<CharacterController>(); resolution = FindObjectOfType<ResolutionCoordinator>();
        move = SteamVR_Input.GetVector2Action("Walk");
    }
    private void Update()
    {
        Vector2 walking = CampusControllerInput.Walk();
#if UNITY_EDITOR
        if(!CampusControllerInput.HardwarePresent)
        {
            // Hardware-free layout preview only; hand interaction still requires controllers.
            walking = new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
        }
#endif
        Step(walking, 0, Time.deltaTime);
    }
    public void Step(Vector2 input, float turnInput, float dt)
    {
        // Keep the test/legacy signature; right-stick input is deliberately ignored.
        if (scriptedLock || trackedHead == null || body == null || !body.enabled) return;
        if (Time.timeScale == 0 || (CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.MovementLocked) || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None))
        { currentSpeed = 0; headingLatched = false; comfortVignette?.ResetMotion(); return; }
        if (dt <= 0) return;
        Vector3 local = transform.InverseTransformPoint(trackedHead.position);
        body.height = Mathf.Clamp(local.y, 1, 2.15f); body.center = new Vector3(local.x, body.height * .5f, local.z);
        if (input.magnitude < .18f) input = Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1);
        if (input == Vector2.zero) { headingLatched = false; currentSpeed = 0; }
        else
        {
            // Looking around during a walk does not unexpectedly steer the player.
            if (!headingLatched)
            {
                walkingForward = Vector3.ProjectOnPlane(trackedHead.forward, Vector3.up).normalized;
                if (walkingForward.sqrMagnitude < .01f) walkingForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                headingLatched = true;
            }
            currentSpeed = Mathf.MoveTowards(currentSpeed, walkingSpeed * input.magnitude, acceleration * dt);
        }
        Vector3 right = Vector3.Cross(Vector3.up, walkingForward);
        Vector3 horizontal = (walkingForward * input.y + right * input.x).normalized * currentSpeed;
        verticalSpeed = body.isGrounded && verticalSpeed < 0 ? -2 : Mathf.Max(-12, verticalSpeed - 15 * dt);
        Vector3 before = transform.position;
        body.Move((horizontal + Vector3.up * verticalSpeed) * dt);
        float travelled = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
        comfortVignette?.ReportMotion(Mathf.Clamp01(travelled / dt / walkingSpeed), false);
        if (input.sqrMagnitude > 0 && body.isGrounded) stepDistance += travelled;
        if (stepDistance > .65f && footsteps != null && footstepClips != null && footstepClips.Length > 0)
        { stepDistance = 0; footsteps.pitch = Random.Range(.94f, 1.06f); footsteps.PlayOneShot(footstepClips[nextClip++ % footstepClips.Length], .7f); }
        if (input == Vector2.zero) stepDistance = 0;
    }
    private void OnDisable() { currentSpeed = 0; headingLatched = false; comfortVignette?.ResetMotion(); }
}
