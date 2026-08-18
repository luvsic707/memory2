using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 时钟指针点击旋转控制器 (Stage 3)
    /// 1. 在 Start 时自动禁用自带的 Animator 动画组件，防止默认的自动旋转动画冲突。
    /// 2. 监听全局鼠标左键点击（或按Q），每次点击使指定的指针（如秒针/分针）平滑顺畅地旋转整整一圈 (360度)。
    /// 3. 支持快速连续点击：每次点击会将目标旋转值增加 360 度，指针会顺畅地连续转动，不会卡顿或重置。
    /// </summary>
    public class ClockHandController : MonoBehaviour
    {
        [Header("旋转目标")]
        [Tooltip("需要旋转的指针物体（若为空，默认使用挂载此脚本的 GameObject）")]
        public Transform handTransform;

        [Header("旋转物理参数")]
        [Tooltip("旋转所绕轴向，默认绕 Z 轴旋转 (Vector3.forward)。根据模型制作标准，可修改为 Vector3.up 或 Vector3.right")]
        public Vector3 rotationAxis = Vector3.forward;

        [Tooltip("每次点击指针旋转的目标角度。秒针为 360 度，分针为 6 度，时针为 0.5 度")]
        public float anglePerClick = 360f;

        [Tooltip("指针旋转的角速度 (度/秒)。让其在一秒内刚好转完点击度数，建议设置与 anglePerClick 相同的值")]
        public float rotationSpeed = 360f;

        // 旋转角度追踪
        private float currentAngle = 0f;
        private float targetAngle = 0f;

        private void Start()
        {
            if (handTransform == null)
            {
                handTransform = transform;
            }

            // 自动防御：禁用时钟物体自带的 Animator 动画，接管指针的绝对控制权
            Animator anim = GetComponentInParent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
                Debug.Log($"[ClockHand] 自动禁用了 '{anim.gameObject.name}' 上的 Animator，以防止默认自动旋转动画冲突。");
            }
        }

        private void Update()
        {
            // 1. 监测鼠标左键点击（或按Q）交互
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q))
            {
                // 每次点击，目标旋转度增加设定的单次点击角度
                targetAngle += anglePerClick;
            }

            // 2. 平滑插值计算当前旋转度数，朝向目标度数递增
            if (Mathf.Abs(currentAngle - targetAngle) > 0.01f)
            {
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
                
                // 应用局部旋转偏移
                handTransform.localRotation = Quaternion.Euler(rotationAxis * currentAngle);
            }
        }
    }
}
