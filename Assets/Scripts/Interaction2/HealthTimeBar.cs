using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 血条/时间条 UI 控件：绑定 HealthTimeController，实时更新 Slider、颜色、以及左上角标签和右上角剩余时间。
    /// 支持低血量时的闪烁警告。
    /// </summary>
    public class HealthTimeBar : MonoBehaviour
    {
        [Header("血条组件")]
        [Tooltip("UI Slider，范围 0~1")]
        [SerializeField] private Slider slider;

        [Tooltip("Slider 的 Fill Image")]
        [SerializeField] private Image fillImage;

        [Header("颜色阈值")]
        [SerializeField] private Color healthyColor = new Color(0.24f, 0.78f, 0.31f);
        [SerializeField] private Color warningColor = new Color(1f, 0.86f, 0.21f);
        [SerializeField] private Color dangerColor = new Color(0.92f, 0.25f, 0.2f);

        [Tooltip("血量低于此值时变为 warning 颜色")]
        [SerializeField][Range(0f, 1f)] private float warningThreshold = 0.5f;

        [Tooltip("血量低于此值时变为 danger 颜色，并触发低血量闪烁")]
        [SerializeField][Range(0f, 1f)] private float dangerThreshold = 0.25f;

        [Header("时间文字")]
        [Tooltip("左上角标签文字")]
        [SerializeField] private string labelString = "Remaining time";

        [Tooltip("标签：相对 Slider 左上角的偏移（X 向右，Y 向下）")]
        [SerializeField] private Vector2 labelOffset = new Vector2(10f, -10f);

        [Tooltip("数值：相对 Slider 右上角的偏移（X 向左，Y 向下）")]
        [SerializeField] private Vector2 valueOffset = new Vector2(-10f, -10f);

        [Tooltip("文字字号")]
        [SerializeField] private float textFontSize = 48f;

        [Tooltip("文字颜色")]
        [SerializeField] private Color textColor = new Color(1f, 0.92f, 0.23f); // 亮黄色

        [Tooltip("文字描边颜色")]
        [SerializeField] private Color textOutlineColor = Color.black;

        [Tooltip("文字描边粗细")]
        [SerializeField][Range(0f, 1f)] private float textOutlineWidth = 0.25f;

        [Tooltip("文字框大小")]
        [SerializeField] private Vector2 textSize = new Vector2(300f, 70f);

        [Header("低血量闪烁提示")]
        [Tooltip("可选：低血量时闪烁的红色半透明 Image")]
        [SerializeField] private Image warningImage;

        [Tooltip("闪烁间隔（秒）")]
        [SerializeField] private float flashInterval = 0.5f;

        private TextMeshProUGUI labelText;
        private TextMeshProUGUI valueText;
        private Coroutine _flashCoroutine;

        private void Start()
        {
            Debug.Log($"[HealthTimeBar] 启动: {gameObject.name}, instanceID={GetInstanceID()}", this);

            ResolveSlider();

            // 清理旧版本脚本残留的文字对象，避免位置/样式冲突
            CleanupOldText("LabelText");
            CleanupOldText("ValueText");

            // 初始化文字
            SetupTimeText();

            if (HealthTimeController.Instance != null)
            {
                HealthTimeController.Instance.HealthChanged += OnHealthChanged;
                HealthTimeController.Instance.LowHealth += OnLowHealth;
                HealthTimeController.Instance.LowHealthRecovered += OnLowHealthRecovered;
                HealthTimeController.Instance.Depleted += OnDepleted;
                OnHealthChanged(HealthTimeController.Instance.HealthPercent);
            }
            else
            {
                Debug.LogWarning("[HealthTimeBar] 场景中找不到 HealthTimeController。", this);
            }

            if (warningImage != null)
                warningImage.enabled = false;

            // 确保 Fill 的 pivot 在左侧，这样血量减少时从右往左缩短
            if (fillImage != null)
            {
                RectTransform fillRT = fillImage.rectTransform;
                fillRT.anchorMin = new Vector2(0f, 0f);
                fillRT.anchorMax = new Vector2(1f, 1f);
                fillRT.pivot = new Vector2(0f, 0.5f);
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (HealthTimeController.Instance != null)
            {
                HealthTimeController.Instance.HealthChanged -= OnHealthChanged;
                HealthTimeController.Instance.LowHealth -= OnLowHealth;
                HealthTimeController.Instance.LowHealthRecovered -= OnLowHealthRecovered;
                HealthTimeController.Instance.Depleted -= OnDepleted;
            }
        }

        private void LateUpdate()
        {
            SyncTextLayout();
        }

        private void SyncTextLayout()
        {
            if (labelText != null)
            {
                RectTransform rt = labelText.rectTransform;
                rt.anchoredPosition = labelOffset;
                rt.sizeDelta = textSize;
                labelText.text = labelString;
                ApplyTextStyle(labelText);
            }

            if (valueText != null)
            {
                RectTransform rt = valueText.rectTransform;
                rt.anchoredPosition = valueOffset;
                rt.sizeDelta = textSize;
                ApplyTextStyle(valueText);
            }

            // 调试：每帧打印 Inspector 字段和实际 RectTransform 值，确认是否同步
            if (labelText != null && valueText != null && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[HealthTimeBar] Inspector offset: label={labelOffset}, value={valueOffset} | " +
                          $"Actual: label={labelText.rectTransform.anchoredPosition}, value={valueText.rectTransform.anchoredPosition}", this);
            }
        }

        private void ResolveSlider()
        {
            if (slider != null) return;
            slider = GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                fillImage = slider.fillRect?.GetComponent<Image>();
            }
        }

        private void CleanupOldText(string name)
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name == name)
                {
                    Debug.Log($"[HealthTimeBar] 清理旧文字对象: {child.name}", this);
                    Destroy(child.gameObject);
                }
            }
        }

        private void SetupTimeText()
        {
            if (slider == null)
            {
                Debug.LogWarning("[HealthTimeBar] 找不到 Slider，无法创建文字。", this);
                return;
            }

            RectTransform sliderRT = slider.GetComponent<RectTransform>();

            // 左上角标签
            labelText = CreateText(sliderRT, "LabelText", true,
                labelOffset, TextAlignmentOptions.TopLeft);
            labelText.text = labelString;

            // 右上角数值
            valueText = CreateText(sliderRT, "ValueText", false,
                valueOffset, TextAlignmentOptions.TopRight);
            valueText.text = "00:00";

            // 统一样式
            ApplyTextStyle(labelText);
            ApplyTextStyle(valueText);
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string name, bool isLeft,
            Vector2 anchoredPosition, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            // 锚定到 Slider 左上角/右上角，避免 Canvas 尺寸变化导致文字飞出
            rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 1f);
            rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
            rt.pivot = new Vector2(isLeft ? 0f : 1f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = textSize;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            return text;
        }

        private void ApplyTextStyle(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.fontSize = textFontSize;
            text.color = textColor;
            text.faceColor = textColor;
            text.outlineColor = textOutlineColor;
            text.outlineWidth = textOutlineWidth;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableWordWrapping = false;
            text.ForceMeshUpdate(true, true);

            Debug.Log($"[HealthTimeBar] 已应用样式到 {text.name}: fontSize={text.fontSize}, color={text.color}, " +
                      $"pos={text.rectTransform.anchoredPosition}, size={text.rectTransform.sizeDelta}, " +
                      $"labelOffset={labelOffset}, valueOffset={valueOffset}", this);
        }

        private void OnDepleted()
        {
            StopWarning();
        }

        public void StopWarning()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (warningImage != null)
                warningImage.enabled = false;
        }

        private void OnHealthChanged(float percent)
        {
            if (slider != null)
                slider.value = percent;

            if (fillImage != null)
            {
                fillImage.color = GetColorForPercent(percent);

                RectTransform fillRT = fillImage.rectTransform;
                Vector2 anchorMax = fillRT.anchorMax;
                anchorMax.x = Mathf.Clamp01(percent);
                fillRT.anchorMax = anchorMax;
            }

            // 更新右上角剩余时间
            if (valueText != null && HealthTimeController.Instance != null)
            {
                valueText.text = FormatTime(HealthTimeController.Instance.RemainingSeconds);
                valueText.ForceMeshUpdate(true, true);
            }
        }

        private string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = total / 60;
            int secs = total % 60;
            return $"{minutes:D2}:{secs:D2}";
        }

        private void OnLowHealth()
        {
            if (warningImage != null && _flashCoroutine == null)
                _flashCoroutine = StartCoroutine(FlashWarning());
        }

        private void OnLowHealthRecovered()
        {
            StopWarning();
        }

        private IEnumerator FlashWarning()
        {
            while (true)
            {
                warningImage.enabled = !warningImage.enabled;
                yield return new WaitForSeconds(flashInterval);
            }
        }

        private Color GetColorForPercent(float percent)
        {
            if (percent <= dangerThreshold)
                return dangerColor;

            if (percent <= warningThreshold)
                return warningColor;

            return healthyColor;
        }
    }

    public static class TransformExtensions
    {
        public static string GetHierarchyPath(this Transform transform)
        {
            if (transform.parent == null)
                return transform.name;
            return transform.parent.GetHierarchyPath() + "/" + transform.name;
        }
    }
}
