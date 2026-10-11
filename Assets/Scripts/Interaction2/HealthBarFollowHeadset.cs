using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class HealthBarFollowHeadset : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform cameraTransform;

    [Header("位置")]
    [SerializeField] private float distance = 1.5f;        // 远近：头显前方多远
    [SerializeField] private float verticalOffset = 0f;    // 上下：正数向上，负数向下
    [SerializeField] private float horizontalOffset = 0f;  // 左右：正数向右，负数向左

    [Header("大小")]
    [SerializeField] private float scale = 1f;             // 整体缩放

    [Header("透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float alpha = 1f;             // 整体不透明度

    [Header("防穿墙")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallBuffer = 0.1f;

    [Header("平滑")]
    [SerializeField] private float followSmooth = 0.2f;

    private CanvasGroup canvasGroup;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null) return;

        // 目标位置：前方 + 上下 + 左右
        Vector3 targetPos = cameraTransform.position
                          + cameraTransform.forward * distance
                          + cameraTransform.up * verticalOffset
                          + cameraTransform.right * horizontalOffset;

        // 防穿墙
        Vector3 direction = targetPos - cameraTransform.position;
        float desiredDistance = direction.magnitude;

        if (Physics.Raycast(cameraTransform.position, direction.normalized,
            out RaycastHit hit, desiredDistance, wallLayers))
        {
            targetPos = hit.point - direction.normalized * wallBuffer;
        }

        // 位置平滑
        transform.position = Vector3.Lerp(transform.position, targetPos, followSmooth);

        // 朝向头显（只在水平面转身）
        Vector3 toCamera = cameraTransform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(-toCamera);

        // 大小
        transform.localScale = Vector3.one * scale;

        // 透明度
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }
}
