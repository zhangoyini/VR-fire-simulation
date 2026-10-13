using UnityEngine;

namespace Jiao.HealthTime
{
    /// <summary>
    /// World Space HUD 防穿墙（Occlusion Pull-In）组件。
    /// 适用对象：跟随头显移动的世界空间 Canvas（血条 HUD、警告文案、教程面板等 head-locked UI）。
    ///
    /// 原理（即视线 Raycast 探测 + 动态拉近）：
    /// 1. 每帧从头显相机沿"相机 → UI 默认方向"发射一条射线（LayerMask 可配置）；
    /// 2. 若射线在到达 UI 默认距离之前命中墙体/障碍物，就把 UI 平滑拉近到
    ///    命中点前方 safeDistanceFromWall（默认 8cm）处，避免 UI 与墙体相交被深度测试切割；
    /// 3. 无遮挡时平滑回到默认距离；可选"极近距离淡出"。
    ///
    /// 使用方法：把本组件挂在 head-locked Canvas 根物体上（应位于头显/相机层级下）。
    /// 注意：固定在世界中不动的 UI（如安全标识牌 Sign_Canvas）不要挂本组件。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HudOcclusionGuard : MonoBehaviour
    {
        [Header("遮挡检测")]
        [Tooltip("会被视为障碍物的 Layer。建议排除 UI 层与 Player 层（玩家自身碰撞体）。")]
        [SerializeField] private LayerMask occlusionMask = ~0;

        [Tooltip("UI 拉近后与墙面保持的距离（米），防止与墙体 z-fighting")]
        [SerializeField] private float safeDistanceFromWall = 0.08f;

        [Tooltip("无遮挡时 UI 距头显的目标距离（米）。≤0 时使用启动时记录的初始距离")]
        [SerializeField] private float defaultDistance = 0f;

        [Tooltip("位置平滑跟随速度（越大越快）")]
        [SerializeField, Range(1f, 30f)] private float smoothSpeed = 12f;

        [Header("可选：极近淡出")]
        [Tooltip("拉近距离小于此值时把 UI 渐隐（0 = 关闭淡出）")]
        [SerializeField] private float fadeBelowDistance = 0f;

        private Camera cam;
        private float initialDistance = 1.5f;
        private Vector3 initialLocalDirection = Vector3.forward;
        private CanvasGroup canvasGroup;

        private void Start()
        {
            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[HudOcclusionGuard] 找不到 Camera.main，组件停用。", this);
                enabled = false;
                return;
            }

            // 记录初始状态：UI 相对头显的方向与距离（UI 应挂在相机层级下，保持头锁定角度）
            Vector3 worldDelta = transform.position - cam.transform.position;
            initialDistance = Mathf.Max(0.1f, worldDelta.magnitude);
            initialLocalDirection = cam.transform.InverseTransformDirection(
                worldDelta.sqrMagnitude > 0.0001f ? worldDelta.normalized : cam.transform.forward);

            if (fadeBelowDistance > 0f)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void LateUpdate()
        {
            if (cam == null)
                return;

            float targetDistance = defaultDistance > 0f ? defaultDistance : initialDistance;
            Vector3 direction = cam.transform.TransformDirection(initialLocalDirection).normalized;

            // 从相机稍前方发射，避免射线起点落在头显/玩家自身碰撞体内部
            Vector3 origin = cam.transform.position + direction * 0.05f;
            float maxDistance = targetDistance + 0.5f;

            float desired = targetDistance;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance,
                    occlusionMask, QueryTriggerInteraction.Ignore)
                && hit.distance < targetDistance)
            {
                // 墙在 UI 前面：拉近到墙面 safeDistanceFromWall 之前
                desired = Mathf.Max(safeDistanceFromWall, hit.distance - safeDistanceFromWall);
            }

            // 帧率无关的平滑插值
            Vector3 targetPosition = cam.transform.position + direction * desired;
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);

            // 可选：贴墙过近时渐隐，避免 UI 糊脸
            if (canvasGroup != null && fadeBelowDistance > 0f)
            {
                float fade = Mathf.Clamp01(
                    (desired - fadeBelowDistance) / Mathf.Max(0.01f, fadeBelowDistance));
                canvasGroup.alpha = fade;
            }
        }
    }
}
