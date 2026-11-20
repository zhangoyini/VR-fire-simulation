#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Step = CampusClassroomTutorial.Step;

public sealed class CampusManualTutorialDriver : MonoBehaviour
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Check()); }
    private static void Enter(CampusClassroomTutorial tutorial, Step step)
    {
        tutorial.GetType().GetMethod("Enter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tutorial,
            new object[] { step, "操作练习 / Practice", null, CampusTutorialView.Demo.None, null, false });
    }
    private IEnumerator Check()
    {
        yield return null;
        var lesson = CampusLessonDirector.Instance; lesson.ManualTickForChecks = true;
        var tutorial = Field<CampusClassroomTutorial>(lesson,"tutorial");
        var view = Field<CampusTutorialView>(tutorial,"view");
        var hud = Field<Transform>(view,"hud");
        var skip = hud.Find("Manual tutorial advance/Tutorial skip").GetComponent<VRMenuButton>();
        int reading = 0, actions = 0;
        foreach (Step step in Enum.GetValues(typeof(Step)))
        {
            if (!CampusClassroomTutorial.CanSkipStep(step) || step == Step.Complete) continue;
            Enter(tutorial,step); lesson.Tick(60,false,false);
            if (tutorial.CurrentStep != step || !skip.isActiveAndEnabled) { Fail("Reading page timed out or missing SKIP: " + step); yield break; }
            skip.OnInteractStart(Camera.main.transform); lesson.Tick(.02f,false,false);
            if (tutorial.CurrentStep == step) { Fail("Reading SKIP did not advance: " + step); yield break; }
            reading++; yield return null;
        }
        var book = Field<CampusPracticeBook>(tutorial,"book");
        foreach (var step in new[] { Step.LookAround, Step.LookDown, Step.MoveHands, Step.MenuPractice, Step.MenuResume,
            Step.FirstLanding, Step.ToWindow, Step.OpenWindow, Step.ToButton, Step.PressButton, Step.ToBook,
            Step.PickUp, Step.PutBack, Step.MapOpen, Step.MapClose, Step.TaskOpen, Step.TaskClose, Step.ToDoor })
        {
            book.ResetBook();
            if (step == Step.PutBack) book.transform.position += Vector3.up * .5f;
            Enter(tutorial,step); tutorial.RequestAdvance(); lesson.Tick(60,false,false);
            if (tutorial.CurrentStep != step || skip.isActiveAndEnabled) { Fail("Incomplete action was bypassed by SKIP/time: " + step); yield break; }
            actions++; yield return null;
        }
        Enter(tutorial,Step.MapOpen); lesson.Tick(.1f,false,false); lesson.Tick(.1f,true,false);
        if (tutorial.CurrentStep != Step.MapClose) { Fail("Real X action did not advance map practice"); yield break; }
        lesson.Tick(.1f,false,false); lesson.Tick(.1f,true,false);
        if (tutorial.CurrentStep != Step.MapFolding) { Fail("Second X did not close map practice"); yield break; }
        Enter(tutorial,Step.TaskOpen); lesson.Tick(.1f,false,false); lesson.Tick(.1f,false,true);
        if (tutorial.CurrentStep != Step.TaskClose) { Fail("Real Y action did not advance task practice"); yield break; }
        Enter(tutorial,Step.OpenWindow); Field<CampusHingeInteractable>(tutorial,"window").SetAngle(1);
        lesson.Tick(.1f,false,false);
        if (tutorial.CurrentStep != Step.WindowDone) { Fail("Actual open window did not complete the action"); yield break; }
        Field<CampusHingeInteractable>(tutorial,"window").SetAngle(0);
        Enter(tutorial,Step.Introduction); lesson.Tick(1,false,false);
        Type.GetType("CampusPreview, Assembly-CSharp-Editor").GetMethod("CaptureLesson").Invoke(null,
            new object[] { "Tutorial_SkipReadingOnly", Camera.main.transform });
        var previous=lesson; FindObjectOfType<SystemUINavigation>().RestartGame();
        float deadline=Time.realtimeSinceStartup+45;
        while ((CampusLessonDirector.Instance==null || CampusLessonDirector.Instance==previous) && Time.realtimeSinceStartup<deadline) yield return null;
        yield return null;
        lesson=CampusLessonDirector.Instance;
        if (lesson==null || lesson==previous || lesson.CurrentPhase!=CampusLessonDirector.Phase.Controls) { Fail("Restart did not restore tutorial"); yield break; }
        lesson.ManualTickForChecks=true; lesson.Tick(60,false,false);
        if (Field<CampusClassroomTutorial>(lesson,"tutorial").CurrentStep!=Step.Introduction) { Fail("Restart reading auto-advanced"); yield break; }
        Debug.Log("TUTORIAL_READING_SKIP_PASS: " + reading + " reading pages wait/skip; " + actions + " incomplete action pages cannot skip; real X/Y/window actions progress; restart retains tutorial.");
        EditorApplication.Exit(0);
    }
    private static void Fail(string reason) { Debug.LogError("TUTORIAL_READING_SKIP_FAIL: " + reason); EditorApplication.Exit(1); }
}
#endif
