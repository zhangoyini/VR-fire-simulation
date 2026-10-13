using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

// OpenXR/Quest feature input, with the existing SteamVR actions as a fallback.
public static class CampusControllerInput
{
    public static XRNode Node(SteamVR_Input_Sources source) => source==SteamVR_Input_Sources.LeftHand?XRNode.LeftHand:XRNode.RightHand;
    public static bool TryPose(XRNode node,out Vector3 position,out Quaternion rotation)
    {
        position=Vector3.zero; rotation=Quaternion.identity;
        var device=InputDevices.GetDeviceAtXRNode(node);
        return device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked &&
            device.TryGetFeatureValue(CommonUsages.devicePosition,out position) && device.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation);
    }
    public static bool Trigger(SteamVR_Input_Sources source)
    {
        var device=InputDevices.GetDeviceAtXRNode(Node(source));
        if(device.isValid && device.TryGetFeatureValue(CommonUsages.trigger,out float value)) return value>=.55f;
        return SteamVR.active && SteamVR_Input.GetBooleanAction("GrabPinch").GetState(source);
    }
    public static bool Confirm()
    {
        if(SteamVR.active) return SteamVR_Input.GetBooleanAction("LessonConfirm").GetState(SteamVR_Input_Sources.RightHand);
        var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if(device.isValid && device.TryGetFeatureValue(CommonUsages.primaryButton,out bool pressed)) return pressed;
        return false;
    }
    public static Vector2 Walk()
    {
        var device=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if(device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 axis)) return axis;
        return SteamVR.active?SteamVR_Input.GetVector2Action("Walk").GetAxis(SteamVR_Input_Sources.LeftHand):Vector2.zero;
    }
    public static bool Pause()
    {
        var device=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if(device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool pressed)) return pressed;
        return SteamVR.active && SteamVR_Input.GetBooleanAction("Pause").GetState(SteamVR_Input_Sources.LeftHand);
    }
    public static bool HardwarePresent => TryPose(XRNode.Head,out _,out _) || SteamVR.active;
    public static void Haptic(SteamVR_Input_Sources source,float amplitude)
    {
        var device=InputDevices.GetDeviceAtXRNode(Node(source));
        if(device.isValid && device.TryGetHapticCapabilities(out var capability) && capability.supportsImpulse)
        { device.SendHapticImpulse(0,amplitude,.035f); return; }
        if(SteamVR.active) SteamVR_Input.GetVibrationAction("Haptic").Execute(0,.035f,100,amplitude,source);
    }
}
