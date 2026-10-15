using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class CampusSmokeVolume : MonoBehaviour
{
    public static readonly HashSet<CampusSmokeVolume> Active = new HashSet<CampusSmokeVolume>();
    private BoxCollider volume;
    private void OnEnable() { volume = GetComponent<BoxCollider>(); Active.Add(this); }
    private void OnDisable() { Active.Remove(this); }
    public bool Contains(Vector3 point)
    {
        if (!isActiveAndEnabled) return false;
        Vector3 local = transform.InverseTransformPoint(point) - volume.center, half = volume.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }
}
