using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 莫比乌斯环滚球参数定位与推动交互控制器 (Stage 3)
    /// 1. 采用纯数学参数公式 (Parametric Placement) 绝对贴合在程序化莫比乌斯环上滚动，避开了物理刚体的抖动和脱轨。
    /// 2. 核心数学设计：
    ///    - 莫比乌斯环的真正拓扑周期是 4π，而不是 2π。跑完第一圈 (2π) 球体会走到环带的“反面”，继续跑第二圈 (共 4π) 才能回到正面原点。
    ///    - 参数 vOffset 必须不为 0 (例如 0.15f)。若为 0 则是对称中线，莫比乌斯环的单侧面扭转将在视觉上完全隐形。
    /// 3. 支持鼠标左键持续推进 (添加速度)，通过阻尼进行减速，保证西西弗斯式的无限推球逻辑。
    /// 4. 利用数值微分计算切线叉积，实时计算并对齐球体的法线朝向与滚动动画。
    /// </summary>
    public class BallOnMobius : MonoBehaviour
    {
        [Header("莫比乌斯环绑定")]
        [Tooltip("绑定的程序化莫比乌斯环（若为空，将在场景中自动寻找）")]
        public MobiusStrip mobiusStrip;

        [Header("球体运动物理参数")]
        [Tooltip("球体半径")]
        public float ballRadius = 0.5f;

        [Tooltip("偏心距 (vOffset)。必须大于 0，用于展现莫比乌斯环的正反两面游走")]
        public float vOffset = 0.35f;

        [Tooltip("推动的加速度大小")]
        public float pushForce = 3f;

        [Tooltip("减速摩擦力阻尼")]
        public float friction = 0.6f;

        [Tooltip("最大滚动速度上限")]
        public float maxVelocity = 10f;

        [Header("视觉与对齐")]
        [Tooltip("球体的视觉子节点（用于呈现滚动旋转，若为空则自动绑定第一个子物体）")]
        public Transform ballVisual;

        [Tooltip("是否翻转法线方向（若球体陷入环带内部，勾选此项以将其翻转到表面）")]
        public bool invertNormal = false;

        [Header("统计配置")]
        [Tooltip("是否向全局统计中心记录玩家的推动计数？（如果是背景装饰球，请在 Inspector 中取消勾选以防止重复计数）")]
        public bool trackBehaviorData = true;

        // 运动状态参数
        [HideInInspector]
        public float t = 0f; // 参数位置 [0 .. 4π]
        private float velocity = 0f; // 当前滚动速度

        /// <summary>
        /// 莫比乌斯环第一圈完成标志位（当 t 超过 2π 时触发，即走上面/反面的分界线，开启拉镜头）
        /// </summary>
        public bool HasCompletedFirstLap { get; private set; } = false;

        private void Start()
        {
            // 自动寻找莫比乌斯环
            if (mobiusStrip == null)
            {
                mobiusStrip = FindObjectOfType<MobiusStrip>();
            }

            // 自动配置视觉物体
            if (ballVisual == null && transform.childCount > 0)
            {
                ballVisual = transform.GetChild(0);
            }
        }

        private void Update()
        {
            if (mobiusStrip == null) return;

            // 1. 监测持续按住交互：按住左键或键盘Q给球体施加持续推力（直接累加速度，不增加行为计数）
            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Q))
            {
                velocity += pushForce * Time.deltaTime;
            }

            // 2. 监测单次点击交互：在鼠标按下的瞬间，调用一次 Push 接口（这里会增加 1 次持久化统计计数）
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q))
            {
                Push(pushForce * 0.3f); // 点击瞬间给球一个微小的初始冲量，并累计 1 次计数
            }

            // 2. 应用摩擦力阻尼
            velocity -= velocity * friction * Time.deltaTime;
            velocity = Mathf.Clamp(velocity, -maxVelocity, maxVelocity);

            // 3. 更新参数位置 t，并在 [0, 4π] 之间循环包裹
            float deltaT = velocity * Time.deltaTime;
            t += deltaT;
            float maxT = 4f * Mathf.PI;
            t = (t % maxT + maxT) % maxT;

            // 检查是否完成了第一圈 (2π)
            if (!HasCompletedFirstLap && t >= 2f * Mathf.PI)
            {
                HasCompletedFirstLap = true;
                Debug.Log("[BallOnMobius] 球体已越过 2π 界限，翻转至反面。莫比乌斯显现触发开启！");
            }

            // 4. 参数化位置解算与 4π 周期翻转
            float uMod = t % (2f * Mathf.PI);
            
            // 判定当前是奇数圈还是偶数圈。奇数圈走反面（偏离 vOffset 为负值），偶数圈走正面（为正值）
            // 这代表了莫比乌斯环在空间扭转 180 度后，球体继续绕行以顺利返回正面的几何事实
            float vRide = (Mathf.Floor(t / (2f * Mathf.PI)) % 2 == 0) ? vOffset : -vOffset;

            float R = mobiusStrip.radius;
            Vector3 centerPos = MobiusStrip.GetMobiusPoint(uMod, vRide, R);

            // 5. 数值微分法计算法线 (使用有限差分 epsilon = 0.01)
            float eps = 0.01f;
            Vector3 dU = (MobiusStrip.GetMobiusPoint(uMod + eps, vRide, R) - MobiusStrip.GetMobiusPoint(uMod - eps, vRide, R)) / (2f * eps);
            Vector3 dV = (MobiusStrip.GetMobiusPoint(uMod, vRide + eps, R) - MobiusStrip.GetMobiusPoint(uMod, vRide - eps, R)) / (2f * eps);

            // 叉积得到垂直于莫比乌斯环表面的法线
            Vector3 normal = Vector3.Cross(dU, dV).normalized;
            if (invertNormal)
            {
                normal = -normal;
            }

            // 6. 应用空间位置与贴合对齐
            transform.position = mobiusStrip.transform.TransformPoint(centerPos) + normal * ballRadius;

            // 7. 渲染滚动旋转动画 (让球体视觉物体绕着与运动方向垂直的 dV 轴转动)
            if (ballVisual != null && Mathf.Abs(velocity) > 0.01f)
            {
                // 计算滚动弧度对应的角度变动
                float distanceMoved = velocity * Time.deltaTime;
                float angleDegrees = (distanceMoved / ballRadius) * Mathf.Rad2Deg;

                // 运动方向的侧向法线（即宽度方向 dV）是旋转轴
                Vector3 rotAxis = dV.normalized;
                ballVisual.Rotate(rotAxis, angleDegrees, Space.World);
            }
        }

        /// <summary>
        /// 公共交互推动接口：向当前滚动方向施加一个力
        /// </summary>
        public void Push(float amount)
        {
            velocity += amount;

            // 仅当开启了统计并且数据中心存在时记录行为数据，防止多球共存时计数倍增
            if (trackBehaviorData && PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddPush();
            }
        }
    }
}
