using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jiao.HealthTime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

[InitializeOnLoad]
public static class CampusRevisionChecks
{
    private const string Flag="Campus.RevisionQA";
    static CampusRevisionChecks()
    { EditorApplication.playModeStateChanged+=state=> { if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag,false)) new GameObject("Revision QA (temporary)").AddComponent<CampusRevisionDriver>(); }; }
    public static void Run()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        var lesson=UnityEngine.Object.FindObjectOfType<CampusLessonDirector>(true);
        if(lesson!=null) lesson.gameObject.SetActive(false); // Independent free-play regression, not a skip-to-win input.
        foreach(var script in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            if(script!=null && ((script.GetType().Namespace??"").StartsWith("Valve.VR") || script is CampusLocomotion || script is CampusHandInteractor || script is DummyController || script is CampusTrackingHeight || script is CampusTrackedHeadPose || script is CampusTrackedHandPose || script is FireSafetyPrefabSupport)) script.enabled=false;
        SessionState.SetBool(Flag,true); EditorApplication.isPlaying=true;
    }
    internal static void Finish(bool pass,string report)
    { SessionState.SetBool(Flag,false); File.WriteAllText("Documentation/RevisionChecks.txt",report); Debug.Log((pass?"CAMPUS_REVISION_PASS":"CAMPUS_REVISION_FAIL")+"\n"+report); EditorApplication.Exit(pass?0:1); }
}
