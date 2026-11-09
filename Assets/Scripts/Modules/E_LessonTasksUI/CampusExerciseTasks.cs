using System.Text;
using Harbourview.Journey;
using UnityEngine;

internal sealed class CampusExerciseTasks
{
    private enum Status { Hidden, Active, Complete }
    private enum Objective { Assembly, Towel, WetTowel, Detour, Rescue, Exit }

    // The regions follow the 3F landings beside the storage pile and the east stair entrance.
    private static readonly Bounds DebrisApproach = new Bounds(new Vector3(-22.4f, 7.6f, .55f), new Vector3(4.8f, .8f, 3.4f));
    private static readonly Bounds EastStairEntrance = new Bounds(new Vector3(22.6f, 7.6f, .05f), new Vector3(5.2f, .8f, 2.5f));
    private readonly CampusHingeInteractable[] exitDoors;
    private readonly CampusRescueStudent student;
    private Status detour, rescue, exit, assembly;
    private int previousFloor = 4;
    private int revision, renderedRevision = -1;
    private CampusLessonDirector.Phase renderedPhase;
    private string details;

    internal CampusExerciseTasks(Transform exitDoor, CampusRescueStudent student)
    {
        exitDoors = exitDoor != null ? exitDoor.GetComponentsInChildren<CampusHingeInteractable>() :
            System.Array.Empty<CampusHingeInteractable>();
        this.student = student;
    }

    internal void Begin()
    {
        assembly = Status.Active;
        Announce("主任务：疏散到一楼操场\nMain objective: Evacuate to the outdoor assembly area", "TASK_ASSEMBLY_STARTED");
    }

    internal void Tick(Vector3 feet)
    {
        if (detour == Status.Hidden && DebrisApproach.Contains(feet))
        {
            detour = Status.Active;
            JourneyDirector.Instance?.Request(JourneyState.Blocked);
            Announce("任务 3：遇见障碍物，请从另一通道逃生\nTask 3: Route blocked. Use Stair B.", "ROUTE_BLOCKED_REROUTE");
        }
        else if (detour == Status.Active && EastStairEntrance.Contains(feet))
        {
            detour = Status.Complete;
            Announce("任务 3 完成：已到达 B 楼梯口\nTask 3 complete: Reached Stair B", "TASK_DETOUR_COMPLETED");
        }

        int floor = Mathf.Clamp(Mathf.FloorToInt((feet.y + .15f) / 3.8f) + 1, 1, 4);
        if (floor == 2 && previousFloor == 3 && rescue == Status.Hidden && student != null)
        {
            rescue = Status.Active;
            Announce("可选支线：轮椅救援\nOptional: Assist the student in the wheelchair", "TASK_RESCUE_STARTED");
        }
        previousFloor = floor;

        if (floor == 1 && exit == Status.Hidden)
        {
            exit = Status.Active;
            Announce("任务 4：找到并打开通往操场的出口\nTask 4: Find and open the exit to the assembly area", "TASK_EXIT_STARTED");
        }
        if (exit == Status.Active && floor == 1)
        {
            foreach (var door in exitDoors)
            {
                if (Mathf.Abs(door.Angle) < 30f) continue;
                exit = Status.Complete;
                Announce("任务 4 完成：出口门已打开\nTask 4 complete: Exit door opened", "TASK_EXIT_COMPLETED");
                break;
            }
        }
    }

    internal void StudentDelivered()
    {
        if (rescue == Status.Active) revision++;
    }

    internal void ConfirmRescue()
    {
        if (rescue != Status.Active) return;
        if (student.Current != CampusRescueStudent.State.Delivered)
        {
            CampusMessagePresenter.Instance?.Show("先将轮椅同学送进避难区，再按呼叫铃确认。\nBring the student into the refuge before pressing the call button.");
            return;
        }

        rescue = Status.Complete;
        JourneyDirector.Instance?.Request(JourneyState.Refuge);
        SessionLogger.Write("RESCUE_COMPLETED");
        Announce("轮椅救援完成，请继续前往一楼操场。\nRescue complete. Continue to the outdoor assembly area.", "REFUGE_INTERCOM_CONFIRMED");
    }

