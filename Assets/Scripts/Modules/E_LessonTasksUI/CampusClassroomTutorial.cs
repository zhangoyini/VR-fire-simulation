using UnityEngine;
using Valve.VR;

// The markers and room boundary describe Classroom 401 in CampusTraining.
internal sealed class CampusClassroomTutorial
{
    internal enum Step
    {
        LookAround, LookDown, MoveHands, TeleportDemo, FirstLanding, Arrival,
        ToWindow, WindowDemo, OpenWindow, WindowDone, ToButton, ButtonDemo, PressButton,
        ButtonDone, ToBook, GrabDemo, PickUp, InspectBook, ReleaseDemo, PutBack,
        FadePrompt, Health, MapDemo, MapOpen, MapClose, MapFolding, TaskDemo, TaskOpen, TaskClose, TaskFolding,
        MenuPractice, MenuResume, ToDoor, Complete
    }

    internal static readonly Vector3 FirstSpot = new Vector3(-11.94f, 11.4f, 7.03f);
    internal static readonly Vector3 ButtonSpot = new Vector3(-10.82f, 11.4f, 7.15f);
    internal static readonly Vector3 ButtonPosition = new Vector3(-10.18f, 12.60f, 7.15f);
    internal static readonly Vector3 BookSpot = new Vector3(-11.39f, 11.4f, 4.55f);
    internal static readonly Vector3 DoorSpot = new Vector3(-12.20f, 11.4f, 2.82f);
    internal const float PromptSeconds = 7f;
    private const float WindowMotionThreshold = .5f;
    // Keep the previous world-space inset at the free end of the original sash.
    private const float WindowGripInset = .0832f;
    private readonly Transform head;
    private readonly CampusTutorialView view;
    private readonly CampusHingeInteractable window;
    private readonly CampusPracticeBook book;
    private readonly Rigidbody bookBody;
    private readonly Vector3 bookHome, windowContact;
    private readonly CampusTrackedHandPose left, right;
    private Vector3 lookStart, leftLow, rightLow;
    private bool lookStarted, lookedAround, sawHands;
    private bool leftRaised, rightRaised, arrived, wasX, wasY;
    private float age, dwell, placedFor, droppedFor;
    private Vector3? destination;
    internal Step CurrentStep { get; private set; }
    internal Vector3 WindowSpot { get; }
    internal bool ShowingWindowDemo => CurrentStep == Step.WindowDemo;
    internal bool FreeClassroomPractice => CurrentStep == Step.ToDoor;
    internal bool AwaitingMenuResume => CurrentStep == Step.MenuResume;
    internal bool AllowsTeleport => destination.HasValue ||
        CurrentStep == Step.OpenWindow || CurrentStep == Step.PressButton ||
        CurrentStep == Step.PickUp || CurrentStep == Step.InspectBook || CurrentStep == Step.PutBack;
    private bool AllowsWindow => CurrentStep == Step.OpenWindow || CurrentStep == Step.WindowDone ||
        (CurrentStep > Step.WindowDone && AllowsTeleport);

    internal CampusClassroomTutorial(Transform head, Transform rig, Transform pane,
        CampusPracticeBook book, CampusTutorialView view)
    {
        this.head = head;
        this.book = book;
        this.view = view;
        window = pane.GetComponentInParent<CampusHingeInteractable>();
        var glass = pane.GetComponent<BoxCollider>();
        Bounds bounds = glass.bounds;
        bool hingeOnRight = window.transform.position.x > bounds.center.x;
        float freeEdge = hingeOnRight ? bounds.min.x : bounds.max.x;
        float gripX = freeEdge + (hingeOnRight ? WindowGripInset : -WindowGripInset);
        Vector3 patch = new Vector3(gripX,
            Mathf.Clamp(12.80f, bounds.min.y + .10f, bounds.max.y - .10f), bounds.min.z - .015f);
        windowContact = window.transform.InverseTransformPoint(patch);
        WindowSpot = new Vector3(patch.x, FirstSpot.y, bounds.min.z - .40f);
        bookHome = book.transform.position;
        bookBody = book.GetComponent<Rigidbody>();
        foreach (var hand in rig.GetComponentsInChildren<CampusHandInteractor>(true))
        {
            if (hand.InputSource == SteamVR_Input_Sources.LeftHand) left = hand.GetComponent<CampusTrackedHandPose>();
            if (hand.InputSource == SteamVR_Input_Sources.RightHand) right = hand.GetComponent<CampusTrackedHandPose>();
        }
        Enter(Step.LookAround, "看看四周，熟悉一下教室。\n<size=80%>Look around the classroom.</size>");
    }

