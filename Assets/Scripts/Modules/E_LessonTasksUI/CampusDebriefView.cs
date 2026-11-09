using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Status = CampusExerciseTasks.Status;

internal sealed class CampusDebriefView
{
    private const float SummarySeconds = 2f;
    private const float DetailSeconds = 2.5f;
    private const int DetailCount = 4;
    private static readonly Color Background = new Color(.045f, .075f, .11f);
    private static readonly Color Accent = new Color(.27f, .87f, .94f);
    private static readonly Color Success = new Color(.50f, .92f, .69f);
    private static readonly Color Failure = new Color(1f, .69f, .60f);

    private readonly CampusDebriefReport report;
    private readonly TMP_FontAsset font;
    private readonly SystemUIAudio audio;
    private readonly VRMenuRayPointer[] rays;
    private readonly GameObject summaryActions, detailActions;
    private readonly RectTransform healthMeter;
    private readonly TMP_Text eyebrow, title, body, englishBody;
    private int detailPage;

    internal CampusDebriefView(GameObject panel, TMP_FontAsset font, SystemUINavigation navigation,
        SystemUIAudio audio, CampusDebriefReport report)
    {
        this.report = report;
        this.font = font;
        this.audio = audio;
        rays = Object.FindObjectsOfType<VRMenuRayPointer>(true);

        // Reuse the campus outcome canvas and its tracked placement.
        foreach (Transform child in panel.transform) child.gameObject.SetActive(false);
        Transform root = Group("Campus debrief", panel.transform).transform;
        root.localScale = Vector3.one * (.00094f / Mathf.Max(.00001f, panel.transform.lossyScale.x));
        CampusTutorialView.Picture(root, Vector2.zero, new Vector2(720, 430), Background);
        CampusTutorialView.Picture(root, new Vector2(0, 209), new Vector2(720, 5), report.success ? Success : Failure);

        eyebrow = Label(root, new Vector2(0, 181), new Vector2(650, 32), 20, "");
        eyebrow.color = Accent;
        title = Label(root, new Vector2(0, 126), new Vector2(650, 76), 37, "");
        body = Label(root, new Vector2(0, -4), new Vector2(640, 172), 25, "");
        englishBody = Label(root, new Vector2(169, -19), new Vector2(316, 192), 22, "");
        englishBody.alignment = TextAlignmentOptions.TopLeft;

        var track = CampusTutorialView.Picture(root, new Vector2(0, -112), new Vector2(248, 8), new Color(.17f, .25f, .34f));
        healthMeter = track.rectTransform;
        float fraction = report.initialHealth > 0 ? Mathf.Clamp01(report.endHealth / report.initialHealth) : 0;
        CampusTutorialView.Picture(healthMeter, new Vector2((fraction - 1f) * 124f, 0), new Vector2(fraction * 248f, 8), Success);

        summaryActions = Group("Debrief summary actions", root);
        Button(summaryActions.transform, -223, "查看明细", "View details", () => Browse(1), true);
        Button(summaryActions.transform, 0, "重新开始", "Restart", navigation.RestartGame);
        Button(summaryActions.transform, 223, "返回主菜单", "Main menu", navigation.ReturnToMainMenu);
        detailActions = Group("Debrief detail actions", root);
        Button(detailActions.transform, -223, "上一页", "Previous", () => Browse(detailPage == 1 ? DetailCount : detailPage - 1));
        Button(detailActions.transform, 0, "返回结算", "Back to summary", () => Render(0, true));
        Button(detailActions.transform, 223, "下一页", "Next", () => Browse(detailPage == DetailCount ? 1 : detailPage + 1), true);
        summaryActions.SetActive(false);
        detailActions.SetActive(false);
        var foreground = root.gameObject.AddComponent<CampusUIForeground>();
        foreground.Configure(Shader.Find("Campus/Foreground UI"));
    }

    internal IEnumerator Play()
    {
        Render(0, false);
        yield return new WaitForSecondsRealtime(SummarySeconds);
        for (int page = 1; page <= DetailCount; page++)
        {
            Render(page, false);
            yield return new WaitForSecondsRealtime(DetailSeconds);
        }
        Render(0, true);
    }

    private void Browse(int page)
    {
        detailPage = page;
        Render(page, true);
    }

