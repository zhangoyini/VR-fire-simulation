using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valve.VR;

// A targeted scene upgrade: preserve the hand-edited layout and all lesson/interaction bindings.
public static class CampusTeleportSetup
{
    [MenuItem("Tools/Campus/Apply 3m Teleport (keeps scene layout)")]
    public static void Apply()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(CampusSceneBuilder.ScenePath);
        ConfigureScene();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        if(!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))
            throw new InvalidOperationException("Cannot save campus teleport configuration.");
        AssetDatabase.SaveAssets();
        Debug.Log("CAMPUS_TELEPORT_APPLIED: two hands, 3m, no footsteps; layout preserved.");
    }

    public static void ConfigureScene()
    {
        var movement=UnityEngine.Object.FindObjectOfType<CampusLocomotion>(true);
        if(movement==null) throw new InvalidOperationException("Campus player is missing.");
        movement.transform.root.gameObject.SetActive(true);
        var movementSettings=new SerializedObject(movement);
        var head=movementSettings.FindProperty("trackedHead").objectReferenceValue as Transform;
        if(head==null) throw new InvalidOperationException("Tracked head is missing.");
        movementSettings.FindProperty("teleportOnly").boolValue=true;
        var footsteps=movementSettings.FindProperty("footsteps").objectReferenceValue as AudioSource;
        if(footsteps!=null) { footsteps.Stop(); footsteps.enabled=false; EditorUtility.SetDirty(footsteps); }
        movementSettings.FindProperty("footsteps").objectReferenceValue=null;
        movementSettings.FindProperty("footstepClips").arraySize=0;
        movementSettings.ApplyModifiedPropertiesWithoutUndo();
        var hands=UnityEngine.Object.FindObjectsOfType<CampusHandInteractor>(true);
        if(hands.Length!=2) throw new InvalidOperationException("Expected two campus hands.");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Interaction/Resources/FireSafetyRay.mat");
        if(material==null) throw new InvalidOperationException("Teleport line material is missing.");
        var pauseController=UnityEngine.Object.FindObjectOfType<SystemUIPauseController>(true);
        var view=UnityEngine.Object.FindObjectOfType<SystemUIView>(true);
        if(pauseController==null || view==null) throw new InvalidOperationException("Campus menus are missing.");
        var pauseSettings=new SerializedObject(pauseController);
        var viewSettings=new SerializedObject(view);
        var menuPanels=new[]{
            pauseSettings.FindProperty("pausePanel").objectReferenceValue as GameObject,
            viewSettings.FindProperty("gameOverPanel").objectReferenceValue as GameObject,
            viewSettings.FindProperty("victoryPanel").objectReferenceValue as GameObject};
        if(menuPanels.Any(panel=>panel==null)) throw new InvalidOperationException("Pause/outcome menu is missing.");
        var buttons=menuPanels.SelectMany(panel=>panel.GetComponentsInChildren<VRMenuButton>(true)).Distinct().ToArray();
        var menuShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/SystemUI/VRMenuLaser.shader");
        if(menuShader==null || buttons.Length==0) throw new InvalidOperationException("Menu ray resources are missing.");
        foreach(var hand in hands)
        {
            var teleport=hand.GetComponent<VRTeleporter>() ?? hand.gameObject.AddComponent<VRTeleporter>();
            var settings=new SerializedObject(teleport);
            settings.FindProperty("playerRoot").objectReferenceValue=movement.transform;
            settings.FindProperty("vrCamera").objectReferenceValue=head;
            settings.FindProperty("inputSource").intValue=(int)hand.InputSource;
            settings.FindProperty("teleportLayer").intValue=~0;
            settings.FindProperty("maxTeleportDistance").floatValue=3;
            settings.FindProperty("maxHeightChange").floatValue=2;
            settings.FindProperty("useCampusInput").boolValue=true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var line=teleport.GetComponent<LineRenderer>();
            line.sharedMaterial=material; line.useWorldSpace=true;
            line.startWidth=.012f; line.endWidth=.006f; line.enabled=false;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            EditorUtility.SetDirty(line);
            var pointer=hand.GetComponent<VRMenuRayPointer>() ?? hand.gameObject.AddComponent<VRMenuRayPointer>();
            var raySettings=new SerializedObject(pointer);
            raySettings.FindProperty("trackedController").objectReferenceValue=hand.transform;
            raySettings.FindProperty("playerRoot").objectReferenceValue=movement.transform;
            raySettings.FindProperty("inputSource").intValue=(int)hand.InputSource;
            raySettings.FindProperty("useCampusInput").boolValue=true;
            raySettings.FindProperty("maxDistance").floatValue=4;
            raySettings.FindProperty("downwardPitchDegrees").floatValue=0;
            raySettings.FindProperty("laserShader").objectReferenceValue=menuShader;
            var registered=raySettings.FindProperty("menuButtons"); registered.arraySize=buttons.Length;
            for(int i=0;i<buttons.Length;i++) registered.GetArrayElementAtIndex(i).objectReferenceValue=buttons[i];
            raySettings.ApplyModifiedPropertiesWithoutUndo();
        }
        int marked=0;
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if(collider.isTrigger || !IsWalkableSurface(collider.name)) continue;
            if(collider.GetComponent<TeleportArea>()==null) collider.gameObject.AddComponent<TeleportArea>();
            marked++;
        }
        if(marked<140) throw new InvalidOperationException("Campus floor/stair teleport coverage is incomplete: "+marked);
        var pause=UnityEngine.Object.FindObjectOfType<SystemUIPauseController>(true);
        if(pause!=null)
        {
            var settings=new SerializedObject(pause);
            settings.FindProperty("teleport").objectReferenceValue=hands.First(h=>h.InputSource==SteamVR_Input_Sources.RightHand).GetComponent<VRTeleporter>();
            var additional=settings.FindProperty("additionalTeleports"); additional.arraySize=1;
            additional.GetArrayElementAtIndex(0).objectReferenceValue=hands.First(h=>h.InputSource==SteamVR_Input_Sources.LeftHand).GetComponent<VRTeleporter>();
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log("CAMPUS_TELEPORT_SURFACES: "+marked);
    }

    private static bool IsWalkableSurface(string name)
    {
        return name=="Continuous floor slab" || name=="Flush corridor landing join" ||
            name.StartsWith("Full landing ") || name=="Half landing" ||
            name.StartsWith("Up flight ") || name.StartsWith("Return flight ") ||
            name=="Front evacuation plaza" || name=="Rear maintenance path" ||
            name=="Side walkway" || name=="Assembly court";
    }
}
