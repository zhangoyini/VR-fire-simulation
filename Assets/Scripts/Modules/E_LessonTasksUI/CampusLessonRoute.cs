using UnityEngine;

// Ground chevrons, not a controller ray or teleport line. Stops at each route corner.
public sealed class CampusLessonRoute : MonoBehaviour
{
    [SerializeField] private Transform[] arrows;
    private Vector3[] points;
    private int index;
    public int WaypointIndex => index;
    public bool Visible => points!=null;
    public string DirectionHint(Transform head)
    {
        if(points==null || head==null) return "";
        Vector3 offset=points[index]-head.position; offset.y=0;
        float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(head.forward,Vector3.up),offset,Vector3.up);
        string direction=Mathf.Abs(angle)<30?"前方 / ahead":Mathf.Abs(angle)>145?"后方 / behind":angle>0?"右侧 / right":"左侧 / left";
        float distance=offset.magnitude;
        for(int i=index;i<points.Length-1;i++) distance+=Vector3.Distance(points[i],points[i+1]);
        return "卫生间路线："+direction+" · "+Mathf.CeilToInt(distance)+" m";
    }
    public void Show(Vector3[] route) { points=route; index=0; }
    public void Hide() { points=null; if(arrows!=null) foreach(var arrow in arrows) if(arrow!=null) arrow.gameObject.SetActive(false); }
    public void Tick(Transform head)
    {
        if(points==null || head==null || points.Length==0) return;
        Vector3 p=head.position; p.y=points[index].y;
        while(index<points.Length-1 && Vector3.Distance(p,points[index])<.85f) index++;
        Vector3 direction=points[index]-p; direction.y=0;
        if(direction.magnitude<.25f) { foreach(var arrow in arrows) arrow.gameObject.SetActive(false); return; }
        float floor=Mathf.Clamp(Mathf.Floor((head.position.y-.8f)/3.8f),0,3)*3.8f;
        // Do not project upper-floor arrows over the open stairwell while descending.
        bool landing=Mathf.Abs(head.position.y-(floor+1.65f))<.35f;
        for(int i=0;i<arrows.Length;i++)
        {
            bool show=landing && i*.55f+.6f<direction.magnitude;
            arrows[i].gameObject.SetActive(show);
            if(show) { Vector3 q=p+direction.normalized*(.6f+i*.55f); q.y=floor+.035f; arrows[i].SetPositionAndRotation(q,Quaternion.LookRotation(direction.normalized,Vector3.up)); }
        }
    }
}
