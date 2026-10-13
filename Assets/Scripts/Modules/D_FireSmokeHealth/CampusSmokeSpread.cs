using System;
using System.Collections.Generic;
using UnityEngine;

// Staged emitters approximate room -> corridor -> stairwell -> lower-floor smoke flow.
// This is a training visual, not a fluid/fire-safety simulation.
public sealed class CampusSmokeSpread : MonoBehaviour
{
    [Serializable]
    public sealed class Stage
    {
        public ParticleSystem particles;
        public CampusSmokeVolume exposure;
        public bool roomClock;
        public int floor = 4;
        public float delay, fillSeconds = 6, emissionRate = 7;
        public float minimumSize = .65f, maximumSize = 1.05f;
        public float settlingDepth;
    }
    [SerializeField] private CampusHingeInteractable roomDoor;
    [SerializeField] private ParticleSystem doorwaySmoke;
    [SerializeField] private Stage[] stages = Array.Empty<Stage>();
    [SerializeField] private float escapeDelay = 8;
    private Vector3 doorwayClosedPosition;
    private bool doorwayPositionCached;
    private ResolutionCoordinator resolution;
    private readonly Dictionary<ParticleSystem,Vector3> sourcePositions=new Dictionary<ParticleSystem,Vector3>();
    public float RoomAge { get; private set; }
    public float CorridorAge { get; private set; }
    public CampusHingeInteractable RoomDoor => roomDoor;
    public Stage[] Stages => stages;
    public void Configure(CampusHingeInteractable door, ParticleSystem portal, Stage[] progression)
    {
        roomDoor = door; doorwaySmoke = portal; stages = progression;
        ResetSpread();
    }
    private void Awake()
    { resolution = FindObjectOfType<ResolutionCoordinator>(); ResetSpread(); }
    public void ResetSpread()
    {
        RoomAge = CorridorAge = 0;
        if (doorwaySmoke != null && !doorwayPositionCached) { doorwayClosedPosition = doorwaySmoke.transform.position; doorwayPositionCached = true; }
        foreach (var stage in stages)
        {
            if (stage.exposure != null) stage.exposure.enabled = false;
            if (stage.particles == null) continue;
            if(!sourcePositions.ContainsKey(stage.particles)) sourcePositions[stage.particles]=stage.particles.transform.position;
            if(stage.settlingDepth>0) stage.particles.transform.position=sourcePositions[stage.particles];
            stage.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stage.particles.main; main.prewarm = false;
            var emission = stage.particles.emission; emission.rateOverTime = 0;
            if (Application.isPlaying) stage.particles.Play();
        }
    }
    private void Update() { if(CampusLessonDirector.Instance==null || !CampusLessonDirector.Instance.SmokeFrozen) Advance(Time.deltaTime); }
    public void Advance(float dt)
    {
        // MODEL API PLACEHOLDER: keep API clock, but no advancing particles or exposure volumes.
        if (dt <= 0 || Time.timeScale == 0 || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None)) return;
        RoomAge += dt;
    }
}
