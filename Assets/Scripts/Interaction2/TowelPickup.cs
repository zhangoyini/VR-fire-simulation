using UnityEngine;
using Harbourview.Journey;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 毛巾拾取物：玩家通过 IInteractable 接口拾取后，回复血量并在限定时间内降低掉血速度。
    /// 需要挂在带有 Collider（建议 Is Trigger）的物体上，并实现 IInteractable 接口。
    /// </summary>
    public class TowelPickup : MonoBehaviour, IInteractable
    {
        [Header("回复效果")]
        [Tooltip("拾取时立即回复的血量")]
        [SerializeField] private float healAmount = 20f;

        [Tooltip("拾取后掉血速度乘以这个值。例如 0.5 表示掉血速度减半。")]
        [SerializeField] private float slowDrainMultiplier = 0.5f;

        [Tooltip("减速效果持续多少秒")]
        [SerializeField] private float slowDurationSeconds = 30f;

        [Header("音效")]
        [Tooltip("拾取时播放的音效")]
        [SerializeField] private AudioClip pickupSound;

        [Header("浮出文字提示")]
        [Tooltip("拾取后是否在世界位置显示浮出文字提示")]
        [SerializeField] private bool showFloatingPrompt = true;

        [Tooltip("提示文字颜色")]
        [SerializeField] private Color promptColor = new Color(0.2f, 0.85f, 0.4f);

        [Tooltip("英文提示，{0} 会被替换为实际回复数值")]
        [SerializeField] private string promptEnglishFormat = "+{0} Time";

        [Tooltip("中文提示，{0} 会被替换为实际回复数值")]
        [SerializeField] private string promptChineseFormat = "+{0} 时间";

        [Header("可选视觉")]
        [Tooltip("平时显示的毛巾模型，拾取后隐藏")]
        [SerializeField] private GameObject visualObject;

        [Tooltip("拾取时生成的特效（会 Instantiate 出来）")]
        [SerializeField] private ParticleSystem pickupEffect;

        private bool isConsumed;
        private string MultiplierKey => $"towel_{gameObject.GetInstanceID()}";

        private void Awake()
        {
            // 自动修复测试占位物的材质：如果检测到 URP 不兼容的洋红色材质，换成蓝色自发光材质
            MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
            if (renderer != null && IsMagenta(renderer.sharedMaterial))
            {
                renderer.material = CreatePreviewMaterial();
            }
        }

        private bool IsMagenta(Material material)
        {
            if (material == null)
                return false;

            // 洋红色材质通常是 Standard shader 在 URP 下失效，颜色接近 (1, 0, 1)
            Color color = material.color;
            return color.r > 0.9f && color.b > 0.9f && color.g < 0.1f;
        }

        private Material CreatePreviewMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            mat.color = new Color(0.2f, 0.7f, 0.9f, 1f);
            return mat;
        }

        public void OnHoverEnter() { }
        public void OnHoverExit() { }
        public void OnInteract(Transform handTransform) { }
        public void OnInteractEnd(Transform handTransform) { }

        public void OnInteractStart(Transform handTransform)
        {
            if (isConsumed)
                return;

            isConsumed = true;

            // 1. 立即回血（血量会被 clamp 到满血上限，不会超过初始八分钟对应的满血值）
            HealthTimeController.Instance?.AddHealth(healAmount);

            // 1.5 浮出文字提示
            if (showFloatingPrompt)
            {
                string english = string.Format(promptEnglishFormat, healAmount);
                string chinese = string.Format(promptChineseFormat, healAmount);
                FloatingTextManager.ShowLocalizedAtHUD(english, chinese, promptColor);
            }

            // 2. 限时减速掉血（由 HealthTimeController 内部计时并自动移除）
            //    计算"安全减速倍率"，保证回血+减速后的等效剩余时间不超过初始八分钟：
            //    约束 回血后血量 / (基础掉血率 × 总倍率 × 安全倍率) ≤ 初始上限
            //      => 安全倍率 ≥ 回血后血量 / (满血 × 总倍率)
            //    这样低血量时保留原始减速救急，高血量(接近满血)时减速自动减弱/取消，
            //    避免"锦上添花"让总剩余时间突破初始八分钟。
            float effectiveMultiplier = slowDrainMultiplier;
            if (HealthTimeController.Instance != null)
            {
                float healthAfterHeal = HealthTimeController.Instance.CurrentHealth;
                float maxHealth = HealthTimeController.Instance.MaxHealth;
                float totalMultBeforeTowel = HealthTimeController.Instance.CurrentMultiplier;
                if (maxHealth > 0f && totalMultBeforeTowel > 0f)
                {
                    float required = healthAfterHeal / (maxHealth * totalMultBeforeTowel);
                    effectiveMultiplier = Mathf.Max(slowDrainMultiplier, Mathf.Min(1f, required));
                }
            }

            HealthTimeController.Instance?.ApplyTimedMultiplier(
                MultiplierKey, effectiveMultiplier, slowDurationSeconds);

            // 3. 音效
            if (pickupSound != null)
                AudioManager.Instance?.PlaySFX(pickupSound, transform.position);

            // 4. 特效
            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            // 5. 日志
            SessionLogger.Write("TOWEL_PICKED_UP",
                $"{{\"heal\":{healAmount},\"slowMultiplier\":{slowDrainMultiplier},\"effectiveMultiplier\":{effectiveMultiplier},\"duration\":{slowDurationSeconds}}}");

            // 6. 隐藏物体：优先隐藏 visualObject，保留 Collider 不破坏交互管理器的状态
            if (visualObject != null)
                visualObject.SetActive(false);
            else
                gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 0.9f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }

        private void Update()
        {
            // 编辑器/开发环境下按 T 键直接测试毛巾拾取（不需要 VR 手柄）
            if (Input.GetKeyDown(KeyCode.T) && !isConsumed)
            {
                OnInteractStart(null);
                Debug.Log($"[TowelPickup] 按 T 键调试拾取已触发：{gameObject.name}", this);
            }
        }

        [ContextMenu("Debug/Test Pickup")]
        private void DebugTestPickup()
        {
            if (Application.isPlaying)
            {
                OnInteractStart(null);
                Debug.Log($"[TowelPickup] 调试拾取已触发：{gameObject.name}", this);
            }
            else
            {
                Debug.LogWarning("[TowelPickup] 只能在 Play Mode 下使用 Debug/Test Pickup。");
            }
        }
#endif
    }
}
