using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampusLessonChecks
{
    static CampusLessonChecks()
    {
        EditorApplication.playModeStateChanged+=state=>
        { if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Campus.LessonQA",false)) new GameObject("Lesson QA (temporary)").AddComponent<CampusLessonDriver>(); };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        foreach(var script in Object.FindObjectsOfType<MonoBehaviour>(true))
            if(script!=null && ((script.GetType().Namespace??"").StartsWith("Valve.VR") || script is CampusLocomotion || script is CampusHandInteractor || script is DummyController || script is CampusTrackingHeight || script is CampusTrackedHeadPose || script is CampusTrackedHandPose || script is FireSafetyPrefabSupport)) script.enabled=false;
        SessionState.SetBool("Campus.LessonQA",true); EditorApplication.isPlaying=true;
    }
}
