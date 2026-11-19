using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampusManualTutorialRun
{
    static CampusManualTutorialRun()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Campus.ManualTutorialQA", false)) return;
            SessionState.SetBool("Campus.ManualTutorialQA", false);
            new GameObject("Manual tutorial validation").AddComponent<CampusManualTutorialDriver>();
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Campus/CampusTraining.unity");
        foreach (var script in Object.FindObjectsOfType<MonoBehaviour>(true))
            if (script != null && ((script.GetType().Namespace ?? "").StartsWith("Valve.VR") || script is CampusLocomotion || script is CampusHandInteractor || script is DummyController || script is CampusTrackingHeight || script is CampusTrackedHeadPose || script is CampusTrackedHandPose || script is FireSafetyPrefabSupport)) script.enabled = false;
        SessionState.SetBool("Campus.ManualTutorialQA", true);
        EditorApplication.isPlaying = true;
    }
}
