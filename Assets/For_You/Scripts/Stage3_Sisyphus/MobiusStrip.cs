using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 程序化莫比乌斯环网格生成器 (Stage 3)
    /// 1. 基于数学参数方程在运行时动态生成莫比乌斯环的 Mesh。
    /// 2. 自动挂载或配置 MeshFilter 和 MeshRenderer，使用标准 URP/Lit 材质兼容设置。
    /// 3. 提供静态数学公式 GetMobiusPoint，供滚球组件进行绝对参数定位。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MobiusStrip : MonoBehaviour
    {
        [Header("莫比乌斯环几何参数")]
        [Tooltip("莫比乌斯环中心圆环半径 (R)")]
        public float radius = 8f;

        [Tooltip("环带的宽度 (W)")]
        public float width = 2f;

        [Header("细分精细度")]
        [Range(30, 500)]
        [Tooltip("环带圆周方向的细分段数 (u)。数值大则更圆滑，推荐 240")]
        public int segmentsU = 240;

        [Range(2, 30)]
        [Tooltip("环带宽度方向的细分段数 (v)。数值越大则横截面网格越密，推荐 8")]
        public int segmentsV = 8;

        [Header("材质设置")]
        [Tooltip("网格材质（若为空，将自动加载标准 Shader 进行着色）")]
        public Material material;

        private Mesh mesh;

        private void Start()
        {
            GenerateMobiusMesh();
        }

        private void OnValidate()
        {
            // 限制参数在安全合理范围内以防生成崩溃
            if (segmentsU < 10) segmentsU = 10;
            if (segmentsV < 2) segmentsV = 2;
            if (radius < 0.1f) radius = 0.1f;
            if (width < 0.05f) width = 0.05f;

            // 实时在编辑器 Scene 窗口生成网格，即使没点击 Play 也能看见定位！
            GenerateMobiusMesh();
        }

        /// <summary>
        /// 程序化构建莫比乌斯网格
        /// </summary>
        public void GenerateMobiusMesh()
        {
            mesh = new Mesh();
            mesh.name = "Procedural_Mobius_Strip";

            int vCountU = segmentsU + 1;
            int vCountV = segmentsV + 1;
            int numVertices = vCountU * vCountV;

            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];

            // 1. 基于莫比乌斯环参数方程计算顶点坐标和 UV
            for (int i = 0; i <= segmentsU; i++)
            {
                // u 从 0 到 2π
                float u = (float)i / segmentsU * 2f * Mathf.PI;
                for (int j = 0; j <= segmentsV; j++)
                {
                    // v 从 -width/2 到 width/2
                    float v = ((float)j / segmentsV - 0.5f) * width;
                    int index = i * vCountV + j;

                    // 计算三维空间坐标
                    vertices[index] = GetMobiusPoint(u, v, radius);

                    // 映射 UV
                    uvs[index] = new Vector2((float)i / segmentsU, (float)j / segmentsV);
                }
            }

            // 2. 构建三角形面片索引
            int numTriangles = segmentsU * segmentsV * 2 * 3;
            int[] triangles = new int[numTriangles];
            int triIndex = 0;

            for (int i = 0; i < segmentsU; i++)
            {
                for (int j = 0; j < segmentsV; j++)
                {
                    int i0 = i * vCountV + j;
                    int i1 = i * vCountV + (j + 1);
                    int i2 = (i + 1) * vCountV + j;
                    int i3 = (i + 1) * vCountV + (j + 1);

                    // 三角面 1 (逆时针/顺时针取决于观察面，RecalculateNormals 将校准法线)
                    triangles[triIndex++] = i0;
                    triangles[triIndex++] = i1;
                    triangles[triIndex++] = i2;

                    // 三角面 2
                    triangles[triIndex++] = i1;
                    triangles[triIndex++] = i3;
                    triangles[triIndex++] = i2;
                }
            }

            // 3. 将数据写入 Mesh 并优化
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();

            // 4. 将网格应用到 MeshFilter，并确保 MeshCollider 存在以防射线穿透
            GetComponent<MeshFilter>().mesh = mesh;

            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            // 5. 设置材质双面渲染以保证可见度 (莫比乌斯环为单侧面结构，必须关闭背面裁剪)
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (material != null)
            {
                mr.sharedMaterial = material;
            }
                // 如果当前没有材质，或者使用的是只读的默认材质，则创建我们专属的双面材质
                if (mr.sharedMaterial == null || 
                    mr.sharedMaterial.name.StartsWith("Default") || 
                    mr.sharedMaterial.name == "Lit" ||
                    mr.sharedMaterial.name != "Mobius_DoubleSided_Material")
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader == null) urpShader = Shader.Find("Standard");
                    
                    Material newMat = new Material(urpShader);
                    newMat.name = "Mobius_DoubleSided_Material";
                    newMat.color = new Color(0.85f, 0.85f, 0.85f);

                    // 彻底关闭背面裁剪 (URP / Built-in 通用双面渲染设置)
                    newMat.SetFloat("_Cull", 0f);           // 0 = Off (Double-Sided)
                    newMat.SetFloat("_RenderFace", 2f);     // 2 = Both (URP 面渲染模式)
                    newMat.SetInt("_DoubleSidedEnable", 1);
                    newMat.EnableKeyword("_DOUBLE_SIDED_ON");

                    mr.sharedMaterial = newMat;
                    Debug.Log("[MobiusStrip] 成功自动装配了双面渲染 (Double-Sided) 材质！");
                }
        }

        /// <summary>
        /// 莫比乌斯环空间参数方程静态数学公式
        /// R: 圆环半径, u: 圆环弧度 (0..2π), v: 宽度偏移量 (-width/2 .. width/2)
        /// </summary>
        public static Vector3 GetMobiusPoint(float u, float v, float R)
        {
            // r = R + v * cos(u / 2)
            // 在 u 绕行一圈 2π 时，v 的朝向反转 180 度，产生扭转
            float r = R + v * Mathf.Cos(u * 0.5f);
            
            float x = r * Mathf.Cos(u);
            float y = v * Mathf.Sin(u * 0.5f); // y 轴方向高度变化产生扭动
            float z = r * Mathf.Sin(u);

            return new Vector3(x, y, z);
        }
    }
}
