using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 总控制器 — "Feed / 内容流"
    /// 
    /// 单系统 + 四阶段演化 + 单曲底层 BGM 实时 DSP 音频滤镜扭曲系统：
    /// Phase 1 (0.00~0.35): 多彩的享受 — 干净甜美、无滤镜（Distortion = 0, High Cutoff = 22000Hz, Pitch = 1.0）
    /// Phase 2 (0.35~0.70): 不自觉的强迫成瘾 — 逐渐调高 Distortion、音速变低变闷，声音开始杂乱扭曲
    /// Phase 3 (0.70~0.96): 数据显形 — Distortion 拉满 + Low Pass 压暗 + 变调，声音变得可怕、闷、不祥；注视私密数据时彻底抽走音量
    /// Phase 4 (0.96~1.00): 抉择时刻 — 暂停卡片，清爽呈现
    /// </summary>
    public class Stage5Controller : MonoBehaviour
    {
        public static Stage5Controller Instance { get; private set; }

        // Stage5Controller 不再使用 Auto-Bootstrap。
        // 请手动将此脚本挂载到 5_Contemporary 场景中的一个 GameObject 上。

        [Header("Phase 动态进度 (0.0 -> 1.0 纯由交互与素材内容驱动)")]
        [Tooltip("当前阶段进度 0→1，Inspector 中可观测")]
        [Range(0f, 1f)] public float phaseProgress = 0f;

        [Header("单曲 BGM + 实时 DSP 滤镜 + 嘈杂人声图层")]
        [Tooltip("贯穿全程的单曲 BGM 音轨（留空将自动加载备用音轨）")]
        public AudioClip singleBgmClip;

        [Tooltip("BGM 全局基础音量")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.55f;

        [Tooltip("Phase 2/3 随卡片增多逐渐叠加的嘈杂人声/环境噪音 Audio Clip")]
        public AudioClip crowdNoiseClip;

        [Tooltip("嘈杂人声图层最大音量")]
        [Range(0f, 1f)]
        public float crowdNoiseVolume = 0.5f;

        [Header("BGM spectral richness (so the LowPass filter has real high-frequency content to remove)")]
        [Tooltip("Amount of high-frequency shimmer/noise texture layered on top of the chord tones (0 = pure clean chord, 1 = strong airy high-frequency texture). Needed because pure low-frequency sine tones give LowPass nothing audible to filter out.")]
        [Range(0f, 1f)] public float bgmHighFreqTextureAmount = 0.35f;

        [Header("Crowd noise procedural fallback (used only when crowdNoiseClip is left empty)")]
        [Tooltip("Duration in seconds of the procedurally generated seamless-loop crowd murmur texture")]
        public float crowdNoiseProceduralDuration = 4f;

        [Tooltip("Density of the procedural crowd murmur layer (0 = sparse, 1 = dense overlapping voices)")]
        [Range(0f, 1f)] public float crowdNoiseProceduralDensity = 0.5f;

        [Header("卡片注视与交互音效（香蕉/Pop 愉悦反馈）")]
        [Tooltip("注视看卡片时的愉悦反馈音效（留空将自动程序化生成 80ms Sine 叮音）")]
        public AudioClip gazePopClip;

        [Tooltip("注视音效基础音量")]
        [Range(0f, 1f)]
        public float gazePopVolume = 0.6f;

        [Header("声音控制")]
        [Tooltip("Phase C 私密数据出现时，全局音量降到此值")]
        public float silenceVolume = 0.05f;

        [Header("实验模式：Brandon Eversole 流体穿梭模式")]
        [Tooltip("勾选后将开启 Brandon Eversole 风格的全屏流体无限穿梭模式")]
        public bool useFluidTunnelMode = false;

        [Header("媒体数据库（图片/纹理）")]
        [Tooltip("将 Assets/For_You 下的 CardMediaDatabase.asset 拖入此槽。留空则卡片以纯色模式运行。")]
        public CardMediaDatabase mediaDatabase;

        [Header("转场")]
        public float transitionDelay = 2f;
        public string nextSceneName = "6_Future";

        [Header("🔧 Stage 5 测试调试接口 (Debug Overrides)")]
        [Tooltip("勾选后将开启调试模式，使用下方手动填写的 4 个 Stage 交互数值")]
        public bool overrideBehaviorData = false;

        [Tooltip("【测试用】Stage 1 香蕉交互次数")]
        public int debugBananaCount = 12;

        [Tooltip("【测试用】Stage 2 祈祷/眼皮交互次数")]
        public int debugPrayerCount = 5;

        [Tooltip("【测试用】Stage 3 莫比乌斯推石次数")]
        public int debugPushCount = 8;

        [Tooltip("【测试用】Stage 4 办公室打字/字模交互次数")]
        public int debugWorkCount = 42;

        // 子系统引用（现役：CustomCorridorBinder）

        // 音频与 DSP 滤镜组件
        private AudioSource _bgmAudioSource;
        private AudioDistortionFilter _distortionFilter;
        private AudioLowPassFilter _lowPassFilter;
        private AudioChorusFilter _chorusFilter;
        private AudioReverbFilter _reverbFilter;
        private AudioSource _crowdAudioSource;
        private AudioSource _sfxAudioSource;

        private int _gazeComboCount = 0;
        private float _lastGazeTime = 0f;

        // 结尾选择
        private bool _endChoiceSpawned = false;
        private bool _isTransitioning = false;

        // 音量与 HUD
        private float _originalVolume = 1f;
        private float _targetVolume = 1f;
        private TMPro.TextMeshProUGUI _phaseStatusText;

                private void OnEnable()
        {
            Stage5AnnounceBus.OnPhaseEntered += HandlePhaseEntered;
        }

        private void OnDisable()
        {
            Stage5AnnounceBus.OnPhaseEntered -= HandlePhaseEntered;
        }

        // Called once whenever a new phase boundary is crossed (broadcast via Stage5AnnounceBus).
        // Triggers a brief one-shot audio distortion pulse marking the transition moment.
        private void HandlePhaseEntered(int phaseIndex)
        {
            _phaseTransitionPulseTimer = 1f;
            Debug.Log("[Stage5 AnnounceBus] Audio system received phase transition broadcast: entering Phase " + phaseIndex);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 0. 关掉之前游戏关卡残留的旁白对话系统 NarratorManager
            if (NarratorManager.Instance != null)
            {
                NarratorManager.Instance.StopCurrent();
                NarratorManager.Instance.gameObject.SetActive(false);
                Debug.Log("[Stage5] 已关闭之前的旁白对话系统 (NarratorManager)。");
            }

            // 确保场景原有的 UI Canvas 保持显示
            EnsureOldCanvasesVisible();

            // 1. 压暗环境
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            Camera mainCam = GetMainCamera();
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = Color.black;
            }

            // 2. 设置玩家视角只可旋转、移速归零
            SetupPlayerFloat();

            // 3. 创建注视准心与 Phase 阶段状态提示 HUD
            CreateHUDUI();

            // 4. 创建子系统
            CreateSubsystems();

            // 4b. 自动挂载 Phase4 献祭物体召唤系统 (Stage5ChaosOfferingSpawner)
            EnsureChaosOfferingSpawner();

            // 4c. 自动挂载全局色彩基调后处理系统 (Stage5PostProcessingController)
            EnsurePostProcessingController();

            // 5. 初始化单曲 + DSP 动态滤镜音频系统
            SetupAudioSystem();

            // 6. 保存原始音量
            _originalVolume = AudioListener.volume;

            Debug.Log("[Stage5] Feed 系统已初始化。单曲 DSP 动态滤镜扭曲系统已就绪。");
        }

        private void SetupAudioSystem()
        {
            // 创建 BGM AudioSource
            GameObject bgmGo = new GameObject("BGM_AudioEngine");
            bgmGo.transform.SetParent(transform, false);

            _bgmAudioSource = bgmGo.AddComponent<AudioSource>();
            _bgmAudioSource.loop = true;
            _bgmAudioSource.volume = bgmVolume;
            _bgmAudioSource.spatialBlend = 0f;

            // 挂载 DSP 实时音频滤镜组件（失真、低通、合唱抖动、大空间混响）
            _distortionFilter = bgmGo.AddComponent<AudioDistortionFilter>();
            _distortionFilter.distortionLevel = 0.0f; // 初始无失真

            _lowPassFilter = bgmGo.AddComponent<AudioLowPassFilter>();
            _lowPassFilter.cutoffFrequency = 22000f; // 初始全频段高频通畅

            _chorusFilter = bgmGo.AddComponent<AudioChorusFilter>();
            _chorusFilter.depth = 0.0f; // 初始无合唱/相位音高抖动

            _reverbFilter = bgmGo.AddComponent<AudioReverbFilter>();
            _reverbFilter.reverbPreset = AudioReverbPreset.Off; // 初始无混响

            // 创建 嘈杂人声图层 AudioSource (Phase 2/3 渐入)
            GameObject crowdGo = new GameObject("CrowdNoise_AudioEngine");
            crowdGo.transform.SetParent(transform, false);

            _crowdAudioSource = crowdGo.AddComponent<AudioSource>();
            _crowdAudioSource.loop = true;
            _crowdAudioSource.volume = 0f; // 初始静音
            _crowdAudioSource.spatialBlend = 0f;

            // If no crowd noise clip was manually assigned, generate a procedural fallback
            // (same pattern as the BGM and pop-click fallbacks below), so this layer is never silent.
            if (crowdNoiseClip == null)
            {
                crowdNoiseClip = CreateProceduralCrowdNoiseClip();
            }

            if (crowdNoiseClip != null)
            {
                _crowdAudioSource.clip = crowdNoiseClip;
                _crowdAudioSource.loop = true;
                _crowdAudioSource.Play();
                Debug.Log("[Stage5] Crowd noise layer mounted: " + crowdNoiseClip.name);
            }

            // 创建 SFX AudioSource
            _sfxAudioSource = gameObject.AddComponent<AudioSource>();
            _sfxAudioSource.loop = false;
            _sfxAudioSource.volume = gazePopVolume;
            _sfxAudioSource.spatialBlend = 0f;

            // 如果没有指定注视音效，全自动算法生成精美清爽的 80ms 叮音
            if (gazePopClip == null)
            {
                gazePopClip = CreateProceduralPopClip();
            }

            // 如果没有手拖 MP3 文件，算法自动程序化生成 6 秒无缝循环的温润 Ambient Synth 和声 BGM Track
            if (singleBgmClip == null)
            {
                singleBgmClip = CreateProceduralAmbientBgmClip();
            }

            if (singleBgmClip != null)
            {
                _bgmAudioSource.clip = singleBgmClip;
                _bgmAudioSource.Play();
                Debug.Log($"[Stage5] 单曲 BGM 启动播放: {singleBgmClip.name}");
            }
        }

        private AudioClip CreateProceduralAmbientBgmClip()
        {
            int sampleRate = 44100;
            float duration = 6.0f; // 6-second seamless-loop warm ambient chord track
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float[] freqs = { 110f, 164.81f, 220f, 277.18f, 329.63f, 440f }; // A Minor 9 / Ambient Chord

            // Additional high-frequency overtone series and noise texture, so the LowPass DSP filter used across
            // Phase 2/3/4 actually has real high-frequency energy to remove -- pure sub-440Hz sine tones give it
            // nothing audible to filter, which is why the phase transitions used to sound almost identical.
            float[] shimmerFreqs = { 880f, 1320f, 1760f, 2640f, 3520f };

            System.Random rng = new System.Random(1234);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float sampleVal = 0f;

                // Slow LFO wandering envelope
                float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.166f * t);

                for (int f = 0; f < freqs.Length; f++)
                {
                    float freq = freqs[f];
                    float amp = 1f / (f + 1); // higher partials fade out
                    sampleVal += Mathf.Sin(2f * Mathf.PI * freq * t) * amp;
                }

                if (bgmHighFreqTextureAmount > 0.001f)
                {
                    float shimmer = 0f;
                    for (int f = 0; f < shimmerFreqs.Length; f++)
                    {
                        float freq = shimmerFreqs[f];
                        float amp = 0.5f / (f + 1);
                        float slowMod = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (0.05f + f * 0.02f) * t);
                        shimmer += Mathf.Sin(2f * Mathf.PI * freq * t) * amp * slowMod;
                    }
                    float noise = ((float)rng.NextDouble() * 2f - 1f);
                    sampleVal += (shimmer * 0.5f + noise * 0.15f) * bgmHighFreqTextureAmount;
                }

                // Seamless fade in/out at the loop edges to avoid crossfade pops
                float fadeEnv = 1f;
                float fadeLen = 0.1f;
                if (t < fadeLen) fadeEnv = t / fadeLen;
                else if (t > duration - fadeLen) fadeEnv = (duration - t) / fadeLen;

                samples[i] = sampleVal * 0.12f * lfo * fadeEnv;
            }

            AudioClip clip = AudioClip.Create("ProceduralAmbientBGM", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralPopClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(880f, 1320f, t / duration);
                float envelope = Mathf.Sin((1f - t / duration) * Mathf.PI * 0.5f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ProceduralPop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

                private AudioClip CreateProceduralCrowdNoiseClip()
        {
            int sampleRate = 44100;
            float duration = Mathf.Max(1f, crowdNoiseProceduralDuration);
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // Layered murmuring texture: several low-passed noise voices at slightly different
            // pitches/phases, approximating distant indistinct crowd chatter. Density is tunable
            // via crowdNoiseProceduralDensity so this fallback can be dialed from sparse to dense
            // without touching code.
            System.Random rng = new System.Random(5678);
            int voiceCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, crowdNoiseProceduralDensity)));
            float[] voiceFreqs = new float[voiceCount];
            float[] voicePhaseSpeeds = new float[voiceCount];
            for (int v = 0; v < voiceCount; v++)
            {
                voiceFreqs[v] = 120f + (float)rng.NextDouble() * 260f;
                voicePhaseSpeeds[v] = 0.3f + (float)rng.NextDouble() * 0.6f;
            }

            float prevNoise = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float sampleVal = 0f;

                for (int v = 0; v < voiceCount; v++)
                {
                    float murmurEnv = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * voicePhaseSpeeds[v] * t + v * 1.7f);
                    sampleVal += Mathf.Sin(2f * Mathf.PI * voiceFreqs[v] * t) * murmurEnv * (1f / voiceCount);
                }

                // Cheap one-pole low-pass on raw noise so it reads as murmur texture rather than harsh static
                float rawNoise = (float)rng.NextDouble() * 2f - 1f;
                float filteredNoise = prevNoise + 0.15f * (rawNoise - prevNoise);
                prevNoise = filteredNoise;
                sampleVal += filteredNoise * 0.25f;

                float fadeEnv = 1f;
                float fadeLen = 0.15f;
                if (t < fadeLen) fadeEnv = t / fadeLen;
                else if (t > duration - fadeLen) fadeEnv = (duration - t) / fadeLen;

                samples[i] = sampleVal * 0.3f * fadeEnv;
            }

            AudioClip clip = AudioClip.Create("ProceduralCrowdNoise", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        [Header("Phase 3 特效与可读性调校")]
        [Tooltip("Phase 3 最大音频失真度（降低失真以提高画面与声音的可读性）")]
        [Range(0f, 1f)] public float phase3MaxDistortion = 0.25f;

        [Tooltip("Phase 3 最低 LowPass 截止频率（维持高频清晰通透）")]
        public float phase3LowPassCutoff = 3500f;

        [Tooltip("Phase 3 音调 Pitch 下限")]
        public float phase3Pitch = 0.92f;

        [Header("Phase 4 彻底混沌音频强度 (Total Chaos)")]
        [Range(0f, 1f)] public float phase4MaxDistortion = 0.55f;
        public float phase4MinLowPassCutoff = 900f;
        public float phase4PitchMin = 0.75f;
        public float phase4PitchMax = 1.15f;
        public float phase4AudioHypnoticFrequency = 0.8f;
        public float phase4AudioBurstInterval = 5f;
        public float phase4AudioBurstDuration = 0.35f;

        [Header("🎛️ Phase 音频强度曲线 (Data-Driven，与视觉系统对称设计，下面这套新系统已取代上方部分硬编码阶段分支公式)")]
        [Tooltip("失真度 (0~1)")]
        public PhaseCurveParam distortionCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 0.2f, phase3 = 0.5f, phase4 = 0.7f };

        [Tooltip("LowPass 截止频率 (Hz)，越低越闷")]
        public PhaseCurveParam lowPassCurve = new PhaseCurveParam { phase1 = 22000f, phase2 = 6000f, phase3 = 2800f, phase4 = 900f };

        [Tooltip("合唱/相位抖动深度 (0~1)")]
        public PhaseCurveParam chorusDepthCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 0.25f, phase3 = 0.45f, phase4 = 0.5f };

        [Tooltip("混响衰减时间 (秒)")]
        public PhaseCurveParam reverbDecayCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 2.0f, phase3 = 3.0f, phase4 = 4.5f };

        [Tooltip("BGM 音调 Pitch")]
        public PhaseCurveParam pitchCurve = new PhaseCurveParam { phase1 = 1.0f, phase2 = 0.94f, phase3 = 0.90f, phase4 = 0.80f };

        [Tooltip("环境噪声图层音量占 crowdNoiseVolume 的比例 (0~1)")]
        public PhaseCurveParam crowdVolumeCurve = new PhaseCurveParam { phase1 = 0f, phase2 = 0.35f, phase3 = 0.55f, phase4 = 0.7f };

        [Header("🔊 音画同步 (通过 Stage5AnnounceBus 读取视觉混沌强度，叠加调制失真/低通，无需引用视觉脚本)")]
        [Tooltip("0=完全不受视觉影响，1=失真/低通最多可被视觉强度放大到 2 倍")]
        [Range(0f, 1f)] public float audioVisualSyncStrength = 0.4f;

        [Header("🔔 阶段切换音效强调 (通过 Stage5AnnounceBus 的 OnPhaseEntered 事件驱动，一次性脉冲)")]
        [Tooltip("脉冲衰减速度 (越大越快恢复平静)")]
        public float phaseTransitionPulseDecay = 3f;

        [Tooltip("每次跨过阶段边界时，额外叠加的失真度脉冲峰值")]
        public float phaseTransitionPulseDistortionBoost = 0.25f;

        private float _phaseTransitionPulseTimer = 0f;

        [Tooltip("Phase 3 漩涡扭曲强度 (0 彻底关闭螺旋拉扯)")]
        [Range(0f, 1.5f)] public float maxVortexAmount = 0.0f;

        [Tooltip("Phase 3 油彩抹平强度 (0.06 保持画面平整)")]
        [Range(0f, 1.5f)] public float maxOilSmearArc = 0.06f;

        [Tooltip("Phase 3 极速拖尾模糊 (0 彻底关闭模糊重影)")]
        [Range(0f, 1.5f)] public float maxSpeedTrails = 0.0f;

        [Tooltip("Phase 3 Glitch 像素故障块强度")]
        [Range(0f, 1.0f)] public float maxGlitchAmount = 0.08f;

        /// <summary>
        /// 核心：夸张演变的单曲 BGM + DSP 音频滤镜扭曲系统 + 混响 + 嘈杂人声图层
        /// 随 phaseProgress (0→1) 极其显著地渐变，确保肉耳 100% 能听出阶段质变！
        /// </summary>
        // Core: data-driven BGM + DSP audio filter distortion system + reverb + crowd noise layer.
        // All intensity parameters are now driven by PhaseCurveParam (symmetric with the visual system),
        // and additionally modulated by the visual chaos intensity published on Stage5AnnounceBus,
        // so audio reacts to visuals without either script directly referencing the other.
        private void UpdateAudioDynamics()
        {
            if (_bgmAudioSource == null) return;

            // Phase-transition one-shot pulse, decays over time (triggered by Stage5AnnounceBus.OnPhaseEntered)
            if (_phaseTransitionPulseTimer > 0f)
            {
                _phaseTransitionPulseTimer = Mathf.Max(0f, _phaseTransitionPulseTimer - Time.deltaTime * phaseTransitionPulseDecay);
            }
            float transitionPulse = _phaseTransitionPulseTimer * phaseTransitionPulseDistortionBoost;

            // Audio-visual sync: read the chaos intensity published by the visual system via the bus,
            // with no direct reference to the visual script.
            float visualSync = Stage5AnnounceBus.VisualChaosIntensity;
            float syncMultiplier = Mathf.Lerp(1f, 1f + visualSync, audioVisualSyncStrength);

            float baseDistortion = distortionCurve.Evaluate(phaseProgress);
            float finalDistortion = Mathf.Clamp01(baseDistortion * syncMultiplier + transitionPulse);

            float baseLowPass = lowPassCurve.Evaluate(phaseProgress);
            float finalLowPass = Mathf.Max(200f, baseLowPass / syncMultiplier);

            float baseChorus = chorusDepthCurve.Evaluate(phaseProgress);
            float finalChorus = Mathf.Clamp01(baseChorus * syncMultiplier);

            float basePitch = pitchCurve.Evaluate(phaseProgress);
            float baseReverbDecay = reverbDecayCurve.Evaluate(phaseProgress);
            float baseCrowdRatio = crowdVolumeCurve.Evaluate(phaseProgress);

            _distortionFilter.distortionLevel = finalDistortion;
            _lowPassFilter.cutoffFrequency = finalLowPass;
            _chorusFilter.depth = finalChorus;
            _bgmAudioSource.pitch = basePitch;

            if (_crowdAudioSource != null)
            {
                _crowdAudioSource.volume = crowdNoiseVolume * baseCrowdRatio;
            }

            // Reverb preset switches discretely by phase (Off -> Room -> Room -> Cave); decay time stays continuous.
            if (phaseProgress < 0.35f)
            {
                _reverbFilter.reverbPreset = AudioReverbPreset.Off;
            }
            else if (phaseProgress < 0.96f)
            {
                _reverbFilter.reverbPreset = AudioReverbPreset.Room;
                _reverbFilter.decayTime = baseReverbDecay;
            }
            else
            {
                // Phase4 total-chaos cult climax: dual-mode rhythm (slow hypnotic pulse + occasional violent burst),
                // Cave reverb for ritual atmosphere, layered on top of the audio-visual sync values above.
                float hypnotic = 0.6f + 0.4f * Mathf.Sin(Time.time * phase4AudioHypnoticFrequency);
                bool inBurst = phase4AudioBurstInterval > 0.01f && (Time.time % phase4AudioBurstInterval) < phase4AudioBurstDuration;
                float burstMul = inBurst ? 1.8f : 1f;
                float chaos = Mathf.Clamp01(hypnotic * burstMul);

                _reverbFilter.reverbPreset = AudioReverbPreset.Cave;
                _reverbFilter.decayTime = baseReverbDecay * (0.6f + 0.4f * chaos);
                _distortionFilter.distortionLevel = Mathf.Clamp01(finalDistortion * chaos);
                _bgmAudioSource.pitch = inBurst
                    ? Random.Range(phase4PitchMin, phase4PitchMax)
                    : Mathf.Lerp(phase4PitchMin + 0.15f, phase4PitchMax - 0.1f, hypnotic);
            }
        }

        public void PlayGazeFeedbackSound()
        {
            if (_sfxAudioSource == null) return;

            // 连击 Pitch 升阶算法：1.2 秒内连续看卡片，音调逐步提升
            if (Time.time - _lastGazeTime < 1.2f)
            {
                _gazeComboCount = Mathf.Min(_gazeComboCount + 1, 8);
            }
            else
            {
                _gazeComboCount = 0;
            }
            _lastGazeTime = Time.time;

            float stepPitch = 1.0f + (_gazeComboCount * 0.06f);
            _sfxAudioSource.pitch = stepPitch;

            if (gazePopClip != null)
            {
                _sfxAudioSource.PlayOneShot(gazePopClip, gazePopVolume);
            }
        }

        private void EnsureOldCanvasesVisible()
        {
            UnityEngine.Canvas[] canvases = FindObjectsOfType<UnityEngine.Canvas>();
            foreach (var c in canvases)
            {
                c.gameObject.SetActive(true);
            }
        }

        private void CreateHUDUI()
        {
            GameObject canvasGo = new GameObject("HUDCanvas");
            UnityEngine.Canvas canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            // 屏幕中央准心小白点
            GameObject dotGo = new GameObject("ReticleDot");
            dotGo.transform.SetParent(canvasGo.transform, false);
            UnityEngine.UI.Image img = dotGo.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 1f, 1f, 0.6f);

            RectTransform rtDot = dotGo.GetComponent<RectTransform>();
            rtDot.sizeDelta = new Vector2(6f, 6f);
            rtDot.anchoredPosition = Vector2.zero;

            // 顶部 Phase 阶段状态栏背景面板
            GameObject bannerBgGo = new GameObject("PhaseBannerPanel");
            bannerBgGo.transform.SetParent(canvasGo.transform, false);
            UnityEngine.UI.Image bgImg = bannerBgGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.12f, 0.88f);

            RectTransform rtBg = bannerBgGo.GetComponent<RectTransform>();
            rtBg.anchorMin = new Vector2(0.5f, 1f);
            rtBg.anchorMax = new Vector2(0.5f, 1f);
            rtBg.pivot = new Vector2(0.5f, 1f);
            rtBg.anchoredPosition = new Vector2(0f, -18f);
            rtBg.sizeDelta = new Vector2(620f, 46f);

            // Phase 阶段文字
            GameObject textGo = new GameObject("PhaseStatusText");
            textGo.transform.SetParent(bannerBgGo.transform, false);
            _phaseStatusText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            _phaseStatusText.fontSize = 20f;
            _phaseStatusText.alignment = TMPro.TextAlignmentOptions.Center;
            _phaseStatusText.fontStyle = TMPro.FontStyles.Bold;

            RectTransform rtText = textGo.GetComponent<RectTransform>();
            rtText.anchorMin = Vector2.zero;
            rtText.anchorMax = Vector2.one;
            rtText.sizeDelta = Vector2.zero;
            rtText.anchoredPosition = Vector2.zero;
        }

        private void UpdatePhaseHUD()
        {
            if (_phaseStatusText == null) return;

            if (phaseProgress < 0.35f)
            {
                _phaseStatusText.text = "<color=#00FFCC>PHASE 1</color>  —  Sensory Liberation & Delights";
            }
            else if (phaseProgress < 0.70f)
            {
                _phaseStatusText.text = "<color=#FFCC00>PHASE 2</color>  —  Compulsive Algorithmic Addiction";
            }
            else if (phaseProgress < 0.96f)
            {
                _phaseStatusText.text = "<color=#FF3366>PHASE 3</color>  —  Data Exposed: Inescapable Destiny";
            }
            else
            {
                _phaseStatusText.text = "<color=#AA55FF>PHASE 4</color>  —  Total Chaos: Break Free or Be Consumed Forever";
            }
        }

        private Camera GetMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