    internal static bool InRoom(Vector3 point) =>
        point.x >= -19.72f && point.x <= -10.28f && point.z >= 2.30f && point.z <= 9.72f &&
        Mathf.Abs(point.y - 11.4f) <= .15f;

    internal bool AllowsTarget(MonoBehaviour target) =>
        (AllowsWindow && target == window) ||
        ((CurrentStep == Step.PressButton || CurrentStep == Step.ButtonDone || FreeClassroomPractice) && target == view.Button) ||
        ((CurrentStep == Step.PickUp || CurrentStep == Step.InspectBook ||
          CurrentStep == Step.ReleaseDemo || CurrentStep == Step.PutBack) && target == book);

    internal bool ShowsContactMarker(MonoBehaviour target) =>
        AllowsWindow && !FreeClassroomPractice && target == window;

    internal bool CanStartContact(MonoBehaviour target, Vector3 handPoint) =>
        target != window || FreeClassroomPractice || (AllowsWindow &&
            Vector3.Distance(handPoint, window.transform.TransformPoint(windowContact)) <= .17f);

    internal void RecordTeleport(Vector3 point)
    {
        if (destination.HasValue && Vector3.Distance(destination.Value, point) <= .32f)
            arrived = true;
    }

    internal void OnPauseOpened()
    {
        if (CurrentStep != Step.MenuPractice) return;
        Enter(Step.MenuResume, "");
        view.SetVisible(false);
    }

    internal void OnMenuResumed()
    {
        if (!AwaitingMenuResume) return;
        Enter(Step.ToDoor, "现在可以在教室内自由活动。\n到达门前高亮处，即可结束教学并开始演练。\n<size=80%>You can now move freely in the classroom.\nReach the marker by the door to start the drill.</size>", DoorSpot);
    }

