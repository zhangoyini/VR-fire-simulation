using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 血条/时间条 UI 控件：绑定 HealthTimeController，实时更新 Slider 和颜色。
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

        [Header("低血量闪烁提示")]
        [Tooltip("可选：低血量时闪烁的红色半透明 Image")]
        [SerializeField] private Image warningImage;

        [Tooltip("闪烁间隔（秒）")]
        [SerializeField] private float flashInterval = 0.5f;

        private Coroutine _flashCoroutine;

        private void Start()
        {
            if (HealthTimeController.Instance != null)
            {
                HealthTimeController.Instance.HealthChanged += OnHealthChanged;
                HealthTimeController.Instance.LowHealth += OnLowHealth;
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

        private void OnHealthChanged(float percent)
        {
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
}
