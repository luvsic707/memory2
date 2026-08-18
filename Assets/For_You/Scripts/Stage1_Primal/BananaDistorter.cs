using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉物理网格扭曲变形组件 (Stage 1)
    /// 1. 在 Start 时获取自身的 MeshFilter，复制一份网格实例（避免修改磁盘上的原始模型文件）。
    /// 2. 智能优化：在编辑器运行模式下，若未开启 Read/Write 读写权限，自动修改导入设置并重新导入，零手工配置成本。
    /// 3. 基于当前关卡的扭曲因子（distortionFactor），直接在 CPU 中改变顶点坐标：
    ///    - 沿 Y 轴高度做旋转（扭麻花 Twist 效果）。
    ///    - 沿 X/Z 轴方向做正弦位移（弯曲 Bend 效果）。
    /// 4. 重新计算包围盒和法线，并应用到 MeshCollider 以保证物理碰撞与扭曲后的外观一致。
    /// </summary>
    public class BananaDistorter : MonoBehaviour
    {
        [Header("扭曲参数")]
        [Tooltip("当前关卡的扭曲因子，值越大变形越严重")]
        public float distortionFactor = 0f;

        [Tooltip("扭曲旋转速度")]
        public float twistRate = 5f;

        [Tooltip("正弦弯曲强度")]
        public float bendRate = 0.4f;

        private void Start()
        {
            // 如果扭曲度极小，忽略计算以提升性能
            if (distortionFactor < 0.05f) return;

            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

#if UNITY_EDITOR
            // 智能辅助：如果在编辑器中运行且模型不可读写，自动用代码将其开启并重新导入模型
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (!string.IsNullOrEmpty(assetPath))
            {
                UnityEditor.ModelImporter mi = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
                if (mi != null && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                    Debug.Log($"<color=green>[BananaDistorter] 自动成功为模型资源 '{mf.sharedMesh.name}' 开启了 'Read/Write' 读写选项并重新导入！</color>");
                }
            }
#endif

            // 安全防护：如果不是在编辑器模式，或者修改后仍不可读写，退出防止报错中断线程
            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[BananaDistorter] 香蕉模型 '{mf.sharedMesh.name}' 未开启 'Read/Write' 读写权限，网格变形将被跳过。");
                return;
            }

            // 实例化一个新的 Mesh 副本，断开与资产文件的链接，防止污染源资源
            Mesh mesh = Instantiate(mf.sharedMesh);
            Vector3[] vertices = mesh.vertices;

            // 执行顶点数学形变
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];

                // 1. 扭转 (Twist)：越靠近顶部 (Y 越高) 旋转角越大
                float angle = v.y * distortionFactor * twistRate;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float newX = v.x * cos - v.z * sin;
                float newZ = v.x * sin + v.z * cos;

                // 2. 弯曲 (Bend)：使用正弦波在侧向产生扭捏拉伸
                newX += Mathf.Sin(v.y * 2f) * distortionFactor * bendRate;
                float newY = v.y + Mathf.Cos(v.x * 2f) * distortionFactor * 0.1f;

                vertices[i] = new Vector3(newX, newY, newZ);
            }

            // 写回网格并重新计算渲染法线
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            mf.mesh = mesh;

            // 如果物体身上或子级使用的是 MeshCollider，同步更新碰撞网格以确保碰撞体贴合
            MeshCollider mc = GetComponentInChildren<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = mesh;
            }
        }
    }
}
