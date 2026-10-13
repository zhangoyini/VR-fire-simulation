using TMPro;
using UnityEngine;

// Objective-only cyan pulse. Ordinary green EXIT signs remain unchanged.
public sealed class CampusDestinationHighlight : MonoBehaviour
{
    [SerializeField] private Renderer plate;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject halo;
    [SerializeField] private Material highlightedText;
    private Color original;
    private Material originalText;
    private bool capturedOriginal;
    private MaterialPropertyBlock block;
    public bool Highlighted { get; private set; }
    private void Awake() { CaptureOriginal(); block=new MaterialPropertyBlock(); }
    private void CaptureOriginal()
    { if(capturedOriginal || label==null) return; original=label.color; originalText=label.fontSharedMaterial; capturedOriginal=true; }
    public void SetHighlighted(bool value)
    {
        CaptureOriginal(); Highlighted=value; if(halo!=null) halo.SetActive(value);
        if(!value) { if(plate!=null) plate.SetPropertyBlock(null); if(label!=null) { label.color=original; label.fontSharedMaterial=originalText; } }
        else Paint();
    }
    private void Update() { if(Highlighted) Paint(); }
    private void Paint()
    {
        if(block==null) block=new MaterialPropertyBlock();
        float pulse=.8f+.2f*Mathf.Sin(Time.unscaledTime*2.4f);
        block.SetColor("_BaseColor",new Color(.025f,.24f,.3f));
        block.SetColor("_EmissionColor",new Color(0,.75f,1)*pulse);
        if(plate!=null) plate.SetPropertyBlock(block); if(label!=null) { label.color=new Color(.6f,1,1); if(highlightedText!=null) label.fontSharedMaterial=highlightedText; }
    }
    private void OnDisable() { SetHighlighted(false); }
}
