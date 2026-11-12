using Jiao.HealthTime;
using Harbourview.Journey;
using TMPro;
using UnityEngine;

// Scene-local chapter authority. Native head tracking stays live in every phase.
[DefaultExecutionOrder(-80)]
public sealed class CampusLessonDirector : MonoBehaviour
{
    public enum Phase { Controls, Ready, Classroom, Alarm, OpenDoor, FindTowel, WetTowel, Descend, Complete }
    public static CampusLessonDirector Instance { get; private set; }
    [SerializeField] private Transform trackedHead;
    [SerializeField] private CampusLocomotion locomotion;
    [SerializeField] private CampusHingeInteractable classroomDoor;
    [SerializeField] private Transform exitDoor;
    [SerializeField] private CampusSmokeSpread smokeSpread;
    [SerializeField] private GameObject fireOrigin, doorwayPlume;
    [SerializeField] private CampusSmokeVolume doorwayExposure;
    [SerializeField] private AudioSource alarm;
    [SerializeField] private CampusTowelInteractable targetTowel;
    [SerializeField] private Collider restroom;
    [SerializeField] private GameObject lessonPanel, gameplayHUD, minimap;
    [SerializeField] private TextMeshProUGUI title, explanation, footer;
    [SerializeField] private CampusLessonAnimation demonstration;
    [SerializeField] private CampusLessonRoute route;
    [SerializeField] private CampusDestinationHighlight destination;
    private const float ClassroomSeconds = 6.5f;
    private const float AlarmSeconds = 3.5f;
    private const float SeatedEyeHeight = 1.20f;
    private const float StandFadeStart = 2.40f;
    private const float StandBlackout = 2.75f;
    private const float ReadyReadingSeconds = 8f;
    [SerializeField] private Transform storyBook, storyLeftHand, storyRightHand, bookRest, storyPage, storyStandingPoint;
    [SerializeField] private Renderer[] trackedHandRenderers;
    [SerializeField] private UnityEngine.UI.Image fade;
    [SerializeField] private CampusPracticeBook[] practiceBooks;
    [SerializeField] private RuntimeFireWindow[] practiceWindows;
    [SerializeField] private Transform classroomBoard;
    [SerializeField] private GameObject classroomBoardSign;
    private ResolutionCoordinator resolution;
    private CampusLessonLayout layout;
    private CampusClassroomTutorial tutorial;
    private CampusNavigationPanels panels;
    private CampusExerciseTasks tasks;
    private CampusObjectiveHighlights highlights;
    private SystemUIPauseController pauseController;
    private float age, coverage;
    private Vector3 bookStart, bookEnd;
    private Quaternion bookRotation;
    private bool fireStarted, finishing, bypassed, loggingReady, storyPrepared;
    private Vector3 cinematicPosition;
    private Quaternion cinematicRotation;
    private float standingGroundY;
    private bool seatedView;
    private float confirmHeld;
    private bool confirmConsumed;
    [System.NonSerialized] public bool ManualTickForChecks;
    public Phase CurrentPhase { get; private set; } = Phase.Controls;
    internal CampusExerciseTasks ExerciseTasks => tasks;
    public float PhaseAge => age;
    public CampusTowelInteractable TargetTowel => targetTowel;
    public CampusHingeInteractable ClassroomDoor => classroomDoor;
    public Collider Restroom => restroom;
    public CampusSmokeVolume DoorwayExposure => doorwayExposure;
    public bool IsPractice => !bypassed && CurrentPhase<=Phase.Ready;
    public bool ExerciseStarted => bypassed || CurrentPhase>=Phase.OpenDoor;
    internal bool RequiresResumeButton => tutorial?.AwaitingMenuResume ?? false;
    public bool GuidedTowelTask => !bypassed && CurrentPhase>=Phase.FindTowel && CurrentPhase<=Phase.Descend;
    private bool CinematicLocked => CurrentPhase==Phase.Classroom || CurrentPhase==Phase.Alarm;
    public bool MovementLocked => !bypassed && (CinematicLocked || CurrentPhase==Phase.Controls && (tutorial==null || !tutorial.AllowsTeleport));
    public bool WorldInteractionLocked => !bypassed && (CinematicLocked || IsPractice);
    public bool AllowsHandTarget(MonoBehaviour target) =>
        target is VRMenuButton || !WorldInteractionLocked || (CurrentPhase==Phase.Controls && tutorial!=null &&
            (tutorial.AllowsTarget(target) || (tutorial.FreeClassroomPractice && IsClassroomPracticeTarget(target))));
    private bool IsClassroomPracticeTarget(MonoBehaviour target)
    {
        // The doorway remains part of the exercise handoff, outside free classroom practice.
        if(target==null || target==classroomDoor) return false;
        if(classroomBoard!=null && target.transform.IsChildOf(classroomBoard.parent)) return true;
        if(target is CampusPracticeBook book) return System.Array.IndexOf(practiceBooks,book)>=0;
        if(!(target is CampusHingeInteractable)) return false;
        // Runtime window hinges are scene-root objects, separate from the classroom hierarchy.
        foreach(var window in practiceWindows)
            if(window!=null && window.HighlightTarget!=null && window.HighlightTarget.IsChildOf(target.transform))
                return true;
        return false;
    }
    public bool ShowsTutorialContactMarker(MonoBehaviour target) => tutorial!=null && tutorial.ShowsContactMarker(target);
    public bool AllowsTutorialContact(MonoBehaviour target,Vector3 handPoint) => tutorial==null || tutorial.CanStartContact(target,handPoint);
    public bool AllowsTeleportLanding(Vector3 point) => !IsPractice || CampusClassroomTutorial.InRoom(point);
    public void RecordTutorialTeleport(Vector3 point) { if(CurrentPhase==Phase.Controls) tutorial?.RecordTeleport(point); }
    public bool DamageSuppressed => !ExerciseStarted;
    public bool SmokeFrozen => !bypassed && !fireStarted;
    public bool CanConfirm => !bypassed && !finishing && Time.timeScale>0 && CurrentPhase==Phase.Ready && age>=ReadyReadingSeconds;
    private void Awake()
    {
        Instance=this; resolution=FindObjectOfType<ResolutionCoordinator>(); layout=GetComponent<CampusLessonLayout>();
        if(fireOrigin!=null) fireOrigin.SetActive(false);
        if(doorwayPlume!=null) doorwayPlume.SetActive(false);
        alarm?.Stop(); ChangePhase(Phase.Controls);
    }
    private void Start()
    {
        // Do not lazy-create SessionLogger before the team's health host has Awoken.
        loggingReady=true; SessionLogger.Write("LESSON_CONTROLS");
        if (minimap != null)
        {
            panels = new CampusNavigationPanels(trackedHead, explanation.font, minimap);
            minimap.SetActive(false);
        }
        var student = FindObjectOfType<CampusRescueStudent>();
        tasks = new CampusExerciseTasks(exitDoor, student, panels);
        highlights = new CampusObjectiveHighlights(transform, trackedHead, explanation.font, classroomDoor, exitDoor, student);
        if(CurrentPhase!=Phase.Controls) return;
        RuntimeFireWindow practiceWindow=null;
        float closest=float.PositiveInfinity;
        foreach(var window in practiceWindows)
        {
            if(window==null || !window.UsesPhysicalHands || window.HighlightTarget==null) continue;
            float distance=Mathf.Abs(window.HighlightTarget.position.x-CampusClassroomTutorial.FirstSpot.x);
            if(distance>=closest) continue;
            closest=distance; practiceWindow=window;
        }
        if(practiceWindow==null || practiceBooks.Length<2 || practiceBooks[1]==null || minimap==null)
        {
            Debug.LogError("Classroom 401 tutorial requires a physical window, the practice books and the live minimap.",this);
            ChangePhase(Phase.Ready);
            return;
        }
        classroomDoor.SetAngle(0);
        Physics.SyncTransforms();
        tutorial=new CampusClassroomTutorial(trackedHead,locomotion.transform,practiceWindow.HighlightTarget,practiceBooks[1],
            new CampusTutorialView(transform,trackedHead,classroomBoard,classroomBoardSign,explanation.font,panels));
        pauseController=FindObjectOfType<SystemUIPauseController>();
        if(pauseController!=null)
        {
            pauseController.Opened+=OnPauseOpened;
            pauseController.ResumedFromMenu+=OnMenuResumed;
        }
    }
    private void OnPauseOpened() => tutorial?.OnPauseOpened();
    private void OnMenuResumed() => tutorial?.OnMenuResumed();
    private void Update()
    {
        if(bypassed || ManualTickForChecks) return;
        if(resolution!=null && resolution.CurrentOutcome!=ResolutionCoordinator.Outcome.None)
        { if(!finishing) { finishing=true; ClearTutorial(); panels?.Hide(false,false,fold:true); HideObjectiveHighlights(); RestoreStandingHeight(); locomotion.SetScriptedLock(false); SetHandsVisible(true); SetStoryHands(false); lessonPanel.SetActive(false); route.Hide(); SetFade(0); } return; }
        if(Time.timeScale==0)
        {
            bool x = CampusControllerInput.TutorialButton(false), y = CampusControllerInput.TutorialButton(true);
            tutorial?.Pause(x,y);
            panels?.Hide(x,y);
            HideObjectiveHighlights();
            lessonPanel.SetActive(false); SetFade(0);
            if(alarm!=null && fireStarted) alarm.Pause();
            return;
        }
        if(alarm!=null && fireStarted && !alarm.isPlaying) alarm.UnPause();
        Tick(Time.deltaTime);
        SubmitConfirmInput(CampusControllerInput.Confirm(),Time.deltaTime);
        // No trigger or contact button can start/advance this chapter.
#if UNITY_EDITOR
        if(!CampusControllerInput.HardwarePresent && Input.GetKeyDown(KeyCode.Return)) Confirm();
        if(!CampusControllerInput.HardwarePresent && Input.GetKeyDown(KeyCode.Backspace)) SkipControls();
#endif
    }
    public void Tick(float dt) => Tick(dt,
        CampusControllerInput.TutorialButton(false), CampusControllerInput.TutorialButton(true));
    internal void Tick(float dt,bool x,bool y)
    {
        if(bypassed || Time.timeScale==0 || dt<=0 || finishing) return;
        age+=dt;
        if(CurrentPhase==Phase.Controls)
        {
            if(tutorial==null) return;
            bool windowDemo=tutorial.ShowingWindowDemo;
            if(tutorial.Tick(dt,x,y))
            { BeginStory(); return; }
            if(!windowDemo && tutorial.ShowingWindowDemo)
            {
                SetText("","","");
                layout.Arrange(true);
                demonstration.ShowWindowDemo();
            }
            UpdatePresentationVisibility();
            if(tutorial.ShowingWindowDemo)
            { layout.SetReadingProgress(0); demonstration.Tick(windowDemo?dt:0); layout.Tick(windowDemo?dt:0); }
            return;
        }
        if(fireStarted && CurrentPhase>=Phase.OpenDoor && CurrentPhase<Phase.Complete) UpdateDoorwaySmoke();
        if(CurrentPhase==Phase.Classroom) TickStory();
        else if(CurrentPhase==Phase.Alarm) TickAlarm();
        else if(CurrentPhase==Phase.OpenDoor)
        {
            if(Mathf.Abs(classroomDoor.Angle)>=15)
            {
                CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.SmokeRules);
                ChangePhase(Phase.FindTowel);
            }
        }
        else if(CurrentPhase==Phase.FindTowel)
        {
            if(restroom.bounds.Contains(trackedHead.position) && targetTowel.IsHeld) ChangePhase(Phase.WetTowel);
        }
        else if(CurrentPhase==Phase.WetTowel)
        {
            coverage=targetTowel.CoversFace(trackedHead)?coverage+dt:0;
            if(coverage>=1.5f) ChangePhase(Phase.Descend);
        }
        else if(CurrentPhase==Phase.Descend)
        {
            int floor=Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y-.8f)/3.8f)+1,1,4);
            bool covered=targetTowel.CoversFace(trackedHead);
            if(floor==3 && covered) { coverage+=dt; if(coverage>=1.5f) ChangePhase(Phase.Complete); } else coverage=0;
        }
        if (ExerciseStarted && !WorldInteractionLocked)
        {
            Vector3 feet = trackedHead.position;
            feet.y = locomotion.transform.position.y;
            if (tasks != null)
            {
                tasks.Tick(feet);
                panels?.ShowExercise(x, y, dt, tasks.Details(CurrentPhase));
                UpdateExerciseGuidance();
                panels?.TickNotice(dt);
            }
        }
        else panels?.Hide(x, y, fold: true);
        UpdatePresentationVisibility();
        if (CurrentPhase == Phase.Ready) layout?.SetReadingProgress(age/ReadyReadingSeconds);
        route?.Tick(trackedHead); layout?.Tick(dt);
    }
    public void Confirm()
    {
        if(!CanConfirm) return;
        BeginStory();
    }
    public void SubmitConfirmInput(bool pressed,float seconds)
    {
        if(!pressed) { confirmHeld=0; confirmConsumed=false; return; }
        if(Time.timeScale==0 || !CanConfirm || confirmConsumed) { confirmHeld=0; return; }
        confirmHeld+=Mathf.Max(0,seconds);
        if(confirmHeld>=.75f)
        { confirmConsumed=true; Confirm(); }
    }
    public void SkipControls()
    {
        if(CurrentPhase!=Phase.Controls) return;
        ClearTutorial();
        ChangePhase(Phase.Ready);
    }
    private void ClearTutorial() { tutorial?.Dispose(); tutorial=null; }
    private void BeginStory()
    {
        ClearTutorial();
        foreach(var hand in FindObjectsOfType<CampusHandInteractor>(true)) hand.Release();
        foreach(var book in practiceBooks) if(book!=null) book.ResetBook();
        cinematicPosition=locomotion.transform.position; cinematicRotation=locomotion.transform.rotation;
        storyPrepared=false; SetHandsVisible(false); ChangePhase(Phase.Classroom);
    }
    private void TickStory()
    {
        SetFade(age<.4f ? age/.4f : 1f-Mathf.InverseLerp(.6f,1.2f,age));
        if(age>=.4f && !storyPrepared)
        {
            classroomDoor.SetAngle(0);
            Vector3 standingPoint=storyStandingPoint!=null?storyStandingPoint.position:locomotion.transform.position;
            // Establish a prescribed desk-facing view under the blackout, without
            // changing the sensor's local head pose or rotating a visible view.
            Vector3 deskDirection=Vector3.ProjectOnPlane(bookRest.position-standingPoint,Vector3.up).normalized;
            Vector3 headDirection=Vector3.ProjectOnPlane(trackedHead.forward,Vector3.up).normalized;
            float turn=Vector3.SignedAngle(headDirection,deskDirection,Vector3.up);
            locomotion.transform.Rotate(Vector3.up,turn,Space.World); cinematicRotation=locomotion.transform.rotation;
            // Measure the room-scale offset after turning so the tracked view stays over the seat.
            Vector3 headOffset = trackedHead.position - locomotion.transform.position;
            standingGroundY = standingPoint.y;
            float seatOffset = Mathf.Min(0f, SeatedEyeHeight - headOffset.y);
            headOffset.y = 0f;
            MoveRig(standingPoint - headOffset + Vector3.up * seatOffset);
            seatedView = seatOffset < 0f;
            Vector3 forward=deskDirection;
            if(forward.sqrMagnitude<.01f) forward=locomotion.transform.forward;
            bookStart=trackedHead.position+forward*.43f-Vector3.up*.32f;
            bookEnd=bookRest.position; bookRotation=Quaternion.LookRotation(forward,Vector3.up);
            storyBook.gameObject.SetActive(true); storyPrepared=true;
        }
        if(!storyPrepared) return;
        float put=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4f,6f,age));
        Vector3 position=Vector3.Lerp(bookStart,bookEnd,put);
        storyBook.SetPositionAndRotation(position,bookRotation*Quaternion.Euler(Mathf.Lerp(-18,0,put),0,0));
        if(storyPage!=null) storyPage.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Mathf.InverseLerp(2.2f,3.6f,age)*Mathf.PI)*110);
        PoseBookHands(position);
        if(age>=ClassroomSeconds) { SetFade(0); SetStoryHands(false); ChangePhase(Phase.Alarm); }
    }

    private void TickAlarm()
    {
        SetFade(age < StandBlackout ? Mathf.InverseLerp(StandFadeStart, StandBlackout, age) :
            1f - Mathf.InverseLerp(StandBlackout + .08f, AlarmSeconds - .10f, age));
        if (age >= StandBlackout) RestoreStandingHeight();
        if (age < AlarmSeconds) return;

        SetHandsVisible(true);
        FindObjectOfType<CampusExperience>()?.BeginTimedExercise();
        ChangePhase(Phase.OpenDoor);
    }

    private void RestoreStandingHeight()
    {
        if (!seatedView) return;
        seatedView = false;
        if (locomotion == null || locomotion.Body == null) return;
        Vector3 position = cinematicPosition;
        position.y = standingGroundY;
        MoveRig(position);
    }

    private void MoveRig(Vector3 p)
    { bool enabled=locomotion.Body.enabled; locomotion.Body.enabled=false; locomotion.transform.position=p; cinematicPosition=p; locomotion.Body.enabled=enabled; Physics.SyncTransforms(); }
    private void LateUpdate()
    {
        if(!CinematicLocked || bypassed || finishing) return;
        // Exclusive cinematic body pose prevents other movement writers and gravity drift.
        locomotion.transform.SetPositionAndRotation(cinematicPosition,cinematicRotation);
    }
    private void PoseBookHands(Vector3 book)
    {
        Vector3 right=bookRotation*Vector3.right, forward=bookRotation*Vector3.forward;
        foreach(var hand in new[]{storyLeftHand,storyRightHand}) if(hand!=null)
        { hand.gameObject.SetActive(true); hand.SetPositionAndRotation(book+right*(hand==storyLeftHand?-.16f:.16f)-forward*.055f-Vector3.up*.025f,bookRotation*Quaternion.Euler(-25,0,hand==storyLeftHand?170:10)); }
    }
    private void SetStoryHands(bool visible)
    { if(storyLeftHand!=null) storyLeftHand.gameObject.SetActive(visible); if(storyRightHand!=null) storyRightHand.gameObject.SetActive(visible); }
    private void SetHandsVisible(bool visible)
    {
        if(locomotion!=null) foreach(var hand in locomotion.GetComponentsInChildren<CampusHandVisual>(true)) hand.SetPresentationVisible(visible);
        if(trackedHandRenderers!=null) foreach(var renderer in trackedHandRenderers) if(renderer!=null) renderer.enabled=visible;
    }
    private void StartFire()
    {
        fireStarted=true; fireOrigin.SetActive(true); alarm?.Play();
        smokeSpread.ResetSpread(); smokeSpread.Advance(110);
        foreach(var stage in smokeSpread.Stages) if(stage.particles!=null) stage.particles.Simulate(5,true,false,true);
    }
    private void UpdateDoorwaySmoke()
    {
        bool on=Mathf.Abs(classroomDoor.Angle)>=15;
        if(doorwayPlume.activeSelf!=on)
        {
            doorwayPlume.SetActive(on);
            foreach(var particle in doorwayPlume.GetComponentsInChildren<ParticleSystem>())
                if(on) particle.Simulate(3,true,false,true); else particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        doorwayExposure.enabled=on;
    }
    private void ChangePhase(Phase phase)
    {
        CurrentPhase=phase; age=coverage=0;
        if(phase==Phase.Alarm) StartFire();
        if(phase==Phase.OpenDoor)
        {
            foreach(var endpoint in FindObjectsOfType<SafeZoneEndpoint>()) endpoint.ResetZone();
            tasks?.Begin();
        }
        if(CinematicLocked && phase!=Phase.Classroom)
        { cinematicPosition=locomotion.transform.position; cinematicRotation=locomotion.transform.rotation; }
        locomotion.SetScriptedLock(CinematicLocked);
        if(CinematicLocked)
            foreach(var hand in FindObjectsOfType<CampusHandInteractor>(true)) hand.Release();
        Present();
        ShowRoute();
        tasks?.TowelPhaseChanged(phase);
        if(loggingReady) SessionLogger.Write("LESSON_"+phase.ToString().ToUpperInvariant());
    }
    private void SetText(string heading,string text,string hint)
    { title.text=heading; explanation.text=text; footer.text=hint; }
    private void ShowRoute()
    {
        route.Hide();
        if(CurrentPhase==Phase.FindTowel)
            route.Show(new[]{new Vector3(-12.2f,11.4f,2),new Vector3(-12.2f,11.4f,0),new Vector3(-12.2f,11.4f,-2),new Vector3(-16.4f,11.4f,-2.62f)});
        else if(CurrentPhase==Phase.Descend)
        {
            float side=trackedHead.position.x<0?-23:23;
            route.Show(new[]{new Vector3(-12.2f,11.4f,-2),new Vector3(-12.2f,11.4f,0),new Vector3(side,11.4f,0)});
        }
    }
    private void Present()
    {
        UpdatePresentationVisibility();
        demonstration?.Show(CurrentPhase,0);
        if(CurrentPhase==Phase.Ready)
            SetText("准备开始 / Ready","开始后将播放短开场。\nA short intro plays before the drill.","长按右手柄 A 键开始。 / Hold right A to begin.");
        else if(CurrentPhase==Phase.Classroom)
            SetText("课间 / Between classes","","");
        else if(CurrentPhase==Phase.Alarm)
            SetText("火灾警报 / Fire alarm","","");
        else if(CurrentPhase==Phase.Complete)
        {
            doorwayPlume.SetActive(false);
            doorwayExposure.enabled=false;
        }
        bool cinema = CinematicLocked;
        if(cinema || CurrentPhase==Phase.Ready)
            layout?.Arrange(!cinema,cinema);
    }

    private void UpdateExerciseGuidance()
    {
        bool inRestroom = restroom.bounds.Contains(trackedHead.position);
        bool markRestroom = tasks.Floor == 4 && !inRestroom && (CurrentPhase == Phase.FindTowel || CurrentPhase == Phase.WetTowel);
        if (destination != null && destination.Highlighted != markRestroom) destination.SetHighlighted(markRestroom);

        Transform target = null;
        string hint = null;
        CampusFaucet nearestFaucet = null;
        if (CurrentPhase == Phase.FindTowel && !targetTowel.IsHeld && inRestroom)
        {
            target = targetTowel.transform;
            hint = "握住扳机，拿起干毛巾\n<size=80%>Hold the trigger to pick up the towel.</size>";
        }
        else if (CurrentPhase == Phase.WetTowel && targetTowel.IsHeld)
        {
            const float rangeSquared = 3f * 3f;
            float nearest = rangeSquared;
            foreach (var faucet in CampusFaucet.Active)
            {
                if (faucet == null || !restroom.bounds.Contains(faucet.transform.position)) continue;
                float distance = (faucet.transform.position - trackedHead.position).sqrMagnitude;
                if (distance >= rangeSquared) continue;
                bool sameWaterState = nearestFaucet == null || faucet.IsOn == nearestFaucet.IsOn;
                if (sameWaterState ? distance >= nearest : !faucet.IsOn) continue;
                nearest = distance;
                nearestFaucet = faucet;
                target = faucet.transform;
                hint = targetTowel.IsWet ? "毛巾已湿，持续握持并遮住口鼻\n<size=80%>Hold the wet towel over your nose and mouth.</size>" :
                    faucet.IsOn ? "在流水下保持 1.5 秒\n<size=80%>Hold under running water for 1.5 s.</size>" :
                    "用空闲手按扳机打开水龙头\n<size=80%>Use your free hand's trigger to turn on the tap.</size>";
            }
        }
        highlights?.Show(CurrentPhase, tasks, inRestroom, targetTowel, nearestFaucet);
        panels?.ShowObjectHint(target,hint,nearestFaucet != null);
    }

    private void HideObjectiveHighlights()
    {
        highlights?.Hide();
        destination?.SetHighlighted(false);
    }
    private void SetFade(float alpha) { if(fade!=null) fade.color=new Color(0,0,0,Mathf.Clamp01(alpha)); }
    private void UpdatePresentationVisibility()
    {
        lessonPanel.SetActive(CurrentPhase==Phase.Controls ? tutorial!=null && tutorial.ShowingWindowDemo : WorldInteractionLocked);
        if(gameplayHUD!=null) gameplayHUD.SetActive(CurrentPhase>=Phase.OpenDoor);
    }
    internal void StudentDelivered() => tasks?.StudentDelivered();
    internal void ConfirmRescue() => tasks?.ConfirmRescue();
    internal void CompleteAssembly()
    {
        tasks?.CompleteAssembly();
        HideObjectiveHighlights();
    }
    public void BypassForChecks()
    {
        ClearTutorial();
        panels?.Hide(false,false,fold:true);
        RestoreStandingHeight();
        bypassed=true; locomotion.SetScriptedLock(false); SetHandsVisible(true); SetStoryHands(false); SetFade(0); lessonPanel.SetActive(false); route.Hide();
        fireOrigin.SetActive(true); doorwayPlume.SetActive(false); doorwayExposure.enabled=false; HideObjectiveHighlights(); CurrentPhase=Phase.Complete;
    }
    private void OnDisable()
    {
        if(pauseController!=null)
        {
            pauseController.Opened-=OnPauseOpened;
            pauseController.ResumedFromMenu-=OnMenuResumed;
        }
        ClearTutorial();
        panels?.Dispose();
        panels = null;
        HideObjectiveHighlights();
        highlights?.Dispose();
        highlights = null;
        if(Instance==this) Instance=null; SetHandsVisible(true); SetStoryHands(false); if(route!=null) route.Hide(); SetFade(0);
        RestoreStandingHeight();
        if(locomotion!=null) locomotion.SetScriptedLock(false);
        if(lessonPanel!=null) lessonPanel.SetActive(false);
        if(Application.isPlaying && !finishing && gameplayHUD!=null) gameplayHUD.SetActive(true);
    }
}
