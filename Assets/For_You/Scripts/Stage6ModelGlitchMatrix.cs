using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 6 终极记忆重聚与视频矩阵控制器 (Stage 6 Model Glitch Matrix Controller)
    /// 1. 自动挂载在场景中放置以前关卡模型的 GameObject 上。
    /// 2. 自动将其材质转换为 Universal Stage 6 Glitch Material。
    /// 3. 在“正常历史材质 (Normal Texture)”与“Stage 5 算法视频流矩阵 (Video Matrix)”之间，
    ///    进行充满叙事浪漫的动态随机脉冲闪烁、水波蔓延与像素 Glitch！
    /// </summary>
    public class Stage6ModelGlitchMatrix : MonoBehaviour
    {
        [Header("视频源配置")]
        [Tooltip("Stage 5 视频列表 (若留空，自动载入项目中的默认视频)")]
        public VideoClip[] stage5VideoClips;

        [Header("闪烁风格控制")]
        [Tooltip("闪烁发生的频率间隔 (秒)")]
        public Vector2 flickerIntervalRange = new Vector2(0.5f, 2.5f);

        [Tooltip("单次视频矩阵暴露闪烁的持续时间 (秒)")]
        public Vector2 flickerDurationRange = new Vector2(0.12f, 0.8f);

        [Header("高清投影配置")]
        [Tooltip("是否允许部分模型在闪烁时呈现高清清晰视频/记忆图像投影 (随时取消勾选撤回)")]
        public bool enableCrispProjection = true;

        [Tooltip("高清清晰投影触发概率 (0~1)")]
        [Range(0f, 1f)] public float crispChance = 0.25f;

        [Tooltip("正常状态下视频矩阵的常驻渗透比例 (0 表示平时完全正常，1 表示完全变成视频流)")]
        [Range(0f, 1f)] public float ambientVideoBlend = 0.15f;

        private List<Renderer> _targetRenderers = new List<Renderer>();
        private List<Material> _glitchMaterials = new List<Material>();
        private VideoPlayer _videoPlayer;
        private RenderTexture _matrixRenderTex;

        private void Start()
        {
            SetupVideoMatrixPlayer();
            SetupModelMaterials();
            StartCoroutine(FlickerRoutine());
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < _glitchMaterials.Count; i++)
            {
                if (_glitchMaterials[i] != null)
                {
                    // 环境轻微水波浮动
                    float wave = (Mathf.Sin(time * 2f + i) * 0.5f + 0.5f) * ambientVideoBlend;
                    if (_glitchMaterials[i].HasProperty("_GlitchBlend") && !_glitchMaterials[i].GetFloat("_GlitchBlend").Equals(1f))
                    {
                        _glitchMaterials[i].SetFloat("_GlitchBlend", Mathf.Max(ambientVideoBlend, wave * 0.35f));
                    }
                }
            }
        }

        /// <summary>
        /// 协程：控制模型在“正常材质”与“Stage 5 视频矩阵”之间随机脉冲闪烁，且每次闪烁自动随机切换下一个视频！
        /// </summary>
        private IEnumerator FlickerRoutine()
        {
            while (true)
            {
                // 等待随机间隔
                float waitTime = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
                yield return new WaitForSeconds(waitTime);

                // 🌟 核心升级：每次闪烁高潮瞬间，随机切换到列表里的下一个视频 Clip！
                SwitchToRandomVideoClip();

                // 🌟 核心升级：每次闪烁时，为每个模型单独随机分配故障风格 (视频矩阵 / 湍流拉伸 / 组合爆发)
                float flickerDur = Random.Range(flickerDurationRange.x, flickerDurationRange.y);

                for (int i = 0; i < _glitchMaterials.Count; i++)
                {
                    if (_glitchMaterials[i] != null && Random.value > 0.15f)
                    {
                        // 🌟 随机分配 4 种视效模式：
                        // 0: 纯 Material 纹理投影 Glitch
                        // 1: 高清视频投影 Mode
                        // 2: 湍流连成一片 Mode
                        // 3: 彩虹视频矩阵 Mode
                        int glitchMode = Random.Range(0, 4);

                        _glitchMaterials[i].SetFloat("_GlitchBlend", 1.0f);
                        _glitchMaterials[i].SetFloat("_PureMatGlitch", 0f);
                        _glitchMaterials[i].SetFloat("_CrispProjection", 0f);
                        _glitchMaterials[i].SetFloat("_GlitchIntensity", 0f);
                        _glitchMaterials[i].SetFloat("_SmearTurbulence", 0f);

                        if (glitchMode == 0) // 纯 Material 纹理投影
                        {
                            _glitchMaterials[i].SetFloat("_PureMatGlitch", 1.0f);
                        }
                        else if (glitchMode == 1 && enableCrispProjection) // 高清视频投影
                        {
                            _glitchMaterials[i].SetFloat("_CrispProjection", 1.0f);
                        }
                        else if (glitchMode == 2) // 湍流连成一片
                        {
                            _glitchMaterials[i].SetFloat("_SmearTurbulence", Random.Range(2.0f, 4.2f));
                        }
                        else // 彩虹视频矩阵
                        {
                            _glitchMaterials[i].SetFloat("_GlitchIntensity", Random.Range(0.4f, 0.95f));
                        }
                    }
                }

                yield return new WaitForSeconds(flickerDur);

                // 恢复平缓正常状态
                for (int i = 0; i < _glitchMaterials.Count; i++)
                {
                    if (_glitchMaterials[i] != null)
                    {
                        _glitchMaterials[i].SetFloat("_GlitchBlend", ambientVideoBlend);
                        _glitchMaterials[i].SetFloat("_GlitchIntensity", 0.05f);
                        _glitchMaterials[i].SetFloat("_SmearTurbulence", 0f);
                        _glitchMaterials[i].SetFloat("_CrispProjection", 0f);
                        _glitchMaterials[i].SetFloat("_PureMatGlitch", 0f);
                    }
                }
            }
        }

        /// <summary>
        /// 随机切换播放不同的视频 Clip (防止单一视频 Loop 显得死板)
        /// </summary>
        private void SwitchToRandomVideoClip()
        {
            if (_videoPlayer == null) return;

            VideoClip nextClip = null;
            if (stage5VideoClips != null && stage5VideoClips.Length > 0)
            {
                int nextIndex = Random.Range(0, stage5VideoClips.Length);
                nextClip = stage5VideoClips[nextIndex];
            }

            if (nextClip != null && _videoPlayer.clip != nextClip)
            {
                _videoPlayer.clip = nextClip;
                _videoPlayer.Play();
            }
        }

        /// <summary>
        /// 初始化后台 Stage 5 视频流 Player
        /// </summary>
        private void SetupVideoMatrixPlayer()
        {
            GameObject vpGo = new GameObject("Stage6_Matrix_VideoPlayer");
            vpGo.transform.SetParent(transform, false);
            _videoPlayer = vpGo.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = true;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            _matrixRenderTex = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _matrixRenderTex.Create();
            _videoPlayer.targetTexture = _matrixRenderTex;

            // 自动加载默认 Stage 5 视频文件
            if (stage5VideoClips != null && stage5VideoClips.Length > 0)
            {
                _videoPlayer.clip = stage5VideoClips[Random.Range(0, stage5VideoClips.Length)];
            }
            else
            {
#if UNITY_EDITOR
                VideoClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/For_You/Art/Stage5_Contemporary/Videos/video1.mp4");
                if (clip != null) _videoPlayer.clip = clip;
#endif
            }

            _videoPlayer.Play();
        }

        /// <summary>
        /// 自动获取子物体下的所有 Renderer 并将其转换为 Stage 6 Glitch 材质
        /// </summary>
        private void SetupModelMaterials()
        {
            Shader glitchShader = Shader.Find("Wakeup/Stage6VideoGlitchShader");
            if (glitchShader == null) glitchShader = Shader.Find("Universal Render Pipeline/Unlit");

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                _targetRenderers.Add(r);

                // 获取原本的 Main Texture
                Texture originalTex = Texture2D.whiteTexture;
                if (r.sharedMaterial != null)
                {
                    if (r.sharedMaterial.HasProperty("_MainTex") && r.sharedMaterial.GetTexture("_MainTex") != null)
                        originalTex = r.sharedMaterial.GetTexture("_MainTex");
                    else if (r.sharedMaterial.HasProperty("_BaseMap") && r.sharedMaterial.GetTexture("_BaseMap") != null)
                        originalTex = r.sharedMaterial.GetTexture("_BaseMap");
                }

                // 创建独立的 Stage 6 材质
                Material newMat = new Material(glitchShader);
                newMat.SetTexture("_MainTex", originalTex);
                newMat.SetTexture("_VideoTex", _matrixRenderTex);
                newMat.SetFloat("_GlitchBlend", ambientVideoBlend);
                newMat.SetFloat("_GlitchIntensity", 0.05f);

                r.material = newMat;
                _glitchMaterials.Add(newMat);
            }

            Debug.Log($"<color=green>[Stage 6 Matrix] 成功自动为 {renderers.Length} 个模型装载了 Stage 5 动态视频矩阵材质！</color>");
        }
    }
}
