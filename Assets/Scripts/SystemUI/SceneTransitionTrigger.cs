using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家身体进入触发区域并停留一段时间后，加载目标场景。
/// 用于 SampleScene 走出安全门后切换到集合区场景（路线 A）。
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("场景")]
    [Tooltip("要加载的场景名（必须在 Build Settings 里）")]
    [SerializeField] private string targetSceneName;

    [Header("玩家判定")]
    [Tooltip("真实玩家身体碰撞体；优先用它判定，避免手柄误触发")]
    [SerializeField] private Collider playerCollider;
    [Tooltip("玩家 Rig 根节点；未指定 playerCollider 时按层级判定")]
    [SerializeField] private Transform playerRoot;

    [Header("停留")]
    [Tooltip("进入区域后需停留多久才切换场景")]
    [SerializeField, Min(0f)] private float dwellSeconds = 0.3f;

    private bool inside;
    private float timer;
    private bool loading;

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other)) { inside = true; timer = 0f; }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other) && !inside) { inside = true; timer = 0f; }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) { inside = false; timer = 0f; }
    }

    private void Update()
    {
        if (!inside || loading || string.IsNullOrEmpty(targetSceneName))
            return;

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogWarning("[SceneTransitionTrigger] 无法加载场景 '" + targetSceneName +
                "'，请确认它已在 Build Settings 中。", this);
            return;
        }

        timer += Time.deltaTime;
        if (timer >= dwellSeconds)
        {
            loading = true;
            SceneManager.LoadScene(targetSceneName);
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (playerCollider != null)
            return other == playerCollider;

        if (playerRoot != null)
            return other.transform == playerRoot || other.transform.IsChildOf(playerRoot);

        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }
}
