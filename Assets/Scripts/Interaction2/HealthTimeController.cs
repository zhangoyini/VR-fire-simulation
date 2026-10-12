using System.Collections.Generic;
using UnityEngine;
using Harbourview.Journey;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 血条/时间控制器：把剩余时间表现为一个持续减少的血量条。
    /// 支持烟雾区加速扣血、毛巾回血/减速，并在血量归零时触发失败事件。
    /// </summary>
    public class HealthTimeController : MonoBehaviour
    {
        public static HealthTimeController Instance { get; private set; }

        [Header("基础数值")]
        [Tooltip("满血值，默认 100")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("基础每秒掉血速度。100 / 480s ≈ 0.208，对应 8 分钟")]
        [SerializeField] private float baseDrainPerSecond = 0.208333f;

        [Header("低血量阈值")]
        [Tooltip("血量百分比低于此值时触发一次 LowHealth 事件")]
        [SerializeField][Range(0f, 1f)] private float lowHealthThreshold = 0.25f;

        [Header("调试")]
        [SerializeField] private bool logEvents = true;

        private float currentHealth;
        private bool isPaused;
        private bool hasTriggeredLowHealth;

        // key = 来源标识（如烟雾区 InstanceID、毛巾 GUID），value = 掉血倍率
        private readonly Dictionary<string, float> multipliers = new Dictionary<string, float>();

        // key = 来源标识，value = 该倍率的过期时间（Time.time），用于毛巾等限时效果
        private readonly Dictionary<string, float> multiplierExpiry = new Dictionary<string, float>();

        public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;
        public bool IsDepleted => currentHealth <= 0f;
        public bool IsPaused => isPaused;

        /// <summary>当前总掉血倍率。</summary>
        public float CurrentMultiplier => GetTotalMultiplier();

        /// <summary>基于当前总倍率估算的剩余秒数。</summary>
        public float RemainingSeconds
        {
            get
            {
                float drainPerSecond = baseDrainPerSecond * GetTotalMultiplier();
                return drainPerSecond > 0f ? currentHealth / drainPerSecond : 0f;
            }
        }

        public event System.Action<float> HealthChanged;
        public event System.Action Depleted;
        public event System.Action LowHealth;
        public event System.Action LowHealthRecovered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HealthTimeController] Duplicate instance destroyed: " + gameObject.name, this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            ResetHealth();
        }

        private void Update()
        {
            if (isPaused || IsDepleted)
                return;

            RemoveExpiredMultipliers();

            float drain = baseDrainPerSecond * GetTotalMultiplier() * Time.deltaTime;
            currentHealth = Mathf.Max(0f, currentHealth - drain);

            HealthChanged?.Invoke(HealthPercent);

            if (!hasTriggeredLowHealth && HealthPercent <= lowHealthThreshold)
            {
                hasTriggeredLowHealth = true;
                LowHealth?.Invoke();

                if (logEvents)
                    SessionLogger.Write("HEALTH_LOW", $"{{\"percent\":{HealthPercent:F2}}}");
            }

            if (hasTriggeredLowHealth && HealthPercent > lowHealthThreshold)
            {
                hasTriggeredLowHealth = false;
                LowHealthRecovered?.Invoke();

                if (logEvents)
                    SessionLogger.Write("HEALTH_RECOVERED", $"{{\"percent\":{HealthPercent:F2}}}");
            }

            if (currentHealth <= 0f)
            {
                Depleted?.Invoke();

                if (logEvents)
                    SessionLogger.Write("HEALTH_DEPLETED");
            }
        }

        private float GetTotalMultiplier()
        {
            float total = 1f;
            foreach (var kvp in multipliers)
            {
                total *= kvp.Value;
            }
            return total;
        }

        /// <summary>重置血量为满血，并清空所有外部倍率。</summary>
#if UNITY_EDITOR
        [ContextMenu("Debug/Reset Health")]
#endif
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            multipliers.Clear();
            multiplierExpiry.Clear();
            isPaused = false;

            bool wasLowHealth = hasTriggeredLowHealth;
            hasTriggeredLowHealth = false;

            HealthChanged?.Invoke(HealthPercent);

            if (wasLowHealth)
                LowHealthRecovered?.Invoke();

            if (logEvents)
                SessionLogger.Write("HEALTH_RESET", $"{{\"maxHealth\":{maxHealth:F2}}}");
        }

        /// <summary>暂停或恢复血量流失。</summary>
        public void Pause(bool pause)
        {
            isPaused = pause;
        }

        /// <summary>直接回复一定血量（受 maxHealth 上限限制）。</summary>
        public void AddHealth(float amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(HealthPercent);

            // 如果回血后脱离低血量状态，立即触发恢复事件
            if (hasTriggeredLowHealth && HealthPercent > lowHealthThreshold)
            {
                hasTriggeredLowHealth = false;
                LowHealthRecovered?.Invoke();

                if (logEvents)
                    SessionLogger.Write("HEALTH_RECOVERED", $"{{\"percent\":{HealthPercent:F2}}}");
            }
        }

        /// <summary>叠加一个外部掉血倍率，例如烟雾区或毛巾减速效果。</summary>
        public void ApplyMultiplier(string key, float multiplier)
        {
            if (string.IsNullOrEmpty(key))
                return;

            multipliers[key] = multiplier;
        }

        /// <summary>叠加一个限时外部掉血倍率，到达 duration 秒后自动移除。</summary>
        public void ApplyTimedMultiplier(string key, float multiplier, float duration)
        {
            if (string.IsNullOrEmpty(key) || duration <= 0f)
                return;

            multipliers[key] = multiplier;
            multiplierExpiry[key] = Time.time + duration;
        }

        /// <summary>移除一个外部掉血倍率。</summary>
        public void RemoveMultiplier(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (multipliers.ContainsKey(key))
                multipliers.Remove(key);

            if (multiplierExpiry.ContainsKey(key))
                multiplierExpiry.Remove(key);
        }

        private void RemoveExpiredMultipliers()
        {
            if (multiplierExpiry.Count == 0)
                return;

            List<string> expiredKeys = null;
            foreach (var kvp in multiplierExpiry)
            {
                if (Time.time >= kvp.Value)
                {
                    expiredKeys ??= new List<string>();
                    expiredKeys.Add(kvp.Key);
                }
            }

            if (expiredKeys == null)
                return;

            foreach (string key in expiredKeys)
            {
                multipliers.Remove(key);
                multiplierExpiry.Remove(key);
            }

            if (logEvents)
                SessionLogger.Write("HEALTH_MULTIPLIER_EXPIRED", $"{{\"keys\":[{string.Join(",", expiredKeys.ConvertAll(k => $"\"{k}\""))}]}}");
        }
    }
}
