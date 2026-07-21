using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 办公室整体坍塌与内部物件剥离控制器 (Stage 4)
    /// 
    /// 1. 将 Office_Building 下的所有子 Office 按点击次数推向 Office (87) 的边界表面。
    /// 2. Office (87) 本身绝对不发生 any 移动，确保玩家核心工位不受穿插。
    /// 3. 【视觉强化】：在外部办公室收拢的同时，各办公室内部的所有子物件（电脑、文件夹、座椅、海报、书本等）
    ///    会从格子间结构上缓缓剥离，产生随机角度的漂移和扭曲，从而形成极其震撼的“官僚主义物品风暴”包围感。
    /// </summary>
    public class OfficeCollapseController : MonoBehaviour
    {
        [System.Serializable]
        public struct PropData
        {
            public Transform transform;
            public Vector3 initialLocalPos;
            public Quaternion initialLocalRot;
            public Vector3 randomOffsetDir;
            public Vector3 randomRotAxis;
        }

        public class OfficeData
        {
            public Transform transform;
            public List<PropData> props = new List<PropData>();
        }

        [Header("场景引用")]
        [Tooltip("Office (87) - 玩家所在的格子间，此物体绝对不移动")]
        public Transform office87;

        [Tooltip("Office_Building - 包含所有其他 Office 的父物体")]
        public Transform officeBuilding;

        [Header("格子间整体移动参数")]
        [Tooltip("每次点击各 Office 朝中心移动的距离（米）")]
        public float movePerClick = 0.3f;

        [Tooltip("每次点击各 Office 整体施加的随机旋转角度范围（度）")]
        public float rotationPerClick = 5f;

        [Tooltip("平滑移动速度（每秒）- 决定移动的动画流畅度")]
        public float smoothSpeed = 3f;

        [Header("安全区尺寸（防止穿入 Office 87 内部）")]
        [Tooltip("Office 87 的安全箱体大小 (宽, 高, 深)。由于其他 Office 的 Pivot 在中心，该尺寸应为 [Office 87 宽度 + 预估阻挡物宽度]（建议 6.5m 左右以防穿插）。")]
        public Vector3 safeBoxSize = new Vector3(6.5f, 5.0f, 6.5f);

        [Header("子物件剥离扭曲参数")]
        [Tooltip("随着点击次数增加，子物件偏离原本位置的最大距离（米）")]
        public float maxPropDisplacement = 1.0f;

        [Tooltip("随着点击次数增加，子物件偏离原本角度的最大旋转度数（度）")]
        public float maxPropRotation = 35f;

        // 各 Office 的目标位置和旋转
        private List<Transform> _allOffices = new List<Transform>();
        private List<Vector3> _targetPositions = new List<Vector3>();
        private List<Quaternion> _targetRotations = new List<Quaternion>();

        // 内部子物件剥离数据
        private List<OfficeData> _officesData = new List<OfficeData>();

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

                // 构建子物件列表
                OfficeData oData = new OfficeData();
                oData.transform = child;

                foreach (Transform propChild in child)
                {
                    // 忽略地毯等大型地表，让小摆件漂起来效果最好
                    if (propChild.name.ToLower().Contains("carpet")) continue;

                    PropData pData = new PropData();
                    pData.transform = propChild;
                    pData.initialLocalPos = propChild.localPosition;
                    pData.initialLocalRot = propChild.localRotation;

                    // 生成随机漂移方向和旋转轴向
                    pData.randomOffsetDir = Random.insideUnitSphere.normalized;
                    pData.randomRotAxis = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

                    oData.props.Add(pData);
                }
                _officesData.Add(oData);
            }

            Debug.Log($"[OfficeCollapse] 已收集 {_allOffices.Count} 个 Office，子物件剥离机制初始化完成。");

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

            for (int i = 0; i < _allOffices.Count; i++)
            {
                if (_allOffices[i] == null) continue;

                Vector3 targetPos = _targetPositions[i];

                if (i % 5 == 4) 
                {
                    // 20% 的办公室贴着天花板高度水平滑行，平铺在顶部
                    float ceilingY = _center.y + halfSize.y;
                    
                    Vector3 toCenterH = new Vector3(_center.x - _allOffices[i].position.x, 0f, _center.z - _allOffices[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity;
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, ceilingY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z));
                }
                else if (i % 5 == 3)
                {
                    // 20% 的办公室贴着地板高度水平滑行，平铺在底部
                    float floorY = _center.y - halfSize.y;
                    
                    Vector3 toCenterH = new Vector3(_center.x - _allOffices[i].position.x, 0f, _center.z - _allOffices[i].position.z);
                    float distH = toCenterH.magnitude;
                    float moveAmount = movePerClick * intensity;
                    
                    Vector3 nextH = _targetPositions[i] + toCenterH.normalized * Mathf.Min(moveAmount, distH);
                    float nextY = Mathf.MoveTowards(_targetPositions[i].y, floorY, moveAmount * 1.5f);
                    
                    targetPos = ClampToOutsideSafeBox(new Vector3(nextH.x, nextY, nextH.z));
                }
                else
                {
                    // 60% 的办公室沿水平/斜向直接向中心靠拢，贴在四周壁面上
                    Vector3 toCenter = (_center - _allOffices[i].position);
                    float dist = toCenter.magnitude;
                    if (dist > 0.01f)
                    {
                        float moveAmount = movePerClick * intensity;
                        Vector3 testPos = _targetPositions[i] + toCenter.normalized * Mathf.Min(moveAmount, dist);
                        targetPos = ClampToOutsideSafeBox(testPos);
                    }
                }

                _targetPositions[i] = targetPos;

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
            if (_totalClicks < 10) return 0.5f;       // 阶段1: 缓慢漂移
            if (_totalClicks < 30) return 1.5f;       // 阶段2: 开始快速扭曲
            return 3.0f;                               // 阶段3: 极速坍塌
        }

        /// <summary>
        /// 持续平滑地将各 Office 及其内部的小物件移向目标位置和旋转
        /// </summary>
        private IEnumerator SmoothMoveLoop()
        {
            while (true)
            {
                // 随着点击次数增加（在 50 次内），剥离偏离强度渐强
                float progress = Mathf.Clamp01(_totalClicks / 50f);

                for (int i = 0; i < _allOffices.Count; i++)
                {
                    if (_allOffices[i] == null) continue;

                    // 1. 格子间主体移动
                    _allOffices[i].position = Vector3.Lerp(_allOffices[i].position, _targetPositions[i], Time.deltaTime * smoothSpeed);
                    _allOffices[i].rotation = Quaternion.Slerp(_allOffices[i].rotation, _targetRotations[i], Time.deltaTime * smoothSpeed);

                    // 2. 内部子摆件剥离偏移移动
                    if (i < _officesData.Count)
                    {
                        var oData = _officesData[i];
                        for (int j = 0; j < oData.props.Count; j++)
                        {
                            var prop = oData.props[j];
                            if (prop.transform == null) continue;

                            Vector3 targetLocalPos = prop.initialLocalPos + prop.randomOffsetDir * (maxPropDisplacement * progress);
                            Quaternion targetLocalRot = prop.initialLocalRot * Quaternion.AngleAxis(maxPropRotation * progress, prop.randomRotAxis);

                            prop.transform.localPosition = Vector3.Lerp(prop.transform.localPosition, targetLocalPos, Time.deltaTime * smoothSpeed);
                            prop.transform.localRotation = Quaternion.Slerp(prop.transform.localRotation, targetLocalRot, Time.deltaTime * smoothSpeed);
                        }
                    }
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
