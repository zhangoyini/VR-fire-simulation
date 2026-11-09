using System.Collections;
using UnityEngine;


public sealed class CampusHingeInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform pivot;
    [SerializeField] private float openSign = 1f;
    [SerializeField] private float maximumAngle = 90f;
    [SerializeField] private bool windowWarning;
    [SerializeField, Min(.08f)] private float travelForFullOpening = .30f;
    private Quaternion closedRotation;
    private Transform owner;
    private float grabAngle, angle;
    private Transform handSpace;
    private Vector3 grabHandPosition, grabPushDirection;
    private Vector3 windowGripLocal, windowGrabRadius;
    private CampusHandInteractor windowHand;
    private Coroutine closeRoutine;
    public float Angle => angle;
    public bool IsHeld => owner != null;
    public bool IsWindow => windowWarning;
    public bool IsHeldBy(Transform hand) => owner == hand;
    private void Awake() { if (pivot == null) pivot = transform; closedRotation = pivot.localRotation; }
    public void Configure(Transform hinge, float sign, bool isWindow)
    { pivot = hinge; openSign = sign; windowWarning = isWindow; closedRotation = hinge.localRotation; }
    public bool TryGetWindowGrip(Transform hand, out Vector3 point)
    {
        point = default;
        if (!IsWindow || !IsHeldBy(hand)) return false;
        point = pivot.TransformPoint(windowGripLocal);
        return true;
    }
    private Vector3 HandPosition(Transform hand) => handSpace != null ? handSpace.InverseTransformPoint(hand.position) : hand.position;
    public void OnInteractStart(Transform hand)
    {
        if (owner != null || hand == null) return;
        if (closeRoutine != null) { StopCoroutine(closeRoutine); closeRoutine = null; }
        owner = hand; grabAngle = angle;
        var locomotion = hand.GetComponentInParent<CampusLocomotion>() ?? FindObjectOfType<CampusLocomotion>();
        handSpace = locomotion != null ? locomotion.transform : null;
        if (IsWindow)
        {
            windowHand = hand.GetComponent<CampusHandInteractor>();
            Vector3 palm = windowHand != null ? windowHand.ContactPoint : hand.position;
            Vector3 grip = palm;
            float nearest = float.PositiveInfinity;
            foreach (var collider in pivot.GetComponentsInChildren<BoxCollider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector3 point = collider.ClosestPoint(palm);
                float distance = (point - palm).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                grip = point;
            }
            windowGripLocal = pivot.InverseTransformPoint(grip);
            windowGrabRadius = Vector3.ProjectOnPlane(grip - pivot.position, pivot.up);
            grabHandPosition = handSpace != null ? handSpace.InverseTransformPoint(palm) : palm;
            return;
        }
        grabHandPosition = HandPosition(hand);

        var leaf = pivot.GetComponentInChildren<BoxCollider>();
        Vector3 radial = Vector3.ProjectOnPlane((leaf != null ? leaf.transform.TransformPoint(leaf.center) : hand.position) - pivot.position, Vector3.up);
        if (radial.sqrMagnitude < .001f) radial = pivot.right;
        Vector3 direction = Vector3.Cross(Vector3.up, radial.normalized) * openSign;
        grabPushDirection = handSpace != null ? handSpace.InverseTransformDirection(direction) : direction;
    }
    public void OnInteract(Transform hand)
    {
        if (owner != hand) return;
        if (IsWindow)
        {
            Vector3 palm = windowHand != null ? windowHand.ContactPoint : hand.position;
            Vector3 displacement = (handSpace != null ? handSpace.InverseTransformPoint(palm) : palm) - grabHandPosition;
            if (handSpace != null) displacement = handSpace.TransformVector(displacement);
            // A hinged sash can only follow the circular part of the hand's motion.
            Vector3 radius = windowGrabRadius + Vector3.ProjectOnPlane(displacement, pivot.up);
            if (windowGrabRadius.sqrMagnitude > .0001f && radius.sqrMagnitude > .0001f)
                SetAngle(grabAngle + Vector3.SignedAngle(windowGrabRadius, radius, pivot.up) * openSign);
            return;
        }
        float travel = Vector3.Dot(HandPosition(hand) - grabHandPosition, grabPushDirection);
        if (Mathf.Abs(travel) < .003f) travel = 0; 
        SetAngle(grabAngle + travel / Mathf.Max(.08f, travelForFullOpening) * maximumAngle);
    }
    public void OnInteractEnd(Transform hand)
    {
        if (owner != hand) return;
        owner = null;
        windowHand = null;

        if (windowWarning && grabAngle > 35f && angle > 35f && (CampusLessonDirector.Instance==null || !CampusLessonDirector.Instance.IsPractice))
        {
            CampusMessagePresenter.Instance?.Show(CampusMessagePresenter.WindowWarning);
            closeRoutine = StartCoroutine(CloseAfterWarning());
        }
    }
    public void SetAngle(float value)
    { angle = Mathf.Clamp(value, windowWarning ? 0 : -maximumAngle, maximumAngle); pivot.localRotation = closedRotation * Quaternion.Euler(0, angle * openSign, 0); }
    private IEnumerator CloseAfterWarning()
    {
        yield return new WaitForSeconds(2f);
        float start = angle;
        for (float t = 0; t < .7f; t += Time.deltaTime) { SetAngle(Mathf.Lerp(start, 0, t / .7f)); yield return null; }
        SetAngle(0); closeRoutine = null;
    }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    private void OnDisable() { owner = null; windowHand = null; StopAllCoroutines(); closeRoutine = null; }
}
