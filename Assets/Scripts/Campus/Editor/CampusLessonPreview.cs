using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class CampusPreview
{
    public static void CaptureLesson(string name,Transform head)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Documentation/Previews");
        var layout=Object.FindObjectOfType<CampusLessonLayout>();
        typeof(CampusLessonLayout).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(layout,null);
        foreach(var anchor in Object.FindObjectsOfType<WorldSpaceUIAnchor>()) anchor.SendMessage("Place");
        // Manual test steps can activate the HUD and damage health within one frame,
        // before its normal Start/Update callbacks. Refresh those actual view methods.
        var experience=Object.FindObjectOfType<CampusExperience>(true); experience.SendMessage("Update");
        var health=Jiao.HealthTime.HealthTimeController.Instance;
        var bar=Object.FindObjectOfType<Jiao.HealthTime.HealthTimeBar>(true);
        if(bar!=null && health!=null) bar.SendMessage("OnHealthChanged",health.HealthPercent);
        var cameraObject=new GameObject("Temporary lesson capture"); var camera=cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>(); camera.nearClipPlane=.05f; camera.farClipPlane=150; camera.fieldOfView=80;
        try
        {
            Shot(camera,name,head.position,head.position+head.forward*2);
            foreach(var text in layout.GetComponentsInChildren<TMPro.TMP_Text>(true))
            { text.ForceMeshUpdate(); if(text.isTextOverflowing) throw new System.InvalidOperationException("Lesson text overflow: "+text.name); }
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }
}
