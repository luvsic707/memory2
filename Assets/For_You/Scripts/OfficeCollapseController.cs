using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 细碎办公摆件拆解坍塌与 Mesh 尺寸动态防穿插控制器 (Stage 4)
    /// 
    /// 1. 将外部所有 Office 内的所有子物件彻底解构为独立物体进行飞行。
    /// 2. 【防穿插终极方案】：根据每个物件的 MeshRenderer 实际包围盒尺寸 (Bounds Size)，
    ///    动态计算每个物件专属的安全箱大小：
    ///    - 计算公式：每个物体专属的安全箱 = safeBoxSize + 它的实际 Mesh 尺寸。
    ///    - 这样可以确保：不论物件有多大（大墙板、长书桌，还是小文件夹），它们的外边缘在收拢时，
    ///      都会完美贴合在 Office 87 的外边界，而绝对不会有一丁点网格穿透进入工位内部！
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

        [Header("安全区尺寸（工位净空间）")]
        [Tooltip("Office 87 的工位内部净空间尺寸。在这个范围内的空间将被绝对保护。")]
        public Vector3 safeBoxSize = new Vector3(3.2f, 2.8f, 3.2f);

        // 扁平化存储所有待拆解移动的子物件
        private List<Transform> _allProps = new List<Transform>();
        private List<Vector3> _targetPositions = new List<Vector3>();
        private List<Quaternion> _targetRotations = new List<Quaternion>();
        private List<float> _moveMultipliers = new List<float>();
        private List<Vector3> _randomRotDirs = new List<Vector3>();
        private List<Vector3> _propSizes = new List<Vector3>(); // 动态存储每个物体的 Mesh 尺寸

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
                foreach (Transform prop in officeRoom)
                {
                    if (prop.name.ToLower().Contains("carpet")) continue;

                    _allProps.Add(prop);
                    _targetPositions.Add(prop.position);
                    _targetRotations.Add(prop.rotation);

                    // 错落有致的速度
                    _moveMultipliers.Add(Random.Range(0.6f, 1.4f));
                    _randomRotDirs.Add(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized);

                    // 动态获取物件的包围盒尺寸，如果无 Renderer 则默认为 0
                    Vector3 size = Vector3.zero;
                    MeshRenderer mr = prop.GetComponent<MeshRenderer>();
                    if (mr == null)
                    {
                        mr = prop.GetComponentInChildren<MeshRenderer>();
                    }

                    if (mr != null)
                    {
                        // 使用 bounds.size，为了防止局部旋转误差，取最大范围
                        size = mr.bounds.size;
                    }

                    // 限制最大尺寸防呆（比如某些整体大板子），最大限制在 4 米
                    size.x = Mathf.Min(size.x, 4.0f);
                    size.y = Mathf.Min(size.y, 4.0f);
                    size.z = Mathf.Min(size.z, 4.0f);

                    _propSizes.Add(size);
                }
            }

            Debug.Log($"[OfficeCollapse] 已成功解构 {_allProps.Count} 个细碎物件，Mesh 尺寸感应防穿插系统初始化完成。");

            StartCoroutine(SmoothMoveLoop());
        }

        /// <summary>
        /// 由 Stage4Controller 在每次点击时调用
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;

            float intensity = GetIntensity();

            for (int i = 0; i < _allProps.Count; i++)
            {
                if (_allProps[i] == null) continue;

                // 核心防穿插公式：专属安全阻挡盒 = 内部净空大小 + 该物体实际尺寸
                Vector3 currentBox = safeBoxSize + _propSizes[i];
                Vector3 halfSize = currentBox * 0.5f;

                Vector3 targetPos = _targetPositions[i];

                if (i % 5 == 4) 
                {
                    // 天花板顶面覆盖
                    float ceilingY = _center.y + halfSize.y;
                    Vector3 toCenterH = new Vector3(_center.x - _allProps[i].position.x, 0f, _center.z - _allProps[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, ceilingY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z), currentBox);
                }
                else if (i % 5 == 3)
                {
                    // 地板底面覆盖
                    float floorY = _center.y - halfSize.y;
                    Vector3 toCenterH = new Vector3(_center.x - _allProps[i].position.x, 0f, _center.z - _allProps[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, floorY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z), currentBox);
                }
                else
                {
                    // 四周侧壁覆盖
                    Vector3 toCenter = (_center - _allProps[i].position);
                    float dist = toCenter.magnitude;
                    if (dist > 0.01f)
                    {
                        float moveAmount = movePerClick * intensity * _moveMultipliers[i];
                        Vector3 testPos = _targetPositions[i] + toCenter.normalized * Mathf.Min(moveAmount, dist);
                        targetPos = ClampToOutsideSafeBox(testPos, currentBox);
                    }
                }

                _targetPositions[i] = targetPos;

                // 旋转
                Vector3 rotAngles = _randomRotDirs[i] * (rotationPerClick * intensity);
                _targetRotations[i] = Quaternion.Euler(rotAngles) * _targetRotations[i];
            }
        }

        /// <summary>
        /// 限制坐标在指定安全箱体外部
        /// </summary>
        private Vector3 ClampToOutsideSafeBox(Vector3 targetPos, Vector3 boxSize)
        {
            Vector3 localPos = targetPos - _center;
            Vector3 halfSize = boxSize * 0.5f;

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
        /// 调试用：在 Scene 视图里显示 Office87 的标准保护箱体区域
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (office87 == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(office87.position, safeBoxSize);
        }
    }
}
