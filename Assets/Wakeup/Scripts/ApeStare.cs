using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 猩猩注视行为脚本 (Staring Ape)
    /// 自动对齐身体朝向相机，并利用骨骼对齐算法，让头部始终平滑注视相机，带有角度限制和阻尼。
    /// </summary>
    public class ApeStare : MonoBehaviour
    {
        [Header("目标设置")]
        [Tooltip("注视的目标摄像机，如果不填则自动寻找主摄像机")]
        public Camera targetCamera;

        [Header("注视参数")]
        [Tooltip("是否旋转身体朝向目标（水平 Y 轴）")]
        public bool rotateBody = true;

        [Tooltip("是否旋转头部骨骼（3D 跟踪）")]
        public bool rotateHead = true;

        [Tooltip("头部关节骨骼（如 Head 或 Neck）")]
        public Transform headBone;

        [Tooltip("头部最大转动夹角（度），防止颈部扭曲过度")]
        [Range(10f, 90f)]
        public float maxStareAngle = 60f;

        [Tooltip("转头/转身体的插值速度（阻尼）")]
        public float smoothSpeed = 3.0f;

        // 骨骼的初始姿态偏移备份
        private Quaternion _localOffsetRotation;
        private Quaternion _currentHeadLocalRotation;
        private bool _hasInitializedHead = false;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // 初始化头部骨骼姿态备份
            if (rotateHead && headBone != null)
            {
                // 备份初始姿态，用于自动校准不同 FBX 模型骨骼的前方朝向差异
                _localOffsetRotation = Quaternion.Inverse(transform.rotation) * headBone.rotation;
                _currentHeadLocalRotation = headBone.localRotation;
                _hasInitializedHead = true;
                Debug.Log($"[ApeStare] 成功初始化 '{gameObject.name}' 的头部骨骼: {headBone.name}");
            }
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            // 1. 身体水平旋转朝向相机
            if (rotateBody)
            {
                Vector3 targetPos = targetCamera.transform.position;
                targetPos.y = transform.position.y; // 保持直立，不随高度倾斜

                Vector3 direction = targetPos - transform.position;
                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetBodyRot = Quaternion.LookRotation(direction, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetBodyRot, Time.deltaTime * smoothSpeed);
                }
            }
        }

        private void LateUpdate()
        {
            // 2. 头部骨骼指向相机（必须放在 LateUpdate 中，防止被 Animator 动画强行覆盖）
            if (rotateHead && headBone != null && _hasInitializedHead && targetCamera != null)
            {
                // 计算头部到相机的向量
                Vector3 targetDir = targetCamera.transform.position - headBone.position;
                if (targetDir.sqrMagnitude > 0.01f)
                {
                    // 转换到身体的局部空间
                    Vector3 localTargetDir = transform.InverseTransformDirection(targetDir);

                    // 计算局部空间下的偏航角 (Yaw) 和俯仰角 (Pitch)
                    float yaw = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(localTargetDir.y, Mathf.Sqrt(localTargetDir.x * localTargetDir.x + localTargetDir.z * localTargetDir.z)) * Mathf.Rad2Deg;

                    // 限制转头角度，防止做出“猫头鹰”式的扭头惊悚效果
                    yaw = Mathf.Clamp(yaw, -maxStareAngle, maxStareAngle);
                    pitch = Mathf.Clamp(pitch, -maxStareAngle, maxStareAngle);

                    // 计算限制后的局部朝向
                    Quaternion clampedLocalLook = Quaternion.Euler(pitch, yaw, 0);

                    // 结合备份的骨骼轴向偏移，算出最终的局部旋转
                    Quaternion targetLocalRot = clampedLocalLook * Quaternion.Inverse(transform.rotation) * (transform.rotation * _localOffsetRotation);

                    // 平滑过渡
                    _currentHeadLocalRotation = Quaternion.Slerp(_currentHeadLocalRotation, targetLocalRot, Time.deltaTime * smoothSpeed);

                    // 应用旋转
                    headBone.localRotation = _currentHeadLocalRotation;
                }
            }
        }
    }
}