    internal void CompleteAssembly()
    {
        assembly = Status.Complete;
        revision++;
    }

    internal string Details(CampusLessonDirector.Phase phase)
    {
        if (revision == renderedRevision && phase == renderedPhase) return details;
        renderedRevision = revision;
        renderedPhase = phase;

        var card = new StringBuilder("<b>任务详情</b>");
        Objective current = CurrentObjective(phase);
        string assemblyInstruction = "沿疏散标识前往一楼室外操场的集合区。";
        if (phase == CampusLessonDirector.Phase.OpenDoor)
            assemblyInstruction = "走近 401 教室门，握住扳机推开门，观察门外。";
        AddRow(card, assembly, "主任务 · 疏散到一楼操场", current == Objective.Assembly ? assemblyInstruction : null);
        if (phase >= CampusLessonDirector.Phase.FindTowel)
        {
            bool finding = phase == CampusLessonDirector.Phase.FindTowel;
            AddRow(card, finding ? Status.Active : Status.Complete, "任务 1 · 寻找毛巾",
                current == Objective.Towel ? "到 411 卫生间，按住任一手柄扳机拿起干毛巾。" : null);
        }
        if (phase >= CampusLessonDirector.Phase.WetTowel)
        {
            bool done = phase == CampusLessonDirector.Phase.Complete;
            AddRow(card, done ? Status.Complete : Status.Active, "任务 2 · 浸湿毛巾并下到三楼",
                current == Objective.WetTowel ? "用空闲手打开水龙头，将毛巾在流水中浸湿，握持并遮住口鼻，再沿楼梯下到三楼。" : null);
        }

        AddRow(card, detour, "任务 3 · 改走另一通道",
            current == Objective.Detour ? "遇见障碍物，请从另一通道逃生。返回三楼走廊，前往东侧 B 楼梯口。" : null);
        string rescueInstruction = null;
        if (current == Objective.Rescue)
            rescueInstruction = student.Current == CampusRescueStudent.State.Delivered ? "同学已送达。按避难区的呼叫铃确认救援完成。" :
                "双手靠近轮椅推把，各按一次扳机开始协助。把同学送到避难区，再按呼叫铃。";
        AddRow(card, rescue, "可选支线 · 轮椅救援", rescueInstruction);
        AddRow(card, exit, "任务 4 · 打开通往操场的出口",
            current == Objective.Exit ? "沿一楼走廊找到大厅双开门，握住扳机推开任意一扇门。" : null);
        return details = card.ToString();
    }

    private Objective CurrentObjective(CampusLessonDirector.Phase phase)
    {
        if (exit != Status.Hidden) return exit == Status.Active ? Objective.Exit : Objective.Assembly;
        if (rescue == Status.Active) return Objective.Rescue;
        if (detour == Status.Active) return Objective.Detour;
        if (phase == CampusLessonDirector.Phase.FindTowel) return Objective.Towel;
        if (phase == CampusLessonDirector.Phase.WetTowel || phase == CampusLessonDirector.Phase.Descend)
            return Objective.WetTowel;
        return Objective.Assembly;
    }

    private static void AddRow(StringBuilder card, Status status, string title, string instruction)
    {
        if (status == Status.Hidden) return;
        card.Append(status == Status.Complete ? "\n<color=#81efa0>✓ 已完成</color>  " : "\n<color=#78d9ff>进行中</color>  ");
        card.Append(title);
        if (instruction != null && status == Status.Active)
            card.Append("\n<color=#a7e9dd>").Append(instruction).Append("</color>\n");
    }

    private void Announce(string message, string eventName)
    {
        revision++;
        CampusMessagePresenter.Instance?.Show(message);
        SessionLogger.Write(eventName);
    }
}
