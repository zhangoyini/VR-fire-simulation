#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiao.HealthTime;
using UnityEditor;
using UnityEngine;

public sealed class CampusLessonDriver : MonoBehaviour
{
    private readonly List<string> report=new List<string>(); private float deadline;
    private void Start() { deadline=Time.realtimeSinceStartup+150; StartCoroutine(Guard(Checks())); }
    private void Update() { if(Time.realtimeSinceStartup>deadline) Finish(false,"Timeout"); }
    private void Check(bool value,string label) { if(!value) throw new InvalidOperationException("FAIL: "+label); report.Add("PASS: "+label); }
    private void Finish(bool pass,string reason)
    {
        SessionState.SetBool("Campus.LessonQA",false); string text=string.Join("\n",report)+"\n"+reason;
        File.WriteAllText("Documentation/LessonChecks.txt",text); Debug.Log((pass?"CAMPUS_LESSON_PASS":"CAMPUS_LESSON_FAIL")+"\n"+text); EditorApplication.Exit(pass?0:1);
    }
    private IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>(); stack.Push(test);
        while(stack.Count>0)
        {
            object next;
            try { var current=stack.Peek(); if(!current.MoveNext()) { stack.Pop(); continue; } next=current.Current; }
            catch(Exception e) { Finish(false,e.ToString()); yield break; }
            if(next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish(true,"Quest tracking, vibration and comfort still need hardware testing.");
    }
    private static T Field<T>(object o,string name) => (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private IEnumerator Advance(CampusLessonDirector lesson)
    {
        if(lesson.TaskAnnouncement) { lesson.Tick(CampusLessonDirector.AnnouncementSeconds+.1f); yield return null; yield break; }
        lesson.Tick(Mathf.Max(0,lesson.MinimumReadingSeconds-lesson.PhaseAge)+.1f); yield return new WaitForSeconds(.36f);
        lesson.SubmitConfirmInput(false,.01f); lesson.SubmitConfirmInput(true,lesson.CurrentPhase==CampusLessonDirector.Phase.Ready?.8f:.01f);
        yield return null;
    }
    private IEnumerator Checks()
    {
        yield return null; yield return null;
        var lesson=FindObjectOfType<CampusLessonDirector>(); lesson.ManualTickForChecks=true;
        var movement=FindObjectOfType<CampusLocomotion>(true); var head=Camera.main.transform;
        var layout=lesson.GetComponent<CampusLessonLayout>(); var health=HealthTimeController.Instance;
        var exposure=FindObjectOfType<CampusSmokeExposure>(); exposure.enabled=false;
        var spread=FindObjectOfType<CampusSmokeSpread>(); var hands=FindObjectsOfType<CampusHandInteractor>(true);
        Check(lesson==CampusLessonDirector.Instance && lesson.CurrentPhase==CampusLessonDirector.Phase.Controls,"Entry begins with the scene-local controls demonstration");
        Check(!Field<GameObject>(lesson,"fireOrigin").activeSelf && !Field<AudioSource>(lesson,"alarm").isPlaying && spread.RoomAge==0,"Practice has no fire, alarm or advancing smoke");
        Check(lesson.IsPractice && !lesson.MovementLocked && !lesson.WorldInteractionLocked && lesson.DamageSuppressed,"Initial teaching allows free movement and world interaction without damage");
        var resolution=FindObjectOfType<ResolutionCoordinator>();
        Check(!resolution.TryShowVictory() && resolution.CurrentOutcome==ResolutionCoordinator.Outcome.None,"Safe endpoints cannot finish the game during free practice");
        var height=movement.GetComponent<CampusTrackingHeight>(); var space=Field<Transform>(height,"trackingSpace");
        Check(head.parent==space && hands.All(h=>h.transform.parent==space && Field<GameObject>(h.GetComponent<DummyController>(),"controller").GetComponent<Valve.VR.SteamVR_Behaviour_Pose>().origin==space),"Head, controller poses and visible interaction proxies use one calibrated origin");

        Check(Valve.VR.SteamVR_Input.GetBooleanAction("LessonConfirm")!=null,"A has its own generated SteamVR action, distinct from grabbing");
        var anchors=GameObject.Find("05 Player & Shared Systems").GetComponentsInChildren<WorldSpaceUIAnchor>(true);
        Check(anchors.All(a=>Mathf.Abs(Field<Vector3>(a,"headLocalOffset").x)<.01f && Field<bool>(a,"followHead")),"All personal HUD/menu anchors are horizontally centred and follow the eyes");
        var map=Field<GameObject>(lesson,"minimap").GetComponent<WorldSpaceUIAnchor>();
        Check(Field<Vector3>(map,"headLocalOffset").z==1.6f && Field<Vector3>(map,"headLocalOffset").y>-.4f,"The map moves from the extreme lower-left into the forward comfort cone");
        Vector3 originalHead=head.localPosition; Quaternion headRotation=head.localRotation;
        Vector3[] handPositions=hands.Select(h=>h.transform.position).ToArray();
        head.localPosition=new Vector3(0,.1f,0); height.Observe(.1f,false,10);
        Check(!height.Calibrated && height.HeightOffset==0,"Invalid tracking cannot calibrate using an editor placeholder");
        height.Observe(.1f,true,.7f,false); height.Observe(.1f,true,.7f,false);
        Check(height.Calibrated && Mathf.Abs(movement.transform.InverseTransformPoint(head.position).y-1.7f)<.001f,"A low/device-origin pose is raised to a 1.70 metre viewpoint");
        Check(hands.Select((h,i)=>Mathf.Abs(h.transform.position.y-handPositions[i].y-1.6f)<.001f).All(v=>v),"Both hands receive the same height offset as the head");
        head.localPosition=new Vector3(0,-.3f,0); height.Observe(-.3f,true,1);
        Check(Mathf.Abs(movement.transform.InverseTransformPoint(head.position).y-1.3f)<.001f && head.localRotation==headRotation,"After calibration, natural crouching and head rotation remain unchanged");
        space.localPosition=Vector3.zero; head.localPosition=originalHead; height.Recalibrate(); height.Observe(1.7f,true,1); height.Observe(1.7f,true,1);

        Check(height.HeightOffset==0,"A floor-relative 1.70 metre pose does not get a duplicate standing-height offset");
        height.Recalibrate(); head.localPosition=new Vector3(0,.65f,0); height.Observe(.65f,true,1,true);
        Check(height.HeightOffset==0 && height.FloorRelative,"Crouching in floor-relative tracking never raises the real floor or controller reach");
        height.Recalibrate(); head.localPosition=new Vector3(0,1.38f,0); height.Observe(1.38f,true,1,true);
        Check(height.HeightOffset==0,"A shorter player's real standing height is preserved instead of lifting both hands");
        head.localPosition=originalHead;
        foreach(var hand in hands)
        {
            var follower=hand.GetComponent<CampusTrackedHandPose>(); Vector3 raw=new Vector3(.25f,.14f,.5f);
            follower.ApplyLocalPose(raw,Quaternion.identity); Vector3 before=hand.transform.position;
            follower.ApplyLocalPose(raw+Vector3.forward*.30f,Quaternion.identity);
            Check(Mathf.Abs(Vector3.Distance(before,hand.transform.position)-.30f)<.001f,"Controller travel maps one-to-one for "+hand.InputSource);
            Check(Mathf.Abs(movement.transform.InverseTransformPoint(hand.transform.position).y-.14f)<.001f,"Floor reach remains 14 cm above the actual virtual floor for "+hand.InputSource);
        }
        Vector3 rigPosition=movement.transform.position;
        for(int i=0;i<6;i++) movement.Step(Vector2.right,1,.05f);
        Check(Vector3.ProjectOnPlane(movement.transform.position-rigPosition,Vector3.up).magnitude<.001f && head.localRotation==headRotation && FindObjectsOfType<VRTeleporter>().Length==2,"Entry uses either-hand teleport; joystick input does not slide or turn the view");
        MoveRig(movement,rigPosition);
        exposure.Tick(true,20); Check(health.HealthPercent==1,"Practice reading and exploration cannot cause smoke damage");
        lesson.Confirm(); Check(lesson.Page==0,"A fresh page cannot be skipped before its reading gate");
        lesson.Tick(.3f);
        Check(layout.IsAnimating && Field<CanvasGroup>(layout,"textGroup").alpha>0 && Field<CanvasGroup>(layout,"textGroup").alpha<1,"The UI unfolds and fades in rather than instantly appearing");
        Capture("Lesson_13_Unfold",head); lesson.Tick(1); Capture("Lesson_01_Headset",head);
        Check(lesson.ControlsDisplaySeconds==30 && lesson.MinimumReadingSeconds==12,"Headset demonstration allows thirty seconds with a deliberate reading gate");
        lesson.Tick(8); Check(lesson.Page==0,"Headset is not automatically replaced after the old twelve-second interval");
        var books=FindObjectsOfType<CampusPracticeBook>(); var practiceWindows=Field<RuntimeFireWindow[]>(lesson,"practiceWindows");
        Check(books.Length==2 && practiceWindows.Length>=2,"Initial classroom has two grabbable books and rear opening windows");
        var owner=hands[0]; var free=hands[1];
        owner.transform.position=books[0].transform.position; var pulses=new List<float>(); Action<float> recorder=a=>pulses.Add(a); owner.HapticRequested+=recorder;
        owner.RefreshContact(); owner.ProcessGrip(true,true,false);
        Check(books[0].IsHeld && !books[0].GetComponent<Rigidbody>().useGravity,"Either hand can directly pick up a practice book");
        Check(!free.Begin(books[0]),"A second hand cannot steal an already held book");
        owner.transform.position+=Vector3.up*.25f; owner.ProcessGrip(false,true,false);
        Check(lesson.Page==0 && books[0].Practised,"A world trigger action practises grabbing without advancing the lesson");
        owner.ProcessGrip(false,false,true); owner.HapticRequested-=recorder;
        Check(!books[0].IsHeld && books[0].GetComponent<Rigidbody>().useGravity && pulses.SequenceEqual(new[]{.3f,.22f}),"Practice book release drops it and requests matching grab/drop vibrations");
        var window=practiceWindows[0].HighlightTarget.GetComponentInParent<CampusHingeInteractable>();
        window.SetAngle(45); window.OnInteractStart(owner.transform); window.OnInteractEnd(owner.transform);
        Check(window.Angle==45 && !CampusMessagePresenter.Instance.IsVisible,"Rear windows open during practice without a premature fire warning");
        window.SetAngle(0);
        var pause=FindObjectOfType<SystemUIPauseController>(); float beforePause=lesson.PhaseAge;
        pause.ShowPause(); lesson.Tick(20); lesson.Confirm();
        Check(lesson.PhaseAge==beforePause && health.HealthPercent==1,"Pause freezes teaching, confirmations and health");
        pause.ResumeGame();
        yield return Advance(lesson); Check(lesson.Page==1,"A key advances to left-stick teaching without a contact button");
        lesson.Tick(1); Capture("Lesson_02_LeftStick",head);
        Check(lesson.ControlsDisplaySeconds==40 && FindObjectsOfType<CampusTeachingHand>(true).Length==2,"Controller teaching includes two animated detailed 3D hands for forty seconds");
        var demo=Field<CampusLessonAnimation>(lesson,"demonstration");
        demo.Tick(15); Capture("Lesson_18_PauseKeyDemo",head);
        Check(Field<TMPro.TMP_Text>(demo,"actionLabel").text.Contains("暂停"),"The actual left-stick click pause control is demonstrated");
        demo.Tick(5);
        Check(Field<TMPro.TMP_Text>(demo,"actionLabel").text.Contains("射线") && Field<GameObject>(demo,"menuDemo").activeInHierarchy,
            "Teaching demonstrates the green menu ray and matching trigger after pause");
        yield return Advance(lesson); Check(lesson.Page==2,"A key advances to push/grab teaching without trigger confirmation");
        lesson.Tick(1); Capture("Lesson_03_Interaction",head);
        demo.Tick(15); Capture("Lesson_19_AKeyDemo",head);
        Check(Field<TMPro.TMP_Text>(demo,"actionLabel").text.Contains("右 A"),"The right A key is animated independently of the grabbing triggers");
        yield return Advance(lesson);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Ready && !lesson.MovementLocked,"Controls end in an unlocked practice/ready phase");
        lesson.Tick(30);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Ready && !layout.IsFull,"An idle ready page shrinks but never starts the game automatically");
        Capture("Lesson_10_PracticeHUD",head);
        yield return Advance(lesson);

        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Classroom && lesson.MovementLocked,"Explicit A hold starts the locked first-person cinematic");
        Vector3 lockedPosition=movement.transform.position; movement.Step(Vector2.right,1,.5f);
        Check(movement.ScriptedLocked && !movement.Body.enabled && movement.transform.position==lockedPosition,"Cinematic disables the actual movement controller, including all stick directions");
        movement.transform.position+=Vector3.right*.6f; lesson.SendMessage("LateUpdate");
        Check(movement.transform.position==lockedPosition,"Exclusive system pose rejects an external body movement writer during the movie");
        Check(hands.All(h=>h.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(m=>!m.enabled)),"Real controller hands are hidden while prescribed animation hands own the story");
        Check(!owner.Begin(lesson.ClassroomDoor),"World manipulation is blocked during system cinematics");
        lesson.Tick(3); var book=Field<Transform>(lesson,"storyBook"); Vector3 reading=book.position;
        Check(book.gameObject.activeSelf && Field<TMPro.TMP_Text>(lesson,"title").text.Contains("系统动画"),"Cinematic visibly identifies system-controlled reading");
        lesson.Tick(5.5f); Check(Mathf.Abs(Field<Transform>(lesson,"storyPage").localEulerAngles.z)>20,"Reading includes a separate animated page-turn beat");
        head.localRotation=Quaternion.Euler(18,0,0); Capture("Lesson_11_Reading",head); head.localRotation=headRotation;
        lesson.Tick(3);
        Check(Vector3.Distance(book.position,reading)>.15f,"Placing the book is a distinct normal-speed motion after reading");
        head.localRotation=Quaternion.Euler(18,0,0); Capture("Lesson_04_PutBookDown",head); head.localRotation=headRotation;
        foreach(var inputHand in hands)
        {
            var source=inputHand.GetComponentInChildren<SkinnedMeshRenderer>(true); var temporary=new Mesh(); source.BakeMesh(temporary,false);
            var story=Field<Transform>(lesson,inputHand.InputSource==Valve.VR.SteamVR_Input_Sources.LeftHand?"storyLeftHand":"storyRightHand");
            var vertices=story.GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
            var matrix=inputHand.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
            Check(vertices.Zip(temporary.vertices,(a,b)=>Vector3.Distance(a,matrix.MultiplyPoint3x4(b))).Max()<.001f,"Baked story glove preserves the actual anatomical handedness: "+inputHand.InputSource);
            Destroy(temporary);
        }
        lesson.Tick(4);
        Check(Mathf.Abs(movement.transform.position.y-rigPosition.y+.3f)<.01f && head.localPosition==originalHead,"Virtual seating moves the rig under a fade, never the tracked head pose");
        Check(Vector3.Distance(book.position,Field<Transform>(lesson,"bookRest").position)<.01f,"The book rests on the actual desk");
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Classroom,"Additional settling/rest beats keep the cinematic longer than the old six-second opening");
        lesson.Tick(7);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Alarm && Field<AudioSource>(lesson,"alarm").isPlaying,"Alarm follows the 22-second normal-life sequence");
        Check(spread.RoomAge>=85 && spread.Stages.Any(s=>s.floor==4 && !s.roomClock && s.exposure.enabled),"The alarm starts with visible corridor smoke already prepared");
        lesson.Tick(5.1f); Check(Field<TMPro.TMP_Text>(lesson,"explanation").text.Contains("焦糊"),"Burning smell follows a distinct alarm-observation beat");
        lesson.Tick(5); Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Alarm,"The odor cue does not disappear after only a few seconds");
        lesson.Tick(4); lesson.Tick(1); Capture("Lesson_05_StandPrompt",head);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Rise && lesson.MovementLocked,"Standing waits indefinitely for deliberate player confirmation");
        yield return Advance(lesson); lesson.Tick(1);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Rising && lesson.MovementLocked,"Standing includes bracing and a fade rather than an immediate unlock");
        lesson.Tick(2.1f);

        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.OpenDoor && lesson.TaskAnnouncement && !lesson.MovementLocked && movement.Body.enabled,"Finishing the cinematic immediately restores free movement, without waiting for a task confirmation");
        foreach(var hand in hands) { hand.GetComponent<CampusTrackedHandPose>().ApplyLocalPose(originalHead+new Vector3(hand.InputSource==Valve.VR.SteamVR_Input_Sources.LeftHand?-.22f:.22f,-.32f,.4f),Quaternion.identity); hand.GetComponent<CampusHandVisual>().SendMessage("LateUpdate"); }
        Check(hands.All(h=>h.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(m=>m.enabled && m.updateWhenOffscreen)),"Both detailed tracked hands are restored after the movie");
        Capture("Lesson_14_HandsRestored",head);
        Check(Mathf.Abs(movement.transform.position.y-rigPosition.y)<.01f && head.localPosition==originalHead && head.localRotation==headRotation,"The cinematic restores standing height and retains natural HMD pose");
        lesson.ClassroomDoor.SetAngle(0); lesson.Tick(.1f);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.OpenDoor,"A closed classroom door does not start fire knowledge");
        lesson.ClassroomDoor.SetAngle(15); lesson.Tick(.1f);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.SmokeLesson && lesson.Page==1 && Field<TMPro.TMP_Text>(lesson,"title").text.Contains("消防知识"),"Opening the starting classroom door immediately shows fire knowledge without entering smoke or waiting for the announcement");
        Check(Field<GameObject>(lesson,"doorwayPlume").GetComponentInChildren<ParticleSystem>().particleCount>0,"Opening the door also shows visible smoke");
        Check(exposure.GraceSeconds==3 && exposure.DamagePerSecond==2f,"Saved smoke parameters match the three-second grace and 2 HP/s");
        float hp=health.HealthPercent; exposure.Tick(true,50);
        Check(lesson.DamageSuppressed && health.HealthPercent==hp,"Reading fire knowledge cannot cause smoke damage");
        lesson.Confirm(); Check(lesson.Page==1,"Fire knowledge enforces a twenty-second minimum reading period");
        lesson.Tick(1); Capture("Lesson_07_FireKnowledge",head);
        Check(Field<TMPro.TMP_Text>(lesson,"explanation").text.Contains("咳嗽") && Field<TMPro.TMP_Text>(lesson,"footer").text.Contains("一氧化碳") && Field<TMPro.TMP_Text>(lesson,"footer").text.Contains("减缓"),"Immersive fire teaching explains limited cloth protection and CO without a confusing drill disclaimer");
        yield return Advance(lesson); lesson.Tick(1);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.FindTowel && lesson.TaskAnnouncement && layout.IsFull,"Task 1 first expands as an animated reading card");
        Capture("Lesson_08_FindTowel",head);
        Check(Field<CampusDestinationHighlight>(lesson,"destination").Highlighted,"Task 1 lights up only the 411 restroom door sign");
        EnvironmentCapture("Lesson_21_Glowing411",new Vector3(-12.2f,13.1f,1.4f),new Vector3(-12.2f,12.8f,-2),lesson,movement,head);
        EnvironmentCapture("Lesson_22_FireFrom401",new Vector3(-12.2f,13.1f,1.1f),new Vector3(-2.2f,12.4f,-1.7f),lesson,movement,head);
        lesson.Tick(15); Check(lesson.TaskAnnouncement && layout.IsFull,"Task card remains expanded beyond the former twelve-second interval");
        exposure.Tick(true,20); Check(health.HealthPercent==hp,"Task announcement protects the player's reading time");
        yield return Advance(lesson); lesson.Tick(1);
        var panel=Field<GameObject>(lesson,"lessonPanel").transform; Vector3 compact=head.InverseTransformPoint(panel.position);
        Check(!lesson.MovementLocked && !layout.IsFull && Mathf.Abs(compact.x)<.01f && Mathf.Abs(compact.y)<.1f,"Unfinished task 1 stays directly ahead, rather than in an uncomfortable corner");
        Capture("Lesson_12_TaskHUD",head);
        Check(Field<CampusLessonRoute>(lesson,"route").Visible && FindObjectsOfType<LineRenderer>(true).All(l=>!l.enabled),"Task automatically shows floor chevrons with idle teleport arcs hidden");
        lesson.Tick(30); Check(lesson.CurrentPhase==CampusLessonDirector.Phase.FindTowel && panel.gameObject.activeSelf,"An unfinished task stays pinned rather than timing out");
        lesson.ClassroomDoor.SetAngle(0); lesson.Tick(.1f); Check(!lesson.DoorwayExposure.isActiveAndEnabled,"Closing the door removes its local visible exposure plume");
        lesson.ClassroomDoor.SetAngle(30);
        var towel=lesson.TargetTowel; owner.transform.position=towel.transform.position; owner.Begin(towel); lesson.Tick(.1f);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.FindTowel,"Remote towel pickup outside the restroom does not issue task 2");
        MoveHead(movement,head,new Vector3(-16.4f,13.1f,-3.3f)); lesson.Tick(.1f); owner.ProcessGrip(false,true,false);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.WetTowel && lesson.TaskAnnouncement && towel.IsHeld,"Task 1 completion expands task 2 without forcing the held towel to drop");

        Check(!lesson.MovementLocked && !lesson.WorldInteractionLocked,"Task 2 announcement does not interrupt held-towel or faucet operations");
        lesson.Tick(1); Capture("Lesson_09_WetTask",head);
        Check(!CampusMessagePresenter.Instance.IsVisible,"Task 2 does not overlap the old towel pickup message");
        yield return Advance(lesson); lesson.Tick(1);
        var faucet=FindObjectsOfType<CampusFaucet>().OrderBy(f=>Vector3.Distance(f.transform.position,towel.transform.position)).First();
        faucet.SetWater(true); owner.transform.SetPositionAndRotation(faucet.WaterVolume.transform.position-new Vector3(0,0,.065f),Quaternion.identity);
        towel.OnInteract(owner.transform); Physics.SyncTransforms(); towel.TickWetting(1.6f);
        Check(towel.IsWet,"Task 2 uses the existing actual running-water wetting interaction");
        spread.Advance(50);
        var flight=spread.Stages.First(s=>s.floor==3 && s.particles.name.StartsWith("Descending"));
        var corridor3=spread.Stages.First(s=>s.floor==3 && s.particles.name=="3F corridor x=-18");
        flight.particles.Simulate(5,true,false,true); corridor3.particles.Simulate(5,true,false,true);
        Check(flight.particles.particleCount>0 && corridor3.particles.particleCount>0 && corridor3.exposure.enabled,"By restroom departure, 4F-to-3F stairs and the 3F corridor contain actual advancing smoke");
        Capture("Lesson_20_RestroomObjective",head);
        PutAtFace(owner.transform,towel,head); lesson.Tick(1.6f);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Descend && Field<CampusLessonRoute>(lesson,"route").Visible && !layout.IsFull,"Face coverage updates the same compact task 2 to staircase guidance");
        owner.Release(); lesson.Tick(.2f); Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Descend,"Dropping the towel cannot complete the descent");
        owner.Begin(towel); MoveHead(movement,head,new Vector3(-23,9.3f,0)); PutAtFace(owner.transform,towel,head); lesson.Tick(1.6f);
        Check(lesson.CurrentPhase==CampusLessonDirector.Phase.Complete && !lesson.MovementLocked && !lesson.DamageSuppressed,"Only arriving on 3F with the held wet towel at the face completes the chapter");
        Check(FindObjectOfType<ResolutionCoordinator>().CurrentOutcome==ResolutionCoordinator.Outcome.None,"Chapter completion cannot bypass whole-building evacuation");
        Check(!panel.gameObject.activeSelf && !Field<CampusLessonRoute>(lesson,"route").Visible,"Completed tasks remove their card and route");
        owner.Release();
        Check(!Field<CampusDestinationHighlight>(lesson,"destination").Highlighted,"Restroom highlighting clears after the towel task");
        var stairGlass=FindObjectsOfType<CampusStairWindow>().First(w=>w.transform.position.x<0 && w.transform.position.y>11);
        stairGlass.Hinge.SetAngle(90);
        EnvironmentCapture("Lesson_23_StairGlassOpen",new Vector3(-23,13.1f,.3f),new Vector3(-23,13,-1.5f),lesson,movement,head);
        stairGlass.Hinge.SetAngle(0);
        yield return ThrowChecks(books[1],towel,owner,movement);
        // A real dropped rigidbody and crouched head, not a direct Begin() bypass.
        var roomFloor=new Vector3(-15,11.4f,-5);
        MoveRig(movement,roomFloor); head.localPosition=new Vector3(0,.65f,0);
        height.Recalibrate(); height.Observe(.65f,true,1,true);
        for(int side=0;side<hands.Length;side++)
        {
            foreach(var other in hands) { other.Release(); other.transform.position=Vector3.one*100; }
            towel.Body.position=roomFloor+new Vector3(0,.08f,.12f); towel.Body.rotation=Quaternion.identity;
            towel.Body.velocity=Vector3.zero; towel.Body.angularVelocity=Vector3.zero;
            yield return new WaitForSeconds(.35f);
            var reaching=hands[side]; var raw=space.InverseTransformPoint(towel.transform.position+Vector3.up*.10f);
            reaching.GetComponent<CampusTrackedHandPose>().ApplyLocalPose(raw,Quaternion.identity);
            Physics.SyncTransforms(); reaching.RefreshContact(); reaching.ProcessGrip(true,true,false);
            Check(towel.IsHeld && towel.HoldingHand==reaching.transform,"Crouching and reaching 10 cm above the dropped towel really picks it up: "+reaching.InputSource);
            Check(height.HeightOffset==0 && raw.y<.25f,"Picking up from the floor needs neither extra arm extension nor an artificial floor offset");
            reaching.ProcessGrip(false,false,true);
        }
        head.localPosition=originalHead;
        CampusMessagePresenter.Instance.Hide();
        pause.ShowPause();
        foreach(var anchor in anchors) anchor.SendMessage("Place");
        Capture("Lesson_15_PauseFront",head);
        var resume=FindObjectsOfType<VRMenuButton>().First(b=>
        {
            var click=Field<UnityEngine.Events.UnityEvent>(b,"onClick");
            return Enumerable.Range(0,click.GetPersistentEventCount()).Any(i=>click.GetPersistentMethodName(i)=="ResumeGame");
        });
        Physics.SyncTransforms();
        Vector3 buttonCenter=resume.GetComponent<Collider>().bounds.center;
        owner.transform.position=head.position+head.rotation*new Vector3(.22f,-.2f,.05f);
        owner.transform.rotation=Quaternion.LookRotation(buttonCenter-owner.transform.position);
        var menuRay=owner.GetComponent<VRMenuRayPointer>();
        menuRay.ProcessInput(false);
        Check(menuRay.IsVisible && menuRay.CurrentTarget==resume && Vector3.Distance(owner.ContactPoint,buttonCenter)>.12f,
            "A paused hand can aim at Resume beyond contact range");
        menuRay.ProcessInput(true);
        Check(Time.timeScale==1,"The menu ray trigger resumes the paused simulation");
        menuRay.ProcessInput(false); Check(!menuRay.IsVisible,"Returning to gameplay hides the dedicated menu ray");
        var view=FindObjectOfType<SystemUIView>(true);
        view.ShowGameOver(); foreach(var anchor in anchors) anchor.SendMessage("Place"); Capture("Lesson_16_FailureFront",head);
        view.ShowVictory(); foreach(var anchor in anchors) anchor.SendMessage("Place"); Capture("Lesson_17_VictoryFront",head);
        view.ShowGameplay();
        Check(FindObjectsOfType<MeshFilter>(true).All(m=>m.sharedMesh!=null),"All story and practice meshes retain saved geometry");
        Check(FindObjectsOfType<CampusLessonDirector>(true).Length==1 && FindObjectsOfType<Canvas>(true).Count(c=>c.name=="Seating fade")==1 && FindObjectsOfType<Transform>(true).Count(t=>t.name=="Lesson contact confirmation")==0,"Repeated scene application leaves one lesson, one fade and no blue confirmation object");
    }
    private void EnvironmentCapture(string name,Vector3 position,Vector3 look,CampusLessonDirector lesson,CampusLocomotion movement,Transform head)
    {
        Vector3 rigPosition=movement.transform.position; Quaternion headRotation=head.localRotation;
        var panel=Field<GameObject>(lesson,"lessonPanel"); var hud=Field<GameObject>(lesson,"gameplayHUD");
        bool panelVisible=panel.activeSelf,hudVisible=hud.activeSelf;
        panel.SetActive(false); hud.SetActive(false);
        MoveHead(movement,head,position); head.rotation=Quaternion.LookRotation(look-position);
        Field<CampusLessonRoute>(lesson,"route").Tick(head); Capture(name,head);
        MoveRig(movement,rigPosition); head.localRotation=headRotation; panel.SetActive(panelVisible); hud.SetActive(hudVisible);
    }
    private IEnumerator ThrowChecks(CampusPracticeBook book,CampusTowelInteractable towel,CampusHandInteractor hand,CampusLocomotion movement)
    {
        var motion=hand.GetComponent<CampusThrowMotion>(); motion.enabled=false;
        Vector3 direction=movement.transform.forward;
        hand.transform.position=new Vector3(-15,12.8f,6); hand.Begin(book);
        float now=Time.unscaledTime;
        for(int i=0;i<7;i++) motion.Observe(new Vector3(0,1.4f,.1f+i*.03f),Quaternion.Euler(0,i*3,0),now-.06f+i*.01f);
        Vector3 start=book.transform.position; hand.ProcessGrip(false,false,true);
        Check(Vector3.Dot(book.GetComponent<Rigidbody>().velocity,direction)>2.8f && book.GetComponent<Rigidbody>().angularVelocity.magnitude>1,"Natural release inherits sampled linear and angular hand motion");
        yield return new WaitForSeconds(.22f);
        Check(Vector3.Dot(book.transform.position-start,direction)>.35f,"Thrown book actually travels under Unity physics, not only a stored velocity");
        hand.transform.position=new Vector3(-15,12.8f,6); hand.Begin(towel); now=Time.unscaledTime;
        for(int i=0;i<7;i++) motion.Observe(new Vector3(0,1.4f,.1f+i*.09f),Quaternion.identity,now-.06f+i*.01f);
        hand.ProcessGrip(false,false,true);
        Check(towel.Body.velocity.magnitude>5 && towel.Body.velocity.magnitude<=7.01f && towel.IsWet,"Fast towel throw is bounded and retains its wet state");
        hand.Begin(book); now=Time.unscaledTime; motion.Observe(Vector3.zero,Quaternion.identity,now-.06f); motion.Observe(Vector3.forward*.18f,Quaternion.identity,now);
        hand.Release();
        Check(book.GetComponent<Rigidbody>().velocity==Vector3.zero,"System/pause release drops safely without throwing");
        motion.ResetHistory(); now=Time.unscaledTime;
        motion.Observe(Vector3.zero,Quaternion.identity,now-.4f); motion.Observe(Vector3.forward*.05f,Quaternion.identity,now-.3f);
        Check(!motion.GetVelocity(now,out _,out _),"Stale or lost tracking cannot launch an object");
        motion.ResetHistory(); motion.Observe(Vector3.zero,Quaternion.identity,now-.03f); motion.Observe(Vector3.one*3,Quaternion.identity,now);
        Check(!motion.GetVelocity(now,out _,out _),"A tracking recenter discontinuity is rejected rather than converted to a throw");
        motion.ResetHistory(); Vector3 position=movement.transform.position; Quaternion rotation=movement.transform.rotation;
        for(int i=0;i<4;i++)
        {
            movement.transform.SetPositionAndRotation(position+Vector3.right*i,rotation*Quaternion.Euler(0,i*10,0));
            hand.transform.position=movement.transform.TransformPoint(new Vector3(.2f,1.3f,.4f));
            motion.Observe(movement.transform.InverseTransformPoint(hand.transform.position),Quaternion.identity,now-.03f+i*.01f);
        }
        motion.GetVelocity(now,out var linear,out _); Check(linear.magnitude<.01f,"Artificial body walking/rotation never contributes to throwing speed");
        movement.transform.SetPositionAndRotation(position,rotation); motion.ResetHistory(); book.ResetBook(); motion.enabled=true;
    }
    private void MoveRig(CampusLocomotion movement,Vector3 p)
    { bool active=movement.Body.enabled; movement.Body.enabled=false; movement.transform.position=p; movement.Body.enabled=active; Physics.SyncTransforms(); }
    private void MoveHead(CampusLocomotion movement,Transform head,Vector3 p) { MoveRig(movement,movement.transform.position+p-head.position); }
    private void PutAtFace(Transform hand,CampusTowelInteractable towel,Transform head)
    { hand.position=head.TransformPoint(new Vector3(0,-.13f,.13f))-hand.rotation*new Vector3(0,0,.065f); towel.OnInteract(hand); Physics.SyncTransforms(); }
    private static void Capture(string name,Transform head)
    { Type.GetType("CampusPreview, Assembly-CSharp-Editor").GetMethod("CaptureLesson",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{name,head}); }
}
#endif