#if UNITY_2023_1_OR_NEWER
                cam = FindAnyObjectByType<Camera>();
#else
                cam = FindObjectOfType<Camera>();
#endif
            }
            return cam;
        }

        private void SetupPlayerFloat()
        {
#if UNITY_2023_1_OR_NEWER
            UniversalPlayer player = FindAnyObjectByType<UniversalPlayer>();
#else
            UniversalPlayer player = FindObjectOfType<UniversalPlayer>();
#endif
            if (player != null)
            {
                player.moveSpeed = 0f;
                player.EnableControl();
                Debug.Log("[Stage5] 玩家移速已设为 0，视角旋转已启用。");
            }

            CursorService.Lock();
        }

        private void Update()
        {
            // 🔧 实时同步测试接口：将 Inspector 里填写的调试数值写进 PlayerBehaviorData
            if (overrideBehaviorData && PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.bananaCount = debugBananaCount;
                PlayerBehaviorData.Instance.prayerCount = debugPrayerCount;
                PlayerBehaviorData.Instance.pushCount = debugPushCount;
                PlayerBehaviorData.Instance.workCount = debugWorkCount;
            }

            // 按下 F1 快速开启/切换测试模式
            if (Input.GetKeyDown(KeyCode.F1))
            {
                overrideBehaviorData = !overrideBehaviorData;
                Debug.Log($"<color=yellow>[Stage5 Debug] 快捷键 F1 切换行为数据测试覆盖: {overrideBehaviorData}</color>");
            }
            // 快捷键调试：按数字键 1~5 直接切到对应 Phase
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                phaseProgress = 0.05f;
                Debug.Log("<color=green>[Stage5 调试] 跳转至 Phase 1 (Sensory Liberation)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                phaseProgress = 0.40f;
                Debug.Log("<color=yellow>[Stage5 调试] 跳转至 Phase 2 (Compulsive Addiction)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                phaseProgress = 0.75f;
                Debug.Log("<color=orange>[Stage5 调试] 跳转至 Phase 3 (Data Exposed)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                phaseProgress = 0.92f;
                Debug.Log("<color=red>[Stage5 调试] 跳转至 Phase 3 (Private Data Nuclear Log)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                phaseProgress = 0.98f;
                Debug.Log("<color=cyan>[Stage5 调试] 跳转至 Phase 4 (Moment of Choice)</color>");
            }

            // 取消固定硬编码时间自增！进度完全交由 CustomCorridorBinder 与内容交互驱动
            phaseProgress = Mathf.Clamp01(phaseProgress);

            // 子系统进度同步（CustomCorridorBinder 直接由 Update 读取 phaseProgress，不再需要每帧从这里强制推送混沌参数——
            // CustomCorridorBinder 现在自己完整拥有一套 PhaseCurveParam 曲线系统，不再依赖这里的 max前缀字段。

            // 动态更新顶部 Phase 阶段状态栏 HUD
            UpdatePhaseHUD();

            // 动态更新单曲 DSP 音频滤镜实时扭曲（随 phaseProgress 0→1 自动 Lerp 参数）
            Stage5AnnounceBus.AnnouncePhase(phaseProgress);

            UpdateAudioDynamics();

            // 音量平滑过渡
            AudioListener.volume = Mathf.Lerp(AudioListener.volume, _targetVolume, Time.deltaTime * 3f);

            // Phase 4 结尾选择
            if (phaseProgress >= 0.96f && !_endChoiceSpawned)
            {
                SpawnEndChoice();
            }

            // P 键调试跳关
            if (Input.GetKeyDown(KeyCode.P) && !_isTransitioning)
            {
                StartCoroutine(TransitionSequence());
            }
        }

        private void EnsurePostProcessingController()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<Stage5PostProcessingController>() == null)
#else
            if (FindObjectOfType<Stage5PostProcessingController>() == null)
#endif
            {
                GameObject go = new GameObject("Stage5PostProcessingController_Auto");
                go.AddComponent<Stage5PostProcessingController>();
                Debug.Log("[Stage5] 自动创建全局色彩基调后处理系统 (Stage5PostProcessingController)。");
            }
        }

        private void EnsureChaosOfferingSpawner()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<Stage5ChaosOfferingSpawner>() == null)
