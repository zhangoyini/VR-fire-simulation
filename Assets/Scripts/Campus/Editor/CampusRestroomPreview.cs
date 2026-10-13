using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static partial class CampusPreview
{
    public static void VerifyRestroomAndInteractions()
    {
        RenderRestroomInteraction();
        CampusRevisionChecks.Run();
    }
    // Render actual project geometry/materials; temporary poses are never saved.
    public static void RenderRestroomInteraction()
    {
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        CampusSceneBuilder.Validate();
        var systems=GameObject.Find("05 Player & Shared Systems");
        var head=systems.GetComponentInChildren<Camera>(true).transform;
        systems.SetActive(false);
        foreach(var particle in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
            particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Directory.CreateDirectory("Documentation/Previews");
        var cameraObject=new GameObject("Temporary restroom preview camera");
        var camera=cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraObject.tag="MainCamera"; camera.nearClipPlane=.05f; camera.farClipPlane=150; camera.fieldOfView=65;
        var faucet=UnityEngine.Object.FindObjectsOfType<CampusFaucet>().OrderByDescending(f=>f.transform.position.y).ThenBy(f=>f.transform.position.x).First();
        var towel=UnityEngine.Object.FindObjectsOfType<CampusTowelInteractable>().OrderByDescending(t=>t.transform.position.y).First();
        var hand=new GameObject("Temporary towel preview hand").transform;
        try
        {
            Shot(camera,"RestroomFixtures_4F",new Vector3(-15.7f,13.0f,-4.35f),new Vector3(-17.7f,12.35f,-2.5f));
            Shot(camera,"Restroom_DryTowel",new Vector3(-16.1f,12.95f,-3.9f),new Vector3(-16.8f,12.35f,-2.55f));
            InvokeRestroomLifecycle(faucet,"Awake"); InvokeRestroomLifecycle(faucet,"OnEnable");
            InvokeRestroomLifecycle(towel,"Awake"); InvokeRestroomLifecycle(towel,"OnEnable");
            faucet.SetWater(true);
            foreach(var particle in faucet.FlowVisual.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(.25f,true,true,true);
            Shot(camera,"Restroom_RunningWater",new Vector3(-18.05f,12.82f,-3.6f),new Vector3(-18.8f,12.35f,-2.64f));
            hand.position=faucet.WaterVolume.transform.position-new Vector3(0,0,.065f);
            towel.OnInteractStart(hand); Physics.SyncTransforms(); towel.TickWetting(1.6f);
            if(!towel.IsWet) throw new InvalidOperationException("Preview cloth failed to become wet under running water.");
            Shot(camera,"Restroom_WetTowel",new Vector3(-18.05f,12.82f,-3.6f),new Vector3(-18.8f,12.35f,-2.64f));
            systems.SetActive(true);
            foreach(var c in systems.GetComponentsInChildren<Camera>(true)) c.enabled=false;
            hand.position=head.TransformPoint(new Vector3(0,-.13f,.13f))-new Vector3(0,0,.065f);
            towel.OnInteract(hand); Physics.SyncTransforms();
            InvokeRestroomLifecycle(systems.GetComponentInChildren<CampusExperience>(true),"Update");
            systems.GetComponentInChildren<SystemUIView>(true).ShowGameplay(); PlaceUI(systems);
            camera.fieldOfView=80;
            Shot(camera,"WetTowel_GameplayHUD",head.position,head.position+head.forward*2);
            var label=(TMPro.TMP_Text)typeof(CampusExperience).GetField("floorLabel",BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(systems.GetComponentInChildren<CampusExperience>(true));
            label.ForceMeshUpdate();
            if(!label.text.Contains("Wet towel") || label.isTextOverflowing)
                throw new InvalidOperationException("Wet-towel HUD missing or overflowing: "+label.text);
            Debug.Log("RESTROOM_RENDER_PASS (dry cloth, open basin, running tap, wet cloth and bilingual protection HUD)");
        }
        finally
        {
            towel.OnInteractEnd(hand); faucet.SetWater(false);
            InvokeRestroomLifecycle(towel,"OnDisable"); InvokeRestroomLifecycle(faucet,"OnDisable");
            UnityEngine.Object.DestroyImmediate(hand.gameObject); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
    private static void InvokeRestroomLifecycle(MonoBehaviour component,string method)
    { component.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(component,null); }
}
