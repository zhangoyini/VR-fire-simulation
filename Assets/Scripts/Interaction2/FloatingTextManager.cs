using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRFire.Utils;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 浮出文字管理器：在指定世界位置显示临时提示（例如“+20 时间”）。
    /// 支持中英文本地化，适合 VR World Space 渲染。
    /// 不需要手动挂到场景里，第一次调用时会自动创建。
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        private const string ManagerName = "FloatingTextManager";

        [Header("HUD 提示锚点")]
        [Tooltip("把 HealthTimeBar 的 World Space Canvas 根物体拖到这里，提示会显示在血条下方。")]
        [SerializeField] private Transform hudAnchor;

        [Tooltip("相对于 HUD 锚点的偏移。Y 为负时显示在血条下方。")]
        [SerializeField] private Vector3 hudOffset = new Vector3(0f, -0.15f, 0f);

        [Header("预制体（可选）")]
        [Tooltip("自定义浮出文字预制体。留空时使用运行时默认样式。")]
        [SerializeField] private GameObject floatingTextPrefab;

        [Header("默认样式")]
        [Tooltip("世界空间 Canvas 的缩放，越小越贴合 VR 场景")]
        [SerializeField] private float canvasWorldScale = 0.004f;

        [SerializeField] private float defaultDuration = 1.5f;
        [SerializeField] private float floatSpeed = 0.3f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.1f, 0f);

        private Transform _cameraTransform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[FloatingTextManager] 发现重复实例，销毁：{gameObject.name}", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObject.name = ManagerName;

            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 在世界位置显示一条普通浮出文字。
        /// </summary>
        public static void Show(string message, Vector3 worldPosition, Color color, float duration = -1f, FloatingTextBillboardMode billboardMode = FloatingTextBillboardMode.Full)
        {
            EnsureInstance();
            if (Instance == null)
                return;

            Instance.ShowInternal(message, worldPosition, color, duration, billboardMode);
        }

        /// <summary>
        /// 在世界位置显示一条中英双语的浮出文字，自动按当前语言选择。
        /// </summary>
        public static void ShowLocalized(string english, string chinese, Vector3 worldPosition, Color color, float duration = -1f, FloatingTextBillboardMode billboardMode = FloatingTextBillboardMode.Full)
        {
            EnsureInstance();
            if (Instance == null)
                return;

            string message = LocalizedText.CurrentLanguage == GameLanguage.Chinese ? chinese : english;
            Instance.ShowInternal(message, worldPosition, color, duration, billboardMode);
        }

        /// <summary>
        /// 在世界位置显示一条中英双语的浮出文字（默认白色）。
        /// </summary>
        public static void ShowLocalized(string english, string chinese, Vector3 worldPosition, float duration = -1f, FloatingTextBillboardMode billboardMode = FloatingTextBillboardMode.Full)
        {
            ShowLocalized(english, chinese, worldPosition, Color.white, duration, billboardMode);
        }

        /// <summary>
        /// 在 HUD 锚点下方显示一条普通浮出文字（水平朝向玩家）。
        /// </summary>
        public static void ShowAtHUD(string message, Color color, float duration = -1f)
        {
            EnsureInstance();
            if (Instance == null)
                return;

            Vector3 position = Instance.GetHUDPosition();
            Instance.ShowInternal(message, position, color, duration, FloatingTextBillboardMode.Horizontal);
        }

        /// <summary>
        /// 在 HUD 锚点下方显示一条中英双语的浮出文字（水平朝向玩家）。
        /// </summary>
        public static void ShowLocalizedAtHUD(string english, string chinese, Color color, float duration = -1f)
        {
            EnsureInstance();
            if (Instance == null)
                return;

            string message = LocalizedText.CurrentLanguage == GameLanguage.Chinese ? chinese : english;
            Vector3 position = Instance.GetHUDPosition();
            Instance.ShowInternal(message, position, color, duration, FloatingTextBillboardMode.Horizontal);
        }

        /// <summary>
        /// 在 HUD 锚点下方显示一条中英双语的浮出文字（默认白色，水平朝向玩家）。
        /// </summary>
        public static void ShowLocalizedAtHUD(string english, string chinese, float duration = -1f)
        {
            ShowLocalizedAtHUD(english, chinese, Color.white, duration);
        }

        private static void EnsureInstance()
        {
            if (Instance != null)
                return;

            GameObject go = new GameObject(ManagerName);
            go.AddComponent<FloatingTextManager>();
        }

        private void ShowInternal(string message, Vector3 worldPosition, Color color, float duration, FloatingTextBillboardMode billboardMode)
        {
            GameObject go = SpawnFloatingTextObject(worldPosition);
            if (go == null)
                return;

            FloatingText ft = go.GetComponent<FloatingText>();
            if (ft == null)
                ft = go.AddComponent<FloatingText>();

            float actualDuration = duration > 0f ? duration : defaultDuration;
            ft.Initialize(message, color, actualDuration, floatSpeed, _cameraTransform, billboardMode);
        }

        private Vector3 GetHUDPosition()
        {
            if (hudAnchor != null)
            {
                // 使用锚点物体的本地坐标系来偏移，保证“下方”跟随血条旋转
                return hudAnchor.position + hudAnchor.TransformDirection(hudOffset);
            }

            // 没有指定锚点时， fallback 到相机正前方偏下
            if (_cameraTransform != null)
            {
                Vector3 forward = _cameraTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    forward.Normalize();
                else
                    forward = _cameraTransform.forward;

                return _cameraTransform.position + forward * 1.5f + Vector3.down * 0.2f;
            }

            return Vector3.zero;
        }

        private GameObject SpawnFloatingTextObject(Vector3 worldPosition)
        {
            Vector3 position = worldPosition + spawnOffset;

            if (floatingTextPrefab != null)
            {
                GameObject go = Instantiate(floatingTextPrefab, position, Quaternion.identity);
                return go;
            }

            return CreateDefaultFloatingText(position);
        }

        private GameObject CreateDefaultFloatingText(Vector3 position)
        {
            GameObject root = new GameObject("FloatingText_Default");
            root.transform.position = position;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.localScale = Vector3.one * canvasWorldScale;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(root.transform, false);

            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta = new Vector2(8f, 1f);

            TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24f;
            // 使用内置中文字体资产，避免中文显示为方块（LiberationSans 默认不含中文 glyph）
            tmp.font = ChineseFontProvider.GetFontOrDefault();
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return root;
        }
    }
}
