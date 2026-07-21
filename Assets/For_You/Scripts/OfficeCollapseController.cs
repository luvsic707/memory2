using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 办公室坍塌控制器 (Stage 4)
    /// 
    /// 将 Office_Building 下的所有子 Office 按点击次数推向 Office (87) 的中心，
    /// 同时施加随机的旋转扭曲。Office (87) 本身绝对不移动。
    /// 
    /// 效果分为三个阶段：
    ///   阶段1 (0-10次): 远处 Office 缓缓开始漂移
    ///   阶段2 (10-30次): 办公室快速扭曲旋转挤压
    ///   阶段3 (30次+):  全体 Office 紧贴 Office87，完全封闭
    /// </summary>
    public class OfficeCollapseController : MonoBehaviour
    {
        [Header("场景引用")]
        [Tooltip("Office (87) - 玩家所在的格子间，此物体绝对不移动")]
        public Transform office87;

        [Tooltip("Office_Building - 包含所有其他 Office 的父物体")]
        public Transform officeBuilding;

        [Header("坍塌参数")]
        [Tooltip("每次点击各 Office 朝中心移动的距离（米）")]
        public float movePerClick = 0.3f;

        [Tooltip("每次点击各 Office 施加的随机旋转角度范围（度）")]
        public float rotationPerClick = 5f;

        [Tooltip("平滑移动速度（每秒）- 决定移动的动画流畅度")]
        public float smoothSpeed = 3f;

        [Tooltip("最终闭合时各 Office 与 Office87 中心的最近距离（米）- 防止穿插")]
        public float minDistanceToCenter = 2.5f;

        // 各 Office 的目标位置和旋转
        private List<Transform> _allOffices = new List<Transform>();
        private List<Vector3> _targetPositions = new List<Vector3>();
        private List<Quaternion> _targetRotations = new List<Quaternion>();

        private Vector3 _center;
        private int _totalClicks = 0;

        private void Start()
        {
            // 获取 Office (87) 中心
            if (office87 == null)
            {
                GameObject found = GameObject.Find("Office (87)");
                if (found != null) office87 = found.transform;
            }

            if (office87 == null)
            {
                Debug.LogError("[OfficeCollapse] 找不到 Office (87)！请手动拖入引用。");
                return;
            }

            _center = office87.position;

            // 收集 Office_Building 下的所有子 Office
            if (officeBuilding == null)
            {
                GameObject found = GameObject.Find("Office_Building");
                if (found != null) officeBuilding = found.transform;
            }

            if (officeBuilding == null)
            {
                Debug.LogError("[OfficeCollapse] 找不到 Office_Building！请手动拖入引用。");
                return;
            }

            foreach (Transform child in officeBuilding)
            {
                _allOffices.Add(child);
                _targetPositions.Add(child.position);
                _targetRotations.Add(child.rotation);
            }

            Debug.Log($"[OfficeCollapse] 已收集 {_allOffices.Count} 个 Office，将向 Office (87) 坍塌。");

            StartCoroutine(SmoothMoveLoop());
        }

        /// <summary>
        /// 由 Stage4Controller 在每次点击时调用
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;

            float intensity = GetIntensity();

            for (int i = 0; i < _allOffices.Count; i++)
            {
                if (_allOffices[i] == null) continue;

                // 计算朝向 Office87 中心的方向
                Vector3 toCenter = (_center - _allOffices[i].position);
                float dist = toCenter.magnitude;

                // 只有距离大于最小距离才继续向内推进
                if (dist > minDistanceToCenter)
                {
                    float moveAmount = movePerClick * intensity;
                    _targetPositions[i] += toCenter.normalized * Mathf.Min(moveAmount, dist - minDistanceToCenter);
                }

                // 每次点击施加随机旋转扭曲（越后期扭曲越大）
                Vector3 randomRotation = new Vector3(
                    Random.Range(-rotationPerClick, rotationPerClick) * intensity,
                    Random.Range(-rotationPerClick, rotationPerClick) * intensity,
                    Random.Range(-rotationPerClick, rotationPerClick) * intensity
                );
                _targetRotations[i] = _allOffices[i].rotation * Quaternion.Euler(randomRotation);
            }
        }

        /// <summary>
        /// 根据点击次数返回当前阶段强度系数（1.0 = 正常, 3.0 = 激烈）
        /// </summary>
        private float GetIntensity()
        {
            if (_totalClicks < 10) return 0.5f;       // 阶段1: 缓慢漂移
            if (_totalClicks < 30) return 1.5f;       // 阶段2: 开始快速扭曲
            return 3.0f;                               // 阶段3: 极速坍塌
        }

        /// <summary>
        /// 持续平滑地将各 Office 移向目标位置和旋转
        /// </summary>
        private IEnumerator SmoothMoveLoop()
        {
            while (true)
            {
                for (int i = 0; i < _allOffices.Count; i++)
                {
                    if (_allOffices[i] == null) continue;
                    _allOffices[i].position = Vector3.Lerp(_allOffices[i].position, _targetPositions[i], Time.deltaTime * smoothSpeed);
                    _allOffices[i].rotation = Quaternion.Slerp(_allOffices[i].rotation, _targetRotations[i], Time.deltaTime * smoothSpeed);
                }
                yield return null;
            }
        }

        /// <summary>
        /// 调试用：在 Scene 视图里显示 Office87 的中心点
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (office87 == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(office87.position, minDistanceToCenter);
        }
    }
}
