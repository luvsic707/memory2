using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 西西弗斯之石控制器 (Sisyphus Rock Controller)
    /// 模拟西西弗斯推石上山的荒谬循环：
    /// 1. 玩家按下 Q 键交互时，石头受到朝向斜坡顶部的推力/初速度，并记录行为计数。
    /// 2. 松开交互后，石头由于物理重力会沿着斜坡滚落。
    /// 3. 当石头滚落掉出地底深渊（低于阈值高度）时，自动重置回斜坡顶端或指定重置点，速度归零，循环往复。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SisyphusRock : MonoBehaviour, IInteractable
    {
        [Header("斜坡与推力参数")]
        [Tooltip("斜坡的顶端参考点（决定推动的方向）")]
        public Transform topOfSlope;

        [Tooltip("推动的初始初速度大小")]
        public float pushForce = 8f;

        [Header("自动重置设置 (防止掉出地图)")]
        [Tooltip("当石头掉落低于此 Y 轴世界坐标时，触发自动重置")]
        public float resetYThreshold = -10f;

        [Tooltip("石头坠落深渊后，重置回哪一个出生点坐标")]
        public Transform resetSpawnPoint;

        [Header("UI 提示")]
        [SerializeField] private string interactHint = "推巨石";

        private Rigidbody rb;

        // 实现 IInteractable 接口的属性
        public string InteractHint => interactHint;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            // 自动防御：确保球体本身有一个 Collider 才能被射线命中
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<SphereCollider>();
                Debug.LogWarning($"[SisyphusRock] '{gameObject.name}' 上没有 Collider！已自动添加 SphereCollider，确保 Q 键射线检测可用。");
            }
        }

        // 实现 IInteractable 接口的交互方法
        public void Interact()
        {
            // 1. 行为数据累计 +1
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddPush();
            }
            else
            {
                Debug.LogWarning("[SisyphusRock] 找不到 PlayerBehaviorData 持久化实例！无法记录推石计数。");
            }

            // 2. 施加向斜坡顶部的冲量/速度
            PushUpward();
        }

        private void PushUpward()
        {
            if (topOfSlope == null)
            {
                Debug.LogError($"[SisyphusRock] '{gameObject.name}' 未指定 topOfSlope！无法确定推动的方向。");
                return;
            }

            // 计算推石朝向
            Vector3 pushDirection = (topOfSlope.position - transform.position).normalized;

            // 给刚体施加初速度，消除之前向下滚落的分速度，使推动非常灵敏
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = pushDirection * pushForce;
#else
            rb.velocity = pushDirection * pushForce;
#endif
            
            Debug.Log($"[SisyphusRock] 玩家推动了巨石！朝向: {pushDirection}，初速度: {pushForce}");
        }

        private void Update()
        {
            // 3. 实时检测坠落深度，越界后自动重置
            if (transform.position.y < resetYThreshold)
            {
                ResetRockPosition();
            }
        }

        private void ResetRockPosition()
        {
            if (resetSpawnPoint == null)
            {
                Debug.LogError($"[SisyphusRock] '{gameObject.name}' 未指定 resetSpawnPoint！无法重置位置。");
                return;
            }

            // 瞬移坐标并彻底抹除之前的物理速度和自转
            transform.position = resetSpawnPoint.position;
            transform.rotation = resetSpawnPoint.rotation;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            Debug.Log("<color=yellow>[SisyphusRock] 巨石已滚落深渊，已被重置回起点，进入下一轮荒诞循环。</color>");
        }
    }
}
