using UnityEngine;

/// <summary>
/// 达利风格融化变形脚本 (CPU 顶点形变)
/// 类似于萨尔瓦多·达利的《记忆的永恒》，让物体呈现出单向、缓慢、垂坠且变软的视觉特征。
/// 该脚本直接操纵 Mesh 顶点，不依赖特定的 Shader，因此非常独立且兼容任何渲染管线（URP/Standard）。
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class DaliMelt : MonoBehaviour
{
    [Header("融化：单向、缓慢、垂坠")]
    [Tooltip("最终下垂的幅度（米）")]
    public float meltAmount = 0.5f;

    [Tooltip("融化起伏变化的速度（极慢！）")]
    public float meltSpeed = 0.15f;

    [Tooltip("下垂程度：系数越大，越是产生顺着高度往下拉长、垂挂的效果")]
    public float droop = 0.6f;

    [Tooltip("极轻微的水平飘动幅度，用于模拟柔软物体的微弱摆动")]
    public float wobble = 0.05f;

    [Header("清晰起伏，不要细碎")]
    [Tooltip("噪声频率缩放。较小的值能产生大块的达利风平滑变形，较大的值会产生细碎恐怖的拉伸")]
    public float noiseScale = 1.2f;

    private Mesh mesh;
    private Vector3[] baseVerts;
    private Vector3[] workVerts;
    private float minY, maxY;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError($"[DaliMelt] 在 {gameObject.name} 上找不到 MeshFilter 或未分配 Mesh！", this);
            enabled = false;
            return;
        }

        // 实例化一个新的 Mesh，防止修改原始的资产文件（Assets 中的模型源文件）
        mesh = Instantiate(mf.sharedMesh);
        mf.mesh = mesh;

        baseVerts = mesh.vertices;
        workVerts = new Vector3[baseVerts.Length];

        // 找出模型在本地坐标系下的上下Y轴范围，用来计算“高度下垂比例”
        minY = float.MaxValue; 
        maxY = float.MinValue;
        foreach (var v in baseVerts)
        {
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }
    }

    void Update()
    {
        float t = Time.time * meltSpeed;

        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 v = baseVerts[i];

            // 顶点在整个模型高度中的相对比例（0 = 最底部，1 = 最顶部）
            float heightRatio = Mathf.InverseLerp(minY, maxY, v.y);

            // 使用 Perlin 噪声计算大块、缓慢变动的变形进度
            float n = Mathf.PerlinNoise(v.x * noiseScale, v.z * noiseScale + t);

            // 越靠上的顶点，因为重力和融化下垂，朝下方(-Y)拉伸的偏移量越大
            float melt = n * meltAmount * Mathf.Pow(heightRatio, 1.5f) * droop;

            // 水平方向的细微横摆飘动，增强液体或流体般的“软化”感
            float sway = (Mathf.PerlinNoise(v.y * noiseScale + t, 0f) - 0.5f) * wobble;

            // 应用计算出的偏移量
            workVerts[i] = v + new Vector3(sway, -melt, sway);
        }

        // 重新分配顶点并刷新网格法线以重建正确的光影表面
        mesh.vertices = workVerts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
