using UnityEngine;
using UnityEngine.SceneManagement;
using Harbourview.Journey;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 失败 UI 控件：血量归零时显示失败面板、暂停计时、通知 JourneyDirector，
    /// 并处理 Restart / Main Menu 按钮事件。视觉样式由 Jiang 负责设计。
    /// </summary>
    public class FailureUI : MonoBehaviour
    {
        [Header("UI 面板")]
        [Tooltip("失败面板根物体，默认应处于隐藏状态")]
        [SerializeField] private GameObject failurePanel;

        [Tooltip("Optional shared outcome gate; leave unassigned for existing failure-only scenes.")]
        [SerializeField] private ResolutionCoordinator resolutionCoordinator;

        [Tooltip("失败提示文字的双语组件（可选）。显示内容在 Inspector 的 LocalizedText 里填写")]
        [SerializeField] private LocalizedText failureMessageLocalized;

        [Header("血条隐藏")]
        [Tooltip("失败时一并隐藏的血条 Canvas 根物体（可选）")]
        [SerializeField] private GameObject healthTimeCanvas;

        [Header("场景跳转")]
        [Tooltip("Restart 按钮要加载的场景名；留空则重载当前场景")]
        [SerializeField] private string restartSceneName = "";

        [Tooltip("Main Menu 按钮要加载的场景名；留空则按钮无效")]
        [SerializeField] private string mainMenuSceneName = "";

        private void Start()
        {
            if (failurePanel != null)
                failurePanel.SetActive(false);

            if (HealthTimeController.Instance != null)
                HealthTimeController.Instance.Depleted += ShowFailure;
        }

        private void OnDestroy()
        {
            if (HealthTimeController.Instance != null)
                HealthTimeController.Instance.Depleted -= ShowFailure;
        }

        private void ShowFailure()
        {
            // 优先使用 SystemUI 的 GameOver 面板
            if (resolutionCoordinator != null)
            {
                resolutionCoordinator.ShowGameOver();
            }
            // 只有没有 SystemUI 时，才 fallback 到自己的面板
            else if (failurePanel != null)
            {
                failurePanel.SetActive(true);
            }

            // 暂停血量流失，防止失败后面板还继续扣血
            HealthTimeController.Instance?.Pause(true);

            // 停止低血量闪烁警告
            var healthBar = Object.FindObjectOfType<HealthTimeBar>();
            healthBar?.StopWarning();

            // 失败时隐藏血条 Canvas
            if (healthTimeCanvas != null)
                healthTimeCanvas.SetActive(false);

            // 通知剧情导演进入复盘状态
            JourneyDirector.Instance?.Request(JourneyState.Debrief);

            SessionLogger.Write("HEALTH_DEPLETED_UI_SHOWN");

            // 如果文本挂了 LocalizedText，确保它在面板显示后刷新一次
            if (failureMessageLocalized != null)
                failureMessageLocalized.Refresh();
        }


        /// <summary>Restart 按钮点击事件。</summary>
        public void OnRestartButton()
        {
            string scene = string.IsNullOrEmpty(restartSceneName)
                ? SceneManager.GetActiveScene().name
                : restartSceneName;

            SessionLogger.Write("RESTART_CLICKED", $"{{\"scene\":\"{scene}\"}}");
            SceneManager.LoadScene(scene);
        }

        /// <summary>Main Menu 按钮点击事件。</summary>
        public void OnMainMenuButton()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.LogWarning("[FailureUI] Main Menu 场景名未设置。", this);
                return;
            }

            SessionLogger.Write("MAIN_MENU_CLICKED", $"{{\"scene\":\"{mainMenuSceneName}\"}}");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
