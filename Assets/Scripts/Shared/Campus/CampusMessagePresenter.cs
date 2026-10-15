using TMPro;
using UnityEngine;

public sealed class CampusMessagePresenter : MonoBehaviour
{
    public const string ElevatorWarning = "发生火灾时，请勿使用电梯！\nDo not use the elevator in case of fire!";
    public const string WindowWarning = "发生火灾时，请勿试图跳窗逃生！\nDo not attempt to jump out of a window to escape during a fire";
    public static CampusMessagePresenter Instance { get; private set; }
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Shader foregroundShader;
    [SerializeField] private Shader foregroundFontShader;
    private Transform panel;
    private TextMeshPro text;
    private float hideAt;
    public string CurrentMessage => text != null ? text.text : "";
    public bool IsVisible => panel!=null && panel.gameObject.activeSelf;
    public void Hide() { if(panel!=null) panel.gameObject.SetActive(false); }
    private void Awake() { Instance = this; }
    public void ConfigureFont(Font source) { if (font == null && source != null) font = TMP_FontAsset.CreateFontAsset(source); }
    public void Show(string message)
    {
        var camera = Camera.main; if (camera == null) return;
        if (panel == null)
        {
            var go = new GameObject("World-space safety warning (1m)", typeof(RectTransform)); panel = go.transform; panel.SetParent(camera.transform, false);
            text = go.AddComponent<TextMeshPro>(); text.font = font != null ? font : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = .55f; text.rectTransform.sizeDelta = new Vector2(1.45f, .45f); text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1, .92f, .55f); text.enableWordWrapping = true;
            go.AddComponent<CampusUIForeground>().Configure(foregroundShader,foregroundFontShader);
        }
        panel.localPosition = new Vector3(0, 0, 1); panel.localRotation = Quaternion.identity;
        panel.gameObject.SetActive(true); text.text = message; hideAt = Time.time + 5;
        text.ForceMeshUpdate();
        panel.GetComponent<CampusUIForeground>().Refresh();
    }
    private void Update() { if (panel != null && panel.gameObject.activeSelf && Time.time >= hideAt) panel.gameObject.SetActive(false); }
    private void OnDestroy() { if (Instance == this) Instance = null; if (panel != null) Destroy(panel.gameObject); }
}
