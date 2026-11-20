using System.Text;
using System.Collections.Generic;
using Harbourview.Journey;
using UnityEngine;

internal sealed class CampusExerciseTasks
{
    internal enum Status { Hidden, Active, Complete }
    private enum Objective { Assembly, Towel, WetTowel, Detour, Rescue, Exit }

    // The regions follow the 3F landings beside the storage pile and the east stair entrance.
    private static readonly Bounds EastStairEntrance = new Bounds(new Vector3(22.6f, 7.6f, .05f), new Vector3(5.2f, .8f, 2.5f));
    private readonly Transform exitTransform;
    private readonly Collider[] debris;
    private readonly CharacterController playerBody;
    private bool wasInsideExit;
    private readonly CampusRescueStudent student;
    private readonly CampusNavigationPanels panels;
    private Status detour, rescue, exit, assembly;
    private int previousFloor = 4;
    private int revision, renderedRevision = -1;
    private CampusLessonDirector.Phase renderedPhase;
    private string details;

    internal int Floor => previousFloor;
    internal bool DetourActive => detour == Status.Active;
    internal bool RescueActive => rescue == Status.Active;
    internal bool ExitActive => exit == Status.Active;
    internal bool AssemblyApproach => exit == Status.Complete && assembly == Status.Active;
    internal Status DetourStatus => detour;
    internal Status RescueStatus => rescue;
    internal Status ExitStatus => exit;
    internal bool ReachedEastStair { get; private set; }

    internal CampusExerciseTasks(Transform exitDoor, CampusRescueStudent student, CampusNavigationPanels panels)
    {
        exitTransform = exitDoor;
        var pile = GameObject.Find("3F Stair A collapsed storage pile");
        debris = pile != null ? pile.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
        playerBody = Object.FindObjectOfType<CampusLocomotion>()?.Body;
        this.student = student;
        this.panels = panels;
    }

    internal void Begin()
    {
        assembly = Status.Active;
        revision++;
        SessionLogger.Write("TASK_ASSEMBLY_STARTED");
    }

    internal void TowelPhaseChanged(CampusLessonDirector.Phase phase)
    {
        switch (phase)
        {
            case CampusLessonDirector.Phase.FindTowel:
                panels?.QueueNotice("<b>任务一 Task 1</b>\n\n前往 411 卫生间寻找毛巾。\n<size=83%>Find a towel in restroom 411.</size>", delay: .5f);
                break;
            case CampusLessonDirector.Phase.WetTowel:
                panels?.QueueNotice("<b>任务二 Task 2</b>\n\n已拿到毛巾。到洗手台打湿毛巾，再下到三楼。\n<size=83%>Towel picked up. Wet it at the sink, then head down to 3F.</size>");
                break;
            case CampusLessonDirector.Phase.Complete:
                panels?.QueueNotice("<b>任务二 Task 2</b>\n\n任务已完成，继续前往一楼操场。\n<size=83%>Task complete. Continue to the schoolyard.</size>", complete: true);
                break;
        }
    }

    internal void Tick(Vector3 feet)
    {
        bool atEastStair = EastStairEntrance.Contains(feet);
        if (atEastStair) ReachedEastStair = true;
        // Taking the safe route directly also satisfies the route objective.
        if (atEastStair && detour != Status.Complete)
        {
            detour = Status.Complete;
            Announce("<b>任务三 Task 3</b>\n\n已到达 B 楼梯口，任务完成。即将前往二楼，走廊会有需要帮助的同学。\n<size=83%>Stair B reached. Task complete. You are heading to 2F. A classmate in the corridor will need your help.</size>",
                "TASK_DETOUR_COMPLETED");
        }
        else if (detour == Status.Hidden && TouchesDebris(feet))
        {
            detour = Status.Active;
            JourneyDirector.Instance?.Request(JourneyState.Blocked);
            Announce("<b>任务三 Task 3</b>\n\n前方有障碍物，请从东侧 B 楼梯撤离。\n<size=83%>The route is blocked. Use the east stairs (B).</size>", "ROUTE_BLOCKED_REROUTE");
        }

        int floor = Mathf.Clamp(Mathf.FloorToInt((feet.y + .15f) / 3.8f) + 1, 1, 4);
        if (floor == 2 && detour == Status.Complete && rescue == Status.Hidden && student != null)
        {
            rescue = Status.Active;
            Announce("<b>可选任务 Optional task</b>\n\n与走廊里的同学交谈，选择“跟我来”，带他到二楼避难区。\n<size=83%>Talk to your classmate in the corridor, choose “Follow me”, and lead him to the 2F refuge.</size>",
                "TASK_RESCUE_STARTED");
        }
        previousFloor = floor;

        if (floor == 1 && exit == Status.Hidden && Mathf.Abs(feet.x) >= 19f && Mathf.Abs(feet.x) <= 27f && Mathf.Abs(feet.z) <= 2.5f)
        {
            exit = Status.Active;
            Announce("<b>任务四 Task 4</b>\n\n沿出口标识找到大厅双开门，离开教学楼。\n<size=83%>Follow the exit signs to the lobby double doors and leave the building.</size>", "TASK_EXIT_STARTED", delay: .5f);
        }
        if (floor == 1 && exitTransform != null)
        {
            Vector3 local = exitTransform.InverseTransformPoint(feet);
            if (Mathf.Abs(local.x) <= 2.1f && local.z >= .15f && local.z <= 3f) wasInsideExit = true;
            if (exit == Status.Active && wasInsideExit && Mathf.Abs(local.x) <= 2.1f && local.z < -.25f && local.z >= -4f)
            {
                exit = Status.Complete;
                Announce("<b>任务四 Task 4</b>\n\n已离开教学楼，任务完成。前往操场集合区。\n<size=83%>You have left the building. Task complete. Continue to the schoolyard assembly area.</size>", "TASK_EXIT_COMPLETED", complete: true);
            }
        }
    }

