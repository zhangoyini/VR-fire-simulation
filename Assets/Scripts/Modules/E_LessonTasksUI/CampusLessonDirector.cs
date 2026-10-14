using Jiao.HealthTime;
using Harbourview.Journey;
using TMPro;
using UnityEngine;

// Scene-local chapter authority. Native head tracking stays live in every phase.
[DefaultExecutionOrder(-80)]
public sealed class CampusLessonDirector : MonoBehaviour
{
    public enum Phase { Controls, Ready, Classroom, Alarm, Rise, Rising, OpenDoor, SmokeLesson, FindTowel, WetTowel, Descend, Complete }
    public static CampusLessonDirector Instance { get; private set; }
    [SerializeField] private Transform trackedHead;
    [SerializeField] private CampusLocomotion locomotion;
    [SerializeField] private CampusHingeInteractable classroomDoor;
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
    public const float AnnouncementSeconds=22;
    public float ControlsDisplaySeconds => page==0?30:40;
    [SerializeField] private Transform storyBook, storyLeftHand, storyRightHand, bookRest, storyPage, storyStandingPoint;
    [SerializeField] private Renderer[] trackedHandRenderers;
    [SerializeField] private UnityEngine.UI.Image fade;
    [SerializeField] private CampusPracticeBook[] practiceBooks;
    [SerializeField] private RuntimeFireWindow[] practiceWindows;
    private ResolutionCoordinator resolution;
    private CampusLessonLayout layout;
    private float age, coverage, nextConfirm;
    private int page;
    private Vector3 standingRig, bookStart, bookEnd;
    private Quaternion bookRotation;
    private bool seated, fireStarted, finishing, bypassed, loggingReady, announcement, storyPrepared, practiceCompact;
    private Vector3 cinematicPosition;
    private Quaternion cinematicRotation;
    private float confirmHeld;
    private bool previousConfirm,confirmConsumed;
    [System.NonSerialized] public bool ManualTickForChecks;
    public Phase CurrentPhase { get; private set; } = Phase.Controls;
    public float PhaseAge => age;
    public int Page => page;
    public CampusTowelInteractable TargetTowel => targetTowel;
    public CampusHingeInteractable ClassroomDoor => classroomDoor;
    public Collider Restroom => restroom;
    public CampusSmokeVolume DoorwayExposure => doorwayExposure;
    public bool IsPractice => !bypassed && CurrentPhase<=Phase.Ready;
    public bool ExerciseStarted => bypassed || CurrentPhase>=Phase.OpenDoor;
    public bool TaskAnnouncement => announcement;
    public bool GuidedTowelTask => !bypassed && CurrentPhase>=Phase.FindTowel && CurrentPhase<=Phase.Descend;
    public bool MovementLocked => !bypassed && (CurrentPhase>=Phase.Classroom && CurrentPhase<=Phase.Rising || CurrentPhase==Phase.SmokeLesson);
    public bool WorldInteractionLocked => MovementLocked;
    public bool DamageSuppressed => !bypassed && (CurrentPhase<=Phase.Rising || announcement || CurrentPhase==Phase.SmokeLesson && (page>0 || age>=9));
    public bool SmokeFrozen => !bypassed && (!fireStarted || CurrentPhase==Phase.SmokeLesson || announcement);
    public float MinimumReadingSeconds => CurrentPhase==Phase.SmokeLesson?20:CurrentPhase==Phase.Controls?12:CurrentPhase==Phase.Rise?12:announcement?AnnouncementSeconds:8;
    public bool CanConfirm => !bypassed && !finishing && Time.timeScale>0 && age>=MinimumReadingSeconds &&
        (IsPractice || CurrentPhase==Phase.Rise || CurrentPhase==Phase.SmokeLesson);
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
    }
    private void Update()
    {
        if(bypassed || ManualTickForChecks) return;
        if(resolution!=null && resolution.CurrentOutcome!=ResolutionCoordinator.Outcome.None)
        { if(!finishing) { finishing=true; locomotion.SetScriptedLock(false); SetHandsVisible(true); SetStoryHands(false); lessonPanel.SetActive(false); minimap.SetActive(false); route.Hide(); SetFade(0); } return; }
        if(Time.timeScale==0) { lessonPanel.SetActive(false); minimap.SetActive(false); SetFade(0); if(alarm!=null && fireStarted) alarm.Pause(); return; }
        if(alarm!=null && fireStarted && !alarm.isPlaying) alarm.UnPause();
        Tick(Time.deltaTime);
        SubmitConfirmInput(CampusControllerInput.Confirm(),Time.deltaTime);
        // No trigger or contact button can start/advance this chapter.
#if UNITY_EDITOR
        if(!Valve.VR.SteamVR.active && Input.GetKeyDown(KeyCode.Return)) Confirm();
        if(!Valve.VR.SteamVR.active && Input.GetKeyDown(KeyCode.Backspace)) SkipControls();
#endif
    }
    public void Tick(float dt)
    {
        if(bypassed || Time.timeScale==0 || dt<=0 || finishing) return;
        age+=dt; UpdatePresentationVisibility();
        if(announcement && age>=AnnouncementSeconds) FoldAnnouncement();
        if(CurrentPhase==Phase.Controls && age>=ControlsDisplaySeconds)
        { if(++page>=3) ChangePhase(Phase.Ready); else { age=0; practiceCompact=false; Present(); } }
        if(CurrentPhase==Phase.SmokeLesson && age>=(page==0?24:32))
        { if(++page>=2) ChangePhase(Phase.FindTowel); else { age=0; Present(); } }
        if(fireStarted && CurrentPhase>=Phase.OpenDoor && CurrentPhase<Phase.Complete) UpdateDoorwaySmoke();
        if(CurrentPhase==Phase.Ready && age>=10 && !practiceCompact)
        { practiceCompact=true; SetPracticeSummary(); layout.Arrange(false); }
        if(CurrentPhase==Phase.Classroom) TickStory();
        else if(CurrentPhase==Phase.Alarm)
        {
            if(age>=5 && page==0) { page=1; SetText("系统动画 · 异常气味 / Opening · Burning smell","警报与焦糊气味 / Alarm and unusual smell","先观察，保持冷静 / Observe and stay calm"); layout.Arrange(false,true); }
            if(age>=14) ChangePhase(Phase.Rise);
        }
        else if(CurrentPhase==Phase.Rising)
        {
            PoseRestingHands(Mathf.Clamp01(age/1.0f));
            SetFade(age>=.9f && age<1.2f?(age-.9f)/.3f:age>=1.2f && age<1.8f?1-(age-1.2f)/.6f:0);
            if(age>=1.2f && seated) { MoveRig(standingRig+Vector3.right*.8f); seated=false; }
            if(age>=3) { SetFade(0); SetStoryHands(false); SetHandsVisible(true); FindObjectOfType<CampusExperience>()?.BeginTimedExercise(); ChangePhase(Phase.OpenDoor); }
        }
        else if(CurrentPhase==Phase.OpenDoor)
        {
            Vector3 p=classroomDoor.transform.parent.position; Vector3 delta=trackedHead.position-p; delta.y=0;
            if(!announcement && Mathf.Abs(classroomDoor.Angle)>=85 && delta.magnitude<1.8f && doorwayExposure.Contains(trackedHead.position)) { page=0; ChangePhase(Phase.SmokeLesson); }
        }
        else if(CurrentPhase==Phase.FindTowel && !announcement)
        {
            if(restroom.bounds.Contains(trackedHead.position) && targetTowel.IsHeld) ChangePhase(Phase.WetTowel);
        }
        else if(CurrentPhase==Phase.WetTowel && !announcement)
        {
            explanation.text=!targetTowel.IsHeld?"重新拿起毛巾 / Re-grab towel":!targetTowel.IsWet?"打开水，浸湿毛巾 / Turn on tap; wet towel":"握持并遮住口鼻 / Hold at mouth & nose";
            coverage=targetTowel.CoversFace(trackedHead)?coverage+dt:0;
            if(coverage>=1.5f) ChangePhase(Phase.Descend);
        }
        else if(CurrentPhase==Phase.Descend)
        {
            int floor=Mathf.Clamp(Mathf.FloorToInt((trackedHead.position.y-.8f)/3.8f)+1,1,4);
            bool covered=targetTowel.CoversFace(trackedHead);
            explanation.text=covered?"沿楼梯下到三楼 / Stairs to 3F":"毛巾放回口鼻前 / Cover your face again";
            if(floor==3 && covered) { coverage+=dt; if(coverage>=1.5f) ChangePhase(Phase.Complete); } else coverage=0;
        }
        if(IsPractice && practiceCompact) SetPracticeSummary();
        if(CurrentPhase==Phase.FindTowel && !announcement) explanation.text="前往 411 拿干毛巾\n411 · Pick up dry towel";
        layout?.SetReadingProgress(age/MinimumReadingSeconds);
        demonstration?.Tick(dt); route?.Tick(trackedHead); layout?.Tick(dt);
    }
    public void Confirm()
    {
        if(!CanConfirm || Time.unscaledTime<nextConfirm) return;
        nextConfirm=Time.unscaledTime+.35f;
        if(CurrentPhase==Phase.Controls)
        { if(++page>=3) ChangePhase(Phase.Ready); else { age=0; practiceCompact=false; Present(); } }
        else if(CurrentPhase==Phase.Ready) BeginStory();
        else if(CurrentPhase==Phase.Rise) ChangePhase(Phase.Rising);
        else if(CurrentPhase==Phase.SmokeLesson)
        { if(++page>=2) ChangePhase(Phase.FindTowel); else { age=0; Present(); } }
    }
    public void SubmitConfirmInput(bool pressed,float seconds)
    {
        bool down=pressed && !previousConfirm; previousConfirm=pressed;
        if(!pressed) { confirmHeld=0; confirmConsumed=false; return; }
        if(Time.timeScale==0 || !CanConfirm || confirmConsumed) { confirmHeld=0; return; }
        confirmHeld+=Mathf.Max(0,seconds);
        // Hold A to enter; a single A press is sufficient to stand/skip a read page.
        if(CurrentPhase==Phase.Ready?confirmHeld>=.75f:down)
        { confirmConsumed=true; Confirm(); }
    }
    private void FoldAnnouncement()
    { announcement=false; SetCompactTask(); layout.Arrange(false); ShowRoute(); UpdatePresentationVisibility(); }
    public void SkipControls() { if(CurrentPhase==Phase.Controls) ChangePhase(Phase.Ready); }
    private void BeginStory()
    {
        foreach(var hand in FindObjectsOfType<CampusHandInteractor>(true)) hand.Release();
        foreach(var book in practiceBooks) if(book!=null) book.ResetBook();
        standingRig=storyStandingPoint!=null?storyStandingPoint.position:locomotion.transform.position;
        cinematicPosition=locomotion.transform.position; cinematicRotation=locomotion.transform.rotation;
        storyPrepared=false; SetHandsVisible(false); ChangePhase(Phase.Classroom);
    }
    private void TickStory()
    {
        SetFade(age<.4f?age/.4f:age<1?1:age<1.6f?1-(age-1)/.6f:age>=14.7f && age<15?(age-14.7f)/.3f:age>=15 && age<15.7f?1-(age-15)/.7f:0);
        if(age>=.4f && !storyPrepared)
        {
            classroomDoor.SetAngle(0);
            Vector3 local=locomotion.transform.InverseTransformPoint(trackedHead.position); local.y=0;
            MoveRig(standingRig-locomotion.transform.TransformVector(local)); standingRig=locomotion.transform.position;
            // Establish a prescribed desk-facing view under the blackout, without
            // changing the sensor's local head pose or rotating a visible view.
            Vector3 deskDirection=Vector3.ProjectOnPlane(bookRest.position-trackedHead.position,Vector3.up).normalized;
            Vector3 headDirection=Vector3.ProjectOnPlane(trackedHead.forward,Vector3.up).normalized;
            float turn=Vector3.SignedAngle(headDirection,deskDirection,Vector3.up);
            locomotion.transform.Rotate(Vector3.up,turn,Space.World); cinematicRotation=locomotion.transform.rotation;
            Vector3 forward=deskDirection;
            if(forward.sqrMagnitude<.01f) forward=locomotion.transform.forward;
            bookStart=trackedHead.position+forward*.43f-Vector3.up*.32f;
            bookEnd=bookRest.position; bookRotation=Quaternion.LookRotation(forward,Vector3.up);
            storyBook.gameObject.SetActive(true); storyPrepared=true;
        }
        if(!storyPrepared) return;
        float put=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-10)/3));
        Vector3 inspect=bookStart-Vector3.up*.10f;
        Vector3 position=age<3?Vector3.Lerp(inspect,bookStart,Mathf.SmoothStep(0,1,(age-1)/2)):Vector3.Lerp(bookStart,bookEnd,put);
        storyBook.SetPositionAndRotation(position,bookRotation*Quaternion.Euler(Mathf.Lerp(-18,0,put),0,0));
        if(storyPage!=null) storyPage.localRotation=Quaternion.Euler(0,0,age>=7 && age<10?Mathf.Sin(Mathf.Clamp01((age-7)/2)*Mathf.PI)*110:0);
        if(age<13) PoseBookHands(position); else PoseRestingHands(Mathf.Clamp01((age-13)/1.2f));
        if(age>=15 && !seated) { MoveRig(standingRig-Vector3.up*.30f); seated=true; }
        string[] beats={"就位 / Settling in","翻阅书本 / Reading","翻页 / Turning a page","放下书本 / Putting book down","双手放回桌面 / Resting hands","虚拟坐下 / Taking a virtual seat","课间休息 / A quiet moment"};
        int beat=age<3?0:age<7?1:age<10?2:age<13?3:age<15?4:age<17?5:6;
        explanation.text=beats[beat];
        if(age>=22) { SetFade(0); SetStoryHands(false); ChangePhase(Phase.Alarm); }
    }
    private void MoveRig(Vector3 p)
    { bool enabled=locomotion.Body.enabled; locomotion.Body.enabled=false; locomotion.transform.position=p; cinematicPosition=p; locomotion.Body.enabled=enabled; Physics.SyncTransforms(); }
    private void LateUpdate()
    {
        if(!MovementLocked || bypassed || finishing) return;
        // Exclusive cinematic body pose prevents other movement writers and gravity drift.
        locomotion.transform.SetPositionAndRotation(cinematicPosition,cinematicRotation);
    }
    private void PoseBookHands(Vector3 book)
    {
        Vector3 right=bookRotation*Vector3.right, forward=bookRotation*Vector3.forward;
        foreach(var hand in new[]{storyLeftHand,storyRightHand}) if(hand!=null)
        { hand.gameObject.SetActive(true); hand.SetPositionAndRotation(book+right*(hand==storyLeftHand?-.16f:.16f)-forward*.055f-Vector3.up*.025f,bookRotation*Quaternion.Euler(-25,0,hand==storyLeftHand?170:10)); }
    }
    private void PoseRestingHands(float t)
    {
        foreach(var hand in new[]{storyLeftHand,storyRightHand}) if(hand!=null)
        {
            hand.gameObject.SetActive(true);
            Vector3 rest=bookEnd+bookRotation*Vector3.right*(hand==storyLeftHand?-.29f:.29f)-bookRotation*Vector3.forward*.12f+Vector3.up*.045f;
            hand.position=Vector3.Lerp(hand.position,rest,t); hand.rotation=bookRotation*Quaternion.Euler(-8,0,hand==storyLeftHand?168:12);
        }
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
        CurrentPhase=phase; age=coverage=0; practiceCompact=false;
        announcement=phase==Phase.OpenDoor || phase==Phase.FindTowel || phase==Phase.WetTowel;
        if(phase==Phase.Alarm) { page=0; StartFire(); }
        if(phase==Phase.OpenDoor)
            foreach(var endpoint in FindObjectsOfType<SafeZoneEndpoint>()) endpoint.ResetZone();
        if(MovementLocked && phase!=Phase.Classroom)
        { cinematicPosition=locomotion.transform.position; cinematicRotation=locomotion.transform.rotation; }
        locomotion.SetScriptedLock(MovementLocked);
        if(WorldInteractionLocked && phase!=Phase.WetTowel)
            foreach(var hand in FindObjectsOfType<CampusHandInteractor>(true)) hand.Release();
        destination?.SetHighlighted(phase==Phase.FindTowel || phase==Phase.WetTowel);
        Present(); if(announcement) ShowRoute();
        if(loggingReady) SessionLogger.Write("LESSON_"+phase.ToString().ToUpperInvariant());
    }
    private void SetText(string heading,string text,string hint)
    { title.text=heading; explanation.text=text; footer.text=hint; }
    private void SetPracticeSummary()
    {
        bool picked=false, opened=false;
        foreach(var book in practiceBooks) picked|=book!=null && book.Practised;
        foreach(var window in practiceWindows) opened|=window!=null && window.IsOpen;
        title.text="自由练习 / Free practice";
        explanation.text=(picked?"✓":"○")+" 拿书 / Book   "+(opened?"✓":"○")+" 推窗 / Window";
        if(CurrentPhase==Phase.Ready) explanation.text="按住右手柄 A 键开始\nHold right A to begin";
    }
    private void SetCompactTask()
    {
        if(CurrentPhase==Phase.OpenDoor) SetText("观察门外 / Doorway","靠近并推开教室门 / Push classroom door","");
        else if(CurrentPhase==Phase.FindTowel) SetText("任务 1 / Task 1","前往 411 拿干毛巾\n411 · Pick up dry towel","");
        else if(CurrentPhase==Phase.WetTowel) SetText("任务 2 / Task 2","打开水，浸湿毛巾 / Wet towel","");
        else if(CurrentPhase==Phase.Descend) SetText("任务 2 / Task 2","握持湿毛巾，下到三楼 / Reach 3F","");
    }
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
        UpdatePresentationVisibility(); demonstration?.Show(CurrentPhase,page); route.Hide();
        if(CurrentPhase==Phase.Controls)
        {
            string[] h={"头显与安全 / Headset & safety","左手移动 / Left-stick movement","双手交互 / Two-hand interaction"};
            string[] t={"自然转头观察；检查佩戴和活动空间。当前可自由走动，无火灾。\nLook around naturally. Check your fit and clear space. You may explore safely now.","左摇杆向四周移动，松开即停。右摇杆无操作。试着走到教室后窗。\nLeft stick moves; release stops. Click it to pause. Right stick is unused. Try walking.","佩戴腕带、握稳手柄。扳机握持、推动；挥手松扳机投掷物体。右 A 确认，左摇杆按下暂停。\nKeep wrist straps on. Triggers grab/push; swing and release the trigger to throw the object. A confirms; left-stick click pauses."};
            SetText(h[page],t[page],"自由练习中，教学会自动换页。 "+(page+1)+" / 3\nExplore freely. Pages advance automatically; A skips after reading.");
        }
        else if(CurrentPhase==Phase.Ready) SetText("自由练习 / Free practice","可继续移动、拿书和推窗。开始后观看系统动画，移动与物体操作暂时锁定。无需实际坐下。\nKeep practising. The opening locks locomotion and object input. Do not physically sit.","按住右手柄 A 键 0.75 秒开始；不使用扳机。\nHold right A for 0.75 s to begin. Triggers only interact.");
        else if(CurrentPhase==Phase.Classroom) SetText("系统动画中 / Opening cinematic","就位、翻书、放书、虚拟坐下\nSettle in, read, put book down, sit virtually","");
        else if(CurrentPhase==Phase.Alarm) SetText("系统动画 · 火灾警报 / Opening · Alarm","注意警报，保持冷静 / Notice alarm; stay calm","");
        else if(CurrentPhase==Phase.Rise) SetText("准备起身 / Prepare to stand","警报与异常焦糊气味提示可能发生火灾。准备观察教室出口。\nThe alarm and burning smell suggest a fire. Prepare to observe the classroom exit.","阅读后按右手柄 A 键虚拟起身。无需实际站起。\nRead, then press right A to stand virtually.");
        else if(CurrentPhase==Phase.Rising) SetText("系统动画 · 起身 / Opening · Standing","扶桌、起身、站稳 / Brace, rise, settle","");
        else if(CurrentPhase==Phase.OpenDoor) SetText("观察门外 / Observe the doorway","走近教室门，按住扳机向前推。请先观察门外情况。\nApproach the classroom door. Hold a trigger and push, then observe conditions outside.","已恢复自由操作。提示会自动收起，无需确认。\nControls unlocked. Guidance folds automatically; no confirmation.");
        else if(CurrentPhase==Phase.SmokeLesson)
        {
            if(page==0) SetText("烟雾与血量 / Smoke & health","门外已有烟雾。观察上方血条：先有 6 秒反应时间，随后缓慢扣血。\nWatch the health bar: smoke gives 6 seconds to react, then health decreases slowly.","9 秒后暂停伤害；教学自动继续，A 可跳过。\nDamage freezes after 9 s. Teaching continues automatically; A skips.");
            else SetText("消防知识 / Fire-safety knowledge","烟雾会引起咳嗽、窒息和中毒。下一步：到卫生间打湿毛巾，持续握持并遮住口鼻，再沿楼梯撤离。\nSmoke can cause coughing, suffocation and poisoning. Next: wet a towel, hold it at your face and evacuate by stairs.","遮住口鼻会减缓扣血，但湿毛巾不能过滤一氧化碳，仍需尽快离开烟雾。\nCoverage slows health loss, not CO poisoning. Leave smoke promptly.");
        }
        else if(CurrentPhase==Phase.FindTowel) SetText("任务 1：寻找毛巾 / Task 1: Find towel","前往对面的 411 卫生间，按住左手或右手扳机拿起台上的干毛巾。\nGo to restroom 411 opposite the classroom. Hold either trigger to pick up a dry towel.","跟随青蓝色高亮箭头与发光门牌。22 秒后提示自动收起。\nFollow cyan arrows and the lit door sign. This card folds after 22 s.");
        else if(CurrentPhase==Phase.WetTowel) SetText("任务 2：浸湿与撤离 / Task 2: Wet & leave","空闲手打开水龙头。毛巾在流水中保持 1.5 秒，持续握持并遮住口鼻，然后沿楼梯下到三楼。\nUse the free hand to turn on water. Wet the towel for 1.5 s; hold it at your face, then take stairs to 3F.","遮住口鼻后扣血减缓，但仍有烟气伤害；继续尽快撤离。\nCoverage slows health loss, but smoke still harms you. Keep escaping.");
        else if(CurrentPhase==Phase.Descend) { SetCompactTask(); ShowRoute(); }
        else if(CurrentPhase==Phase.Complete)
        { doorwayPlume.SetActive(false); doorwayExposure.enabled=false; CampusMessagePresenter.Instance?.Show("两项练习完成！继续观察路线，前往安全地点。\nBoth exercises complete. Continue observing the route to a safe endpoint."); }
        bool cinema=CurrentPhase==Phase.Classroom || CurrentPhase==Phase.Alarm || CurrentPhase==Phase.Rising;
        layout?.Arrange(!cinema && CurrentPhase!=Phase.Descend && CurrentPhase!=Phase.Complete,cinema);
    }
    private void SetFade(float alpha) { if(fade!=null) fade.color=new Color(0,0,0,Mathf.Clamp01(alpha)); }
    private void UpdatePresentationVisibility()
    {
        bool message=CampusMessagePresenter.Instance!=null && CampusMessagePresenter.Instance.IsVisible;
        lessonPanel.SetActive(CurrentPhase!=Phase.Complete && (!message || WorldInteractionLocked));
        if(gameplayHUD!=null) gameplayHUD.SetActive(CurrentPhase>=Phase.OpenDoor);
        if(minimap!=null) minimap.SetActive(!WorldInteractionLocked && !announcement);
    }
    public void BypassForChecks()
    {
        bypassed=true; locomotion.SetScriptedLock(false); SetHandsVisible(true); SetStoryHands(false); SetFade(0); lessonPanel.SetActive(false); route.Hide();
        if(seated) { MoveRig(standingRig); seated=false; }
        fireOrigin.SetActive(true); doorwayPlume.SetActive(false); doorwayExposure.enabled=false; destination?.SetHighlighted(false); CurrentPhase=Phase.Complete;
    }
    private void OnDisable()
    {
        if(Instance==this) Instance=null; destination?.SetHighlighted(false); SetHandsVisible(true); SetStoryHands(false); if(route!=null) route.Hide(); SetFade(0);
        if(locomotion!=null) locomotion.SetScriptedLock(false);
        if(lessonPanel!=null) lessonPanel.SetActive(false);
        if(Application.isPlaying && seated && locomotion!=null && locomotion.Body!=null) { MoveRig(standingRig); seated=false; }
        if(Application.isPlaying && !finishing && gameplayHUD!=null) gameplayHUD.SetActive(true);
    }
}
