using UnityEngine;
using UnityEngine.Video;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 5_Contemporary_1 专属高级 3D 走廊绑定器 (循序渐进算法渗透 + 崩坏狂点抗争突破机制)
    /// Phase 1: 沉浸刷屏 (25+ 视频)
    /// Phase 2: 算法丝滑渗透 (40 步，偏好权重 10%->90% 渐进，流体平缓加剧)
    /// Phase 3&4 融合: 空间高潮崩坏与玩家点击抗争 (任由崩坏->重置Phase1轮回; 疯狂连击鼠标->突破至Stage6!)
    /// </summary>
    public class CustomCorridorBinder : MonoBehaviour
    {
        [Header("媒体数据库")]
        public CardMediaDatabase mediaDatabase;

        [Header("手动搭建的走廊墙体")]
        [Tooltip("走廊尽头的正面墙/Quad（播放交替图像/视频）")]
        public Renderer frontWallRenderer;

        [Tooltip("走廊四周的 4 面墙体 Cube（左、右、天花板、地面）")]
        public Renderer[] sideWallRenderers;

        [Header("物理墙体震颤")]
        public float physicalWarpIntensity = 0.25f;

        [Header("Phase 1~3 节奏与步数配置 (超级延长大片版)")]
        [Tooltip("Phase 1 必须刷完的独立视频总数 (默认 40 个视频，长效沉浸)")]
        public int requiredPhase1Videos = 40;

        [Tooltip("Phase 2 算法渐进渗透的总切屏步数 (默认 80 步，约 5-6 分钟极其漫长平缓)")]
        public int requiredPhase2Steps = 80;

        [Tooltip("Phase 3 空间崩坏倒计时 (秒)，留出 30 秒足够的高潮崩溃沉淀时间")]
        public float collapseCountdown = 30f;

        [Tooltip("Phase 3 觉醒突破所需的疯狂连击鼠标次数 (连击 30 次打碎信息茧房)")]
        public int requiredResistanceClicks = 30;

        private Material _frontMat;
        private Material _wallMat;

        // 双 VideoPlayer 预加载系统
        private VideoPlayer _videoPlayerA;
        private VideoPlayer _videoPlayerB;
        private RenderTexture _renderTexA;
        private RenderTexture _renderTexB;
        private bool _activePlayerIsA = true;
        private bool _isVideoReady = false;

        private Texture _currentTex;
        private Texture _nextTex;
        private Texture _lastValidTex;

        private float _switchTimer = 0f;
        private float _currentInterval = 2.0f;
        private float _transTimer = 0f;
        private bool _isTransitioning = false;
        private int _currentModeIndex = 0; // 0:Grid, 1:Strips, 2:Fluid, 3:Portal, 4:Data

        // 步数与历史索引
        private int _phase1VideoCount = 0;
        private int _phase2StepCount = 0;
        private float _collapseTimer = 0f;
        private int _resistanceClickCount = 0;
        private bool _hasTriggeredBreakthrough = false;
        private int _lastVideoIndex = -1;

        private Vector3[] _initialWallPositions;
        private Quaternion[] _initialWallRotations;
        private Vector3[] _initialWallScales;

        // Phase 3/4 提示 UI Text
        private GameObject _resistanceUiGo;
        private TextMeshPro _resistanceTmp;

        private static CustomCorridorBinder _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[CustomCorridorBinder] 检测到场景中存在重复的 Binder 实例，已自动销毁多余实例！");
                Destroy(this.gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            ContentCardSpawner spawner = FindObjectOfType<ContentCardSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
                Debug.Log("[CustomCorridorBinder] 已自动禁用散落卡片生成器，全面使用 5_Contemporary_1 专属 3D 走廊！");
            }

            SetupDoubleBufferedVideoPlayers();

            Shader frontShader = Shader.Find("Wakeup/CorridorFrontShader");
            if (frontShader == null) frontShader = Shader.Find("Universal Render Pipeline/Unlit");
            _frontMat = new Material(frontShader);

            Shader wallShader = Shader.Find("Wakeup/CorridorWallShader");
            if (wallShader == null) wallShader = Shader.Find("Universal Render Pipeline/Unlit");
            _wallMat = new Material(wallShader);

            if (frontWallRenderer != null) frontWallRenderer.material = _frontMat;

            if (sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                _initialWallPositions = new Vector3[sideWallRenderers.Length];
                _initialWallRotations = new Quaternion[sideWallRenderers.Length];
                _initialWallScales = new Vector3[sideWallRenderers.Length];

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        sideWallRenderers[i].material = _wallMat;
                        _initialWallPositions[i] = sideWallRenderers[i].transform.localPosition;
                        _initialWallRotations[i] = sideWallRenderers[i].transform.localRotation;
                        _initialWallScales[i] = sideWallRenderers[i].transform.localScale;
                    }
                }
            }

            CreateInvisibleGroundFloor();

            PickNextMedia();
            _currentTex = _nextTex != null ? _nextTex : Texture2D.blackTexture;
            _lastValidTex = _currentTex;
            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void CreateInvisibleGroundFloor()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Invisible_Static_Safety_Floor";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(50f, 1f, 100f);
            
            // 隐藏 Renderer 保持透明，仅保留 BoxCollider 坚固托住玩家
            Renderer r = ground.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        private void SetupDoubleBufferedVideoPlayers()
        {
            // Player A
            GameObject vpGoA = new GameObject("CorridorVideoPlayer_A");
            vpGoA.transform.SetParent(transform, false);
            AudioSource audioSourceA = vpGoA.AddComponent<AudioSource>();
            audioSourceA.playOnAwake = false;
            audioSourceA.spatialBlend = 0f; // 2D 环绕立体声
            audioSourceA.volume = 0.85f;

            _videoPlayerA = vpGoA.AddComponent<VideoPlayer>();
            _videoPlayerA.playOnAwake = false;
            _videoPlayerA.isLooping = true;
            _videoPlayerA.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayerA.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayerA.EnableAudioTrack(0, true);
            _videoPlayerA.SetTargetAudioSource(0, audioSourceA);
            _videoPlayerA.prepareCompleted += OnVideoPrepared;

            _renderTexA = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _renderTexA.Create();
            _videoPlayerA.targetTexture = _renderTexA;

            // Player B
            GameObject vpGoB = new GameObject("CorridorVideoPlayer_B");
            vpGoB.transform.SetParent(transform, false);
            AudioSource audioSourceB = vpGoB.AddComponent<AudioSource>();
            audioSourceB.playOnAwake = false;
            audioSourceB.spatialBlend = 0f;
            audioSourceB.volume = 0.85f;

            _videoPlayerB = vpGoB.AddComponent<VideoPlayer>();
            _videoPlayerB.playOnAwake = false;
            _videoPlayerB.isLooping = true;
            _videoPlayerB.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayerB.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _videoPlayerB.EnableAudioTrack(0, true);
            _videoPlayerB.SetTargetAudioSource(0, audioSourceB);
            _videoPlayerB.prepareCompleted += OnVideoPrepared;

            _renderTexB = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _renderTexB.Create();
            _videoPlayerB.targetTexture = _renderTexB;
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            _isVideoReady = true;
        }

        private void Update()
        {
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            float time = Time.time;

            UpdateRhythmTempo(phaseProgress);
            HandleControlModeAndInput(phaseProgress);

            // 动态调节视频音源音量 (Phase 1&2 音质饱满，Phase 3 音量随抽走压迫沉寂)
            float videoVolume = phaseProgress > 0.70f ? Mathf.Lerp(0.85f, 0.05f, Mathf.InverseLerp(0.70f, 0.96f, phaseProgress)) : 0.85f;
            if (_videoPlayerA != null && _videoPlayerA.GetTargetAudioSource(0) != null)
                _videoPlayerA.GetTargetAudioSource(0).volume = videoVolume;
            if (_videoPlayerB != null && _videoPlayerB.GetTargetAudioSource(0) != null)
                _videoPlayerB.GetTargetAudioSource(0).volume = videoVolume;
            if (_frontMat != null)
            {
                float progress = 0f;
                if (_isTransitioning)
                {
                    _transTimer += Time.deltaTime;
                    float transDur = 0.35f;
                    progress = Mathf.Clamp01(_transTimer / transDur);
                    if (_transTimer >= transDur) _isTransitioning = false;
                }

                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.95f : 0f;
                _frontMat.SetFloat("_TransitionProgress", progress);
                _frontMat.SetFloat("_TransitionMode", (float)_currentModeIndex);
                _frontMat.SetFloat("_GlitchIntensity", glitch);
            }

            // 5. 驱动 Side Walls 四周墙面多维流体 (Phase 1&2 轻松有趣、浪漫圆润)
            if (_wallMat != null)
            {
                bool isPhase1 = phaseProgress < 0.35f;

                float p2Ratio = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float p3Ratio = Mathf.InverseLerp(0.70f, 1.00f, phaseProgress);

                float jelly = isPhase1 ? 0.6f : Mathf.Lerp(0.6f, 0.1f, p2Ratio);

                float speed = isPhase1 ? 0.6f : (0.8f + p2Ratio * 1.5f + p3Ratio * 3.5f);

                // 梦幻轻柔 Phase 1&2 (Dreamy Soft Pastel Aura) ➔ 狂乱高潮 Phase 3
                float glitch = isPhase1 ? 0.08f : (0.08f + p2Ratio * 0.22f + p3Ratio * 0.65f); // Phase 1 极轻微梦幻！
                float rgbShift = isPhase1 ? 0.006f : (0.006f + p2Ratio * 0.015f + p3Ratio * 0.035f); // 柔和梦幻光晕
                float waveWarp = isPhase1 ? 0.15f : (0.15f + p2Ratio * 0.45f + p3Ratio * 1.0f);

                float angle = isPhase1 ? 0.1f : (0.1f + Mathf.Sin(time * 0.3f) * (0.4f + p2Ratio * 0.6f));
                float vortex = isPhase1 ? 0.05f : (0.05f + p2Ratio * 0.6f + p3Ratio * 1.2f);
                float sliceShift = isPhase1 ? 0.06f : (0.06f + p2Ratio * 0.2f + p3Ratio * 0.65f); // 轻柔水波切片
                // 参考图 1 油彩弧形抹平与参考图 2 爆炸极速拖尾 (Phase 1 渐进至 Phase 4)
                float borderFade = isPhase1 ? 0f : (p2Ratio * 0.2f + p3Ratio * 1.0f);
                float oilSmear = isPhase1 ? 0.05f : (0.05f + p2Ratio * 0.45f + p3Ratio * 1.0f);
                float speedTrails = isPhase1 ? 0f : (p2Ratio * 0.35f + p3Ratio * 1.0f);

                _wallMat.SetFloat("_JellyAmount", jelly);
                _wallMat.SetFloat("_FlowSpeed", speed);
                _wallMat.SetFloat("_GlitchAmount", glitch);
                _wallMat.SetFloat("_RGBShift", rgbShift);
                _wallMat.SetFloat("_WaveWarp", waveWarp);
                _wallMat.SetFloat("_BorderFade", borderFade);
                _wallMat.SetFloat("_OilSmearArc", oilSmear);
                _wallMat.SetFloat("_ExplosiveRadialTrails", speedTrails);

                _wallMat.SetFloat("_FlowAngle", angle);
                _wallMat.SetFloat("_VortexAmount", vortex);
                _wallMat.SetFloat("_SliceOffset", sliceShift);
            }

            // 物理 Cube (1)~(5) 墙面：保持原汁原味结实长方形走廊结构！
            if (sideWallRenderers != null && _initialWallPositions != null && _initialWallScales != null)
            {
                bool isPhase1 = phaseProgress < 0.35f;
                float physIntensity = phaseProgress > 0.70f ? Mathf.InverseLerp(0.70f, 1.0f, phaseProgress) * physicalWarpIntensity : 0f;

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        // 1. 严格基于你在 Scene 里搭好的初始 Scale 进行 5% 微小软呼吸，绝不改变长条结构！
                        Vector3 baseScale = _initialWallScales[i];
                        Vector3 jellyScale = baseScale;
                        if (phaseProgress < 0.70f)
                        {
                            float breathe = Mathf.Sin(time * 1.5f + i * 0.8f) * 0.03f;
                            jellyScale = new Vector3(baseScale.x * (1.0f + breathe), baseScale.y * (1.0f - breathe), baseScale.z * (1.0f + breathe * 0.5f));
                        }
                        sideWallRenderers[i].transform.localScale = jellyScale;

                        // 2. 物理位置微震
                        Vector3 waveOffset = new Vector3(
                            Mathf.Sin(time * 2.2f + i) * 0.03f,
                            Mathf.Cos(time * 1.8f + i) * 0.03f,
                            Mathf.Sin(time * 1.5f + i) * 0.02f
                        ) * (isPhase1 ? 0.2f : (0.2f + physIntensity));

                        sideWallRenderers[i].transform.localPosition = _initialWallPositions[i] + waveOffset;
                        float angleOffset = Mathf.Sin(time * 4.0f + i) * 2.5f * physIntensity;
                        sideWallRenderers[i].transform.localRotation = _initialWallRotations[i] * Quaternion.Euler(angleOffset, 0f, angleOffset);
                    }
                }
            }
        }

        private float _mediaPlayTimer = 0f;

        /// <summary>
        /// 全新交互控制逻辑 (Phase 1 刷屏 ➔ Phase 2 循序渐进 ➔ Phase 3&4 点击抗争)
        /// </summary>
        private void HandleControlModeAndInput(float phaseProgress)
        {
            _mediaPlayTimer += Time.deltaTime;
            bool playerClicked = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

            if (phaseProgress < 0.35f)
            {
                // Phase 1: 必须看完 40 个视频，每次展示至少 2.2 秒防偷跑
                bool readyForNextClick = _mediaPlayTimer >= 2.2f && !_isTransitioning;

                if (playerClicked && readyForNextClick)
                {
                    _mediaPlayTimer = 0f;
                    _phase1VideoCount++;
                    TriggerNextMediaSwitch();

                    float p = Mathf.Clamp01((float)_phase1VideoCount / Mathf.Max(1, requiredPhase1Videos)) * 0.35f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;

                    Debug.Log($"<color=cyan>[Phase 1 刷屏点击] 点击切换视频成功！已刷视频: {_phase1VideoCount}/{requiredPhase1Videos} (Phase进度: {p * 100f:F1}%)</color>");
                }
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2: 算法半自动渐进推移 (80 步)
                _switchTimer += Time.deltaTime;
                bool timerExpired = _switchTimer >= _currentInterval;

                if ((playerClicked || timerExpired) && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    _phase2StepCount++;
                    TriggerNextMediaSwitch();

                    float p = 0.35f + Mathf.Clamp01((float)_phase2StepCount / Mathf.Max(1, requiredPhase2Steps)) * 0.35f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;

                    float themeWeight = Mathf.Lerp(10f, 90f, Mathf.InverseLerp(0.35f, 0.70f, p));
                    Debug.Log($"<color=yellow>[Phase 2 算法推流] 切屏步骤: {_phase2StepCount}/{requiredPhase2Steps} (偏好渗透率: {themeWeight:F0}%, Phase进度: {p * 100f:F1}%)</color>");
                }
            }
            else
            {
                // Phase 3 & 4 融合高潮：空间高频崩坏 + 玩家连击抗争机制！
                _switchTimer += Time.deltaTime;
                if (_switchTimer >= _currentInterval && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    TriggerNextMediaSwitch();
                }

                _collapseTimer += Time.deltaTime;
                EnsureResistanceUI();

                if (_resistanceTmp != null)
                {
                    float remainTime = Mathf.Max(0f, collapseCountdown - _collapseTimer);
                    _resistanceTmp.text = $"[ SYSTEM COLLAPSING... ]\nPRESS/CLICK TO BREAK FREE! ({_resistanceClickCount}/{requiredResistanceClicks})";
                }

                // 玩家疯狂连击抗争检测
                if (playerClicked && !_hasTriggeredBreakthrough)
                {
                    _resistanceClickCount++;
                    Debug.Log($"<color=red>[Phase 3 觉醒连击] 突破抗争点击 +1！当前累计: {_resistanceClickCount}/{requiredResistanceClicks}</color>");

                    if (_resistanceClickCount >= requiredResistanceClicks)
                    {
                        _hasTriggeredBreakthrough = true;
                        Debug.Log("<color=green>[觉醒突破] 玩家通过疯狂连击成功打破算法信息茧房死循环！正式跳转加载 Stage 6！</color>");
                        if (_resistanceUiGo != null) _resistanceUiGo.SetActive(false);
                        
                        if (Stage5Controller.Instance != null)
                        {
                            Stage5Controller.Instance.phaseProgress = 1.0f;
                            Stage5Controller.Instance.TriggerSceneTransition();
                        }
                        return;
                    }
                }

                // 若倒计时结束仍未集满抗争连击，被动吞噬重置回 Phase 1
                if (_collapseTimer >= collapseCountdown)
                {
                    Debug.Log("<color=red>[崩坏吞噬] 玩家未做出抗争，被算法信息茧房重置吞噬！回到 Phase 1 死循环！</color>");
                    _collapseTimer = 0f;
                    _resistanceClickCount = 0;
                    _phase1VideoCount = 0;
                    _phase2StepCount = 0;
                    if (_resistanceUiGo != null) _resistanceUiGo.SetActive(false);
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = 0.02f;
                }
            }
        }

        private void TriggerNextMediaSwitch()
        {
            _isTransitioning = true;
            _transTimer = 0f;

            _currentModeIndex = Random.Range(0, 5);

            if (_nextTex != null) _currentTex = _nextTex;
            if (_currentTex != null) _lastValidTex = _currentTex;

            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void UpdateRhythmTempo(float phaseProgress)
        {
            if (phaseProgress < 0.35f)
            {
                _currentInterval = 4.5f;
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2: 漫长平缓，时间从 7.0 秒逐渐变化到 4.5 秒
                _currentInterval = Mathf.Lerp(7.0f, 4.5f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                // Phase 3: 狂乱高频 1.0s ~ 0.5s 切屏
                _currentInterval = Mathf.Lerp(1.0f, 0.5f, Mathf.InverseLerp(0.70f, 1.00f, phaseProgress));
            }
        }

        /// <summary>
        /// 循序渐进的偏好算法渗透推流逻辑
        /// </summary>
        private void PickNextMedia()
        {
            if (mediaDatabase == null) return;

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            string theme = GetDominantTheme();

            // 在 Phase 2 中，偏好算法推送权重从 10% 循序渐进增加到 90%
            float themeWeight = 0f;
            if (phaseProgress < 0.35f)
            {
                themeWeight = 0.0f; // Phase 1 纯娱乐短视频
            }
            else if (phaseProgress < 0.70f)
            {
                themeWeight = Mathf.Lerp(0.10f, 0.90f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                themeWeight = 1.0f; // Phase 3 偏好强行霸屏
            }

            bool pickThemeVideo = Random.value < themeWeight;

            VideoClip[] videoPool = pickThemeVideo ? GetThemeVideoPool(theme) : mediaDatabase.entertainmentVideos;
            if (videoPool == null || videoPool.Length == 0) videoPool = mediaDatabase.entertainmentVideos;

            if (videoPool != null && videoPool.Length > 0)
            {
                int nextIndex = Random.Range(0, videoPool.Length);
                if (videoPool.Length > 1 && nextIndex == _lastVideoIndex)
                {
                    nextIndex = (nextIndex + 1) % videoPool.Length;
                }
                _lastVideoIndex = nextIndex;

                VideoClip clip = videoPool[nextIndex];
                if (clip != null)
                {
                    _activePlayerIsA = !_activePlayerIsA;
                    VideoPlayer activePlayer = _activePlayerIsA ? _videoPlayerA : _videoPlayerB;
                    RenderTexture activeTex = _activePlayerIsA ? _renderTexA : _renderTexB;

                    _isVideoReady = false;
                    activePlayer.clip = clip;
                    activePlayer.Prepare();
                    activePlayer.Play();

                    _nextTex = activeTex;
                    return;
                }
            }

            // 静态图片兜底
            if (phaseProgress < 0.35f)
            {
                _nextTex = mediaDatabase.GetEntertainmentTexture();
            }
            else
            {
                _nextTex = pickThemeVideo ? mediaDatabase.GetThemeTexture(theme) : mediaDatabase.GetEntertainmentTexture();
            }

            if (_nextTex == null) _nextTex = _lastValidTex;
        }

        private VideoClip[] GetThemeVideoPool(string theme)
        {
            if (mediaDatabase == null) return null;
            switch (theme.ToLower())
            {
                case "banana": return mediaDatabase.bananaVideos;
                case "prayer": return mediaDatabase.prayerVideos;
                case "push": return mediaDatabase.pushVideos;
                case "work": return mediaDatabase.workVideos;
            }
            return mediaDatabase.entertainmentVideos;
        }

        private void ApplyTexturesToWalls()
        {
            Texture texA = _currentTex != null ? _currentTex : _lastValidTex;
            Texture texB = _nextTex != null ? _nextTex : _lastValidTex;

            if (_frontMat != null)
            {
                if (_frontMat.HasProperty("_MainTex") && texA != null) _frontMat.SetTexture("_MainTex", texA);
                if (_frontMat.HasProperty("_NextTex") && texB != null) _frontMat.SetTexture("_NextTex", texB);
            }

            if (_wallMat != null)
            {
                Texture mainWallTex = texB != null ? texB : texA;
                Texture subWallTex = texA != null ? texA : texB;

                if (mainWallTex != null)
                {
                    if (_wallMat.HasProperty("_MainTex")) _wallMat.SetTexture("_MainTex", mainWallTex);
                    if (_wallMat.HasProperty("_BaseMap")) _wallMat.SetTexture("_BaseMap", mainWallTex);
                    _wallMat.mainTexture = mainWallTex;
                }
                if (subWallTex != null && _wallMat.HasProperty("_SubTex"))
                {
                    _wallMat.SetTexture("_SubTex", subWallTex);
                }
            }
        }

        private void EnsureResistanceUI()
        {
            if (_resistanceUiGo == null && frontWallRenderer != null)
            {
                _resistanceUiGo = new GameObject("Phase3_ResistanceUI");
                _resistanceUiGo.transform.SetParent(frontWallRenderer.transform, false);
                _resistanceUiGo.transform.localPosition = new Vector3(0f, 0.1f, -0.1f);

                _resistanceTmp = _resistanceUiGo.AddComponent<TextMeshPro>();
                _resistanceTmp.fontSize = 0.45f;
                _resistanceTmp.alignment = TextAlignmentOptions.Center;
                _resistanceTmp.color = new Color(1.0f, 0.2f, 0.2f, 1.0f);
            }
            if (_resistanceUiGo != null) _resistanceUiGo.SetActive(true);
        }

        private string GetDominantTheme()
        {
            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes  = PlayerBehaviorData.Instance.pushCount;
                works   = PlayerBehaviorData.Instance.workCount;
            }
            if (bananas + prayers + pushes + works == 0) works = 45;

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
            if (max == bananas) return "banana";
            if (max == prayers) return "prayer";
            if (max == pushes)  return "push";
            return "work";
        }
    }
}
