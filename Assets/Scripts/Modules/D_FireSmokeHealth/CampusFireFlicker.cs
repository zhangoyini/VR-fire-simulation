using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class CampusFireFlicker : MonoBehaviour
{
    private Light glow;
    private void Awake() { glow=GetComponent<Light>(); }
    private void Update() { glow.intensity=1.7f+Mathf.PerlinNoise(Time.time*6, .2f)*1.1f; }
}
