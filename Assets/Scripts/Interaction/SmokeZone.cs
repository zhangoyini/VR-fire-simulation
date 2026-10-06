using UnityEngine;
using Harbourview.Journey;

namespace Jiao.HealthTime
{
    /// <summary>
    /// 烟雾区：玩家进入后提高掉血倍率，离开后自动恢复。
    /// 需要挂在带有 Collider（Is Trigger）的物体上。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SmokeZone : MonoBehaviour
    {
        [Header("扣血倍率")]
        [Tooltip("玩家在烟雾区中时，基础掉血速度乘以这个值。例如 3 表示掉血速度变成 3 倍。")]
        [SerializeField] private float drainMultiplier = 3f;

        [Header("可选视觉反馈")]
        [Tooltip("进入/离开烟雾区时开关的视觉效果，例如粒子系统或半透明烟雾模型")]
        [SerializeField] private GameObject visualEffect;

        private string MultiplierKey => $"smoke_{gameObject.GetInstanceID()}";
        private int _overlappingPlayers = 0;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning("[SmokeZone] Collider 需要勾选 Is Trigger 才能检测玩家进入：" + gameObject.name, this);
            }
        }

        private void Start()
        {
            if (visualEffect != null)
                visualEffect.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            _overlappingPlayers++;

            if (_overlappingPlayers == 1)
            {
                HealthTimeController.Instance?.ApplyMultiplier(MultiplierKey, drainMultiplier);
                SessionLogger.Write("SMOKE_ENTER", $"{{\"zone\":\"{gameObject.name}\",\"multiplier\":{drainMultiplier}}}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            _overlappingPlayers = Mathf.Max(0, _overlappingPlayers - 1);

            if (_overlappingPlayers == 0)
            {
                HealthTimeController.Instance?.RemoveMultiplier(MultiplierKey);
                SessionLogger.Write("SMOKE_EXIT", $"{{\"zone\":\"{gameObject.name}\"}}");
            }
        }

        private void OnDisable()
        {
            // 场景切换或物体禁用时，确保倍率被移除，避免残留。
            if (_overlappingPlayers > 0)
            {
                HealthTimeController.Instance?.RemoveMultiplier(MultiplierKey);
                _overlappingPlayers = 0;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
#endif
    }
}
