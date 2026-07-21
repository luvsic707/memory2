using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 细碎办公摆件拆解坍塌控制器 (Stage 4)
    /// 
    /// 彻底打破以“整个Office格子间”为单位的平移，而是将外部所有 Office 内的
    /// 【所有细碎子物件】（隔板墙体、显示器、文件夹、书籍、座椅、垃圾桶、海报）全部彻底拆解。
    /// 
    /// 每次点击，成百上千个细碎的小物件将以不同的速度和旋转，独立地飞向并平铺在
    /// Office (87) 的透明保护箱体表面，最终形成一面由无数办公垃圾和文件拼凑而成的三维包围墙。
    /// </summary>
    public class OfficeCollapseController : MonoBehaviour
    {
        [Header("场景引用")]
        [Tooltip("Office (87) - 玩家所在的格子间，此物体及其子物体绝对不动")]
        public Transform office87;

        [Tooltip("Office_Building - 包含所有其他 Office 的父物体")]
        public Transform officeBuilding;

        [Header("小物件移动参数")]
        [Tooltip("每次点击物件朝中心移动的基础距离（米）")]
        public float movePerClick = 0.4f;

        [Tooltip("每次点击物件施加的随机旋转角度范围（度）")]
        public float rotationPerClick = 8f;

        [Tooltip("平滑移动速度（每秒）")]
        public float smoothSpeed = 3.5f;

        [Header("安全区尺寸（防止穿入 Office 87 内部）")]
        [Tooltip("Office 87 的安全保护箱体大小。细碎物件会整齐地贴在这个盒子的表面堆叠（建议宽度设为 3.5m 左右，即可形成贴身包装）")]
        public Vector3 safeBoxSize = new Vector3(3.6f, 3.2f, 3.6f);

        // 扁平化存储所有待拆解移动的子物件
        private List<Transform> _allProps = new List<Transform>();
        private List<Vector3> _targetPositions = new List<Vector3>();
        private List<Quaternion> _targetRotations = new List<Quaternion>();
        private List<float> _moveMultipliers = new List<float>();
        private List<Vector3> _randomRotDirs = new List<Vector3>();

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

            // 收集 Office_Building 下所有格子间中的每一个子物件
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

            foreach (Transform officeRoom in officeBuilding)
            {
                // 遍历格子间内的每一个细碎物件
                foreach (Transform prop in officeRoom)
                {
                    // 过滤掉极其微小或者容易穿帮的节点
                    if (prop.name.ToLower().Contains("carpet")) continue;

                    _allProps.Add(prop);
                    _targetPositions.Add(prop.position);
                    _targetRotations.Add(prop.rotation);

                    // 给予每个物体不同的移动速度乘数，产生 staggered（错落有致）的飞行美感
                    _moveMultipliers.Add(Random.Range(0.6f, 1.4f));

                    // 预分配旋转轴向
                    _randomRotDirs.Add(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized);
                }
            }

            Debug.Log($"[OfficeCollapse] 成功拆解并装配了 {_allProps.Count} 个细碎小物件。它们将独立坍塌！");

            StartCoroutine(SmoothMoveLoop());
        }

        /// <summary>
        /// 由 Stage4Controller 在每次点击时调用
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;

            float intensity = GetIntensity();
            Vector3 halfSize = safeBoxSize * 0.5f;

            for (int i = 0; i < _allProps.Count; i++)
            {
                if (_allProps[i] == null) continue;

                Vector3 targetPos = _targetPositions[i];

                // 将所有零散的物体分流堆叠到 5 个不同的箱体表面，防止其重叠在同一个平面
                Vector3 targetCenter = _center;
                if (i % 5 == 4) 
                {
                    // 分流至天花板顶面
                    float ceilingY = _center.y + halfSize.y;
                    Vector3 toCenterH = new Vector3(_center.x - _allProps[i].position.x, 0f, _center.z - _allProps[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, ceilingY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z));
                }
                else if (i % 5 == 3)
                {
                    // 分流至地板底面
                    float floorY = _center.y - halfSize.y;
                    Vector3 toCenterH = new Vector3(_center.x - _allProps[i].position.x, 0f, _center.z - _allProps[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, floorY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z));
                }
                else
                {
                    // 分流至四周壁面（Left, Right, Front, Back）
                    Vector3 toCenter = (_center - _allProps[i].position);
                    float dist = toCenter.magnitude;
                    if (dist > 0.01f)
                    {
                        float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                        Vector3 testPos = _targetPositions[i] + toCenter.normalized * Mathf.Min(moveAmount, dist);
                        targetPos = ClampToOutsideSafeBox(testPos);
                    }
                }

                _targetPositions[i] = targetPos;

                // 施加旋转
                Vector3 rotAngles = _randomRotDirs[i] * (rotationPerClick * intensity);
                _targetRotations[i] = Quaternion.Euler(rotAngles) * _targetRotations[i];
            }
        }

        /// <summary>
        /// 限制坐标在安全箱体外部
        /// </summary>
        private Vector3 ClampToOutsideSafeBox(Vector3 targetPos)
        {
            Vector3 localPos = targetPos - _center;
            Vector3 halfSize = safeBoxSize * 0.5f;

            // 检查是否进入了安全箱体内部
            bool isInside = Mathf.Abs(localPos.x) < halfSize.x &&
                            Mathf.Abs(localPos.y) < halfSize.y &&
                            Mathf.Abs(localPos.z) < halfSize.z;

            if (isInside)
            {
                // 找出距离最近的面，然后推到外部表面
                float dx = halfSize.x - Mathf.Abs(localPos.x);
                float dy = halfSize.y - Mathf.Abs(localPos.y);
                float dz = halfSize.z - Mathf.Abs(localPos.z);

                if (dx <= dy && dx <= dz)
                {
                    localPos.x = Mathf.Sign(localPos.x) * halfSize.x;
                }
                else if (dy <= dx && dy <= dz)
                {
                    localPos.y = Mathf.Sign(localPos.y) * halfSize.y;
                }
                else
                {
                    localPos.z = Mathf.Sign(localPos.z) * halfSize.z;
                }
            }

            return _center + localPos;
        }

        /// <summary>
        /// 根据点击次数返回当前阶段强度系数（1.0 = 正常, 3.0 = 激烈）
        /// </summary>
        private float GetIntensity()
        {
            if (_totalClicks < 10) return 0.5f;
            if (_totalClicks < 30) return 1.5f;
            return 3.0f;
        }

        /// <summary>
        /// 持续平滑地将所有细碎物体移向目标位置和旋转
        /// </summary>
        private IEnumerator SmoothMoveLoop()
        {
            while (true)
            {
                for (int i = 0; i < _allProps.Count; i++)
                {
                    if (_allProps[i] == null) continue;
                    _allProps[i].position = Vector3.Lerp(_allProps[i].position, _targetPositions[i], Time.deltaTime * smoothSpeed);
                    _allProps[i].rotation = Quaternion.Slerp(_allProps[i].rotation, _targetRotations[i], Time.deltaTime * smoothSpeed);
                }
                yield return null;
            }
        }

        /// <summary>
        /// 调试用：在 Scene 视图里显示 Office87 的保护箱体区域
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (office87 == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(office87.position, safeBoxSize);
        }
    }
}
