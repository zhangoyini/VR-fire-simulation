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
    public bool SegmentInterval(Vector3 from,Vector3 to,out float enter,out float exit)
    {
        enter=0; exit=1;
        if(!isActiveAndEnabled || volume==null) return false;
        Vector3 origin=transform.InverseTransformPoint(from)-volume.center;
        Vector3 direction=transform.InverseTransformPoint(to)-transform.InverseTransformPoint(from);
        Vector3 half=volume.size*.5f;
        for(int axis=0;axis<3;axis++)
        {
            if(Mathf.Abs(direction[axis])<.000001f)
            { if(Mathf.Abs(origin[axis])>half[axis]) return false; continue; }
            float a=(-half[axis]-origin[axis])/direction[axis], b=(half[axis]-origin[axis])/direction[axis];
            enter=Mathf.Max(enter,Mathf.Min(a,b)); exit=Mathf.Min(exit,Mathf.Max(a,b));
            if(enter>=exit) return false;
        }
        return true;
    }
}
