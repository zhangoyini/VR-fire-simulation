using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 血条/时间条 UI 控件：绑定 HealthTimeController，实时更新 Slider 和颜色。
    /// 支持低血量时的闪烁警告。
    /// </summary>
    public class HealthTimeBar : MonoBehaviour
    {
        private static readonly Color TowelProtectionColor = new Color(0.18f, 0.62f, 1f);

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

        [Header("低血量闪烁提示")]
        [Tooltip("可选：低血量时闪烁的红色半透明 Image")]
        [SerializeField] private Image warningImage;

        [Tooltip("闪烁间隔（秒）")]
        [SerializeField] private float flashInterval = 0.5f;

        private Coroutine _flashCoroutine;
        private bool towelProtected;
        private float displayedPercent = 1f;
        private Image stateIcon;
        private TMP_Text healthNumber;
        public string CurrentStateIcon { get; private set; }

        public void ApplyArtwork()
        {
            if (slider == null || fillImage == null || stateIcon != null) return;
            var baseSprite = FireTrainingUIStyle.Load("hud/hud_health_base_flat");
            if (baseSprite == null) return;
            var rect = (RectTransform)slider.transform;
            float width = rect.rect.width;
            if (width <= 0) width = 1.5f;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(width, width * baseSprite.rect.height / baseSprite.rect.width);
            rect.anchoredPosition = Vector2.zero;
            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.enabled = true;
                FireTrainingUIStyle.Apply(background, "hud/hud_health_base_flat");
                background.rectTransform.anchorMin = Vector2.zero;
                background.rectTransform.anchorMax = Vector2.one;
                background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
                background.transform.SetAsFirstSibling();
            }
            var area = (RectTransform)fillImage.transform.parent;
            area.anchorMin = new Vector2(.174f, .235f);
            area.anchorMax = new Vector2(.835f, .775f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            FireTrainingUIStyle.Apply(fillImage, "hud/hud_fill");
            fillImage.rectTransform.anchorMin = Vector2.zero;
            fillImage.rectTransform.anchorMax = Vector2.one;
            fillImage.rectTransform.offsetMin = fillImage.rectTransform.offsetMax = Vector2.zero;
            stateIcon = new GameObject("Health state icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            stateIcon.transform.SetParent(slider.transform, false);
            stateIcon.raycastTarget = false; stateIcon.preserveAspect = true;
            stateIcon.rectTransform.anchorMin = stateIcon.rectTransform.anchorMax = new Vector2(.09f, .5f);
            stateIcon.rectTransform.sizeDelta = Vector2.one * width * .075f;
            var label = new GameObject("Health value", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(slider.transform, false);
            healthNumber = label.GetComponent<TextMeshProUGUI>();
            var fontSource = GetComponentInChildren<TMP_Text>(true);
            healthNumber.font = fontSource != null && fontSource != healthNumber ? fontSource.font :
                Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            healthNumber.rectTransform.anchorMin = healthNumber.rectTransform.anchorMax = new Vector2(.92f, .5f);
            healthNumber.rectTransform.sizeDelta = new Vector2(140, 110);
            healthNumber.transform.localScale = Vector3.one * (width / 1000f);
            healthNumber.enableAutoSizing = true; healthNumber.fontSizeMax = 86; healthNumber.fontSizeMin = 48;
            healthNumber.alignment = TextAlignmentOptions.Center; healthNumber.color = Color.white;
            healthNumber.raycastTarget = false;
            OnHealthChanged(displayedPercent);
        }

        private void Start()
        {
            ApplyArtwork();
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

        internal void SetTowelProtection(bool active)
        {
            if (towelProtected == active) return;
            towelProtected = active;
            if (fillImage != null)
                OnHealthChanged(displayedPercent);
        }

        private void OnHealthChanged(float percent)
        {
            displayedPercent = Mathf.Clamp01(percent);
            percent = displayedPercent;
            CurrentStateIcon = towelProtected ? "icon_protection" : percent <= dangerThreshold ? "icon_cross" :
                percent <= warningThreshold ? "icon_warning" : "icon_health";
            if (stateIcon != null) FireTrainingUIStyle.Apply(stateIcon, "icons/" + CurrentStateIcon);
            if (healthNumber != null) healthNumber.text = Mathf.RoundToInt(percent * 100f).ToString();
            // 同时更新 Slider 值和 Fill 的 RectTransform，避免 Slider 的 Fill Rect 机制失效
            if (slider != null)
                slider.value = percent;

            if (fillImage != null)
            {
                fillImage.color = GetColorForPercent(percent);

                // 直接通过 anchorMax 控制血条宽度，比依赖 Slider 的 Fill Rect 更稳
                RectTransform fillRT = fillImage.rectTransform;
                Vector2 anchorMax = fillRT.anchorMax;
                anchorMax.x = Mathf.Clamp01(percent);
                fillRT.anchorMax = anchorMax;
            }
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
            if (towelProtected)
                return TowelProtectionColor;

            if (percent <= dangerThreshold)
                return dangerColor;

            if (percent <= warningThreshold)
                return warningColor;

            return healthyColor;
        }
    }
}
