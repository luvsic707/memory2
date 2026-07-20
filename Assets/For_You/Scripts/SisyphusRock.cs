using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 西西弗斯之石控制器 (Sisyphus Rock Controller) - 简化与物理引导版
    /// 模拟西西弗斯推石上山的荒谬循环：
    /// 1. 状态机机制：分为 Pushing（推动上山）和 RollingDown（自动滚落）两个状态。
    /// 2. 物理引导：通过将刚体侧向（横向）速度和位置投影并纠偏，防止球体滑落到斜坡外。
    ///    注意：我们只约束横向偏移，保留法线（垂直于斜坡）方向的速度与位置，以确保物理碰撞正常，防止穿透斜坡。
    /// 3. Q 键交互：玩家按下 Q 键时施加向上的冲量。当到达山顶时，球体自动进入滚落状态。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SisyphusRock : MonoBehaviour, IInteractable
    {
        public enum RockState
        {
            Pushing,      // 正在被玩家往上推
            RollingDown   // 到达山顶，正在自动滚落回起点
        }

        [Header("核心场景引用")]
        [Tooltip("斜坡的顶端参考点")]
        public Transform topOfSlope;

        [Tooltip("巨石滚落回的出生点参考点")]
        public Transform resetSpawnPoint;

        [Header("物理与推动参数")]
        [Tooltip("玩家每次按 Q 推动时施加的向上冲量大小")]
        public float pushForce = 5f;

        [Tooltip("到达山顶后，自动滚落时的向下辅助拉力大小")]
        public float rollDownForce = 15f;

        [Tooltip("当球体距离顶端小于此值时，判定为到达山顶")]
        public float topThreshold = 2.5f;

        [Tooltip("当球体滚回距离起点小于此值时，判定为回到坡底")]
        public float bottomThreshold = 2.5f;

        [Header("重置与防卡")]
        [Tooltip("当球体 Y 坐标低于此值时，强制进行瞬移重置")]
        public float resetYThreshold = -20f;

        [Tooltip("是否开启中心线物理引导，防止球体滚偏掉出斜坡")]
        public bool constraintToCenterline = true;

        [Header("计数增加时机")]
        [Tooltip("是否在玩家按 Q 键推动时让计数增加")]
        public bool incrementOnInteract = false;

        [Tooltip("是否在巨石滚落到底重置时让计数增加 (推荐：代表完成了一次循环)")]
        public bool incrementOnReset = true;

        private Rigidbody rb;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private RockState currentState = RockState.Pushing;

        // 实现 IInteractable 接口的属性
        public string InteractHint => currentState == RockState.Pushing ? "推巨石" : "巨石滚落中...";

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            // 记录最初在场景中的位置和旋转作为防呆备份
            initialPosition = transform.position;
            initialRotation = transform.rotation;

            // 自动防御：确保球体有 Collider，防止射线穿透
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<SphereCollider>();
                Debug.LogWarning($"[SisyphusRock] '{gameObject.name}' 上没有 Collider！已自动添加 SphereCollider，确保 Q 键射线检测可用。");
            }
        }

        // 实现 IInteractable 接口的交互方法
        public void Interact()
        {
            // 如果巨石正在滚落中，不接受玩家的推动交互
            if (currentState != RockState.Pushing)
            {
                return;
            }

            // 1. 行为数据累计 (若开启)
            if (incrementOnInteract)
            {
                IncrementPushCount();
            }

            // 2. 施加向上推动的冲量
            PushUpward();
        }

        private void IncrementPushCount()
        {
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddPush();
            }
            else
            {
                Debug.LogWarning("[SisyphusRock] 找不到 PlayerBehaviorData 实例！无法记录推石计数。");
            }
        }

        private void PushUpward()
        {
            Transform target = (topOfSlope != null) ? topOfSlope : transform;
            Vector3 pushDirection = (target.position - transform.position).normalized;

            // 使用 Impulse 模式施加力，使每次交互更有明显的物理碰撞推动感
            rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            
            Debug.Log($"[SisyphusRock] 玩家推动了巨石！朝向: {pushDirection}，冲量大小: {pushForce}");
        }

        private void Update()
        {
            // 3. 状态切换检测与更新
            float distToTop = topOfSlope != null ? Vector3.Distance(transform.position, topOfSlope.position) : float.MaxValue;
            Vector3 startPos = resetSpawnPoint != null ? resetSpawnPoint.position : initialPosition;
            float distToBottom = Vector3.Distance(transform.position, startPos);

            if (currentState == RockState.Pushing)
            {
                // 到达山顶，开始自动滚落
                if (distToTop < topThreshold)
                {
                    currentState = RockState.RollingDown;
                    Debug.Log("<color=orange>[SisyphusRock] 巨石已推至山顶！失去平衡，开始滚落！</color>");
                }
            }
            else if (currentState == RockState.RollingDown)
            {
                // 滚落回起点，重新开启推动
                if (distToBottom < bottomThreshold && rb.linearVelocity.magnitude < 1f)
                {
                    currentState = RockState.Pushing;
                    
                    // 荒谬的胜利：在滚回坡底时增加一次推石计数
                    if (incrementOnReset)
                    {
                        IncrementPushCount();
                    }
                    
                    Debug.Log("<color=green>[SisyphusRock] 巨石已滚落回坡底，进入下一轮推石循环。</color>");
                }
            }

            // 4. 防坠落安全越界检测
            if (transform.position.y < resetYThreshold)
            {
                Debug.LogWarning($"[SisyphusRock] 巨石跌出地图阈值（Y: {transform.position.y:F2}），自动复位。");
                ForceResetToStart();
            }
        }

        private void FixedUpdate()
        {
            Transform top = (topOfSlope != null) ? topOfSlope : transform;
            Vector3 startPos = resetSpawnPoint != null ? resetSpawnPoint.position : initialPosition;
            Vector3 endPos = top.position;

            Vector3 line = endPos - startPos;
            if (line.sqrMagnitude < 0.1f) return;

            Vector3 lineDir = line.normalized;

            // 5. 限制运动在中心线上（物理引导机制）
            if (constraintToCenterline)
            {
                // 计算横向（侧向）的方向向量，垂直于中心线和世界向上方向
                Vector3 sideDir = Vector3.Cross(lineDir, Vector3.up).normalized;
                if (sideDir.sqrMagnitude < 0.001f)
                {
                    sideDir = Vector3.right;
                }

                // A. 仅消除侧向漂移速度：只移除 sideDir 方向的速度分量，保留沿斜坡以及垂直于斜坡表面的速度，以便碰撞器正常工作
#if UNITY_6000_0_OR_NEWER
                Vector3 vel = rb.linearVelocity;
                float lateralSpeed = Vector3.Dot(vel, sideDir);
                rb.linearVelocity = vel - sideDir * lateralSpeed;
#else
                Vector3 vel = rb.velocity;
                float lateralSpeed = Vector3.Dot(vel, sideDir);
                rb.velocity = vel - sideDir * lateralSpeed;
#endif

                // B. 仅纠正侧向位置：只计算在 sideDir 方向的偏离量，并拉回中心线，高度方向（法线）由物理引擎和碰撞体自然决定
                Vector3 toRock = transform.position - startPos;
                float lateralOffsetAmount = Vector3.Dot(toRock, sideDir);
                Vector3 lateralOffset = sideDir * lateralOffsetAmount;
                
                // 如果侧向偏移过大，平滑拉回中心线
                if (lateralOffset.sqrMagnitude > 0.0001f)
                {
                    rb.MovePosition(transform.position - lateralOffset * 0.5f);
                }
            }

            // 6. 滚落状态下的物理辅助推力
            if (currentState == RockState.RollingDown)
            {
                // 给球体施加向起点的拉力，让滚落更具动感和速度
                Vector3 rollDirection = -lineDir;
                rb.AddForce(rollDirection * rollDownForce, ForceMode.Force);
            }
        }

        /// <summary>
        /// 发生意外越界或调试时使用的强制复位方法
        /// </summary>
        public void ForceResetToStart()
        {
            Vector3 startPos = resetSpawnPoint != null ? resetSpawnPoint.position : initialPosition;
            Quaternion startRot = resetSpawnPoint != null ? resetSpawnPoint.rotation : initialRotation;

            transform.position = startPos;
            transform.rotation = startRot;

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
            currentState = RockState.Pushing;

            Debug.Log("[SisyphusRock] 巨石已被强制复位至坡底。");
        }
    }
}
