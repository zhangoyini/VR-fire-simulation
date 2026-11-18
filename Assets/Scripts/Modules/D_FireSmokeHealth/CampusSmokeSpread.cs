using System;
using System.Collections.Generic;
using UnityEngine;

// Staged emitters approximate room -> corridor -> stairwell -> lower-floor smoke flow.
// This is a training visual, not a fluid/fire-safety simulation.
[DefaultExecutionOrder(45)]
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
        [Range(.25f, 1f)] public float strength = 1f;
        internal float Strength => strength > 0f ? strength : 1f;
        internal float Density { get; set; }
    }
    [SerializeField] private CampusHingeInteractable roomDoor;
    [SerializeField] private ParticleSystem doorwaySmoke;
    [SerializeField] private Stage[] stages = Array.Empty<Stage>();
    [SerializeField] private float escapeDelay = 8;
    private Vector3 doorwayClosedPosition;
    private bool doorwayPositionCached;
    private ResolutionCoordinator resolution;
    private bool lowerFloorClockRunning = true;
    private readonly Dictionary<ParticleSystem,Vector3> sourcePositions=new Dictionary<ParticleSystem,Vector3>();
    public float RoomAge { get; private set; }
    public float CorridorAge { get; private set; }
    public float LowerFloorAge { get; private set; }
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
        RoomAge = CorridorAge = LowerFloorAge = 0;
        lowerFloorClockRunning = true;
        if (doorwaySmoke != null && !doorwayPositionCached) { doorwayClosedPosition = doorwaySmoke.transform.position; doorwayPositionCached = true; }
        foreach (var stage in stages)
        {
            stage.Density = 0f;
            if (stage.exposure != null)
            {
                stage.exposure.Strength = 0f;
                stage.exposure.enabled = false;
            }
            if (stage.particles == null) continue;
            if(!sourcePositions.ContainsKey(stage.particles)) sourcePositions[stage.particles]=stage.particles.transform.position;
            if(stage.settlingDepth>0) stage.particles.transform.position=sourcePositions[stage.particles];
            stage.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stage.particles.main; main.prewarm = false;
            var emission = stage.particles.emission; emission.rateOverTime = 0;
            if (Application.isPlaying) stage.particles.Play();
        }
    }
    public void PrepareOpening()
    {
        ResetSpread();
        lowerFloorClockRunning = false;
        Advance(110f); // Prepare only the fire floor, never the lower-floor clock.
    }
    public void BeginExerciseSpread()
    {
        LowerFloorAge = 0f;
        lowerFloorClockRunning = true;
    }
    private void Update() { if(CampusLessonDirector.Instance==null || !CampusLessonDirector.Instance.SmokeFrozen) Advance(Time.deltaTime); }
    public void Advance(float dt)
    {
        if (dt <= 0 || Time.timeScale == 0 || (resolution != null && resolution.CurrentOutcome != ResolutionCoordinator.Outcome.None)) return;
        float previouslyReady = Mathf.Max(0, RoomAge - escapeDelay);
        RoomAge += dt;
        float readyTime = Mathf.Max(0, RoomAge - escapeDelay) - previouslyReady;
        float openness = roomDoor != null ? Mathf.Clamp01(Mathf.Abs(roomDoor.Angle) / 60f) : 1;
        // Closed doors leak slowly at the bottom; an opened doorway feeds the ceiling plume faster.
        float flowTime = readyTime * Mathf.Lerp(.35f, 1f, openness);
        CorridorAge += flowTime;
        if (lowerFloorClockRunning) LowerFloorAge += flowTime;
        if (doorwaySmoke != null) doorwaySmoke.transform.position = doorwayClosedPosition + Vector3.up * (openness * 1.8f);
        foreach (var stage in stages)
        {
            float age = stage.floor < 4 ? LowerFloorAge : stage.roomClock ? RoomAge : CorridorAge;
            float density = Mathf.Clamp01((age - stage.delay) / Mathf.Max(.1f, stage.fillSeconds));
            stage.Density = density;
            if (stage.particles != null)
            {
                var emission = stage.particles.emission; emission.rateOverTime = stage.emissionRate * density * stage.Strength;
                var main = stage.particles.main;
                float sizeScale = Mathf.Lerp(.6f, 1f, density) * Mathf.Lerp(.72f, 1f, stage.Strength);
                main.startSize = new ParticleSystem.MinMaxCurve(stage.minimumSize * sizeScale, stage.maximumSize * sizeScale);
                if(stage.settlingDepth>0 && sourcePositions.TryGetValue(stage.particles,out var ceilingPosition))
                    stage.particles.transform.position=ceilingPosition-Vector3.up*(stage.settlingDepth*Mathf.SmoothStep(0,1,density));
            }
            // No invisible corridor damage ahead of the visible plume.
            if (stage.exposure != null)
            {
                stage.exposure.Strength = density >= .35f ? stage.Strength * density : 0f;
                stage.exposure.enabled = stage.exposure.Strength > 0f;
            }
        }
    }
}
