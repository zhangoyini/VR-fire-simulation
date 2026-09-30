using UnityEngine;

namespace Harbourview.Journey
{
    /// <summary>
    /// Verification order for the four states described in the group proposal v1.1 (Appendix D).
    /// Any scene may reference this enum; only JourneyDirector is allowed to mutate the active value.
    /// </summary>
    public enum JourneyState
    {
        Idle,
        Awareness,
        CorridorDecision,
        Blocked,
        Descending,
        Refuge,
        Ground,
        Debrief
    }

    /// <summary>
    /// The single authority for journey state. Module D spec section 3:
    /// every trigger must ask this director instead of keeping private booleans,
    /// otherwise the state is guaranteed to drift during integration.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class JourneyDirector : MonoBehaviour
    {
        public static JourneyDirector Instance { get; private set; }

        [SerializeField] private bool logTransitions = true;
        [SerializeField] private JourneyState initialState = JourneyState.Idle;

        private JourneyState _state;

        public JourneyState State => _state;

        public delegate void StateChangedHandler(JourneyState from, JourneyState to);
        public event StateChangedHandler OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[JourneyDirector] Duplicate instance destroyed: " + gameObject.name, this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _state = initialState;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Request(JourneyState next)
        {
            if (next == _state)
            {
                return;
            }

            JourneyState from = _state;
            _state = next;

            if (logTransitions)
            {
                SessionLogger.Write("STATE_" + next.ToString().ToUpperInvariant());
            }

            OnStateChanged?.Invoke(from, next);
        }

        /// <summary>True when the caller matches the active state, or when no director exists yet.</summary>
        public static bool Is(JourneyState expected)
        {
            return Instance == null || Instance.State == expected;
        }
    }
}
