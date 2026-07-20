using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 石台网格表面受力崩裂形变组件 (Stage 2)
    /// 1. 在 Start 时复制 Mesh 的顶点和法线数据作为基础模板。
    /// 2. 支持编辑器下自动开启模型 Read/Write 可读写属性。
    /// 3. 根据 Stage2Controller 传入的当前晃动强度 (distortionFactor)，在 Update 中直接修改网格顶点：
    ///    - 顶点沿法线方向做正弦/余弦位移，实现表面的隆起、粗糙和撕裂起伏（崩裂感）。
    /// </summary>
    public class RockFractureDistorter : MonoBehaviour
    {
        [Header("崩裂变形参数")]
        [Tooltip("变形强度系数，值越大隆起越明显")]
        public float fractureScale = 0.5f;

        [Tooltip("表面波纹频率（数值越大起伏越密集）")]
        public float waveFrequency = 6f;

        [Tooltip("表面抖动时间变化速度")]
        public float timeSpeed = 15f;

        [Tooltip("是否在变形时重新计算法线（开启后光影会随崩裂实时变化，但高精度模型可能会影响帧率）")]
        public bool recalculateNormals = false;

        [HideInInspector]
        public float currentIntensity = 0f; // 由 Stage2Controller 实时注入的晃动强度 (0.0 到 1.0+)

        private MeshFilter mf;
        private Mesh targetMesh;
        private Vector3[] originalVertices;
        private Vector3[] originalNormals;
        private bool isValid = false;

        private void Start()
        {
            mf = GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"[RockFracture] '{gameObject.name}' 身上或子级未发现 MeshFilter，网格崩裂不生效。");
                return;
            }

#if UNITY_EDITOR
            // 智能辅助：在编辑器中运行且模型不可读写时，自动开启
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                UnityEditor.ModelImporter mi = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                    Debug.Log($"<color=green>[RockFracture] 自动成功为石台模型 '{mf.sharedMesh.name}' 开启了 'Read/Write' 读写选项！</color>");
                }
            }
#endif

            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[RockFracture] 石台模型 '{mf.sharedMesh.name}' 未开启 'Read/Write' 读写权限，表面崩裂效果将被跳过。");
                return;
            }

            // 拷贝网格以防污染原始资产文件
            targetMesh = Instantiate(mf.sharedMesh);
            originalVertices = targetMesh.vertices;
            originalNormals = targetMesh.normals;

            // 确保有法线信息，否则无法进行法线方向的位移
            if (originalNormals == null || originalNormals.Length != originalVertices.Length)
            {
                targetMesh.RecalculateNormals();
                originalNormals = targetMesh.normals;
            }

            mf.mesh = targetMesh;

            // 同步更新 MeshCollider
            MeshCollider mc = GetComponentInChildren<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = targetMesh;
            }

            isValid = true;
        }

        private void Update()
        {
            if (!isValid || currentIntensity <= 0.01f) return;

            Vector3[] displacedVertices = new Vector3[originalVertices.Length];
            float t = Time.time * timeSpeed;

            // 基于顶点原始法线方向，利用阶梯函数 (Step Functions) 与三角波计算硬朗的板块碎裂位移
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 v = originalVertices[i];
                Vector3 n = originalNormals[i];

                // 1. 使用三角波 (PingPong) 代替 Sin 弦波，产生锋利的脊线和棱角，而非圆润的波浪
                float valX = v.x * waveFrequency + t;
                float valZ = v.z * waveFrequency + t;
                float triX = Mathf.PingPong(valX, 1.0f) * 2f - 1f;
                float triZ = Mathf.PingPong(valZ, 1.0f) * 2f - 1f;
                float rawValue = triX * triZ;

                // 2. 引入阶梯阈值 (Threshold Step)，将平滑倾斜转为“板块断裂位移”，产生错落的硬面阶梯 (Slabs)
                float stepValue = 0f;
                if (rawValue > 0.35f) stepValue = 1.0f;
                else if (rawValue < -0.35f) stepValue = -1.0f;

                // 3. 叠加高频硬朗抖动 (使用 Sign 阶跃函数消除震荡的平滑过渡)
                float jitter = Mathf.Sign(Mathf.Sin(v.y * waveFrequency * 3.5f - t * 2f)) * 0.2f;

                float displacement = (stepValue + jitter);

                // 最终位置 = 原位置 + 法线方向 * 崩裂位移 * 注入的强度 * 整体形变系数
                displacedVertices[i] = v + n * displacement * currentIntensity * fractureScale;
            }

            targetMesh.vertices = displacedVertices;
            targetMesh.RecalculateBounds();

            if (recalculateNormals)
            {
                targetMesh.RecalculateNormals();
            }

            // 更新物理碰撞，使得站在上面的玩家能感受到颠簸（如果是 MeshCollider）
            MeshCollider mc = GetComponentInChildren<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = targetMesh;
            }
        }
    }
}
