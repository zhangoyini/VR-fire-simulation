#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class CampusManualTutorialDriver : MonoBehaviour
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Check()); }
    private IEnumerator Check()
    {
        yield return null;
        var lesson = CampusLessonDirector.Instance;
        lesson.ManualTickForChecks = true;
        int pages = 0;
        while (lesson.CurrentPhase == CampusLessonDirector.Phase.Controls && pages < 45)
        {
            var tutorial = Field<CampusClassroomTutorial>(lesson, "tutorial");
            var step = tutorial.CurrentStep;
            var view = Field<CampusTutorialView>(tutorial, "view");
            if (step == CampusClassroomTutorial.Step.FadePrompt || step == CampusClassroomTutorial.Step.MapFolding || step == CampusClassroomTutorial.Step.TaskFolding)
            { lesson.Tick(1, false, false); continue; }
            lesson.Tick(60, false, false);
            if (tutorial.CurrentStep != step) { Fail("Page advanced automatically: " + step); yield break; }
            if (step == CampusClassroomTutorial.Step.MapOpen || step == CampusClassroomTutorial.Step.TaskOpen)
            {
                lesson.Tick(.6f, step == CampusClassroomTutorial.Step.MapOpen, step == CampusClassroomTutorial.Step.TaskOpen);
                if (tutorial.CurrentStep != step || !Field<CampusNavigationPanels>(lesson,"panels").IsExpanded)
                { Fail("Map/task input must work without changing the manual page: " + step); yield break; }
                lesson.Tick(.1f, false, false);
            }
            var hud = Field<Transform>(view, "hud");
            var skip = hud.Find("Manual tutorial advance/Tutorial skip").GetComponent<VRMenuButton>();
            if (!skip.isActiveAndEnabled) { Fail("SKIP unavailable on " + step); yield break; }
            foreach (var text in hud.GetComponentsInChildren<TMPro.TMP_Text>())
            { text.ForceMeshUpdate(); if (text.isTextOverflowing) { Fail("Overflow: " + text.text); yield break; } }
            var prompt = Field<RectTransform>(view, "promptBackground");
            var skipRect = skip.GetComponent<RectTransform>();
            var cardCorners = new Vector3[4]; var skipCorners = new Vector3[4];
            prompt.GetWorldCorners(cardCorners); skipRect.GetWorldCorners(skipCorners);
            var head = Camera.main.transform;
            if (head.InverseTransformPoint(skipCorners[1]).y >= head.InverseTransformPoint(cardCorners[0]).y)
            { Fail("SKIP overlaps the instruction on " + step); yield break; }
            string capture = step == CampusClassroomTutorial.Step.Introduction ? "Tutorial_Manual_Intro" :
                step == CampusClassroomTutorial.Step.TeleportDemo ? "Tutorial_Manual_Controller" :
                step == CampusClassroomTutorial.Step.WindowDemo ? "Tutorial_Manual_Window" : null;
            if (capture != null)
                Type.GetType("CampusPreview, Assembly-CSharp-Editor").GetMethod("CaptureLesson", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { capture, head });
            skip.OnInteractStart(Camera.main.transform);
            lesson.Tick(.02f, false, false);
            if (lesson.CurrentPhase == CampusLessonDirector.Phase.Controls && tutorial.CurrentStep == step)
            { Fail("SKIP did not advance " + step); yield break; }
            pages++;
            yield return null;
        }
        if (lesson.CurrentPhase != CampusLessonDirector.Phase.Classroom) { Fail("Manual tutorial did not finish"); yield break; }
        var previous = lesson;
        FindObjectOfType<SystemUINavigation>().RestartGame();
        float deadline = Time.realtimeSinceStartup + 45;
        while ((CampusLessonDirector.Instance == null || CampusLessonDirector.Instance == previous) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        lesson = CampusLessonDirector.Instance;
        if (lesson == null || lesson == previous || lesson.CurrentPhase != CampusLessonDirector.Phase.Controls)
        { Fail("Restart did not restore manual tutorial"); yield break; }
        lesson.ManualTickForChecks = true;
        lesson.Tick(60, false, false);
        if (Field<CampusClassroomTutorial>(lesson,"tutorial").CurrentStep != CampusClassroomTutorial.Step.Introduction)
        { Fail("Restart tutorial auto-advanced"); yield break; }
        Debug.Log("MANUAL_TUTORIAL_PASS: " + pages + " visible pages wait for SKIP; restart returns to manual Introduction.");
        EditorApplication.Exit(0);
    }
    private static void Fail(string reason) { Debug.LogError("MANUAL_TUTORIAL_FAIL: " + reason); EditorApplication.Exit(1); }
}
#endif
