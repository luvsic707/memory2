using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 石台网格表面受力崩裂形变组件 (Stage 2)
    /// 1. 将网格转化为 Faceted Flat Shading（扁平着色/低多边形风格），打破共享顶点，实现绝对锋利的石质切面。
    /// 2. 在 Update 中，顶点沿着独立法线方向发生阶梯位移，配合法线重构，呈现硬朗错落的石头碎裂视觉。
    /// </summary>
    public class RockFractureDistorter : MonoBehaviour
    {
        [Header("崩裂变形参数")]
        [Tooltip("变形强度系数，值越大隆起越明显")]
        public float fractureScale = 0.4f;

        [Tooltip("表面波纹频率（数值越大起伏越密集）")]
        public float waveFrequency = 5f;

        [Tooltip("表面抖动时间速度")]
        public float timeSpeed = 12f;

        [Tooltip("必须开启重算法线以呈现硬面折角阴影")]
        public bool recalculateNormals = true;

        [HideInInspector]
        public float currentIntensity = 0f; // 由 Stage2Controller 实时注入的晃动强度

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
            // 自动激活可读写
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                UnityEditor.ModelImporter mi = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                    Debug.Log($"[RockFracture] 自动为石台模型 '{mf.sharedMesh.name}' 开启了 'Read/Write' 读写选项！");
                }
            }
#endif

            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[RockFracture] 石台模型 '{mf.sharedMesh.name}' 未开启 'Read/Write' 读写权限，表面崩裂效果将被跳过。");
                return;
            }

            // 复制网格以保护原始资产
            targetMesh = Instantiate(mf.sharedMesh);

            // 核心：分裂共享顶点以实现 100% 扁平硬朗的 Faceted Shading 面效果！
            MakeMeshFaceted(targetMesh);

            originalVertices = targetMesh.vertices;
            originalNormals = targetMesh.normals;

            mf.mesh = targetMesh;

            // 同步更新 MeshCollider
            MeshCollider mc = GetComponentInChildren<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = targetMesh;
            }

            isValid = true;
        }

        /// <summary>
        /// 复制并分离共享的顶点，使每个三角面拥有独立的顶点和法线，呈现硬边低模效果
        /// </summary>
        private void MakeMeshFaceted(Mesh mesh)
        {
            Vector3[] oldVertices = mesh.vertices;
            int[] oldTriangles = mesh.triangles;
            Vector3[] newVertices = new Vector3[oldTriangles.Length];
            int[] newTriangles = new int[oldTriangles.Length];

            for (int i = 0; i < oldTriangles.Length; i++)
            {
                newVertices[i] = oldVertices[oldTriangles[i]];
                newTriangles[i] = i;
            }

            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private void Update()
        {
            if (!isValid || currentIntensity <= 0.01f) return;

            Vector3[] displacedVertices = new Vector3[originalVertices.Length];
            float t = Time.time * timeSpeed;

            // 阶梯式碎裂位移计算
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 v = originalVertices[i];
                Vector3 n = originalNormals[i];

                // 1. 三角波 PingPong
                float valX = v.x * waveFrequency + t;
                float valZ = v.z * waveFrequency + t;
                float triX = Mathf.PingPong(valX, 1.0f) * 2f - 1f;
                float triZ = Mathf.PingPong(valZ, 1.0f) * 2f - 1f;
                float rawValue = triX * triZ;

                // 2. 阶梯断裂阈值 (Slabs)
                float stepValue = 0f;
                if (rawValue > 0.35f) stepValue = 1.0f;
                else if (rawValue < -0.35f) stepValue = -1.0f;

                // 3. 高频 Sign 阶跃抖动
                float jitter = Mathf.Sign(Mathf.Sin(v.y * waveFrequency * 3f - t * 1.5f)) * 0.15f;

                float displacement = (stepValue + jitter);

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
