using System.Collections.Generic;
using UnityEngine;

public static class CampusTeamRuntime
{
    private static readonly HashSet<string> available = new HashSet<string>();
    public static bool Any => available.Count > 0;
    public static bool Complete => available.Count == 5;
    public static bool Has(string module) => available.Contains(module);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { available.Clear(); }

    public static void Load()
    {
        available.Clear();
        foreach (var profile in Resources.LoadAll<CampusTeamModuleProfile>("CampusTeamModules"))
            if (profile != null && profile.integrated && profile.module != null && profile.module.Length == 1 && "ABCDE".Contains(profile.module)) available.Add(profile.module);
    }

    public static string Owner(MonoBehaviour behaviour)
    {
        if (behaviour == null) return null;
        string name = behaviour.GetType().Name;
        switch (name)
        {
            case "CampusLocomotion": case "CampusComfortVignette": case "CampusPracticeBook": case "CampusThrowMotion":
                return "A";
            case "CampusHingeInteractable": case "CampusElevatorButton": case "CampusStairWindow": case "CampusRefugeCall":
            case "DoorInteraction": case "DoorProximityTrigger": case "RuntimeFireWindow": case "FireSafetyElevator":
            case "FireSafetyPrefabSupport": case "VRFireSafetyInteraction": case "Door_Rotation": case "Window_Reminder":
                return "B";
            case "CampusFaucet": case "CampusTowelInteractable": case "TowelPickup": case "PickableTowel":
                return "C";
            case "CampusFireFlicker": case "CampusSmokeSpread": case "CampusSmokeExposure": case "CampusSmokeVolume":
            case "HealthTimeController": case "HealthTimeBar": case "AudioManager": case "SmokeZone": case "FailureUI":
                return "D";
            case "CampusLessonDirector": case "CampusLessonAnimation": case "CampusLessonLayout": case "CampusLessonRoute":
            case "CampusDestinationHighlight": case "CampusTeachingHand": case "CampusMinimap": case "CampusExperience":
            case "CampusRouteCue": case "CampusMessagePresenter": case "VRMenuButton": case "VRMenuRayPointer":
            case "SystemUIPauseController": case "SystemUINavigation": case "AssemblyOutcomeController":
            case "SafeZoneEndpoint": case "GeoFence": case "JourneyDirector": case "NotableCue":
                return "E";
            default: return null;
        }
    }

    public static bool TargetAvailable(MonoBehaviour target)
    {
        if (Complete) return true;
        string owner = Owner(target);
        return owner == null || Has(owner);
    }
}
