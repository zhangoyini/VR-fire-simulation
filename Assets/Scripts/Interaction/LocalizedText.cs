using UnityEngine;
using TMPro;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 当前游戏语言。所有 LocalizedText 共享同一个设置。
    /// </summary>
    public enum GameLanguage
    {
        English,
        Chinese
    }

    /// <summary>
    /// 双语文本组件：根据当前语言设置自动切换 TextMeshPro 的显示文字。
    /// 挂在任何需要中英文切换的 TextMeshProUGUI 物体上。
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        public static GameLanguage CurrentLanguage { get; private set; } = GameLanguage.English;

        [Header("多语言文本")]
        [TextArea(2, 4)]
        [SerializeField] private string englishText = "";

        [TextArea(2, 4)]
        [SerializeField] private string chineseText = "";

        private TextMeshProUGUI _targetText;

        private void Awake()
        {
            _targetText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>根据当前语言刷新文本显示。</summary>
        public void Refresh()
        {
            if (_targetText == null)
                return;

            _targetText.text = CurrentLanguage == GameLanguage.Chinese ? chineseText : englishText;
        }

        /// <summary>切换全局语言，并刷新场景中所有 LocalizedText。</summary>
        public static void SetLanguage(GameLanguage language)
        {
            CurrentLanguage = language;

            LocalizedText[] allTexts = FindObjectsOfType<LocalizedText>(true);
            foreach (var text in allTexts)
            {
                text.Refresh();
            }
        }

        /// <summary>根据系统语言自动选择英文或中文。</summary>
        public static void AutoSelectLanguage()
        {
            GameLanguage language = Application.systemLanguage == SystemLanguage.Chinese
                || Application.systemLanguage == SystemLanguage.ChineseSimplified
                || Application.systemLanguage == SystemLanguage.ChineseTraditional
                ? GameLanguage.Chinese
                : GameLanguage.English;

            SetLanguage(language);
        }
    }
}
