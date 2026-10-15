using UnityEngine;

[RequireComponent(typeof(Rigidbody),typeof(BoxCollider))]
public sealed class CampusPracticeBook : MonoBehaviour,IInteractable
{
    private Rigidbody body;
    private Transform owner;
    private Vector3 home;
    private Quaternion homeRotation;
    public bool IsHeld => owner!=null;
    public bool Practised { get; private set; }
    public bool CanGrab(Transform hand) => hand!=null && (!IsHeld || owner==hand) && Time.timeScale>0;
    private void Awake()
    {
        body=GetComponent<Rigidbody>(); home=transform.position; homeRotation=transform.rotation;
        var player=FindObjectOfType<CampusLocomotion>(true);
        if(player!=null) foreach(var collider in player.GetComponentsInChildren<Collider>(true))
            if(!collider.isTrigger) Physics.IgnoreCollision(GetComponent<BoxCollider>(),collider,true);
    }
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    public void OnInteractStart(Transform hand)
    {
        if(!CanGrab(hand) || IsHeld) return;
        owner=hand; Practised=true; body.velocity=body.angularVelocity=Vector3.zero;
        body.isKinematic=true; body.useGravity=false; Follow();
    }
    public void OnInteract(Transform hand) { if(owner==hand) Follow(); }
    private void LateUpdate()
    {
        if(IsHeld && Time.timeScale>0) Follow();
        // Keep practice usable if a book is lost below the building.
        else if(!IsHeld && transform.position.y<home.y-5) ResetBook();
    }
    private void Follow()
    {
        transform.SetPositionAndRotation(owner.TransformPoint(new Vector3(0,0,.09f)),owner.rotation*Quaternion.Euler(-15,0,0));
        body.position=transform.position; body.rotation=transform.rotation;
    }
    public void OnInteractEnd(Transform hand)
    {
        if(owner!=hand || owner==null) return;
        Follow(); Transform releasing=owner; owner=null; body.isKinematic=false; body.useGravity=true;
        CampusThrowMotion.ApplyRelease(releasing,body);
    }
    public void ResetBook()
    {
        owner=null; body.isKinematic=false; body.useGravity=true; body.velocity=body.angularVelocity=Vector3.zero;
        body.position=home; body.rotation=homeRotation; transform.SetPositionAndRotation(home,homeRotation);
    }
    private void OnDisable() { if(owner!=null) OnInteractEnd(owner); }
}
