using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 6 终极魔幻复杂 3D 矩阵构建器 (Stage 6 Infinite 3D Matrix & Slice Builder)
    /// 1. 100% 还原艺术参考图 1 (3D 无限悬浮方块阵列通道) 与 参考图 2 (360度极速爆裂切片)。
    /// 2. 挂载在 Stage 6 场景即可一键自动生成复杂的魔幻多维空间！
    /// 3. 将过往关卡模型与 Stage 5 视频流融合交织在无限阵列中。
    /// </summary>
    public class Stage6ComplexMatrixBuilder : MonoBehaviour
    {
        [Header("视频源配置 (Inspector 拖拽)")]
        [Tooltip("Stage 5 视频列表 (若留空，自动载入项目中的默认视频)")]
        public VideoClip[] stage5VideoClips;

        [Header("3D 矩阵通道配置 (参考图 1)")]
        [Tooltip("矩阵通道在 X/Y 方向上的网格数量 (比如 6x6)")]
        public Vector2Int gridCount = new Vector2Int(7, 7);

        [Tooltip("矩阵通道沿 Z 轴深度的层数")]
        public int depthLayers = 14;

        [Tooltip("网格方块之间的间距")]
        public Vector3 gridSpacing = new Vector3(2.2f, 2.2f, 3.5f);

        [Tooltip("3D 矩阵波浪起伏速度")]
        public float waveSpeed = 1.8f;

        [Tooltip("3D 矩阵波浪起伏幅度")]
        public float waveAmplitude = 0.45f;

        [Header("核心旋切与爆裂 (参考图 2)")]
        [Tooltip("中央魔幻核心方块旋转速度")]
        public Vector3 centerRotationSpeed = new Vector3(15f, 25f, 10f);

        private List<Transform> _matrixCubes = new List<Transform>();
        private List<Vector3> _initialPositions = new List<Vector3>();
        private Transform _centerCore;

        private RenderTexture _matrixVideoRenderTex;
        private VideoPlayer _matrixVideoPlayer;

        private void Start()
        {
            SetupVideoMatrixPlayer();
            BuildMagicalMatrixField();
        }

        private void Update()
        {
            float time = Time.time;

            // 1. 参考图 1：驱动 3D 矩阵网格波浪浮动与波动 (3D Matrix Wave Motion)
            for (int i = 0; i < _matrixCubes.Count; i++)
            {
                if (_matrixCubes[i] != null)
                {
                    Vector3 initPos = _initialPositions[i];
                    float waveX = Mathf.Sin(time * waveSpeed + initPos.z * 0.5f) * waveAmplitude;
                    float waveY = Mathf.Cos(time * waveSpeed * 0.8f + initPos.x * 0.8f) * waveAmplitude;
                    float waveZ = Mathf.Sin(time * waveSpeed * 1.2f + initPos.y * 0.6f) * 0.3f;

                    _matrixCubes[i].localPosition = initPos + new Vector3(waveX, waveY, waveZ);

                    // 微弱 3D 呼吸旋转
                    float rotAngle = Mathf.Sin(time * 1.5f + i) * 8f;
                    _matrixCubes[i].localRotation = Quaternion.Euler(rotAngle, rotAngle * 0.5f, rotAngle * 0.3f);
                }
            }

            // 2. 参考图 2：驱动中央魔幻核心的极速旋转与爆裂 (Center Core Rotation)
            if (_centerCore != null)
            {
                _centerCore.Rotate(centerRotationSpeed * Time.deltaTime, Space.Self);
                float pulse = 1.0f + Mathf.Sin(time * 3f) * 0.12f;
                _centerCore.localScale = Vector3.one * pulse * 2.5f;
            }

            // 3. 动态轮换视频 Clip (每隔一段时间随机切到下一个视频 Clip)
            if (time > _nextVideoSwapTime)
            {
                _nextVideoSwapTime = time + Random.Range(1.2f, 3.5f);
                SwapMatrixVideoClip();
            }
        }

        private float _nextVideoSwapTime = 0f;

        private void SwapMatrixVideoClip()
        {
            if (_matrixVideoPlayer == null || stage5VideoClips == null || stage5VideoClips.Length == 0) return;
            VideoClip nextClip = stage5VideoClips[Random.Range(0, stage5VideoClips.Length)];
            if (nextClip != null && _matrixVideoPlayer.clip != nextClip)
            {
                _matrixVideoPlayer.clip = nextClip;
                _matrixVideoPlayer.Play();
            }
        }

        /// <summary>
        /// 自动生成参考图 1 风格的 3D 无限魔幻矩阵空间
        /// </summary>
        private void BuildMagicalMatrixField()
        {
            GameObject matrixHolder = new GameObject("Stage6_3D_Magical_Matrix_Field");
            matrixHolder.transform.SetParent(transform, false);

            Shader glitchShader = Shader.Find("Wakeup/Stage6VideoGlitchShader");
            if (glitchShader == null) glitchShader = Shader.Find("Universal Render Pipeline/Unlit");

            // 构建双重材质：一个映射彩虹视频矩阵，一个映射过往关卡纹理
            Material matrixMat = new Material(glitchShader);
            matrixMat.SetTexture("_VideoTex", _matrixVideoRenderTex);
            matrixMat.SetFloat("_GlitchBlend", 0.65f);
            matrixMat.SetFloat("_GlitchIntensity", 0.45f);

            int halfX = gridCount.x / 2;
            int halfY = gridCount.y / 2;

            // 1. 生成 3D 方块矩阵通道 (Tunnel Field - 参考图 1)
            for (int z = 0; z < depthLayers; z++)
            {
                for (int x = -halfX; x <= halfX; x++)
                {
                    for (int y = -halfY; y <= halfY; y++)
                    {
                        // 留出中央中空通道供玩家游览
                        if (Mathf.Abs(x) <= 1 && Mathf.Abs(y) <= 1) continue;

                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = $"Matrix_Cube_{x}_{y}_{z}";
                        cube.transform.SetParent(matrixHolder.transform, false);

                        Vector3 pos = new Vector3(
                            x * gridSpacing.x,
                            y * gridSpacing.y,
                            z * gridSpacing.z
                        );

                        cube.transform.localPosition = pos;
                        cube.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                        cube.GetComponent<Renderer>().material = matrixMat;

                        Destroy(cube.GetComponent<Collider>()); // 优化性能

                        _matrixCubes.Add(cube.transform);
                        _initialPositions.Add(pos);
                    }
                }
            }

            // 2. 生成参考图 2 风格的中央爆裂核心 (Center Slice Core - 参考图 2)
            _centerCore = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            _centerCore.name = "Center_Explosive_Core_Pic2";
            _centerCore.SetParent(matrixHolder.transform, false);
            _centerCore.localPosition = new Vector3(0f, 0f, depthLayers * gridSpacing.z * 0.5f);
            _centerCore.localScale = Vector3.one * 2.5f;

            Material coreMat = new Material(glitchShader);
            coreMat.SetTexture("_VideoTex", _matrixVideoRenderTex);
            coreMat.SetFloat("_GlitchBlend", 0.85f);
            coreMat.SetFloat("_GlitchIntensity", 0.8f);
            _centerCore.GetComponent<Renderer>().material = coreMat;

            Destroy(_centerCore.GetComponent<Collider>());

            Debug.Log($"<color=green>[Stage 6 Builder] 成功自动生成了包含 {_matrixCubes.Count} 个魔幻 3D 方块的图 1 & 图 2 风格矩阵空间！</color>");
        }

        private void SetupVideoMatrixPlayer()
        {
            GameObject vpGo = new GameObject("Stage6_Builder_VideoPlayer");
            vpGo.transform.SetParent(transform, false);
            _matrixVideoPlayer = vpGo.AddComponent<VideoPlayer>();
            _matrixVideoPlayer.playOnAwake = true;
            _matrixVideoPlayer.isLooping = true;
            _matrixVideoPlayer.renderMode = VideoRenderMode.RenderTexture;

            _matrixVideoRenderTex = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _matrixVideoRenderTex.Create();
            _matrixVideoPlayer.targetTexture = _matrixVideoRenderTex;

            if (stage5VideoClips != null && stage5VideoClips.Length > 0)
            {
                _matrixVideoPlayer.clip = stage5VideoClips[Random.Range(0, stage5VideoClips.Length)];
            }
            else
            {
#if UNITY_EDITOR
                VideoClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/For_You/Art/Stage5_Contemporary/Videos/video1.mp4");
                if (clip != null) _matrixVideoPlayer.clip = clip;
#endif
            }

            _matrixVideoPlayer.Play();
        }
    }
}
