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
    private readonly TMP_Text eyebrow, title, body;
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

        eyebrow = Label(root, new Vector2(0, 181), new Vector2(650, 32), 23, "校园消防疏散演练");
        eyebrow.color = Accent;
        title = Label(root, new Vector2(0, 130), new Vector2(650, 62), 48, "");
        body = Label(root, new Vector2(0, -10), new Vector2(620, 183), 30, "");

        var track = CampusTutorialView.Picture(root, new Vector2(0, -112), new Vector2(248, 8), new Color(.17f, .25f, .34f));
        healthMeter = track.rectTransform;
        float fraction = report.initialHealth > 0 ? Mathf.Clamp01(report.endHealth / report.initialHealth) : 0;
        CampusTutorialView.Picture(healthMeter, new Vector2((fraction - 1f) * 124f, 0), new Vector2(fraction * 248f, 8), Success);

        summaryActions = Group("Debrief summary actions", root);
        Button(summaryActions.transform, -223, "查看明细", () => Browse(1), true);
        Button(summaryActions.transform, 0, "重新开始", navigation.RestartGame);
        Button(summaryActions.transform, 223, "返回主菜单", navigation.ReturnToMainMenu);
        detailActions = Group("Debrief detail actions", root);
        Button(detailActions.transform, -223, "上一页", () => Browse(detailPage == 1 ? DetailCount : detailPage - 1));
        Button(detailActions.transform, 0, "返回结算", () => Render(0, true));
        Button(detailActions.transform, 223, "下一页", () => Browse(detailPage == DetailCount ? 1 : detailPage + 1), true);
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
        eyebrow.text = summary ? "校园消防疏散演练" : $"本轮明细   {page} / {DetailCount}";
        body.alignment = summary ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        body.rectTransform.anchoredPosition = new Vector2(0, summary ? -10 : -22);
        body.rectTransform.sizeDelta = new Vector2(620, summary ? 183 : 226);
        body.fontSizeMin = 20f;
        body.fontSizeMax = summary ? 30f : 27f;
        title.color = summary ? report.success ? Success : Failure : Color.white;
        if (summary)
        {
            title.text = report.success ? "成功撤离" : "演练失败";
            string result = report.success ? "已到达" + report.endLocation :
                (report.depleted ? "血量耗尽" : "演练结束") + " · " + report.endLocation;
            string task = report.success || string.IsNullOrEmpty(report.currentTask) ? "" : "\n结束时的任务 · " + report.currentTask;
            body.text = result + $"\n正式演练用时  {Clock(report.duration)}\n剩余血量  {Points(report.endHealth)} / {Points(report.initialHealth)}" + task;
            return;
        }

        switch (page)
        {
            case 1:
                title.text = "任务完成情况";
                string rescue = report.rescue == Status.Hidden ? "未触发" : report.rescue == Status.Complete ? "救助成功" :
                    report.rescueDelivered ? "未完成 · 已送达，待按铃" : report.rescueStarted ? "救助未完成" : "未参与";
                body.text = $"1  寻找毛巾    {State(report.tasks[0])}\n2  浸湿毛巾并下到三楼    {State(report.tasks[1])}\n" +
                    $"3  遇阻后改走 B 楼梯    {State(report.tasks[2])}\n4  打开操场出口    {State(report.tasks[3])}\n" +
                    $"可选 · 轮椅救援    {rescue}\n" +
                    $"<size=22>触发 {YesNo(report.rescue != Status.Hidden)}  ·  开始 {YesNo(report.rescueStarted)}  ·  送达 {YesNo(report.rescueDelivered)}  ·  按铃 {YesNo(report.rescue == Status.Complete)}</size>";
                break;
            case 2:
                title.text = "血量与受伤";
                int worst = 0;
                for (int i = 1; i < report.smokeLossByFloor.Length; i++)
                    if (report.smokeLossByFloor[i] > report.smokeLossByFloor[worst]) worst = i;
                body.text = $"初始 {Points(report.initialHealth)}  ·  剩余 {Points(report.endHealth)}  ·  累计损失 {Points(report.TotalLoss)}\n" +
                    $"基础时间消耗  {Points(report.timeLoss)} 点\n烟雾伤害  {Points(report.smokeLoss)} 点" +
                    (report.otherLoss > 0 ? $"\n其他扣血  {Points(report.otherLoss)} 点" : "") +
                    (report.smokeLossByFloor[worst] > 0 ? $"\n烟雾扣血最多：{worst + 1} 楼，{Points(report.smokeLossByFloor[worst])} 点" : "");
                break;
            case 3:
                title.text = "毛巾防护";
                string coverage = report.smokeSeconds > 0
                    ? $"有效防护 {report.protectedSeconds:0.#} / {report.smokeSeconds:0.#} 秒 · {report.protectedSeconds / report.smokeSeconds:P0}"
                    : "无烟雾暴露";
                body.text = $"拿到毛巾  {Done(report.towelPickedUp)}   ·   打湿毛巾  {Done(report.towelWet)}\n" +
                    $"遮住口鼻  {Done(report.firstProtection >= 0)}\n" +
                    (report.firstProtection >= 0 ? $"首次启用防护  {Clock(report.firstProtection)}\n" : "") +
                    $"实际烟雾中  {coverage}";
                break;
            default:
                title.text = "路线与改道";
                var floorLabels = report.floors.ConvertAll(floor => floor + "F");
                if (floorLabels.Count > 8)
                {
                    floorLabels.RemoveRange(1, floorLabels.Count - 7);
                    floorLabels.Insert(1, "…");
                }
                string floors = string.Join(" → ", floorLabels);
                body.text = $"经过楼层  {floors}\nA 楼梯障碍提示  {(report.tasks[2] == Status.Hidden ? "未触发" : "已触发")}\n" +
                    $"B 楼梯口  {(report.eastStairReached ? "已到达" : "未到达")}\n操场出口  {State(report.tasks[3])}   ·   集合点  {(report.success ? "已到达" : "未到达")}";
                break;
        }
    }

    private static string State(Status status) => status == Status.Complete ? "已完成" : status == Status.Active ? "未完成" : "未触发";
    private static string Done(bool value) => value ? "已完成" : "未完成";
    private static string YesNo(bool value) => value ? "是" : "否";
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

    private void Button(Transform parent, float x, string label, UnityAction action, bool primary = false)
    {
        var image = CampusTutorialView.Picture(parent, new Vector2(x, -161), new Vector2(197, 53),
            primary ? new Color(.08f, .39f, .49f) : new Color(.14f, .22f, .30f));
        image.name = label;
        Label(image.transform, Vector2.zero, new Vector2(181, 48), 28, label);
        var collider = image.gameObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(197, 53, 18);
        collider.isTrigger = true;
        var button = image.gameObject.AddComponent<VRMenuButton>();
        button.Bind(action, audio);
        foreach (var ray in rays) ray.RegisterMenuButton(button);
    }
}
