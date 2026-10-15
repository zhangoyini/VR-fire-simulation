using System.Collections.Generic;
using UnityEngine;

// Attach to the lever only: its solid contact collider selects/highlights the switch,
// not the entire wash counter. The other hand can operate it while one holds a towel.
public sealed class CampusFaucet : MonoBehaviour,IInteractable
{
    public static readonly HashSet<CampusFaucet> Active=new HashSet<CampusFaucet>();
    [SerializeField] private GameObject flowingWater;
    [SerializeField] private BoxCollider waterVolume;
    [SerializeField] private AudioSource waterAudio;
    [SerializeField] private Vector3 openEuler=new Vector3(32,0,0);
    private Quaternion closedRotation=Quaternion.identity;
    private Transform pressingHand;
    private ResolutionCoordinator resolution;
    public bool IsOn { get; private set; }
    public BoxCollider WaterVolume => waterVolume;
    public GameObject FlowVisual => flowingWater;
    public AudioSource WaterAudio => waterAudio;
    public void Configure(GameObject flow,BoxCollider volume,AudioSource audio)
    { flowingWater=flow; waterVolume=volume; waterAudio=audio; closedRotation=transform.localRotation; SetWater(false); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() { Active.Clear(); }
    private void Awake() { closedRotation=transform.localRotation; resolution=FindObjectOfType<ResolutionCoordinator>(); SetWater(false); }
    private void OnEnable() { Active.Add(this); }
    private void OnDisable() { Active.Remove(this); SetWater(false); pressingHand=null; }
    private bool Blocked => Time.timeScale==0 || (resolution!=null && resolution.CurrentOutcome!=ResolutionCoordinator.Outcome.None);
    public void OnHoverEnter() { }
    public void OnHoverExit() { }
    public void OnInteractStart(Transform hand)
    { if(hand==null || pressingHand!=null || Blocked) return; pressingHand=hand; SetWater(!IsOn); }
    public void OnInteract(Transform hand) { }
    public void OnInteractEnd(Transform hand) { if(pressingHand==hand) pressingHand=null; }
    public void SetWater(bool on)
    {
        IsOn=on; transform.localRotation=closedRotation*(on?Quaternion.Euler(openEuler):Quaternion.identity);
        if(flowingWater!=null) flowingWater.SetActive(on);
        if(waterVolume!=null) waterVolume.enabled=on;
        if(waterAudio!=null && Application.isPlaying)
        { if(on && !Blocked) waterAudio.Play(); else waterAudio.Stop(); }
    }
    private void Update()
    {
        if(waterAudio==null || !IsOn) return;
        if(Blocked) waterAudio.Pause(); else if(!waterAudio.isPlaying) waterAudio.UnPause();
    }
    public bool TouchesRunningWater(Collider towel)
    {
        return IsOn && isActiveAndEnabled && !Blocked && waterVolume!=null && waterVolume.enabled && towel!=null &&
            Physics.ComputePenetration(towel,towel.transform.position,towel.transform.rotation,
                waterVolume,waterVolume.transform.position,waterVolume.transform.rotation,out _,out _);
    }
}