    internal bool Tick(float dt, bool x, bool y)
    {
        age += dt;
        bool xDown = x && !wasX, yDown = y && !wasY;
        wasX = x;
        wasY = y;
        view.SetVisible(true);
        view.Tick(age, dt);
        if (CurrentStep == Step.WindowDemo || CurrentStep == Step.OpenWindow || CurrentStep == Step.WindowDone)
            view.SetContact(window.IsHeld ? (Vector3?)null : window.transform.TransformPoint(windowContact));
        if (CurrentStep == Step.ToBook || CurrentStep == Step.GrabDemo || CurrentStep == Step.PickUp)
            view.SetBookHighlight(book.IsHeld ? (Vector3?)null : book.transform.position);

        switch (CurrentStep)
        {
            case Step.LookAround:
                Vector3 heading = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (heading.sqrMagnitude > .15f)
                {
                    if (!lookStarted) { lookStarted = true; lookStart = heading; }
                    lookedAround |= Vector3.Angle(lookStart, heading) >= 35f;
                }
                if (age >= PromptSeconds && lookedAround)
                    Enter(Step.LookDown, "低头看看你的双手\n<size=80%>Look down at your hands.</size>");
                break;
            case Step.LookDown:
                if (!sawHands)
                {
                    dwell = HandsVisible() && head.forward.y < -.25f ? dwell + dt : 0;
                    if (dwell >= .35f)
                    {
                        leftLow = left.transform.position;
                        rightLow = right.transform.position;
                        sawHands = true;
                    }
                }
                if (age >= PromptSeconds && sawHands) Enter(Step.MoveHands, "抬起双手，试着动一动\n<size=80%>Raise both hands and move them.</size>");
                break;
            case Step.MoveHands:
                leftRaised |= Raised(left, leftLow);
                rightRaised |= Raised(right, rightLow);
                if (age >= PromptSeconds && leftRaised && rightRaised)
                    Enter(Step.TeleportDemo, "到前面的标记处\n<size=80%>Move to the marker ahead.</size>", demo: CampusTutorialView.Demo.Teleport, marker: FirstSpot);
                break;
            case Step.TeleportDemo:
                if (age >= PromptSeconds) Enter(Step.FirstLanding, "到前面的标记处\n<size=80%>Move to the marker ahead.</size>", FirstSpot);
                break;
            case Step.FirstLanding:
                if (arrived) Enter(Step.Arrival, "已到达\n<size=80%>Marker reached.</size>", marker: FirstSpot, success: true);
                break;
            case Step.Arrival:
                if (age >= PromptSeconds) Enter(Step.ToWindow, "到窗边看看\n<size=80%>Move to the window.</size>", WindowSpot);
                break;
            case Step.ToWindow:
                if (arrived) Enter(Step.WindowDemo, "");
                break;
            case Step.WindowDemo:
                if (age >= PromptSeconds) Enter(Step.OpenWindow, "抓住远离转轴的高亮处，沿弧线向外推\n<size=80%>Grip the highlighted edge and swing it outward.</size>");
                break;
            case Step.OpenWindow:
                if (window.Angle >= WindowMotionThreshold)
                    Enter(Step.WindowDone, "窗户已打开。门也用同样的方式打开。\n<size=80%>Window open. Doors work the same way.</size>");
                break;
            case Step.WindowDone:
                if (age >= PromptSeconds && !window.IsHeld)
                    Enter(Step.ToButton, "到教学按钮旁\n<size=80%>Move to the practice button.</size>", ButtonSpot);
                break;
            case Step.ToButton:
                if (arrived) Enter(Step.ButtonDemo, "手靠近按钮，按住扳机，再松开\n<size=80%>Reach for the button. Press and release the trigger.</size>", demo: CampusTutorialView.Demo.Press);
                break;
            case Step.ButtonDemo:
                if (age >= PromptSeconds) Enter(Step.PressButton, "试着按下按钮\n<size=80%>Try pressing the button.</size>");
                break;
            case Step.PressButton:
                if (view.Button.Pressed) Enter(Step.ButtonDone, "操作成功\n<size=80%>Button pressed.</size>");
                break;
            case Step.ButtonDone:
                if (age >= PromptSeconds && !view.Button.IsHeld)
                    Enter(Step.ToBook, "到桌边，拿起这本书\n<size=80%>Go to the desk and pick up the book.</size>", BookSpot);
                break;
            case Step.ToBook:
                if (arrived) Enter(Step.GrabDemo, "靠近书本，持续按住扳机\n<size=80%>Reach for the book and hold the trigger.</size>", demo: CampusTutorialView.Demo.Grab);
                break;
            case Step.GrabDemo:
                if (age >= PromptSeconds) Enter(Step.PickUp, "按住扳机，拿起这本书\n<size=80%>Hold the trigger to pick up the book.</size>");
                break;
            case Step.PickUp:
                if (book.IsHeld && (book.transform.position.y >= bookHome.y + .045f ||
                    HorizontalDistance(book.transform.position, bookHome) >= .20f))
                    Enter(Step.InspectBook, "转动手腕，看看手里的书\n<size=80%>Turn your wrist to look at the book.</size>");
                break;
            case Step.InspectBook:
                if (age >= PromptSeconds)
                    Enter(Step.ReleaseDemo, "把书放回桌上\n<size=80%>Put the book back on the desk.</size>", demo: CampusTutorialView.Demo.Release);
                break;
            case Step.ReleaseDemo:
                if (age >= PromptSeconds) Enter(Step.PutBack, "把书放回桌上\n<size=80%>Put the book back on the desk.</size>");
                break;
            case Step.PutBack:
                TickPlacement(dt);
                break;
            case Step.FadePrompt:
                if (age >= .65f) Enter(Step.Health, "上方是你的血量。受伤会扣血，耗尽后演练结束。\n教学期间不会受到伤害。\n<size=80%>Your health is shown above. The drill ends at zero HP.\nYou take no damage during the tutorial.</size>");
                break;
            case Step.Health:
                if (age >= PromptSeconds) Enter(Step.MapDemo, "按左手柄的 X 展开小地图，再按 X 收起\n<size=80%>Press X on the left controller to open or close the map.</size>", demo: CampusTutorialView.Demo.X);
                break;
            case Step.MapDemo:
                if (age >= PromptSeconds) Enter(Step.MapOpen, "按 X 展开小地图\n<size=80%>Press X to open the map.</size>");
                break;
            case Step.MapOpen:
                if (xDown) Enter(Step.MapClose, "再按 X 收起小地图\n<size=80%>Press X again to close the map.</size>");
                break;
            case Step.MapClose:
                if (xDown) Enter(Step.MapFolding, "");
                break;
            case Step.MapFolding:
                if (age >= CampusNavigationPanels.ZoomSeconds)
                    Enter(Step.TaskDemo, "按左手柄的 Y 查看任务详情，再按 Y 收起\n<size=80%>Press Y on the left controller to open or close Tasks.</size>", demo: CampusTutorialView.Demo.Y);
                break;
            case Step.TaskDemo:
                if (age >= PromptSeconds) Enter(Step.TaskOpen, "按 Y 查看任务详情\n<size=80%>Press Y to view Tasks.</size>");
                break;
            case Step.TaskOpen:
                if (yDown) Enter(Step.TaskClose, "再按 Y 收起任务详情\n<size=80%>Press Y again to close Tasks.</size>");
                break;
            case Step.TaskClose:
                if (yDown) Enter(Step.TaskFolding, "");
                break;
            case Step.TaskFolding:
                if (age >= CampusNavigationPanels.ZoomSeconds)
                    Enter(Step.MenuPractice, "按下左摇杆打开菜单。\n用手柄射线选中「继续」，按扳机返回游戏。\n<size=80%>Click the left stick to pause.\nPoint at Resume and press the trigger.</size>", demo: CampusTutorialView.Demo.Menu);
                break;
            case Step.ToDoor:
                if (arrived) Enter(Step.Complete, "操作练习完成\n<size=80%>Tutorial complete.</size>", marker: DoorSpot, success: true);
                break;
            case Step.Complete:
                return age >= PromptSeconds;
        }
        return false;
    }

