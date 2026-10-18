using UnityEngine;
using Valve.VR;

[DefaultExecutionOrder(30)]
public sealed class CampusHandInteractor : MonoBehaviour
{
    [SerializeField] private SteamVR_Input_Sources inputSource;
    [SerializeField] private float contactDistance = .12f;
    [SerializeField] private float doorContactDistance = .50f;
    [SerializeField] private float pickupDistance=.20f;
    private SteamVR_Action_Boolean trigger;
    private SteamVR_Action_Vibration vibration;
    private ResolutionCoordinator resolution;
    private MonoBehaviour candidate, held;
    private CampusContactHighlight highlight;
    private float nextPulse, lastAngle;
    private bool usedThisGrip;
    private bool previousGrip;
    private CampusThrowMotion throwMotion;
    private readonly Collider[] hits = new Collider[64];
    public SteamVR_Input_Sources InputSource => inputSource;
    public Vector3 ContactPoint => GetComponent<CampusHandVisual>()?.ContactPoint ?? transform.position;
    public event System.Action<float> HapticRequested;
    public void ConfigureSource(SteamVR_Input_Sources source) { inputSource = source; }
    private void Awake()
    { trigger = SteamVR_Input.GetBooleanAction("GrabPinch"); vibration = SteamVR_Input.GetVibrationAction("Haptic"); resolution = FindObjectOfType<ResolutionCoordinator>(); throwMotion=GetComponent<CampusThrowMotion>() ?? gameObject.AddComponent<CampusThrowMotion>(); }
    private void Update()
    {
        bool blocked = Time.timeScale == 0 || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None);
        RefreshContact(blocked || (CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.WorldInteractionLocked));
        bool grip=CampusControllerInput.Trigger(inputSource), down=grip && !previousGrip, up=!grip && previousGrip;
        previousGrip=grip;
        ProcessGrip(down, grip, up);
    }
    public void ProcessGrip(bool down, bool grip, bool up)
    {
        bool blocked = Time.timeScale == 0 || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None);
        if ((blocked || (CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.WorldInteractionLocked)) && !(candidate is VRMenuButton))
        {
            var lesson=CampusLessonDirector.Instance;
            // Keep the just-collected towel held while task 2 unfolds. Releases still work.
            if(!blocked && lesson!=null && lesson.CurrentPhase==CampusLessonDirector.Phase.WetTowel && lesson.TaskAnnouncement && held==lesson.TargetTowel && grip && !up)
            { ((IInteractable)held).OnInteract(transform); return; }
            Release(); return;
        }
        if (down || !grip) usedThisGrip = false;
        // A held trigger may arrive at a door after the press; buttons still need an explicit press.
        if (!usedThisGrip && candidate != null && (down || (grip && (candidate is CampusHingeInteractable || candidate is CampusTowelInteractable || candidate is CampusPracticeBook)))) usedThisGrip = Begin(candidate);
        if (held != null && grip)
        {
            ((IInteractable)held).OnInteract(transform);
            var hinge = held as CampusHingeInteractable;
            if (hinge != null && Mathf.Abs(hinge.Angle - lastAngle) > 2 && Time.unscaledTime >= nextPulse)
            { Pulse(.16f); lastAngle = hinge.Angle; nextPulse = Time.unscaledTime + .08f; }
        }
        if (up || !grip) Release(up);
        GetComponent<DummyController>()?.SetContactGesture(grip, held is CampusHingeInteractable, held is CampusElevatorButton || held is CampusFaucet);
    }
    public void RefreshContact(bool menuOnly = false)
    {
        if (held != null) return;
        // Paused physics has no FixedUpdate; synchronise moving head-following menu colliders.
        if(menuOnly || Time.timeScale==0) Physics.SyncTransforms();
        Vector3 point=ContactPoint;

        float searchRadius =Mathf.Max(contactDistance, pickupDistance, doorContactDistance);
        int count = Physics.OverlapSphereNonAlloc(point, searchRadius, hits, ~0, QueryTriggerInteraction.Collide);
        MonoBehaviour best = null; float distance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null || hits[i].transform.IsChildOf(transform)) continue;
            foreach (var component in hits[i].GetComponentsInParent<MonoBehaviour>())
            {
                if (!(component is IInteractable) || !component.isActiveAndEnabled || !CampusTeamRuntime.TargetAvailable(component) || (menuOnly && !(component is VRMenuButton))) continue;
                if (component is CampusHingeInteractable hinge && hinge.IsHeld && !hinge.IsHeldBy(transform)) continue;
                if (component is CampusTowelInteractable towel && !towel.CanGrab(transform)) continue;
                if (component is CampusPracticeBook book && !book.CanGrab(transform)) continue;
                if (component is CampusRescueStudent student && !student.CanPush(transform)) continue;
                float d = Vector3.Distance(point, hits[i].ClosestPoint(point));

                //if(d>(component is CampusTowelInteractable || component is CampusPracticeBook?pickupDistance:contactDistance)) continue;
                float allowedDistance;

                if (component is CampusHingeInteractable)
                {
                    allowedDistance = doorContactDistance;
                }
                else if (component is CampusTowelInteractable ||
                         component is CampusPracticeBook)
                {
                    allowedDistance = pickupDistance;
                }
                else
                {
                    allowedDistance = contactDistance;
                }

                if (d > allowedDistance)
                    continue;

                // Prevent proximity volumes selecting doors before the hand reaches the actual leaf.
                if (hits[i].isTrigger && !(component is VRMenuButton)) continue;
                if (d < distance) { distance = d; best = component; } break;
            }
        }
        if (best == candidate) return;
        if (candidate != null) ((IInteractable)candidate).OnHoverExit();
        if (highlight != null) highlight.SetHover(this, false);
        candidate = best; highlight = null;
        if (candidate != null)
        {
            ((IInteractable)candidate).OnHoverEnter();
            highlight = candidate.GetComponent<CampusContactHighlight>() ?? candidate.gameObject.AddComponent<CampusContactHighlight>();
            highlight.SetHover(this, true);
        }
    }
    public bool Begin(MonoBehaviour target)
    {
        if (target == null || held != null || !(target is IInteractable) || !CampusTeamRuntime.TargetAvailable(target)) return false;
        if(CampusLessonDirector.Instance!=null && CampusLessonDirector.Instance.WorldInteractionLocked && !(target is VRMenuButton)) return false;
        if (target is CampusHingeInteractable hinge && hinge.IsHeld && !hinge.IsHeldBy(transform)) return false;
        if (target is CampusTowelInteractable towel && !towel.CanGrab(transform)) return false;
        if (target is CampusPracticeBook book && !book.CanGrab(transform)) return false;
        if (target is CampusRescueStudent student && !student.CanPush(transform)) return false;
        // Wheelchair handles are click switches; release must not stop following.
        if (target is CampusRescueStudent)
        {
            ((IInteractable)target).OnInteractStart(transform);
            Pulse(.3f);
            return true;
        }
        throwMotion?.ResetHistory(); held = target; ((IInteractable)held).OnInteractStart(transform);
        if((target is CampusTowelInteractable || target is CampusPracticeBook) && highlight!=null) highlight.SetHover(this,false);
        lastAngle = target is CampusHingeInteractable h ? h.Angle : 0; Pulse(.3f); return true;
    }
    public void Release(bool allowThrow=false)
    {
        if (held != null)
        {
            if(throwMotion!=null) throwMotion.ReleaseAllowed=allowThrow;
            bool towel=held is CampusTowelInteractable || held is CampusPracticeBook;
            ((IInteractable)held).OnInteractEnd(transform);
            if(towel)
            {
                Pulse(.22f); if(highlight!=null) highlight.SetHover(this,false);
                candidate=null; highlight=null;
            }
        }
        held = null;
        if(throwMotion!=null) throwMotion.ReleaseAllowed=false;
    }
    private void Pulse(float amplitude) { HapticRequested?.Invoke(amplitude); CampusControllerInput.Haptic(inputSource,amplitude); }
    private void OnDisable() { Release(); if (highlight != null) highlight.SetHover(this, false); candidate = null; highlight = null; }
}
