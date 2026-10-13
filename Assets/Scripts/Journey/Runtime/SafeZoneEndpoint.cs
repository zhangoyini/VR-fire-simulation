using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

namespace Harbourview.Journey
{
    /// <summary>
    /// T12 - reaching the safe endpoint. Attached to the assembly area zone together with a GeoFence.
    /// Arriving here never "wins" anything: it simply moves the journey into the debrief state so
    /// Module F can present the review (proposal R5 / R6).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeZoneEndpoint : MonoBehaviour
    {
        public enum EndpointType
        {
            GroundAssembly,
            RefugeArea
        }

        [SerializeField] private EndpointType endpointType = EndpointType.GroundAssembly;
        [SerializeField] private GeoFence zone;

        public UnityEvent onReached = new UnityEvent();

        public EndpointType Type => endpointType;

        private void Awake()
        {
            if (zone == null)
            {
                zone = GetComponent<GeoFence>();
            }

            if (zone != null)
            {
                zone.onDwellElapsed.AddListener(HandleReached);
            }
            else
            {
                Debug.LogWarning("[SafeZoneEndpoint] No GeoFence found on " + gameObject.name, this);
            }
        }

        private void OnDestroy()
        {
            if (zone != null)
            {
                zone.onDwellElapsed.RemoveListener(HandleReached);
            }
        }

        private void HandleReached()
        {
            if(CampusLessonDirector.Instance!=null && !CampusLessonDirector.Instance.ExerciseStarted) return;
            if (JourneyDirector.Instance != null)
            {
                JourneyDirector.Instance.Request(
                    endpointType == EndpointType.RefugeArea ? JourneyState.Refuge : JourneyState.Ground);
            }

            float elapsed = Time.realtimeSinceStartup;
            string payload = "{\"type\":\"" + endpointType.ToString().ToLowerInvariant()
                + "\",\"totalMs\":" + ((int)(elapsed * 1000f)).ToString(CultureInfo.InvariantCulture) + "}";

            SessionLogger.Write("ENDPOINT_REACHED", payload);
            onReached.Invoke();
        }

        /// <summary>Re-arms the zone so a retry pass is logged separately.</summary>
        public void ResetZone()
        {
            if (zone != null)
            {
                zone.Arm();
            }
        }
    }
}
