using System.Collections.Generic;
using UnityEngine;

// Deliberately a game mechanic, not respiratory protection or a real-world safety model.
[DefaultExecutionOrder(40),RequireComponent(typeof(Rigidbody),typeof(BoxCollider))]
public sealed class CampusTowelInteractable : MonoBehaviour,IInteractable
{
    public static readonly HashSet<CampusTowelInteractable> Active=new HashSet<CampusTowelInteractable>();
    [SerializeField] private Renderer cloth;
    [SerializeField] private Material dryMaterial,wetMaterial;
    [SerializeField,Min(.1f)] private float wettingSeconds=1.5f;
    [SerializeField] private Vector3 gripOffset=new Vector3(0,0,.065f);
    [SerializeField] private Vector3 gripEuler=new Vector3(90,0,0);
    [SerializeField,Range(.04f,.25f)] private float faceClearance=.12f;
    private Rigidbody body;
    private BoxCollider surface;
    private Transform owner;
    private ResolutionCoordinator resolution;
    private float waterContact;
    private bool shownGripHint;
    public bool IsWet { get; private set; }
    public bool IsHeld => owner!=null;
    public Transform HoldingHand => owner;
    public float WetProgress => Mathf.Clamp01(waterContact/wettingSeconds);
    public Rigidbody Body => body;
    public BoxCollider Surface => surface;
    public void Configure(Renderer renderer,Material dry,Material wet)
    { cloth=renderer; dryMaterial=dry; wetMaterial=wet; if(!IsWet && cloth!=null) cloth.sharedMaterial=dryMaterial; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() { Active.Clear(); }
    private void Awake()
    {
        body=GetComponent<Rigidbody>(); surface=GetComponent<BoxCollider>(); resolution=FindObjectOfType<ResolutionCoordinator>();
        if(cloth!=null) cloth.sharedMaterial=dryMaterial;
        // A held cloth must not block locomotion or get trapped inside the avatar on release.
        var player=GameObject.Find("05 Player & Shared Systems");
        if(player!=null) foreach(var collider in player.GetComponentsInChildren<Collider>(true))
            if(!collider.isTrigger) Physics.IgnoreCollision(surface,collider,true);
    }
    private void OnEnable() { Active.Add(this); }
    private void OnDisable() { Active.Remove(this); if(owner!=null) OnInteractEnd(owner); }
    private bool Blocked => Time.timeScale==0 || (resolution!=null && resolution.CurrentOutcome!=ResolutionCoordinator.Outcome.None);
    public bool CanGrab(Transform hand) => false; // MODEL API PLACEHOLDER: dry towel is a fixed prop.
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    public void OnInteractStart(Transform hand)
    {
        if(!CanGrab(hand) || IsHeld) return;
        owner=hand; body.velocity=Vector3.zero; body.angularVelocity=Vector3.zero;
        body.isKinematic=true; body.useGravity=false; body.interpolation=RigidbodyInterpolation.None;
        FollowHand();
        if(!shownGripHint)
        {
            shownGripHint=true;
            if(CampusLessonDirector.Instance==null || !CampusLessonDirector.Instance.GuidedTowelTask)
                CampusMessagePresenter.Instance?.Show("按住扳机拿着毛巾，在流水下浸湿。\nHold the trigger; wet the towel under running water.");
        }
    }
    public void OnInteract(Transform hand) { if(owner==hand && hand!=null) FollowHand(); }
    private void LateUpdate()
    { if(IsHeld && !Blocked) { FollowHand(); TickWetting(Time.deltaTime); } }
    private void FollowHand()
    {
        Vector3 p=owner.TransformPoint(gripOffset); Quaternion q=owner.rotation*Quaternion.Euler(gripEuler);
        body.position=p; body.rotation=q; transform.SetPositionAndRotation(p,q);
    }
    public void OnInteractEnd(Transform hand)
    {
        if(owner!=hand || owner==null) return;
        FollowHand(); Transform releasing=owner; owner=null; waterContact=IsWet?wettingSeconds:0;
        body.isKinematic=false; body.useGravity=true; body.interpolation=RigidbodyInterpolation.Interpolate;
        CampusThrowMotion.ApplyRelease(releasing,body);
    }
    public void TickWetting(float seconds)
    {
        if(!IsHeld || IsWet || Blocked || seconds<=0) return;
        bool watered=false;
        foreach(var faucet in CampusFaucet.Active)
            if(faucet!=null && faucet.TouchesRunningWater(surface)) { watered=true; break; }
        waterContact=watered?Mathf.Min(wettingSeconds,waterContact+seconds):0;
        if(waterContact<wettingSeconds) return;
        IsWet=true; if(cloth!=null) cloth.sharedMaterial=wetMaterial;
        if(CampusLessonDirector.Instance==null || !CampusLessonDirector.Instance.GuidedTowelTask)
            CampusMessagePresenter.Instance?.Show("毛巾已浸湿，持续握持并放在口鼻前。\nTowel is wet. Keep holding it over your mouth and nose.");
    }
    public bool CoversFace(Transform head)
    {
        if(!isActiveAndEnabled || !IsWet || !IsHeld || head==null) return false;
        Vector3 local=head.InverseTransformPoint(transform.position);
        if(local.z<.02f || local.z>.45f) return false; // Not behind the head or held at arm's length.
        Vector3 mouth=head.TransformPoint(new Vector3(0,-.10f,.07f));
        return Vector3.Distance(surface.ClosestPoint(mouth),mouth)<=faceClearance;
    }
    public static bool HasProtection(Transform head)
    { foreach(var towel in Active) if(towel!=null && towel.CoversFace(head)) return true; return false; }
}