    private bool TouchesDebris(Vector3 feet)
    {
        float radius = playerBody != null ? playerBody.radius : .2f;
        float height = playerBody != null ? playerBody.height : 1.7f;
        float margin = playerBody != null ? playerBody.skinWidth + .025f : .035f;
        var contacts = Physics.OverlapCapsule(feet + Vector3.up * radius,
            feet + Vector3.up * Mathf.Max(radius, height - radius), radius + margin,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var contact in contacts)
            if (System.Array.IndexOf(debris, contact) >= 0) return true;
        return false;
    }

    internal void StudentDelivered()
    {
        if (rescue != Status.Active) return;
        CompleteRescue();
    }

    internal void ConfirmRescue()
    {
        if (rescue != Status.Active) return;
        if (student.Current != CampusRescueStudent.State.Delivered)
        {
            CampusMessagePresenter.Instance?.Show("先与同学交谈，再带他进入避难区。\nTalk to your classmate, then lead him into the refuge.");
            return;
        }

        CompleteRescue();
    }

    private void CompleteRescue()
    {
        rescue = Status.Complete;
        JourneyDirector.Instance?.Request(JourneyState.Refuge);
        SessionLogger.Write("RESCUE_COMPLETED");
        Announce("<b>可选任务 Optional task</b>\n\n轮椅救援已完成，继续前往一楼操场。\n<size=83%>Rescue complete. Continue to the schoolyard.</size>", "TASK_RESCUE_COMPLETED", complete: true);
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

        var card = new StringBuilder("<b>任务详情 / Tasks</b>");
        Objective current = CurrentObjective(phase);
        string assemblyInstruction = AssemblyApproach ? "进入操场高亮集合区并停留。" : "沿疏散标识前往一楼操场集合区。";
        string assemblyEnglish = AssemblyApproach ? "Enter the highlighted assembly area and stay." :
            "Follow exit signs to the 1F schoolyard assembly area.";
        if (phase == CampusLessonDirector.Phase.OpenDoor)
        {
            assemblyInstruction = "握住扳机推开 401 教室门，观察门外。";
            assemblyEnglish = "Hold the trigger to open the classroom door. Check outside.";
        }
        AddRow(card, assembly, "主任务 疏散到一楼操场", "Main Schoolyard",
            current == Objective.Assembly ? assemblyInstruction : null, assemblyEnglish);
        if (phase >= CampusLessonDirector.Phase.FindTowel)
        {
            bool finding = phase == CampusLessonDirector.Phase.FindTowel;
            AddRow(card, finding ? Status.Active : Status.Complete, "任务 1 寻找毛巾", "1 Find a towel",
                current == Objective.Towel ? "到 411 卫生间，按住任一手柄扳机拿起干毛巾。" : null,
                "In restroom 411, hold either trigger to pick up the towel.");
        }
        if (phase >= CampusLessonDirector.Phase.WetTowel)
        {
            bool done = phase == CampusLessonDirector.Phase.Complete;
            AddRow(card, done ? Status.Complete : Status.Active, "任务 2 浸湿毛巾并下到三楼", "2 Wet towel, reach 3F",
                current == Objective.WetTowel ? "用空闲手开水龙头并浸湿毛巾。捂住口鼻，沿楼梯下到三楼。" : null,
                "Use your free hand to wet the towel. Cover your nose and mouth, then take the stairs to 3F.");
        }

        AddRow(card, detour, "任务 3 改走另一通道", "3 Detour via Stair B",
            current == Objective.Detour ? "路线受阻。沿三楼走廊返回，前往东侧 B 楼梯。" : null,
            "Route blocked. Return along the 3F corridor to Stair B on the east side.");
        string rescueInstruction = null, rescueEnglish = null;
        if (current == Objective.Rescue)
        {
            bool delivered = student.Current == CampusRescueStudent.State.Delivered;
            rescueInstruction = delivered ? "同学已安全到达避难区。" :
                "走近同学并选择“跟我来”。每次传送后，同学会平滑跟上；带他进入避难区。";
            rescueEnglish = delivered ? "Your classmate has reached the refuge safely." :
                "Approach your classmate and choose “Follow me”. He will roll after you each time you teleport. Lead him into the refuge.";
        }
        AddRow(card, rescue, "可选支线 轮椅救援", "Optional Rescue", rescueInstruction, rescueEnglish);
        AddRow(card, exit, "任务 4 找到出口并撤离", "4 Find the exit and evacuate",
            current == Objective.Exit ? "沿一楼走廊到大厅双开门，推开任意一扇并走到室外。" : null,
            "Follow the 1F corridor to the double doors. Open either door and cross outside.");
        if (phase >= CampusLessonDirector.Phase.FindTowel)
            card.Append("\n<color=#80551b>安全提示 Safety</color>  烟雾暴露 3 秒后扣血，传送也计时；湿毛巾只能减缓扣血，不能过滤一氧化碳。")
                .Append("\n<size=83%>Smoke costs HP after 3 s, including during teleport. A wet towel slows HP loss but cannot filter carbon monoxide.</size>");
        return details = card.ToString();
    }

