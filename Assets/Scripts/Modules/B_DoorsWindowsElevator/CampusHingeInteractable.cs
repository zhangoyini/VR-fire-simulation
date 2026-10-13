using System.Collections;
using UnityEngine;

// A hand owns the hinge until release. Angle follows its radial displacement, not an animation.
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
        // MODEL ONLY: no door/window action until independent module B is integrated.
    }
    public void OnInteract(Transform hand)
    {
        // MODEL API PLACEHOLDER: no continuous push-distance calculation.
    }
    public void OnInteractEnd(Transform hand)
    {
        if (owner == hand) owner = null;
        // MODEL API PLACEHOLDER: warning and delayed window closure are restored by module 03.
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
