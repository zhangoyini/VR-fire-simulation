using TMPro;
using UnityEngine;

public sealed class CampusMessagePresenter : MonoBehaviour
{
    public const string SmokeWarning = "警告：请离开烟雾区\nWarning: Please leave the smoke area.";
    public const string SmokeRules = "烟雾暴露累计超过 3 秒会扣血\nSmoke exposure starts draining health after 3 seconds.";
    public const string ElevatorWarning = "发生火灾时，请勿使用电梯！\nDo not use the elevator in case of fire!";
    public const string WindowWarning = "发生火灾时，请勿试图跳窗逃生！\nDo not attempt to jump out of a window to escape during a fire";
    public const string RescueStairWarning = "请先带轮椅同学到二楼避难区，再使用楼梯。\nPlease lead your classmate to the 2F refuge before using the stairs.";
    public static CampusMessagePresenter Instance { get; private set; }
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Shader foregroundShader;
    [SerializeField] private Shader foregroundFontShader;
    private Transform panel;
    private CampusRescueNoticeCard rescueWarningCard;
    private TextMeshProUGUI text;
    private float hideAt;
    private bool priorityWarning;
    public string CurrentMessage => RescueWarningVisible ? RescueStairWarning : text != null ? text.text : "";
    private bool RescueWarningVisible => rescueWarningCard != null && rescueWarningCard.gameObject.activeSelf;
    public bool IsVisible => RescueWarningVisible || (panel!=null && panel.gameObject.activeSelf);
    public bool HasPriorityWarning => IsVisible && priorityWarning;
    public TMP_FontAsset DisplayFont => font != null ? font : TMP_Settings.defaultFontAsset;
    public void Hide()
    {
        if(panel!=null) panel.gameObject.SetActive(false);
        if(rescueWarningCard!=null) rescueWarningCard.gameObject.SetActive(false);
    }
    private void Awake() { Instance = this; }
    public void ConfigureFont(Font source) { if (font == null && source != null) font = TMP_FontAsset.CreateFontAsset(source); }
    public void Show(string message)
    {
        var camera = Camera.main; if (camera == null) return;
        bool exercise = CampusLessonDirector.Instance?.ExerciseStarted ?? false;
        bool safety = exercise && (message == SmokeWarning || message == SmokeRules ||
            message == ElevatorWarning || message == WindowWarning || message == RescueStairWarning);
        if (HasPriorityWarning && !safety) return;
        if (message == RescueStairWarning)
        {
            if (rescueWarningCard == null)
            {
                var locomotion = FindObjectOfType<CampusLocomotion>();
                var head = locomotion != null && locomotion.TrackedHead != null ? locomotion.TrackedHead : camera.transform;
                rescueWarningCard = CampusRescueNoticeCard.Create(head, DisplayFont, "Refuge stair warning");
            }
            if (panel != null) panel.gameObject.SetActive(false);
            priorityWarning = safety;
            hideAt = Time.time + 2.5f;
            rescueWarningCard.Show(message, new Color(1, .92f, .55f));
            return;
        }
        if (rescueWarningCard != null) rescueWarningCard.gameObject.SetActive(false);
        if (panel == null)
        {
            var group = CampusCompactWarning.Create(camera.transform, DisplayFont, out var warningText);
            panel = group.transform;
            text = (TextMeshProUGUI)warningText;
            text.color = FireTrainingUIStyle.Ink;
            group.gameObject.AddComponent<CampusUIForeground>().Configure(foregroundShader,foregroundFontShader);
        }
        priorityWarning = safety;
        panel.GetComponent<CampusCompactWarning>().Place();
        panel.gameObject.SetActive(true);
        panel.GetComponent<CampusCompactWarning>().SetMessage(message);
        hideAt = Time.time + (safety ? 2.5f : 5f);
        text.ForceMeshUpdate();
        panel.GetComponent<CampusUIForeground>().Refresh();
    }
    private void Update()
    {
        // Keep the warning pending while the player reads the map or task details.
        if (RescueWarningVisible && (CampusLessonDirector.Instance?.NavigationPanelOpen ?? false))
        {
            hideAt += Time.deltaTime;
            return;
        }
        if (IsVisible && Time.time >= hideAt) Hide();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; if (panel != null) Destroy(panel.gameObject); if (rescueWarningCard != null) Destroy(rescueWarningCard.gameObject); }
}
