using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FloorManager : MonoBehaviour
{
    [Serializable]
    private class FloorVisibilityProfile
    {
        [SerializeField] private int floorNumber;
        [SerializeField] private GameObject[] visibleObjects;

        public int FloorNumber => floorNumber;
        public GameObject[] VisibleObjects => visibleObjects;
    }

    private static readonly List<FloorManager> ActiveManagers = new List<FloorManager>();
    private static int? sharedCurrentFloor;
    private static float nextTransitionTime;

    [Header("Floor State")]
    [SerializeField] private int currentFloor = 4;
    [SerializeField] private int minimumFloor = 1;
    [SerializeField] private int maximumFloor = 4;

    [Header("Floor Display")]
    [SerializeField] private TMP_Text floorText;

    [Header("Visibility Configuration")]
    [Tooltip("Every object controlled by this manager. Each floor profile turns these objects off, then turns its Visible Objects on.")]
    [SerializeField] private GameObject[] managedObjects;
    [Tooltip("For each floor, list the objects that should be visible. Add newly managed objects here in the Inspector; do not Destroy them.")]
    [SerializeField] private FloorVisibilityProfile[] floorProfiles;

    private const float TransitionCooldownSeconds = 0.75f;

    public int CurrentFloor => currentFloor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedState()
    {
        ActiveManagers.Clear();
        sharedCurrentFloor = null;
        nextTransitionTime = 0f;
    }

    private void OnEnable()
    {
        ActiveManagers.RemoveAll(manager => manager == null);

        if (!ActiveManagers.Contains(this))
        {
            ActiveManagers.Add(this);
        }

        if (sharedCurrentFloor.HasValue)
        {
            currentFloor = sharedCurrentFloor.Value;
        }
        else
        {
            sharedCurrentFloor = currentFloor;
        }

        ApplyFloorConfiguration();
    }

    private void OnDisable()
    {
        ActiveManagers.Remove(this);
        ActiveManagers.RemoveAll(manager => manager == null);

        if (ActiveManagers.Count == 0)
        {
            sharedCurrentFloor = null;
            nextTransitionTime = 0f;
        }
    }

    internal bool TryTransition(int destinationFloor, Transform target, Transform playerRig)
    {
        if (Time.time < nextTransitionTime)
        {
            return false;
        }

        if (destinationFloor < minimumFloor || destinationFloor > maximumFloor)
        {
            Debug.LogWarning($"Floor {destinationFloor} is outside the configured range {minimumFloor}-{maximumFloor}.", this);
            return false;
        }

        if (target == null || playerRig == null)
        {
            Debug.LogWarning("Assign both a landing target and Player Rig to this stair route.", this);
            return false;
        }

        ActiveManagers.RemoveAll(manager => manager == null);
        foreach (FloorManager manager in ActiveManagers)
        {
            if (!manager.HasProfile(destinationFloor))
            {
                Debug.LogWarning($"FloorManager '{manager.name}' has no visibility profile for floor {destinationFloor}; transition was cancelled.", manager);
                return false;
            }
        }

        playerRig.SetPositionAndRotation(target.position, target.rotation);
        sharedCurrentFloor = destinationFloor;
        nextTransitionTime = Time.time + TransitionCooldownSeconds;

        foreach (FloorManager manager in ActiveManagers)
        {
            manager.currentFloor = destinationFloor;
            manager.ApplyFloorConfiguration();
        }

        Debug.Log("Current Floor: " + destinationFloor, this);
        return true;
    }

    private bool HasProfile(int floorNumber)
    {
        if (floorProfiles == null)
        {
            return false;
        }

        foreach (FloorVisibilityProfile profile in floorProfiles)
        {
            if (profile != null && profile.FloorNumber == floorNumber)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyFloorConfiguration()
    {
        FloorVisibilityProfile selectedProfile = null;
        if (floorProfiles != null)
        {
            foreach (FloorVisibilityProfile profile in floorProfiles)
            {
                if (profile != null && profile.FloorNumber == currentFloor)
                {
                    selectedProfile = profile;
                    break;
                }
            }
        }

        if (selectedProfile == null)
        {
            Debug.LogWarning($"No visibility profile is configured for floor {currentFloor} on '{name}'.", this);
            return;
        }

        if (managedObjects != null)
        {
            foreach (GameObject target in managedObjects)
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        if (selectedProfile.VisibleObjects != null)
        {
            foreach (GameObject target in selectedProfile.VisibleObjects)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }
        }

        if (floorText != null)
        {
            floorText.text = currentFloor + "F";
        }
    }
}
