using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Phase 1~4 全局色彩基调后处理控制器 (Global Color Grading per Phase)
    ///
    /// Phase1: 干净明快、原始色彩不加任何滤镜
    /// Phase2: 逐渐褪色发闷，像蒙了一层灰
    /// Phase3: 色彩过饱和溢出，霓虹感爆棚
    /// Phase4: 接近黑白/极暗基调 + 高频 glitch 色偏噪点，光污染感
    ///
    /// 用 URP Global Volume + ColorAdjustments/ChromaticAberration/FilmGrain Override 实现，
    /// 对屏幕上所有内容（墙面、视频内容、正面墙、Phase4 献祭 3D 物体）统一生效，
    /// 不需要逐个修改 Shader。完全独立运作：只读取 Stage5Controller.phaseProgress，不依赖/不修改任何其他脚本的内部状态。
    /// </summary>
    public class Stage5PostProcessingController : MonoBehaviour
    {
        [Header("Phase 色彩基调曲线 (Data-Driven，可在 Inspector 里自由重新设计)")]
        [Tooltip("饱和度 (-100~100)：Phase1 保持 0 不加滤镜，Phase2 转轻度负值发闷褪色，Phase3 顶格饱和霓虹溢出，Phase4 转极大负值接近黑白。注意：Phase2 终值不要设得太深，否则 Phase3 前半段会因为从较低起点一路往上爬而显得拖沓不够夸张")]
        public PhaseCurveParam saturationCurve = new PhaseCurveParam { phase1 = 0f, phase2 = -15f, phase3 = 100f, phase4 = -100f };

        [Tooltip("曝光补偿 (EV)：Phase2 略微压暗发闷，Phase3 略微提亮霓虹，Phase4 大幅压暗营造极暗基调")]
        public PhaseCurveParam postExposureCurve = new PhaseCurveParam { phase1 = 0f, phase2 = -0.1f, phase3 = 0.35f, phase4 = -0.6f };

        [Tooltip("对比度 (-100~100)")]
        public PhaseCurveParam contrastCurve = new PhaseCurveParam { phase1 = 0f, phase2 = -8f, phase3 = 35f, phase4 = 35f };

        [Header("Phase4 光污染 / Glitch 噪点曲线")]
        [Tooltip("色差/色偏强度 (0~1)：Phase3 起就开始明显，Phase4 大幅拉高，制造屏幕边缘色散光污染感")]
        public PhaseCurveParam chromaticAberrationCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 0.05f, phase3 = 0.3f, phase4 = 0.6f };

        [Tooltip("胶片颗粒噪点强度 (0~1)：Phase3 起就开始明显，Phase4 大幅拉高，制造粗糙故障噪点感")]
        public PhaseCurveParam filmGrainCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 0.1f, phase3 = 0.35f, phase4 = 0.8f };

        [Header("Phase4 双模态爆闪节奏 (与走廊形变系统的 chaosPulse 概念一致，独立实现避免跨脚本耦合)")]
        public float phase4HypnoticFrequency = 0.8f;
        public float phase4BurstInterval = 5f;
        public float phase4BurstDuration = 0.35f;
        [Range(1f, 3f)] public float phase4BurstIntensity = 1.6f;

        private Volume _volume;
        private ColorAdjustments _colorAdjustments;
        private ChromaticAberration _chromaticAberration;
        private FilmGrain _filmGrain;

        private void Start()
        {
            GameObject volGo = new GameObject("Stage5_GlobalColorVolume");
            volGo.transform.SetParent(transform, false);

            _volume = volGo.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = profile;

            _colorAdjustments = profile.Add<ColorAdjustments>(true);
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.contrast.overrideState = true;

            _chromaticAberration = profile.Add<ChromaticAberration>(true);
            _chromaticAberration.intensity.overrideState = true;

            _filmGrain = profile.Add<FilmGrain>(true);
            _filmGrain.intensity.overrideState = true;
            _filmGrain.type.overrideState = true;
            _filmGrain.type.value = FilmGrainLookup.Thin1;

            // 确保摄像机真的启用了后处理渲染，否则上面这层 Volume 不会显示任何效果
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var camData = mainCam.GetUniversalAdditionalCameraData();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                }
            }

            Debug.Log("<color=cyan>[Stage5PostProcessingController] 全局色彩基调后处理系统已创建。</color>");
        }

        private void Update()
        {
            if (_colorAdjustments == null) return;

            float phaseProgress = Stage5Controller.Instance != null ? Stage5Controller.Instance.phaseProgress : 0f;
            float time = Time.time;

            // Phase4 双模态节奏：大部分时间是缓慢催眠脉冲，偶发剧烈爆闪
            float chaosPulse = 1f;
            if (phaseProgress >= 0.96f)
            {
                float hypnotic = 0.6f + 0.4f * Mathf.Sin(time * phase4HypnoticFrequency);
                bool inBurst = phase4BurstInterval > 0.01f && (time % phase4BurstInterval) < phase4BurstDuration;
                chaosPulse = hypnotic * (inBurst ? phase4BurstIntensity : 1f);
            }

            _colorAdjustments.saturation.value = saturationCurve.Evaluate(phaseProgress);
            _colorAdjustments.postExposure.value = postExposureCurve.Evaluate(phaseProgress);
            _colorAdjustments.contrast.value = contrastCurve.Evaluate(phaseProgress);

            _chromaticAberration.intensity.value = Mathf.Clamp01(chromaticAberrationCurve.Evaluate(phaseProgress) * chaosPulse);
            _filmGrain.intensity.value = Mathf.Clamp01(filmGrainCurve.Evaluate(phaseProgress) * chaosPulse);

            // 将当前视觉混沌强度发布到总线上，供音频等其他系统读取同步，无需直接引用本脚本：
            // 饱和度偏离 0 越多说明画面越“炸”——不管是过饱和还是接近黑白，都算偏离干净原始状态。
            float chaosIntensity = Mathf.Clamp01(Mathf.Abs(_colorAdjustments.saturation.value) / 100f);
            Stage5AnnounceBus.PublishVisualChaosIntensity(chaosIntensity);
        }

        private void OnDestroy()
        {
            if (_volume != null && _volume.profile != null)
            {
                Destroy(_volume.profile);
            }
        }
    }
}
