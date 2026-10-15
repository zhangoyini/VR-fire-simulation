using UnityEngine;
using UnityEngine.Events;

namespace Harbourview.Journey
{
    /// <summary>
    /// One reusable component for every volume-based trigger in the journey (T03 - T12).
    /// It intentionally does not freeze movement or show any failure state: the spec
    /// requires deviations to be softened, never punished (proposal R9 / R12).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GeoFence : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayers = ~0;
        [Tooltip("Optional exact player-body collider. Other colliders cannot enter or reset this zone.")]
        [SerializeField] private Collider requiredCollider;
        [SerializeField] private bool requireStateMatch;
        [SerializeField] private JourneyState expectedState = JourneyState.CorridorDecision;
        [SerializeField] private float dwellSeconds = 0.8f;
        [SerializeField] private bool fireOnce = true;
        [SerializeField] private bool writeEnterExitLog = true;

        public UnityEvent onEntered = new UnityEvent();
        public UnityEvent onExited = new UnityEvent();
        public UnityEvent onDwellElapsed = new UnityEvent();

        private bool _inside;
        private bool _armed = true;
        private float _elapsed;

        public bool IsInside => _inside;
        public bool IsArmed => _armed;

        public void Arm()
        {
            _armed = true;
            _elapsed = 0f;
        }

        private void Reset()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsTarget(other) || !StateMatches())
            {
                return;
            }

            _inside = true;
            _elapsed = 0f;

            if (writeEnterExitLog)
            {
                SessionLogger.Write("GEOFENCE_ENTER", "{\"id\":\"" + gameObject.name + "\"}");
            }

            onEntered.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsTarget(other))
            {
                return;
            }

            _inside = false;
            _elapsed = 0f;

            if (writeEnterExitLog)
            {
                SessionLogger.Write("GEOFENCE_EXIT", "{\"id\":\"" + gameObject.name + "\"}");
            }

            onExited.Invoke();
        }

        private void Update()
        {
            if (!_inside || !_armed)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < dwellSeconds)
            {
                return;
            }

            if (fireOnce)
            {
                _armed = false;
            }

            onDwellElapsed.Invoke();
        }

        private bool IsTarget(Collider other)
        {
            return (requiredCollider == null || other == requiredCollider)
                && (targetLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        private bool StateMatches()
        {
            return !requireStateMatch || JourneyDirector.Is(expectedState);
        }
    }
}
