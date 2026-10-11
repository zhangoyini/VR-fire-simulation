using System;
using UnityEngine;

public class StairTeleportTrigger : MonoBehaviour
{
    [Serializable]
    private class FloorRoute
    {
        [SerializeField] private int fromFloor;
        [SerializeField] private int toFloor;
        [SerializeField] private Transform landingTarget;

        public int FromFloor => fromFloor;
        public int ToFloor => toFloor;
        public Transform LandingTarget => landingTarget;
    }

    [Header("References")]
    [SerializeField] private FloorManager floorManager;
    [SerializeField] private Transform playerRig;
    [SerializeField] private string playerTag = "Player";

    [Header("Routes")]
    [Tooltip("Add one route per starting floor. Assign each route's destination floor and landing Transform in the Inspector.")]
    [SerializeField] private FloorRoute[] routes;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) || floorManager == null || routes == null)
        {
            return;
        }

        foreach (FloorRoute route in routes)
        {
            if (route != null && route.FromFloor == floorManager.CurrentFloor)
            {
                floorManager.TryTransition(route.ToFloor, route.LandingTarget, playerRig);
                return;
            }
        }
    }
}