    private readonly List<CampusTaskCardList.Entry> cardEntries = new List<CampusTaskCardList.Entry>(6);
    private int cardRevision = -1;
    private CampusLessonDirector.Phase cardPhase;
    internal IReadOnlyList<CampusTaskCardList.Entry> CardEntries(CampusLessonDirector.Phase phase)
    {
        if (cardRevision == revision && cardPhase == phase) return cardEntries;
        cardRevision = revision; cardPhase = phase;
        cardEntries.Clear();
        AddCard(assembly, "主任务 到操场集合", "Main Reach the assembly area");
        bool towelStarted = phase >= CampusLessonDirector.Phase.FindTowel && phase <= CampusLessonDirector.Phase.Complete;
        if (towelStarted)
            AddCard(phase == CampusLessonDirector.Phase.FindTowel ? Status.Active : Status.Complete,
                "1 到 411 寻找毛巾", "1 Find a towel in room 411");
        if (phase >= CampusLessonDirector.Phase.WetTowel && phase <= CampusLessonDirector.Phase.Complete)
            AddCard(phase == CampusLessonDirector.Phase.Complete ? Status.Complete : Status.Active,
                "2 打湿毛巾并下到三楼", "2 Wet towel, reach 3F");
        AddCard(detour, "3 前往 B 楼梯", "3 Reach Stair B");
        AddCard(rescue, "可选 护送同学到二楼避难区", "Optional Escort to 2F refuge");
        AddCard(exit, "4 找到出口并走到室外", "4 Find exit, go outside");
        return cardEntries;
    }
    private void AddCard(Status status, string chinese, string english)
    {
        if (status != Status.Hidden)
            cardEntries.Add(new CampusTaskCardList.Entry(chinese, english, status == Status.Complete));
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

    internal string CurrentTaskName(CampusLessonDirector.Phase phase, bool english = false)
    {
        switch (CurrentObjective(phase))
        {
            case Objective.Towel: return english ? "Task 1 Find a towel" : "任务 1 寻找毛巾";
            case Objective.WetTowel: return english ? "Task 2 Wet the towel and reach 3F" : "任务 2 浸湿毛巾并下到三楼";
            case Objective.Detour: return english ? "Task 3 Detour via Stair B" : "任务 3 改走 B 楼梯";
            case Objective.Rescue:
                if (student != null && student.AssistanceStarted)
                    return english ? "Optional Wheelchair rescue" : "可选支线 轮椅救援";
                break;
            case Objective.Exit: return english ? "Task 4 Find the exit and evacuate" : "任务 4 找到出口并撤离";
            default:
                if (phase == CampusLessonDirector.Phase.OpenDoor)
                    return english ? "Check the exit from room 401" : "观察 401 教室出口";
                break;
        }
        return english ? "Reach the schoolyard assembly point" : "前往操场集合点";
    }

    private static void AddRow(StringBuilder card, Status status, string title, string englishTitle,
        string instruction, string englishInstruction)
    {
        if (status == Status.Hidden) return;
        bool hasInstruction = instruction != null && status == Status.Active;
        bool mainlineTask = title.StartsWith("主任务") || title.StartsWith("任务 ");
        bool highlightCurrent = hasInstruction && mainlineTask;
        card.Append(status == Status.Complete ? "\n<color=#2f6941>✓ 已完成</color>  " :
            highlightCurrent ? "\n<color=#80551b><b>当前目标</b></color>  " :
            mainlineTask ? "\n<color=#315c75>待处理</color>  " : "\n<color=#315c75>进行中</color>  ");
        card.Append(highlightCurrent ? "<b>" : "").Append(title).Append(highlightCurrent ? "</b>" : "")
            .Append("  <size=80%>").Append(englishTitle)
            .Append(status == Status.Complete ? " Done</size>" : " Active</size>");
        if (instruction != null && status == Status.Active)
            card.Append(highlightCurrent ? "\n<color=#24282b>" : "\n<color=#315c55>").Append(instruction)
                .Append("\n<size=83%>").Append(englishInstruction).Append("</size></color>");
    }

    private void Announce(string message, string eventName, bool complete = false, float delay = 0, System.Action onFinished = null)
    {
        revision++;
        panels?.QueueNotice(message, complete, delay, onFinished);
        SessionLogger.Write(eventName);
    }
}
