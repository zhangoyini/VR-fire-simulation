using UnityEngine;
using TMPro;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 浮出文字的 billboard 模式。
    /// </summary>
    public enum FloatingTextBillboardMode
    {
        /// <summary>完全面向相机（适合世界空间提示）。</summary>
        Full,

        /// <summary>只在水平方向上转向相机，保持文字水平不倾斜（适合 HUD 下方提示）。</summary>
        Horizontal
    }

    /// <summary>
    /// 单个浮出文字实例：向上飘动、面向相机、淡出后自毁。
    /// 由 FloatingTextManager 生成，通常不需要手动挂到物体上。
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private TextMeshProUGUI _tmpText;
        private Transform _lookAtTarget;
        private FloatingTextBillboardMode _billboardMode;

        private float _duration;
        private float _elapsed;
        private float _floatSpeed;
        private Color _baseColor;
        private bool _initialized;

        /// <summary>
        /// 初始化浮出文字的内容、颜色、持续时间、飘动速度和 billboard 模式。
        /// </summary>
        public void Initialize(string message, Color color, float duration, float floatSpeed, Transform lookAtTarget, FloatingTextBillboardMode billboardMode = FloatingTextBillboardMode.Full)
        {
            _tmpText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_tmpText == null)
            {
                Debug.LogWarning("[FloatingText] 找不到 TextMeshProUGUI 组件，无法显示文字。", this);
                Destroy(gameObject);
                return;
            }

            _tmpText.text = message;
            _baseColor = color;
            _tmpText.color = _baseColor;

            _duration = duration;
            _floatSpeed = floatSpeed;
            _lookAtTarget = lookAtTarget;
            _billboardMode = billboardMode;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            // 向上飘动
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

            // 面向相机
            if (_lookAtTarget != null)
            {
                Vector3 toTarget = _lookAtTarget.position - transform.position;

                if (_billboardMode == FloatingTextBillboardMode.Horizontal)
                    toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(-toTarget);
            }

            // 淡出
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _tmpText.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, Mathf.Lerp(_baseColor.a, 0f, t));

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
