using UnityEngine;
using Valve.VR;

public class SystemUIPauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameplayUI;
    [SerializeField] private ResolutionCoordinator resolution;
    [SerializeField] private Behaviour teleport;
    [SerializeField] private BoxCollider contactVolume;
    [SerializeField] private VRMenuButton[] pauseButtons;
    private SteamVR_Action_Boolean grab;
    private SteamVR_Action_Boolean pauseAction;
    private bool paused;
    private float previousTimeScale;
    private bool previousTeleportEnabled;
    private bool previousGameplayVisible;

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
        if (gameplayUI != null) gameplayUI.SetActive(false);
        pausePanel.SetActive(true);
    }
    public void ResumeGame() { EndPause(true); }
    public void TogglePause() { if (paused) ResumeGame(); else ShowPause(); }
    private void OnOutcome(ResolutionCoordinator.Outcome outcome) { EndPause(false); }
    private void EndPause(bool restoreGameplay)
    {
        if (!paused) return;
        paused = false;
        // Navigation already restores 1; do not overwrite it during scene unload.
        if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        if (teleport != null) teleport.enabled = previousTeleportEnabled;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (restoreGameplay && gameplayUI != null) gameplayUI.SetActive(previousGameplayVisible);
    }

    private void Update()
    {
        if (pauseAction != null && resolution != null
            && resolution.CurrentOutcome == ResolutionCoordinator.Outcome.None
            && pauseAction.GetStateDown(SteamVR_Input_Sources.LeftHand))
        {
            TogglePause();
            return;
        }
        if (!paused || contactVolume == null || grab == null
            || !grab.GetStateDown(SteamVR_Input_Sources.RightHand)) return;
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