    private bool HandsVisible() => left != null && right != null &&
        left.PhysicalTrackingValid && right.PhysicalTrackingValid &&
        InView(left.transform.position) && InView(right.transform.position);

    private bool InView(Vector3 position) => Vector3.Angle(head.forward, position - head.position) <= 49f;

    private static bool Raised(CampusTrackedHandPose hand, Vector3 start) =>
        hand.PhysicalTrackingValid && hand.transform.position.y - start.y >= .11f;

    private void TickPlacement(float dt)
    {
        Vector3 position = book.transform.position;
        Vector3 offset = position - bookHome;
        // The tabletop is centred slightly to the left of book 2. Allow a loose fit anywhere on it.
        bool aboveDesk = offset.x >= -.62f && offset.x <= .17f && Mathf.Abs(offset.z) <= .29f;
        bool onDesk = aboveDesk && Mathf.Abs(offset.y) <= .115f;
        bool released = !book.IsHeld;
        bool settled = bookBody.velocity.sqrMagnitude <= .12f;
        placedFor = released && onDesk && settled ? placedFor + dt : 0;
        if (placedFor >= .25f)
        {
            Enter(Step.FadePrompt, "");
            return;
        }
        droppedFor = released && !onDesk ? droppedFor + dt : 0;
        if (released && droppedFor >= .45f &&
            ((aboveDesk && offset.y < -.25f) || offset.y < -1f || !InRoom(new Vector3(position.x, 11.4f, position.z))))
        {
            book.ResetBook();
            Enter(Step.PickUp, "书已回到桌上，重新拿起\n<size=80%>The book is back on the desk. Pick it up again.</size>");
            return;
        }
        view.UpdatePrompt(droppedFor >= .35f ? "重新拿起这本书，再放回桌上\n<size=80%>Pick the book up and put it back on the desk.</size>" : "把书放回桌上\n<size=80%>Put the book back on the desk.</size>");
        view.SetBookHighlight(droppedFor >= .35f ? position : (Vector3?)null);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

    private void Enter(Step step, string prompt, Vector3? landing = null,
        CampusTutorialView.Demo demo = CampusTutorialView.Demo.None, Vector3? marker = null, bool success = false)
    {
        CurrentStep = step;
        age = dwell = placedFor = droppedFor = 0;
        arrived = false;
        destination = landing;
        view.Show(step, prompt, demo);
        view.SetMarker(marker ?? landing, success);
        view.SetPlacement(step == Step.ReleaseDemo || step == Step.PutBack ? bookHome : (Vector3?)null);
    }

    internal void Pause(bool x, bool y)
    {
        wasX = x;
        wasY = y;
        view.SetVisible(false);
    }
    internal void Dispose() => view.Dispose();
}
