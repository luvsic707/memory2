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

        [Tooltip("注视/点击卡片时，单次增加的 phaseProgress 增量")]
        public float progressPerGaze = 0.006f;

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

        [Header("卡片注视与交互音效（香蕉/Pop 愉悦反馈）")]
        [Tooltip("注视看卡片时的愉悦反馈音效（留空将自动程序化生成 80ms Sine 叮音）")]
        public AudioClip gazePopClip;

        [Tooltip("注视音效基础音量")]
        [Range(0f, 1f)]
        public float gazePopVolume = 0.6f;

        [Header("注视检测")]
        [Tooltip("注视射线的最大检测距离")]
        public float gazeRayDistance = 50f;

        [Tooltip("持续注视多久算一次交互（秒）")]
        public float gazeHoldTime = 0.35f;

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

        // 子系统引用
        private ContentCardSpawner _spawner;
        private FeedEnvironment _environment;

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

        // 注视状态
        private ContentCard _currentGazedCard = null;
        private float _gazeTimer = 0f;
        private bool _gazeTriggered = false;

        // 结尾选择
        private bool _endChoiceSpawned = false;
        private bool _isTransitioning = false;

        // 音量与 HUD
        private float _originalVolume = 1f;
        private float _targetVolume = 1f;
        private TMPro.TextMeshProUGUI _phaseStatusText;

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

            if (crowdNoiseClip != null)
            {
                _crowdAudioSource.clip = crowdNoiseClip;
                _crowdAudioSource.Play();
                Debug.Log($"[Stage5] 嘈杂人声图层已挂载: {crowdNoiseClip.name}");
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
            float duration = 6.0f; // 6秒无缝循环温润 Ambient 氛圈音轨
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float[] freqs = { 110f, 164.81f, 220f, 277.18f, 329.63f, 440f }; // A Minor 9 / Ambient Chord

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float sampleVal = 0f;

                // 柔和 LFO 慢速漫游包络
                float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.166f * t);

                for (int f = 0; f < freqs.Length; f++)
                {
                    float freq = freqs[f];
                    float amp = 1f / (f + 1); // 高频渐弱
                    sampleVal += Mathf.Sin(2f * Mathf.PI * freq * t) * amp;
                }

                // 边缘无缝淡入淡出（防 Crossfade 爆音）
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

        [Header("Phase 3 特效与可读性调校")]
        [Tooltip("Phase 3 最大音频失真度（降低失真以提高画面与声音的可读性）")]
        [Range(0f, 1f)] public float phase3MaxDistortion = 0.25f;

        [Tooltip("Phase 3 最低 LowPass 截止频率（维持高频清晰通透）")]
        public float phase3LowPassCutoff = 3500f;

        [Tooltip("Phase 3 音调 Pitch 下限")]
        public float phase3Pitch = 0.92f;

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
        private void UpdateAudioDynamics()
        {
            if (_bgmAudioSource == null) return;

            if (phaseProgress < 0.35f)
            {
                // Phase 1 (0.00 ~ 0.35): 干净甜美、全频通透、高保真、无混响、无嘈杂人声
                float t = Mathf.InverseLerp(0f, 0.35f, phaseProgress);
                _distortionFilter.distortionLevel = 0.0f;
                _lowPassFilter.cutoffFrequency = 22000f; // 22kHz 全频段通透
                _chorusFilter.depth = 0.0f;
                _reverbFilter.reverbPreset = AudioReverbPreset.Off;
                _bgmAudioSource.pitch = Mathf.Lerp(1.0f, 0.95f, t);

                if (_crowdAudioSource != null) _crowdAudioSource.volume = 0f;
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2 (0.35 ~ 0.70): 显著变闷压高频 + 电音失真 + 混响渐强 + 嘈杂人声渐入
                float t = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                _distortionFilter.distortionLevel = Mathf.Lerp(0.02f, 0.15f, t); // 温和失真
                _lowPassFilter.cutoffFrequency = Mathf.Lerp(22000f, 6000f, t);   // 维持清晰通透
                _chorusFilter.depth = Mathf.Lerp(0.0f, 0.25f, t);
                _reverbFilter.reverbPreset = AudioReverbPreset.Room;
                _reverbFilter.decayTime = Mathf.Lerp(1.0f, 2.0f, t);
                _bgmAudioSource.pitch = Mathf.Lerp(0.98f, 0.94f, t);

                if (_crowdAudioSource != null && crowdNoiseClip != null)
                {
                    _crowdAudioSource.volume = Mathf.Lerp(0f, crowdNoiseVolume * 0.35f, t);
                }
            }
            else if (phaseProgress < 0.96f)
            {
                // Phase 3 (0.70 ~ 0.96): 减轻特效重度，极大提升内容与文字的画面/声音可读性！
                float t = Mathf.InverseLerp(0.70f, 0.96f, phaseProgress);
                _distortionFilter.distortionLevel = Mathf.Lerp(0.15f, phase3MaxDistortion, t); // 轻微温和破音，绝不炸耳
                _lowPassFilter.cutoffFrequency = Mathf.Lerp(6000f, phase3LowPassCutoff, t);     // 保持 3500Hz 清晰人声与视频音效
                _chorusFilter.depth = Mathf.Lerp(0.25f, 0.40f, t);
                _reverbFilter.reverbPreset = AudioReverbPreset.Room;                            // 使用自然房间混响取代恐怖洞穴
                _reverbFilter.decayTime = Mathf.Lerp(2.0f, 2.8f, t);
                _bgmAudioSource.pitch = Mathf.Lerp(0.94f, phase3Pitch, t);                      // 自然音乐步调

                if (_crowdAudioSource != null && crowdNoiseClip != null)
                {
                    _crowdAudioSource.volume = Mathf.Lerp(crowdNoiseVolume * 0.35f, crowdNoiseVolume * 0.5f, t);
                }
            }
            else
            {
                // Phase 4 (0.96 ~ 1.00): 抉择时刻保持定格
                _distortionFilter.distortionLevel = 0.10f;
                _lowPassFilter.cutoffFrequency = 8000f;
                _chorusFilter.depth = 0.1f;
                _reverbFilter.reverbPreset = AudioReverbPreset.Off;
                _bgmAudioSource.pitch = 1.0f;

                if (_crowdAudioSource != null) _crowdAudioSource.volume = 0f;
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
                _phaseStatusText.text = "<color=#AA55FF>PHASE 4</color>  —  Moment of Choice: Stay Here or Challenge Stage 6";
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
                if (_spawner != null) _spawner.enabled = true;
                Debug.Log("<color=green>[Stage5 调试] 跳转至 Phase 1 (Sensory Liberation)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                phaseProgress = 0.40f;
                if (_spawner != null) _spawner.enabled = true;
                Debug.Log("<color=yellow>[Stage5 调试] 跳转至 Phase 2 (Compulsive Addiction)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                phaseProgress = 0.75f;
                if (_spawner != null) _spawner.enabled = true;
                Debug.Log("<color=orange>[Stage5 调试] 跳转至 Phase 3 (Data Exposed)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                phaseProgress = 0.92f;
                if (_spawner != null) _spawner.enabled = true;
                Debug.Log("<color=red>[Stage5 调试] 跳转至 Phase 3 (Private Data Nuclear Log)</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                phaseProgress = 0.98f;
                Debug.Log("<color=cyan>[Stage5 调试] 跳转至 Phase 4 (Moment of Choice)</color>");
            }

            // 取消固定硬编码时间自增！进度完全交由 CustomCorridorBinder 与内容交互驱动
            phaseProgress = Mathf.Clamp01(phaseProgress);

            // 同步旋钮与视觉滑块给子系统
            if (_spawner != null) _spawner.phaseProgress = phaseProgress;
            if (_environment != null) _environment.phaseProgress = phaseProgress;

            CustomCorridorBinder binder = FindObjectOfType<CustomCorridorBinder>();
            if (binder != null)
            {
                binder.maxVortexAmount = maxVortexAmount;
                binder.maxOilSmearArc = maxOilSmearArc;
                binder.maxSpeedTrails = maxSpeedTrails;
                binder.maxGlitchAmount = maxGlitchAmount;
            }

            // 动态更新顶部 Phase 阶段状态栏 HUD
            UpdatePhaseHUD();

            // 动态更新单曲 DSP 音频滤镜实时扭曲（随 phaseProgress 0→1 自动 Lerp 参数）
            UpdateAudioDynamics();

            // 注视射线检测
            ProcessGaze();

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
                    Debug.Log("[Stage5] 检测到场景中已存在 CustomCorridorBinder，直接绑定使用！");
                }
            }
            else
            {
                GameObject spawnerGo = new GameObject("ContentCardSpawner");
                spawnerGo.transform.SetParent(transform, false);
                _spawner = spawnerGo.AddComponent<ContentCardSpawner>();
                _spawner.OnCardSpawned += OnCardSpawned;
                _spawner.mediaDatabase = mediaDatabase;
            }

            GameObject envGo = new GameObject("FeedEnvironment");
            envGo.transform.SetParent(transform, false);
            _environment = envGo.AddComponent<FeedEnvironment>();

            GameObject relicGo = new GameObject("Stage5RelicSpawner");
            relicGo.transform.SetParent(transform, false);
            relicGo.AddComponent<Stage5RelicSpawner>();

            GameObject popGo = new GameObject("Stage5PopGeometryEffect");
            popGo.transform.SetParent(transform, false);
            popGo.AddComponent<Stage5PopGeometryEffect>();
            Debug.Log("[Stage5] 自动挂载视频同款魔幻 Pop 几何阵列与 HUD 锁定框系统。");
        }

        private void OnCardSpawned(ContentCard card)
        {
            card.OnChoiceSelected += OnChoiceSelected;
        }

        private void ProcessGaze()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, gazeRayDistance))
            {
                ContentCard card = hit.collider.GetComponentInParent<ContentCard>();
                if (card != null)
                {
                    if (_currentGazedCard != card)
                    {
                        if (_currentGazedCard != null) _currentGazedCard.OnGazeExit();
                        _currentGazedCard = card;
                        _gazeTimer = 0f;
                        _gazeTriggered = false;
                    }

                    _gazeTimer += Time.deltaTime;

                    float requiredGazeTime = card.isChoiceCard ? 0.15f : gazeHoldTime;

                    if (_gazeTimer >= requiredGazeTime && !_gazeTriggered)
                    {
                        _gazeTriggered = true;
                        card.OnGazeEnter();

                        // 播放卡片注视音效叠加 (Pitch Stacking)
                        PlayGazeFeedbackSound();

                        phaseProgress += progressPerGaze;
                        phaseProgress = Mathf.Clamp01(phaseProgress);

                        if (card.isPrivateDataCard)
                        {
                            _targetVolume = silenceVolume;
                        }
                        else
                        {
                            _targetVolume = _originalVolume;
                        }

                        LogPhase();
                    }

                    return;
                }
            }

            if (_currentGazedCard != null)
            {
                _currentGazedCard.OnGazeExit();
                _currentGazedCard = null;
                _gazeTimer = 0f;
                _gazeTriggered = false;
            }

            _targetVolume = _originalVolume;
        }

        private void SpawnEndChoice()
        {
            _endChoiceSpawned = true;
            Debug.Log("[Stage5] 进入 Phase 4 抉择时刻：抉择卡片开始与其他卡片一道，从远方源源不断飞向玩家。");

            if (_spawner != null) _spawner.enabled = true;
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
                Debug.Log("[Stage5] 玩家看中[INFINITE LOOP]抉择卡：重置进度至 Phase 1，无缝重置卡片流循环！");
                
                // 清理飞过的选择卡
                ContentCard[] cards = FindObjectsOfType<ContentCard>();
                foreach (var c in cards)
                {
                    if (c.isChoiceCard) Destroy(c.gameObject);
                }

                if (_spawner != null) _spawner.enabled = true;

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
                         : "4 (Moment of Choice)";
            Debug.Log($"[Stage5] Phase {phase} | Progress: {phaseProgress:F3}");
        }

        private void OnDestroy()
        {
            AudioListener.volume = _originalVolume;
        }
    }
}
