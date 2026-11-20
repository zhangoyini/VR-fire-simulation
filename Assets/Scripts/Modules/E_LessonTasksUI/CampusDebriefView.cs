using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Status = CampusExerciseTasks.Status;

internal sealed class CampusDebriefView
{
    private const int DetailCount = 5;
    private static readonly Color Background = new Color(.045f, .075f, .11f);
    private static readonly Color Accent = new Color(.20f, .30f, .36f);
    private static readonly Color Success = new Color(.20f, .40f, .25f);
    private static readonly Color Failure = new Color(.64f, .19f, .15f);

    private readonly CampusDebriefReport report;
    private readonly TMP_FontAsset font;
    private readonly SystemUIAudio audio;
    private readonly VRMenuRayPointer[] rays;
    private readonly GameObject summaryPanel, detailsPanel;
    private readonly CampusUIForeground foreground;
    private readonly Image[] stars = new Image[5];
    private readonly Sprite earnedStar;
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
        if (panel.TryGetComponent<Image>(out var panelImage)) panelImage.enabled = false;
        Transform root = Group("Campus debrief", panel.transform).transform;
        root.localScale = Vector3.one * (.00094f / Mathf.Max(.00001f, panel.transform.lossyScale.x));
        summaryPanel = Group("Outcome summary", root);
        var artwork = CampusTutorialView.Picture(summaryPanel.transform, Vector2.zero, new Vector2(820, 512.5f), Color.white,
            FireTrainingUIStyle.Load(report.success ? "panels/result_success" : "panels/result_failure"));
        artwork.name = report.success ? "成功逃离" : "逃离失败";
        earnedStar = FireTrainingUIStyle.Load("decorations/rating_earned");
        var emptyStar = FireTrainingUIStyle.Load("decorations/rating_empty");
        float spacing = report.success ? 74f : 95f;
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i] = CampusTutorialView.Picture(summaryPanel.transform,
                new Vector2((i - 2) * spacing, report.success ? 9f : 15f), Vector2.one * 92f,
                new Color(1, 1, 1, .45f), emptyStar);
            stars[i].name = $"Rating star {i + 1}";
        }
        var viewDetails = Group("查看明细", summaryPanel.transform).GetComponent<RectTransform>();
        viewDetails.anchoredPosition = report.success ? new Vector2(6, -124) : new Vector2(4, -149);
        viewDetails.sizeDelta = new Vector2(359, 68);
        Label(summaryPanel.transform, viewDetails.anchoredPosition + new Vector2(0, -19), new Vector2(260, 21), 15,
            "View details").color = new Color32(246, 233, 204, 255);
        BindButton(viewDetails.gameObject, viewDetails.sizeDelta, () => Browse(1));
        Label(summaryPanel.transform, new Vector2(0, report.success ? -76 : -88), new Vector2(640, 22), 15,
            report.success ? "You evacuated safely. Review your drill details." :
            "You did not evacuate safely. Review your actions.");

        detailsPanel = Group("Drill details", root);
        Transform details = detailsPanel.transform;
        var paper = CampusTutorialView.Picture(details, Vector2.zero, new Vector2(820, 520), Background);
        FireTrainingUIStyle.Apply(paper, "panels/report_sheet_plain");
        CampusTutorialView.Picture(details, new Vector2(0, 209), new Vector2(720, 5), report.success ? Success : Failure);
        eyebrow = Label(details, new Vector2(0, 181), new Vector2(650, 32), 20, "");
        eyebrow.color = Accent;
        title = Label(details, new Vector2(0, 126), new Vector2(650, 76), 37, "");
        body = Label(details, new Vector2(-169, -19), new Vector2(316, 192), 23, "");
        body.alignment = TextAlignmentOptions.TopLeft;
        englishBody = Label(details, new Vector2(169, -19), new Vector2(316, 192), 22, "");
        englishBody.alignment = TextAlignmentOptions.TopLeft;

        Button(details, new Vector2(-223, -149), "上一页", "Previous", () => Browse(detailPage == 1 ? DetailCount : detailPage - 1));
        Button(details, new Vector2(0, -149), "返回结算", "Back to summary", () => Browse(0));
        Button(details, new Vector2(223, -149), "下一页", "Next", () => Browse(detailPage == DetailCount ? 1 : detailPage + 1), true);
        Button(details, new Vector2(-111, -205), "重新开始", "Restart", navigation.RestartGame);
        Button(details, new Vector2(111, -205), "返回主菜单", "Main menu", navigation.ReturnToMainMenu);
        detailsPanel.SetActive(false);
        foreground = root.gameObject.AddComponent<CampusUIForeground>();
        foreground.Configure(Shader.Find("Campus/Foreground UI"));
    }

    internal IEnumerator Play()
    {
        yield return new WaitUntil(() => summaryPanel == null || summaryPanel.activeInHierarchy || detailPage != 0);
        yield return new WaitForSecondsRealtime(.3f);
        int rating = report.Stars;
        for (int i = 0; i < stars.Length && summaryPanel != null; i++)
        {
            var star = stars[i];
            bool earned = i < rating;
            if (earned) star.sprite = earnedStar;
            star.color = Color.white;
            if (summaryPanel.activeInHierarchy) audio?.PlayRatingStar(earned);
            const float duration = .24f;
            for (float elapsed = 0; elapsed < duration && summaryPanel.activeInHierarchy; elapsed += Time.unscaledDeltaTime)
            {
                star.rectTransform.localScale = Vector3.one * (1f + .22f * Mathf.Sin(Mathf.PI * elapsed / duration));
                yield return null;
            }
            star.rectTransform.localScale = Vector3.one;
            if (summaryPanel.activeInHierarchy) yield return new WaitForSecondsRealtime(.09f);
        }
    }

    private void Browse(int page)
    {
        detailPage = page;
        bool summary = page == 0;
        summaryPanel.SetActive(summary);
        detailsPanel.SetActive(!summary);
        if (summary) return;
        eyebrow.text = $"本轮明细 / Drill details   {page} / {DetailCount}";

        switch (page)
        {
            case 1:
                title.text = Bilingual("演练概览", "Drill overview");
                string result = report.success ? "已安全撤离" : report.depleted ? "血量耗尽" : "演练结束";
                string resultEnglish = report.success ? "Evacuated safely" : report.depleted ? "Health depleted" : "Drill ended";
                body.text = $"{result}\n结束位置  {report.endLocation}\n正式演练用时  {Clock(report.duration)}\n" +
                    $"剩余血量  {Points(report.endHealth)} / {Points(report.initialHealth)}\n评星  {report.Stars} / 5";
                englishBody.text = $"{resultEnglish}\nEnd location  {report.endLocationEnglish}\nDrill time  {Clock(report.duration)}\n" +
                    $"Health left  {Points(report.endHealth)} / {Points(report.initialHealth)}\nRating  {report.Stars} / 5";
                if (!report.success && !string.IsNullOrEmpty(report.currentTask))
                {
                    body.text += "\n结束时的任务  " + report.currentTask;
                    englishBody.text += "\nTask at end  " + report.currentTaskEnglish;
                }
                break;
            case 2:
                title.text = Bilingual("任务完成情况", "Task progress");
                string rescue = report.rescue == Status.Hidden ? "未触发" : report.rescue == Status.Complete ? "救助成功" :
                    report.rescueDelivered ? "已送达，未呼救" : report.rescueStarted ? "救助未完成" : "未参与";
                string rescueEnglish = report.rescue == Status.Hidden ? "Not triggered" : report.rescue == Status.Complete ? "Rescued" :
                    report.rescueDelivered ? "Delivered, call pending" : report.rescueStarted ? "Incomplete" : "Skipped";
                body.text = $"1  寻找毛巾    {State(report.tasks[0])}\n2  浸湿毛巾并下到三楼    {State(report.tasks[1])}\n" +
                    $"3  遇阻后改走 B 楼梯    {State(report.tasks[2])}\n4  找到出口并撤离    {State(report.tasks[3])}\n" +
                    $"可选 轮椅救援    {rescue}\n" +
                    $"<size=82%>触发 {YesNo(report.rescue != Status.Hidden)} 开始 {YesNo(report.rescueStarted)}\n" +
                    $"送达 {YesNo(report.rescueDelivered)} 完成 {YesNo(report.rescue == Status.Complete)}</size>";
                englishBody.text = $"1  Find a towel {State(report.tasks[0], true)}\n2  Wet towel, reach 3F {State(report.tasks[1], true)}\n" +
                    $"3  Detour via Stair B {State(report.tasks[2], true)}\n4  Find exit and evacuate {State(report.tasks[3], true)}\n" +
                    $"Optional rescue {rescueEnglish}\n" +
                    $"<size=82%>Triggered {YesNo(report.rescue != Status.Hidden, true)} Started {YesNo(report.rescueStarted, true)}\n" +
                    $"Delivered {YesNo(report.rescueDelivered, true)} Complete {YesNo(report.rescue == Status.Complete, true)}</size>";
                break;
            case 3:
                title.text = Bilingual("血量与受伤", "Health and damage");
                int worst = 0;
                for (int i = 1; i < report.smokeLossByFloor.Length; i++)
                    if (report.smokeLossByFloor[i] > report.smokeLossByFloor[worst]) worst = i;
                body.text = $"初始 {Points(report.initialHealth)} 剩余 {Points(report.endHealth)}\n累计损失  {Points(report.TotalLoss)} 点\n" +
                    $"基础时间消耗  {Points(report.timeLoss)} 点\n烟雾伤害  {Points(report.smokeLoss)} 点" +
                    (report.otherLoss > 0 ? $"\n其他扣血  {Points(report.otherLoss)} 点" : "") +
                    (report.smokeLossByFloor[worst] > 0 ? $"\n烟雾扣血最多：{worst + 1} 楼，{Points(report.smokeLossByFloor[worst])} 点" : "");
                englishBody.text = $"Start {Points(report.initialHealth)} End {Points(report.endHealth)}\nTotal lost  {Points(report.TotalLoss)} HP\n" +
                    $"Time-based loss  {Points(report.timeLoss)} HP\nSmoke damage  {Points(report.smokeLoss)} HP" +
                    (report.otherLoss > 0 ? $"\nOther loss  {Points(report.otherLoss)} HP" : "") +
                    (report.smokeLossByFloor[worst] > 0 ? $"\nMost smoke damage  {worst + 1}F {Points(report.smokeLossByFloor[worst])} HP" : "");
                break;
            case 4:
                title.text = Bilingual("毛巾防护", "Towel protection");
                string coverage = report.smokeSeconds > 0
                    ? $"有效防护 {report.protectedSeconds:0.#} / {report.smokeSeconds:0.#} 秒 {report.protectedSeconds / report.smokeSeconds:P0}"
                    : "无烟雾暴露";
                body.text = $"拿到毛巾  {Done(report.towelPickedUp)}\n打湿毛巾  {Done(report.towelWet)}\n" +
                    $"遮住口鼻  {Done(report.firstProtection >= 0)}\n" +
                    (report.firstProtection >= 0 ? $"首次启用防护  {Clock(report.firstProtection)}\n" : "") +
                    $"实际烟雾中  {coverage}";
                string coverageEnglish = report.smokeSeconds > 0
                    ? $"In smoke {report.protectedSeconds:0.#} / {report.smokeSeconds:0.#} s protected ({report.protectedSeconds / report.smokeSeconds:P0})"
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
        // TextMesh Pro may create extra glyph meshes as each Chinese detail page opens.
        Canvas.ForceUpdateCanvases();
        foreground.Refresh();
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

    private void Button(Transform parent, Vector2 position, string label, string english, UnityAction action, bool primary = false)
    {
        var size = new Vector2(197, 53);
        var image = CampusTutorialView.Picture(parent, position, size,
            primary ? new Color(.08f, .39f, .49f) : new Color(.14f, .22f, .30f));
        image.name = label;
        Label(image.transform, Vector2.zero, new Vector2(181, 48), 22, Bilingual(label, english));
        BindButton(image.gameObject, size, action);
    }

    private void BindButton(GameObject target, Vector2 size, UnityAction action)
    {
        var collider = target.AddComponent<BoxCollider>();
        collider.size = new Vector3(size.x, size.y, 18);
        collider.isTrigger = true;
        var button = target.AddComponent<VRMenuButton>();
        button.Bind(action, audio);
        foreach (var ray in rays) ray.RegisterMenuButton(button);
    }
}
