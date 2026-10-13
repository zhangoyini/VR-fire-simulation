using System.Collections.Generic;
using UnityEngine;

// Per-renderer property blocks preserve material assets and support two hovering hands.
public sealed class CampusContactHighlight : MonoBehaviour
{
    private readonly HashSet<Object> hands = new HashSet<Object>();
    private Renderer[] renderers;
    private MaterialPropertyBlock[] original;
    public void SetHover(Object hand, bool active)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(); original = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { original[i] = new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(original[i]); }
        }
        if (active) hands.Add(hand); else hands.Remove(hand);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (hands.Count == 0) { renderers[i].SetPropertyBlock(original[i]); continue; }
            var block = new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(block);
            Color baseColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor") ? renderers[i].sharedMaterial.GetColor("_BaseColor") : Color.white;
            block.SetColor("_BaseColor", Color.Lerp(baseColor, new Color(1, .77f, .14f), .65f)); renderers[i].SetPropertyBlock(block);
        }
    }
}
