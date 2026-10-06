using UnityEngine;
using Harbourview.Journey;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 统一管理血量/时间系统的音效：毛巾拾取声、低血量循环警告声。
    /// 每个场景挂一个空物体命名为 AudioManager 即可。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("音频源")]
        [Tooltip("用于一次性音效的 AudioSource")]
        [SerializeField] private AudioSource sfxSource;

        [Tooltip("用于循环警告音的 AudioSource，需要 Loop 勾选")]
        [SerializeField] private AudioSource warningSource;

        [Header("默认音效")]
        [Tooltip("低血量时自动播放的循环警告音。保持柔和，符合 calm tone 要求")]
        [SerializeField] private AudioClip lowHealthWarningClip;

        [Header("音量")]
        [SerializeField][Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField][Range(0f, 1f)] private float warningVolume = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AudioManager] Duplicate instance destroyed: " + gameObject.name, this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 如果没有指定音频源，自动创建
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (warningSource == null)
            {
                warningSource = gameObject.AddComponent<AudioSource>();
                warningSource.playOnAwake = false;
                warningSource.loop = true;
            }
        }

        private void Start()
        {
            if (HealthTimeController.Instance != null)
            {
                HealthTimeController.Instance.LowHealth += OnLowHealth;
                HealthTimeController.Instance.Depleted += OnDepleted;
            }
        }

        private void OnDestroy()
        {
            if (HealthTimeController.Instance != null)
            {
                HealthTimeController.Instance.LowHealth -= OnLowHealth;
                HealthTimeController.Instance.Depleted -= OnDepleted;
            }

            if (Instance == this)
                Instance = null;
        }

        /// <summary>在指定世界位置播放一次性音效。</summary>
        public void PlaySFX(AudioClip clip, Vector3 position)
        {
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
        }

        /// <summary>在 AudioManager 位置播放一次性音效。</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
                return;

            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        /// <summary>播放循环警告音。</summary>
        public void StartWarning(AudioClip clip)
        {
            if (clip == null || warningSource == null)
                return;

            if (warningSource.isPlaying && warningSource.clip == clip)
                return;

            warningSource.clip = clip;
            warningSource.volume = warningVolume;
            warningSource.Play();
        }

        /// <summary>停止循环警告音。</summary>
        public void StopWarning()
        {
            if (warningSource != null)
                warningSource.Stop();
        }

        private void OnLowHealth()
        {
            if (lowHealthWarningClip != null)
                StartWarning(lowHealthWarningClip);
        }

        private void OnDepleted()
        {
            // 失败时停止警告音，避免和失败面板音效冲突
            StopWarning();
        }
    }
}