#else
            if (FindObjectOfType<Stage5ChaosOfferingSpawner>() == null)
#endif
            {
                GameObject go = new GameObject("Stage5ChaosOfferingSpawner_Auto");
                go.AddComponent<Stage5ChaosOfferingSpawner>();
                Debug.Log("[Stage5] 自动创建 Phase4 献祭物体召唤系统 (Stage5ChaosOfferingSpawner)。");
            }
        }

        private void CreateSubsystems()
        {
            if (useFluidTunnelMode)
            {
                CustomCorridorBinder binder = FindObjectOfType<CustomCorridorBinder>();
                if (binder == null)
                {
                    GameObject binderGo = new GameObject("CustomCorridorBinder_Auto");
                    binderGo.transform.SetParent(transform, false);
                    binder = binderGo.AddComponent<CustomCorridorBinder>();
                    binder.mediaDatabase = mediaDatabase;
                    Debug.Log("[Stage5] 自动创建手工 3D 走廊绑定器。");
                }
                else
                {
                    binder.mediaDatabase = mediaDatabase;
                    Debug.Log("[Stage5] 检测到场景中已存在 CustomCorridorBinder，已成功强制绑定 mediaDatabase 素材库！");
                }
            }
            else
            {
                Debug.Log("[Stage5] 未检测到 CustomCorridorBinder，场景请确保 Cube 走廊已手动配置。");
            }
        }



        private void SpawnEndChoice()
        {
            _endChoiceSpawned = true;
            Debug.Log("[Stage5] 进入 Phase 4 抉择时刻。");
        }

        private void OnChoiceSelected(string action)
        {
            if (_isTransitioning) return;

            if (action == "continue")
            {
                Debug.Log("[Stage5] 玩家看中[BREAK THE LOOP]抉择卡：挑战 Stage 6！");
                StartCoroutine(TransitionSequence());
            }
            else if (action == "stay")
            {
                Debug.Log("[Stage5] 玩家选择[INFINITE LOOP]：重置进度至 Phase 1。");
                phaseProgress = 0.05f;
                _endChoiceSpawned = false;
            }
        }

        public void TriggerSceneTransition()
        {
            if (!_isTransitioning)
            {
                StartCoroutine(TransitionSequence());
            }
        }

        public IEnumerator TransitionSequence()
        {
            _isTransitioning = true;
            EventBus.RaiseAnnouncement("The feed never ends. But you chose to look away and face the future.");
            yield return new WaitForSeconds(transitionDelay);
            PerformSceneTransition();
        }

        private void PerformSceneTransition()
        {
            AudioListener.volume = _originalVolume;
            EventBus.RaiseSceneComplete();

#if UNITY_EDITOR
            if (FindAnyObjectByType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning($"[Stage5Controller] 单关测试模式：直接加载 {nextSceneName}");
                SceneManager.LoadScene(nextSceneName);
            }
#endif
        }

        private void LogPhase()
        {
            string phase = phaseProgress < 0.35f ? "1 (Sensory Liberation)"
                         : phaseProgress < 0.70f ? "2 (Compulsive Addiction)"
                         : phaseProgress < 0.96f ? "3 (Data Exposed)"
                         : "4 (Total Chaos)";
            Debug.Log($"[Stage5] Phase {phase} | Progress: {phaseProgress:F3}");
        }

        private void OnDestroy()
        {
            AudioListener.volume = _originalVolume;
            Stage5AnnounceBus.ResetState();
        }
    }
}
