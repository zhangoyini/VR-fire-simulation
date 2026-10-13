using UnityEngine;

// Animated teaching gloves are separate from the two actual tracked hands.
public sealed class CampusTeachingHand : MonoBehaviour
{
    [SerializeField] private Mesh relaxed,fist;
    private Mesh animated;
    private Vector3[] rest,closed,vertices;
    public void Configure(Mesh open,Mesh grip) { relaxed=open; fist=grip; }
    private void Awake()
    {
        animated=Instantiate(relaxed); animated.name="Transient teaching glove";
        GetComponent<MeshFilter>().sharedMesh=animated;
        rest=relaxed.vertices; closed=fist.vertices; vertices=new Vector3[rest.Length];
    }
    public void SetGrip(float amount)
    {
        if(animated==null) return;
        for(int i=0;i<vertices.Length;i++) vertices[i]=Vector3.Lerp(rest[i],closed[i],Mathf.Clamp01(amount));
        animated.vertices=vertices; animated.RecalculateBounds();
    }
    private void OnDestroy() { if(animated!=null) Destroy(animated); }
}
