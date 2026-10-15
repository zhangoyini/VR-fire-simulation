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
    private Coroutine closeRoutine;
    public float Angle => angle;
    public bool IsHeld => owner != null;
    public bool IsHeldBy(Transform hand) => owner == hand;
    private void Awake() { if (pivot == null) pivot = transform; closedRotation = pivot.localRotation; }
    public void Configure(Transform hinge, float sign, bool isWindow)
    { pivot = hinge; openSign = sign; windowWarning = isWindow; closedRotation = hinge.localRotation; }
    private Vector3 HandPosition(Transform hand) => handSpace != null ? handSpace.InverseTransformPoint(hand.position) : hand.position;
    public void OnInteractStart(Transform hand)
    {
        if (owner != null || hand == null) return;
        if (closeRoutine != null) { StopCoroutine(closeRoutine); closeRoutine = null; }
        owner = hand; grabAngle = angle;
        var locomotion = hand.GetComponentInParent<CampusLocomotion>() ?? FindObjectOfType<CampusLocomotion>();
        handSpace = locomotion != null ? locomotion.transform : null;
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
        float travel = Vector3.Dot(HandPosition(hand) - grabHandPosition, grabPushDirection);
        if (Mathf.Abs(travel) < .003f) travel = 0; 
        SetAngle(grabAngle + travel / Mathf.Max(.08f, travelForFullOpening) * maximumAngle);
    }
    public void OnInteractEnd(Transform hand)
    {
        if (owner != hand) return;
        owner = null;

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
    private void OnDisable() { owner = null; StopAllCoroutines(); closeRoutine = null; }
}
