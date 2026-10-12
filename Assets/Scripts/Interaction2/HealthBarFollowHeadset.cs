using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class HealthBarFollowHeadset : MonoBehaviour
{
    public enum BillboardMode
    {
        FullBillboard,    // 完全面向头显，3D 空间最真实
        YOnlyBillboard,   // 只在水平方向跟随，保持竖直（更像传统 HUD）
        FixedUpright      // 固定竖直，不跟随头显旋转
    }

    [Header("跟随目标")]
    [SerializeField] private Transform cameraTransform;

    [Header("旋转模式")]
    [Tooltip("FullBillboard=完全面向头显；YOnlyBillboard=只在水平方向转身，保持竖直；FixedUpright=固定竖直")]
    [SerializeField] private BillboardMode billboardMode = BillboardMode.YOnlyBillboard;

    [Header("位置")]
    [Tooltip("头显前方距离")]
    [SerializeField] private float distance = 1.5f;
    [Tooltip("正数向上，负数向下")]
    [SerializeField] private float verticalOffset = 0f;
    [Tooltip("正数向右，负数向左")]
    [SerializeField] private float horizontalOffset = 0f;

    [Header("大小")]
    [SerializeField] private float scale = 1f;

    [Header("透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float alpha = 1f;

    [Header("防穿墙")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallBuffer = 0.1f;

    [Header("平滑")]
    [Range(0.01f, 1f)]
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

        // 计算目标位置
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

        transform.position = Vector3.Lerp(transform.position, targetPos, followSmooth);

        // 根据模式设置旋转
        switch (billboardMode)
        {
            case BillboardMode.FullBillboard:
                {
                    Vector3 toCamera = cameraTransform.position - transform.position;
                    if (toCamera.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(-toCamera);
                }
                break;

            case BillboardMode.YOnlyBillboard:
                {
                    // 只在水平方向跟随头显，保持竖直
                    Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                    if (forward.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(forward.normalized);
                }
                break;

            case BillboardMode.FixedUpright:
                // 固定朝向 +Z，保持竖直
                break;
        }

        transform.localScale = Vector3.one * scale;
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }
}