    private void Render(int page, bool interactive)
    {
        bool summary = page == 0;
        summaryActions.SetActive(interactive && summary);
        detailActions.SetActive(interactive && !summary);
        healthMeter.gameObject.SetActive(summary);
        eyebrow.text = summary ? "校园消防疏散演练 / Campus Fire Drill" : $"本轮明细 / Drill details   {page} / {DetailCount}";
        englishBody.gameObject.SetActive(!summary);
        body.alignment = summary ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        body.rectTransform.anchoredPosition = summary ? new Vector2(0, -4) : new Vector2(-169, -19);
        body.rectTransform.sizeDelta = summary ? new Vector2(640, 172) : new Vector2(316, 192);
        body.fontSizeMin = 18f;
        body.fontSizeMax = summary ? 25f : 23f;
        title.color = summary ? report.success ? Success : Failure : Color.white;
        if (summary)
        {
            title.text = report.success ? Bilingual("成功撤离", "Evacuation successful") : Bilingual("演练失败", "Evacuation incomplete");
            string result = report.success ? "已到达" + report.endLocation :
                (report.depleted ? "血量耗尽" : "演练结束") + " · " + report.endLocation;
            string englishResult = (report.success ? "Reached " : report.depleted ? "Health depleted · " : "Drill ended · ") + report.endLocationEnglish;
            string task = report.success || string.IsNullOrEmpty(report.currentTask) ? "" :
                "\n" + Bilingual("结束时的任务 · " + report.currentTask, "Task at end · " + report.currentTaskEnglish);
            body.text = Bilingual(result, englishResult) +
                $"\n正式演练用时 / Drill time   {Clock(report.duration)}\n剩余血量 / Health left   {Points(report.endHealth)} / {Points(report.initialHealth)}" + task;
            return;
        }

        switch (page)
        {
            case 1:
                title.text = Bilingual("任务完成情况", "Task progress");
                string rescue = report.rescue == Status.Hidden ? "未触发" : report.rescue == Status.Complete ? "救助成功" :
                    report.rescueDelivered ? "未完成 · 已送达，待按铃" : report.rescueStarted ? "救助未完成" : "未参与";
                string rescueEnglish = report.rescue == Status.Hidden ? "Not triggered" : report.rescue == Status.Complete ? "Rescued" :
                    report.rescueDelivered ? "Delivered, call pending" : report.rescueStarted ? "Incomplete" : "Skipped";
                body.text = $"1  寻找毛巾    {State(report.tasks[0])}\n2  浸湿毛巾并下到三楼    {State(report.tasks[1])}\n" +
                    $"3  遇阻后改走 B 楼梯    {State(report.tasks[2])}\n4  打开操场出口    {State(report.tasks[3])}\n" +
                    $"可选 · 轮椅救援    {rescue}\n" +
                    $"<size=82%>触发 {YesNo(report.rescue != Status.Hidden)}  ·  开始 {YesNo(report.rescueStarted)}\n" +
                    $"送达 {YesNo(report.rescueDelivered)}  ·  按铃 {YesNo(report.rescue == Status.Complete)}</size>";
                englishBody.text = $"1  Find a towel · {State(report.tasks[0], true)}\n2  Wet towel, reach 3F · {State(report.tasks[1], true)}\n" +
                    $"3  Detour via Stair B · {State(report.tasks[2], true)}\n4  Open schoolyard exit · {State(report.tasks[3], true)}\n" +
                    $"Optional rescue · {rescueEnglish}\n" +
                    $"<size=82%>Triggered {YesNo(report.rescue != Status.Hidden, true)} · Started {YesNo(report.rescueStarted, true)}\n" +
                    $"Delivered {YesNo(report.rescueDelivered, true)} · Call made {YesNo(report.rescue == Status.Complete, true)}</size>";
                break;
            case 2:
                title.text = Bilingual("血量与受伤", "Health and damage");
                int worst = 0;
                for (int i = 1; i < report.smokeLossByFloor.Length; i++)
                    if (report.smokeLossByFloor[i] > report.smokeLossByFloor[worst]) worst = i;
                body.text = $"初始 {Points(report.initialHealth)}  ·  剩余 {Points(report.endHealth)}\n累计损失  {Points(report.TotalLoss)} 点\n" +
                    $"基础时间消耗  {Points(report.timeLoss)} 点\n烟雾伤害  {Points(report.smokeLoss)} 点" +
                    (report.otherLoss > 0 ? $"\n其他扣血  {Points(report.otherLoss)} 点" : "") +
                    (report.smokeLossByFloor[worst] > 0 ? $"\n烟雾扣血最多：{worst + 1} 楼，{Points(report.smokeLossByFloor[worst])} 点" : "");
                englishBody.text = $"Start {Points(report.initialHealth)} · End {Points(report.endHealth)}\nTotal lost  {Points(report.TotalLoss)} HP\n" +
                    $"Time-based loss  {Points(report.timeLoss)} HP\nSmoke damage  {Points(report.smokeLoss)} HP" +
                    (report.otherLoss > 0 ? $"\nOther loss  {Points(report.otherLoss)} HP" : "") +
                    (report.smokeLossByFloor[worst] > 0 ? $"\nMost smoke damage  {worst + 1}F · {Points(report.smokeLossByFloor[worst])} HP" : "");
                break;
            case 3:
                title.text = Bilingual("毛巾防护", "Towel protection");
                string coverage = report.smokeSeconds > 0
                    ? $"有效防护 {report.protectedSeconds:0.#} / {report.smokeSeconds:0.#} 秒 · {report.protectedSeconds / report.smokeSeconds:P0}"
                    : "无烟雾暴露";
                body.text = $"拿到毛巾  {Done(report.towelPickedUp)}\n打湿毛巾  {Done(report.towelWet)}\n" +
                    $"遮住口鼻  {Done(report.firstProtection >= 0)}\n" +
                    (report.firstProtection >= 0 ? $"首次启用防护  {Clock(report.firstProtection)}\n" : "") +
                    $"实际烟雾中  {coverage}";
                string coverageEnglish = report.smokeSeconds > 0
                    ? $"In smoke · {report.protectedSeconds:0.#} / {report.smokeSeconds:0.#} s protected ({report.protectedSeconds / report.smokeSeconds:P0})"
                    : "No smoke exposure";
                englishBody.text = $"Towel picked up  {YesNo(report.towelPickedUp, true)}\nTowel wetted  {YesNo(report.towelWet, true)}\n" +
                    $"Nose and mouth covered  {YesNo(report.firstProtection >= 0, true)}\n" +
                    (report.firstProtection >= 0 ? $"First used at  {Clock(report.firstProtection)}\n" : "") +
                    coverageEnglish;
                break;
            default:
                title.text = Bilingual("路线与改道", "Route and detour");
                var floorLabels = report.floors.ConvertAll(floor => floor + "F");
                if (floorLabels.Count > 8)
                {
                    floorLabels.RemoveRange(1, floorLabels.Count - 7);
                    floorLabels.Insert(1, "…");
                }
                string floors = string.Join(" → ", floorLabels);
                body.text = $"经过楼层  {floors}\nA 楼梯障碍提示  {(report.tasks[2] == Status.Hidden ? "未触发" : "已触发")}\n" +
                    $"B 楼梯口  {(report.eastStairReached ? "已到达" : "未到达")}\n操场出口  {State(report.tasks[3])}\n集合点  {(report.success ? "已到达" : "未到达")}";
                englishBody.text = $"Floors visited  {floors}\nStair A blockage alert  {(report.tasks[2] == Status.Hidden ? "Not triggered" : "Triggered")}\n" +
                    $"Stair B reached  {YesNo(report.eastStairReached, true)}\nSchoolyard exit  {State(report.tasks[3], true)}\nAssembly point reached  {YesNo(report.success, true)}";
                break;
        }
    }

