using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

[RequireComponent(typeof(LineRenderer))]
public class VRTeleporter : MonoBehaviour
{
    [Header("SteamVR Input")]
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any;
    [SerializeField] private SteamVR_Action_Boolean teleportAction;

    [Header("Player Setup")]
    [SerializeField] private Transform playerRoot; 
    [SerializeField] private Transform vrCamera;   

    [Header("Arc Settings")]
    [SerializeField, Min(1)] private int resolution = 40;
    [SerializeField] private float initialVelocity = 8f; 
    [SerializeField] private LayerMask teleportLayer;    

    private LineRenderer lineRenderer;
    private Vector3 teleportTarget;
    private bool isValidLocationFound;
    private bool isAimingTeleport;
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false; 

        if (teleportAction == null)
            teleportAction = SteamVR_Input.GetBooleanAction("Teleport");

        if (vrCamera != null && vrCamera.GetComponent<VRControlTutorial>() == null)
            vrCamera.gameObject.AddComponent<VRControlTutorial>();
    }

    private void LateUpdate()
    {
        if (teleportAction == null || playerRoot == null) 
            return;

        bool teleportHeld = teleportAction.GetState(inputSource);
        if (teleportHeld)
        {
            isAimingTeleport = true;
            RenderTeleportArc();
        }
        else if (isAimingTeleport)
        {
            // Detect release from the held state, then use the latest controller pose.
            RenderTeleportArc();

            if (isValidLocationFound)
            {
                ExecuteTeleport();
            }

            lineRenderer.enabled = false;
            isValidLocationFound = false;
            isAimingTeleport = false;
        }
    }

    private void RenderTeleportArc()
    {
        lineRenderer.enabled = true;
        List<Vector3> arcPoints = new List<Vector3>();

        Vector3 currentPosition = transform.position;
        Vector3 currentVelocity = transform.forward * initialVelocity;

        arcPoints.Add(currentPosition);
        isValidLocationFound = false;

        float timeStep = 0.05f;
        for (int i = 0; i < resolution; i++)
        {
            Vector3 nextPosition = currentPosition + currentVelocity * timeStep + 0.5f * Physics.gravity * timeStep * timeStep;
            Vector3 directVector = nextPosition - currentPosition;

            
            if (TryGetFirstBlockingHit(currentPosition, directVector.normalized, directVector.magnitude, out RaycastHit hit))
            {
                arcPoints.Add(hit.point);

                bool isTeleportLayer = (teleportLayer.value & (1 << hit.collider.gameObject.layer)) != 0;
                if (isTeleportLayer && hit.collider.GetComponent<TeleportArea>() != null)
                {
                    teleportTarget = hit.point;
                    isValidLocationFound = true;
                }

                // Any first solid hit stops the arc. Only marked floor hits are valid targets.
                break;
            }

            
            currentPosition = nextPosition;
            currentVelocity += Physics.gravity * timeStep;
            arcPoints.Add(currentPosition);
        }

        
        lineRenderer.positionCount = arcPoints.Count;
        lineRenderer.SetPositions(arcPoints.ToArray());

        lineRenderer.startColor = isValidLocationFound ? Color.green : Color.red;
        lineRenderer.endColor = isValidLocationFound ? Color.green : Color.red;
    }

    private bool TryGetFirstBlockingHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit firstHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool foundHit = false;
        firstHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = raycastHits[i].collider;
            if (candidate == null)
                continue;

            Transform hitTransform = candidate.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            // Do not let the player's own body or rig block its teleport arc.
            if (playerRoot != null && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot)))
                continue;

            if (!foundHit || raycastHits[i].distance < firstHit.distance)
            {
                firstHit = raycastHits[i];
                foundHit = true;
            }
        }

        return foundHit;
    }

    private void ExecuteTeleport()
    {
        if (vrCamera == null)
        {
            playerRoot.position = teleportTarget;
            return;
        }


        Vector3 cameraOffset = vrCamera.position - playerRoot.position;
        cameraOffset.y = 0; // Keep the player safely locked to floor level height

        playerRoot.position = teleportTarget - cameraOffset;
    }
}
