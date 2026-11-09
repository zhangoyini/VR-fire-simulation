using System;
using System.Collections.Generic;
using Harbourview.Journey;
using Jiao.HealthTime;
using UnityEngine;

[Serializable]
internal sealed class CampusDebriefReport
{
    public float initialHealth, endHealth, timeLoss, smokeLoss, otherLoss, duration;
    public float smokeSeconds, protectedSeconds, teleportSmokeSeconds, teleportProtectedSeconds;
    public float firstProtection = -1f;
    public float[] smokeLossByFloor = new float[4];
    public List<int> floors = new List<int>();
    public bool towelPickedUp, towelWet, rescueStarted, rescueDelivered, eastStairReached;
    public bool success, depleted;
    public string endLocation, currentTask;
    internal string endLocationEnglish, currentTaskEnglish;
    public CampusExerciseTasks.Status[] tasks = new CampusExerciseTasks.Status[4];
    public CampusExerciseTasks.Status rescue;

    private bool wasProtected;
    internal float TotalLoss => timeLoss + smokeLoss + otherLoss;

    internal void Observe(int floor, CampusTowelInteractable towel, bool protection, float elapsed)
    {
        if (floors.Count == 0 || floors[floors.Count - 1] != floor) floors.Add(floor);
        if (towel != null)
        {
            towelPickedUp |= towel.IsHeld;
            towelWet |= towel.IsWet;
        }
        ObserveProtection(protection, elapsed);
    }

    internal void ObserveProtection(bool protection, float elapsed)
    {
        if (protection == wasProtected) return;
        wasProtected = protection;
        if (protection && firstProtection < 0) firstProtection = elapsed;
        SessionLogger.Write(protection ? "TOWEL_PROTECTION_STARTED" : "TOWEL_PROTECTION_ENDED");
    }

    internal void MeasureSmoke(float seconds, bool protection, bool teleport)
    {
        if (teleport)
        {
            teleportSmokeSeconds += seconds;
            if (protection) teleportProtectedSeconds += seconds;
        }
        else
        {
            smokeSeconds += seconds;
            if (protection) protectedSeconds += seconds;
        }
    }

    internal void AddLoss(float amount, HealthTimeController.LossSource source, Vector3? position)
    {
        switch (source)
        {
            case HealthTimeController.LossSource.Time: timeLoss += amount; break;
            case HealthTimeController.LossSource.Smoke:
                smokeLoss += amount;
                if (position.HasValue)
                {
                    int floor = Mathf.Clamp(Mathf.FloorToInt((position.Value.y - .8f) / 3.8f), 0, 3);
                    smokeLossByFloor[floor] += amount;
                }
                break;
            default: otherLoss += amount; break;
        }
    }

    internal void Finish(CampusLessonDirector lesson, CampusRescueStudent student)
    {
        var phase = lesson.CurrentPhase;
        var exercise = lesson.ExerciseTasks;
        tasks[0] = phase >= CampusLessonDirector.Phase.WetTowel ? CampusExerciseTasks.Status.Complete :
            phase == CampusLessonDirector.Phase.FindTowel ? CampusExerciseTasks.Status.Active : CampusExerciseTasks.Status.Hidden;
        tasks[1] = phase == CampusLessonDirector.Phase.Complete ? CampusExerciseTasks.Status.Complete :
            phase >= CampusLessonDirector.Phase.WetTowel ? CampusExerciseTasks.Status.Active : CampusExerciseTasks.Status.Hidden;
        if (exercise == null) return;
        tasks[2] = exercise.DetourStatus;
        tasks[3] = exercise.ExitStatus;
        eastStairReached = exercise.ReachedEastStair;
        currentTask = exercise.CurrentTaskName(phase);
        currentTaskEnglish = exercise.CurrentTaskName(phase, english: true);
        rescue = exercise.RescueStatus;
        rescueStarted = student != null && student.AssistanceStarted;
        rescueDelivered = student != null && student.Current == CampusRescueStudent.State.Delivered;
    }
}
