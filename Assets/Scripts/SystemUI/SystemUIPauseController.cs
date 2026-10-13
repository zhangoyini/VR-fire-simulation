using UnityEngine;
using Valve.VR;

public class SystemUIPauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameplayUI;
    [SerializeField] private ResolutionCoordinator resolution;
    [SerializeField] private Behaviour teleport;
    [SerializeField] private Behaviour[] additionalTeleports;
    [SerializeField] private Behaviour[] menuInteractions;
    [SerializeField] private BoxCollider contactVolume;
    [SerializeField] private VRMenuButton[] pauseButtons;
    private SteamVR_Action_Boolean grab;
    private SteamVR_Action_Boolean pauseAction;
    private bool paused;
    private float previousTimeScale;
    private bool previousTeleportEnabled;
    private bool previousGameplayVisible;
    private bool[] previousAdditionalTeleportStates;
    private bool[] previousMenuInteractionStates;
    private bool menuInteractionsBlocked;
    private bool previousPauseInput,previousRightTrigger;

    private void Awake()
    {
        grab = SteamVR_Input.GetBooleanAction("GrabPinch");
        pauseAction = SteamVR_Input.GetBooleanActionFromPath("/actions/default/in/Pause");
        if (pausePanel != null) pausePanel.SetActive(false);
    }
    private void OnEnable()
    {
        if (resolution != null) resolution.OutcomeClaimed += OnOutcome;
    }
    private void OnDisable()
    {
        if (resolution != null) resolution.OutcomeClaimed -= OnOutcome;
        EndPause(false);
        RestoreMenuInteractions();
    }

    [ContextMenu("Pause Menu/Show (Play Mode)")]
    public void ShowPause()
    {
        if (!Application.isPlaying || paused || pausePanel == null || resolution == null
            || resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None) return;
        previousTimeScale = Time.timeScale;
        previousTeleportEnabled = teleport != null && teleport.enabled;
        previousGameplayVisible = gameplayUI != null && gameplayUI.activeSelf;
        paused = true;
        Time.timeScale = 0f;
        if (teleport != null) teleport.enabled = false;
        previousAdditionalTeleportStates = DisableBehaviours(additionalTeleports);
        BlockMenuInteractions();
        if (gameplayUI != null) gameplayUI.SetActive(false);
        pausePanel.SetActive(true);
    }
    public void ResumeGame() { EndPause(true); }
    public void TogglePause() { if (paused) ResumeGame(); else ShowPause(); }
    private void OnOutcome(ResolutionCoordinator.Outcome outcome)
    {
        EndPause(false);
        BlockMenuInteractions();
    }
    private void EndPause(bool restoreGameplay)
    {
        if (!paused) return;
        paused = false;
        // Navigation already restores 1; do not overwrite it during scene unload.
        if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        if (teleport != null) teleport.enabled = previousTeleportEnabled;
        RestoreBehaviours(additionalTeleports, previousAdditionalTeleportStates);
        if (resolution == null || resolution.CurrentOutcome == ResolutionCoordinator.Outcome.None)
            RestoreMenuInteractions();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (restoreGameplay && gameplayUI != null) gameplayUI.SetActive(previousGameplayVisible);
    }

    // Optional scene-local adapters: preserve teammate components and their enabled states.
    private static bool[] DisableBehaviours(Behaviour[] behaviours)
    {
        if (behaviours == null) return null;
        var states = new bool[behaviours.Length];
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null)
            {
                states[i] = behaviours[i].enabled;
                behaviours[i].enabled = false;
            }
        return states;
    }
    private static void RestoreBehaviours(Behaviour[] behaviours, bool[] states)
    {
        if (behaviours == null || states == null) return;
        for (int i = 0; i < behaviours.Length && i < states.Length; i++)
            if (behaviours[i] != null) behaviours[i].enabled = states[i];
    }
    private void BlockMenuInteractions()
    {
        if (menuInteractionsBlocked) return;
        previousMenuInteractionStates = DisableBehaviours(menuInteractions);
        menuInteractionsBlocked = true;
    }
    private void RestoreMenuInteractions()
    {
        if (!menuInteractionsBlocked) return;
        RestoreBehaviours(menuInteractions, previousMenuInteractionStates);
        menuInteractionsBlocked = false;
    }

    private void Update()
    {
        if (!paused && resolution != null && resolution.CurrentOutcome == ResolutionCoordinator.Outcome.None)
            RestoreMenuInteractions();
        bool pauseInput=CampusControllerInput.Pause(), pauseDown=pauseInput && !previousPauseInput;
        previousPauseInput=pauseInput;
        bool rightTrigger=CampusControllerInput.Trigger(SteamVR_Input_Sources.RightHand), rightDown=rightTrigger && !previousRightTrigger;
        previousRightTrigger=rightTrigger;
        if (resolution != null
            && resolution.CurrentOutcome == ResolutionCoordinator.Outcome.None
            && pauseDown)
        {
            TogglePause();
            return;
        }
        if (!paused || contactVolume == null || !rightDown) return;
        // FixedUpdate/trigger callbacks stop at timeScale=0. Recheck only our pause
        // buttons against Henry's existing contact box, using the same input/action.
        Transform hand = contactVolume.transform;
        Physics.SyncTransforms();
        Vector3 size = Vector3.Scale(contactVolume.size, hand.lossyScale) * 0.5f;
        foreach (Collider hit in Physics.OverlapBox(hand.TransformPoint(contactVolume.center),
            size, hand.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            VRMenuButton button = hit.GetComponentInParent<VRMenuButton>();
            if (button != null && button.isActiveAndEnabled && pauseButtons != null
                && System.Array.IndexOf(pauseButtons, button) >= 0)
            {
                button.OnInteractStart(hand);
                break;
            }
        }
    }
}