    private static string Bilingual(string chinese, string english) => $"{chinese}\n<size=78%>{english}</size>";
    private static string State(Status status, bool english = false) => status == Status.Complete ? (english ? "Done" : "已完成") :
        status == Status.Active ? (english ? "Incomplete" : "未完成") : (english ? "Not triggered" : "未触发");
    private static string Done(bool value) => value ? "已完成" : "未完成";
    private static string YesNo(bool value, bool english = false) => value ? (english ? "Yes" : "是") : (english ? "No" : "否");
    private static string Points(float value) => value.ToString("0.#");
    private static string Clock(float seconds)
    {
        int elapsed = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{elapsed / 60:D2}:{elapsed % 60:D2}";
    }

    private static GameObject Group(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.transform.SetParent(parent, false);
        return node;
    }

    private TMP_Text Label(Transform parent, Vector2 position, Vector2 size, int pointSize, string value) =>
        CampusTutorialView.CreateLabel(font, parent, position, size, pointSize, value);

    private void Button(Transform parent, float x, string label, string english, UnityAction action, bool primary = false)
    {
        var image = CampusTutorialView.Picture(parent, new Vector2(x, -161), new Vector2(197, 53),
            primary ? new Color(.08f, .39f, .49f) : new Color(.14f, .22f, .30f));
        image.name = label;
        Label(image.transform, Vector2.zero, new Vector2(181, 48), 22, Bilingual(label, english));
        var collider = image.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(197, 53, 18);
        collider.isTrigger = true;
        var button = image.gameObject.AddComponent<VRMenuButton>();
        button.Bind(action, audio);
        foreach (var ray in rays) ray.RegisterMenuButton(button);
    }
}
