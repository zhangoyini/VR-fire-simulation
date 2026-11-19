using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class FireTrainingButtonVisual : MonoBehaviour
{
    private Image face;
    private VRMenuButton button;
    private VRMenuRayPointer[] rays;
    private bool contact;
    private float pressedUntil;
    private string family;
    public string ArtworkOverride { get; set; }
    private void OnEnable()
    {
        face = GetComponent<Image>();
        button = GetComponent<VRMenuButton>();
        rays = FindObjectsOfType<VRMenuRayPointer>(true);
        family = name.Contains("Start") || name.Contains("Resume") || name.Contains("继续") || name.Contains("查看") || name.Contains("下一页") ? "primary" : "secondary";
        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.color = family == "primary" ? FireTrainingUIStyle.Ink : Color.white;
        Refresh();
    }
    public void Hover(bool value) { contact = value; }
    public void Press() { pressedUntil = Time.unscaledTime + .14f; }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (face == null) return;
        bool hover = contact;
        foreach (var ray in rays) if (ray != null && ray.CurrentTarget == button) hover = true;
        if (!string.IsNullOrEmpty(ArtworkOverride))
        {
            FireTrainingUIStyle.Apply(face, ArtworkOverride);
            face.color = Time.unscaledTime < pressedUntil ? new Color(.7f,.7f,.7f) : hover ? new Color(.85f,1f,1f) : Color.white;
            return;
        }
        var selectable = GetComponent<Button>();
        string state = selectable != null && !selectable.interactable ? "disabled" : Time.unscaledTime < pressedUntil ? "pressed" : hover ? "hover" : "normal";
        FireTrainingUIStyle.Apply(face, "buttons/button_" + family + "_" + state);
    }
    private void OnDisable() { contact = false; pressedUntil = 0; }
}
