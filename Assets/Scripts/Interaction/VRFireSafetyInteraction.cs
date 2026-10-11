using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

[RequireComponent(typeof(LineRenderer))]
public class VRFireSafetyInteraction : MonoBehaviour
{
    private const string ElevatorWarning = "发生火灾时，请勿使用电梯！\nDo not use the elevator in case of fire!";
    public static bool IsWindowPushGestureActive { get; private set; }
    public static bool IsElevatorButtonPressGestureActive { get; private set; }

    private enum SafetyTargetType
    {
        None,
        ElevatorButton,
        Window
    }

    [Header("Right controller input")]
    [SerializeField] private SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.RightHand;
    [Tooltip("GrabPinch is already bound to the Quest Touch trigger in this project.")]
    [SerializeField] private SteamVR_Action_Boolean triggerAction;
    [SerializeField] private SteamVR_Action_Vibration hapticAction;
    [SerializeField, Min(0.01f)] private float hapticInterval = 0.04f;
    [SerializeField, Min(0.001f)] private float hapticDuration = 0.012f;
    [SerializeField, Range(1f, 320f)] private float hapticFrequency = 90f;

    [Header("Interaction ray")]
    [Tooltip("The pose used as the start and direction of the interaction ray. Empty uses this object.")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LineRenderer directionLine;
    [SerializeField, Min(0.1f)] private float maxInteractionDistance = 20f;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [SerializeField] private Color directionLineColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color targetLineColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Header("Target edge highlight")]
    [SerializeField] private Color targetOutlineColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField, Min(0.001f)] private float targetOutlineWidth = 0.018f;

    [Header("Message in front of the headset")]
    [Tooltip("Empty uses the main camera, then the Camera under [CameraRig].")]
    [SerializeField] private Camera vrCamera;
    [SerializeField, Min(0.1f)] private float messageDistance = 1f;
    [Tooltip("Set to 0 to keep the last warning visible until the next interaction.")]
    [SerializeField, Min(0f)] private float messageDuration = 5f;

    private FireSafetyTargetOutline targetOutline;
    private Transform activeTarget;
    private SafetyTargetType activeTargetType;
    private RuntimeFireWindow activeWindow;
    private RuntimeFireWindow pushedWindow;
    private bool triggerWasHeld;
    private float nextHapticTime;
    private GameObject messageRoot;
    private TMP_Text messageText;
    private TMP_FontAsset bilingualFont;
    private Font prefabWarningFont;
    private float messageHideTime = -1f;
    private bool cameraLookupWarningShown;
    private readonly RaycastHit[] raycastHits = new RaycastHit[64];

    private Transform RayTransform => rayOrigin != null ? rayOrigin : transform;

    private Vector3 ViewerPosition
    {
        get
        {
            if (vrCamera == null) vrCamera = FindVRCamera();
            return vrCamera != null ? vrCamera.transform.position : RayTransform.position;
        }
    }

    public void ConfigurePrefabResources(Font warningFont)
    {
        if (warningFont != null) prefabWarningFont = warningFont;
    }

    private void Awake()
    {
        if (triggerAction == null)
            triggerAction = SteamVR_Input.GetBooleanAction("GrabPinch");

        if (hapticAction == null)
            hapticAction = SteamVR_Input.GetVibrationAction("Haptic");

        if (directionLine == null)
            directionLine = GetComponent<LineRenderer>();

        if (directionLine != null)
        {
            directionLine.useWorldSpace = true;
            directionLine.positionCount = 2;
            directionLine.widthMultiplier = Mathf.Max(directionLine.widthMultiplier, 0.01f);
            directionLine.numCapVertices = Mathf.Max(directionLine.numCapVertices, 2);
            directionLine.enabled = false;
        }

        GameObject outlineObject = new GameObject("Fire Safety Target Outline");
        targetOutline = outlineObject.AddComponent<FireSafetyTargetOutline>();
        targetOutline.Hide();
    }

    private void LateUpdate()
    {
        UpdateMessageTimer();

        if (triggerAction == null)
        {
            IsWindowPushGestureActive = false;
            IsElevatorButtonPressGestureActive = false;
            return;
        }

        bool triggerHeld = triggerAction.GetState(inputSource);

        if (triggerHeld)
        {
            if (!triggerWasHeld)
                BeginInteraction();

            UpdateInteractionRay();
            UpdateWindowPush();
        }
        else if (triggerWasHeld)
        {
            EndInteraction();
        }

        IsWindowPushGestureActive = triggerHeld && pushedWindow != null;
        IsElevatorButtonPressGestureActive = triggerHeld && activeTargetType == SafetyTargetType.ElevatorButton;

        triggerWasHeld = triggerHeld;
    }

    private void BeginInteraction()
    {
        HideTargetOutline();
        HideMessage();

        if (directionLine != null)
            directionLine.enabled = true;
    }

    private void UpdateInteractionRay()
    {
        Transform originTransform = RayTransform;
        Vector3 origin = originTransform.position;
        Vector3 direction = originTransform.forward.normalized;
        Vector3 endPoint = origin + direction * maxInteractionDistance;
        RaycastHit hit;

        if (TryGetFirstBlockingHit(origin, direction, out hit))
        {
            endPoint = hit.point;

            if (pushedWindow != null)
            {
                // Once the trigger has locked a window, keep the same target
                // even if the hand passes through the moving pane or the ray
                // briefly hits the wall behind it.
                activeWindow = pushedWindow;
                SetActiveTarget(pushedWindow.HighlightTarget, SafetyTargetType.Window);
            }
            else
            {
                Transform target;
                SafetyTargetType targetType;
                if (TryGetSafetyTarget(hit, out target, out targetType))
                    SetActiveTarget(target, targetType);
                else
                    HideTargetOutline();
            }
        }
        else if (pushedWindow == null)
        {
            HideTargetOutline();
        }

        if (directionLine != null)
        {
            directionLine.enabled = true;
            directionLine.positionCount = 2;
            directionLine.SetPosition(0, origin);
            directionLine.SetPosition(1, endPoint);

            Color lineColor = activeTarget != null ? targetLineColor : directionLineColor;
            directionLine.startColor = lineColor;
            directionLine.endColor = lineColor;
        }
    }

    private void EndInteraction()
    {
        if (activeTargetType == SafetyTargetType.ElevatorButton)
            ShowMessage(ElevatorWarning);

        if (pushedWindow != null)
        {
            // Releasing the trigger stops direct manipulation but intentionally
            // does not toggle or auto-close the window.
            pushedWindow.EndPush();
            pushedWindow = null;
        }

        HideTargetOutline();

        if (directionLine != null)
            directionLine.enabled = false;
    }

    private void UpdateWindowPush()
    {
        if (pushedWindow == null && activeTargetType == SafetyTargetType.Window && activeWindow != null)
        {
            if (activeWindow.BeginPush(RayTransform.position, ViewerPosition))
            {
                pushedWindow = activeWindow;
                PulseHaptic(0.2f);
            }
        }

        if (pushedWindow == null)
            return;

        float previousAmount = pushedWindow.OpeningAmount;
        pushedWindow.UpdatePush(RayTransform.position);
        float amountDelta = pushedWindow.OpeningAmount - previousAmount;
        if (Mathf.Abs(amountDelta) > 0.0005f)
        {
            float movement = Mathf.Clamp01(Mathf.Abs(amountDelta) / 0.03f);
            PulseHaptic(Mathf.Lerp(0.18f, 0.8f, movement));
        }
    }

    private void PulseHaptic(float amplitude)
    {
        if (hapticAction == null || Time.unscaledTime < nextHapticTime)
            return;

        hapticAction.Execute(0f, hapticDuration, hapticFrequency,
            Mathf.Clamp01(amplitude), inputSource);
        nextHapticTime = Time.unscaledTime + hapticInterval;
    }

    private bool TryGetFirstBlockingHit(Vector3 origin, Vector3 direction, out RaycastHit firstHit)
    {
        firstHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            maxInteractionDistance,
            interactionLayers,
            QueryTriggerInteraction.Collide);

        RaycastHit[] hits = raycastHits;
        if (hitCount == raycastHits.Length)
        {
            // NonAlloc results are unordered; a full buffer may omit the nearest wall.
            hits = Physics.RaycastAll(origin, direction, maxInteractionDistance,
                interactionLayers, QueryTriggerInteraction.Collide);
            hitCount = hits.Length;
        }

        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null)
                continue;

            // Ignore unrelated trigger volumes; only the open-window target participates.
            if (candidate.collider.isTrigger && !RuntimeFireWindow.TryFind(candidate.collider, out _))
                continue;

            Transform hitTransform = candidate.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (!foundHit || candidate.distance < firstHit.distance)
            {
                firstHit = candidate;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool TryGetSafetyTarget(
        RaycastHit hit,
        out Transform target,
        out SafetyTargetType targetType)
    {
        activeWindow = null;
        if (RuntimeFireWindow.TryFind(hit.collider, out RuntimeFireWindow window) &&
            window.CanInteract(ViewerPosition))
        {
            activeWindow = window;
            target = window.HighlightTarget;
            targetType = SafetyTargetType.Window;
            return true;
        }
        FireSafetyElevator elevator = hit.collider.GetComponentInParent<FireSafetyElevator>();
        if (elevator != null && elevator.TryGetButton(hit.collider, ViewerPosition, out target))
        {
            targetType = SafetyTargetType.ElevatorButton;
            return true;
        }

        target = null;
        targetType = SafetyTargetType.None;
        return false;
    }

    private void SetActiveTarget(Transform target, SafetyTargetType targetType)
    {
        if (activeTarget == target && activeTargetType == targetType)
            return;

        activeTarget = target;
        activeTargetType = targetType;

        if (targetOutline != null)
            targetOutline.Show(activeTarget, targetOutlineColor, targetOutlineWidth);
    }

    private void HideTargetOutline()
    {
        activeTarget = null;
        activeTargetType = SafetyTargetType.None;
        activeWindow = null;

        if (targetOutline != null)
            targetOutline.Hide();
    }

    private void ShowMessage(string text)
    {
        if (!EnsureMessagePanel())
            return;

        messageText.text = text;
        messageRoot.SetActive(true);
        messageHideTime = messageDuration > 0f ? Time.time + messageDuration : -1f;
    }

    private bool EnsureMessagePanel()
    {
        if (messageText != null && messageRoot != null)
            return true;

        if (vrCamera == null)
            vrCamera = FindVRCamera();

        if (vrCamera == null)
        {
            if (!cameraLookupWarningShown)
            {
                Debug.LogWarning("VRFireSafetyInteraction could not find a camera for the safety warning.", this);
                cameraLookupWarningShown = true;
            }

            return false;
        }

        messageRoot = new GameObject("Fire Safety Warning Canvas");
        messageRoot.transform.SetParent(vrCamera.transform, false);
        messageRoot.transform.localPosition = new Vector3(0f, -0.08f, messageDistance);
        messageRoot.transform.localRotation = Quaternion.identity;

        Canvas canvas = messageRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = vrCamera;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = messageRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = messageRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1200f, 400f);
        canvasRect.localScale = Vector3.one * 0.001f;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(messageRoot.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.02f, 0.88f);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("Warning Text");
        textObject.transform.SetParent(messageRoot.transform, false);
        messageText = textObject.AddComponent<TextMeshProUGUI>();
        Font sourceFont = prefabWarningFont != null ? prefabWarningFont : Resources.Load<Font>("NotoSansCJKsc-Regular");
        if (sourceFont != null)
        {
            bilingualFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (!bilingualFont.TryAddCharacters(ElevatorWarning.Replace("\n", "")))
                Debug.LogWarning("Some warning characters could not be added to the font atlas.", this);
        }
        else Debug.LogError("Missing bundled Chinese font: NotoSansCJKsc-Regular.", this);
        messageText.font = bilingualFont != null ? bilingualFont : TMP_Settings.defaultFontAsset;
        messageText.fontSize = 48f;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 32f;
        messageText.fontSizeMax = 48f;
        messageText.color = Color.white;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = true;
        messageText.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.1f);
        textRect.anchorMax = new Vector2(0.95f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        messageRoot.SetActive(false);
        return true;
    }

    private Camera FindVRCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera;

        GameObject cameraRig = GameObject.Find("[CameraRig]");
        if (cameraRig != null)
        {
            Camera rigCamera = cameraRig.GetComponentInChildren<Camera>(true);
            if (rigCamera != null)
                return rigCamera;
        }

        return FindObjectOfType<Camera>();
    }

    private void UpdateMessageTimer()
    {
        if (messageRoot != null && messageHideTime > 0f && Time.time >= messageHideTime)
        {
            messageRoot.SetActive(false);
            messageHideTime = -1f;
        }
    }

    private void HideMessage()
    {
        if (messageRoot != null)
            messageRoot.SetActive(false);

        messageHideTime = -1f;
    }

    private void OnDisable()
    {
        if (pushedWindow != null)
        {
            pushedWindow.EndPush();
            pushedWindow = null;
        }
        triggerWasHeld = false;
        IsWindowPushGestureActive = false;
        IsElevatorButtonPressGestureActive = false;
        HideTargetOutline();

        if (directionLine != null)
            directionLine.enabled = false;
    }

    private void OnDestroy()
    {
        if (targetOutline != null)
            Destroy(targetOutline.gameObject);

        if (messageRoot != null)
            Destroy(messageRoot);

        if (bilingualFont != null)
        {
            foreach (Texture2D atlas in bilingualFont.atlasTextures)
                if (atlas != null) Destroy(atlas);
            if (bilingualFont.material != null) Destroy(bilingualFont.material);
            Destroy(bilingualFont);
        }
    }
}
