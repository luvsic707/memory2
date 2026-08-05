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

        // Phase 3/4 提示 UI Text
        private GameObject _resistanceUiGo;
        private TextMeshPro _resistanceTmp;

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

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        sideWallRenderers[i].material = _wallMat;
                        _initialWallPositions[i] = sideWallRenderers[i].transform.localPosition;
                        _initialWallRotations[i] = sideWallRenderers[i].transform.localRotation;
                    }
                }
            }

            PickNextMedia();
            _currentTex = _nextTex != null ? _nextTex : Texture2D.blackTexture;
            _lastValidTex = _currentTex;
            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void SetupDoubleBufferedVideoPlayers()
        {
            GameObject vpGoA = new GameObject("CorridorVideoPlayer_A");
            vpGoA.transform.SetParent(transform, false);
            _videoPlayerA = vpGoA.AddComponent<VideoPlayer>();
            _videoPlayerA.playOnAwake = false;
            _videoPlayerA.isLooping = true;
            _videoPlayerA.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayerA.prepareCompleted += OnVideoPrepared;

            _renderTexA = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _renderTexA.Create();
            _videoPlayerA.targetTexture = _renderTexA;

            GameObject vpGoB = new GameObject("CorridorVideoPlayer_B");
            vpGoB.transform.SetParent(transform, false);
            _videoPlayerB = vpGoB.AddComponent<VideoPlayer>();
            _videoPlayerB.playOnAwake = false;
            _videoPlayerB.isLooping = true;
            _videoPlayerB.renderMode = VideoRenderMode.RenderTexture;
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

            // 驱动 Front Wall 艺术过渡
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

            // 驱动 Side Walls 四周墙面多维流体 (Phase 2 极其循序渐进地平缓加剧)
            if (_wallMat != null)
            {
                bool isPhase1 = phaseProgress < 0.35f;

                // Phase 2 演进权重 0 -> 1
                float p2Ratio = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float p3Ratio = Mathf.InverseLerp(0.70f, 1.00f, phaseProgress);

                float speed = isPhase1 ? 0.6f : (0.8f + p2Ratio * 1.5f + p3Ratio * 2.0f);
                float glitch = isPhase1 ? 0f : (p2Ratio * 0.4f + p3Ratio * 0.6f);
                float rgbShift = isPhase1 ? 0f : (p2Ratio * 0.02f + p3Ratio * 0.03f); // 极其平缓色差
                float waveWarp = isPhase1 ? 0f : (p2Ratio * 0.6f + p3Ratio * 1.2f);   // 循序渐进水波

                float angle = isPhase1 ? 0f : (Mathf.Sin(time * 0.3f) * (0.5f + p2Ratio * 0.8f));
                float vortex = isPhase1 ? 0f : (p2Ratio * 0.8f + p3Ratio * 1.2f);
                float sliceShift = isPhase1 ? 0f : (p2Ratio * 0.4f + p3Ratio * 0.6f);

                _wallMat.SetFloat("_FlowSpeed", speed);
                _wallMat.SetFloat("_GlitchAmount", glitch);
                _wallMat.SetFloat("_RGBShift", rgbShift);
                _wallMat.SetFloat("_WaveWarp", waveWarp);

                _wallMat.SetFloat("_FlowAngle", angle);
                _wallMat.SetFloat("_VortexAmount", vortex);
                _wallMat.SetFloat("_SliceOffset", sliceShift);
            }

            // 物理墙面震颤 (Phase 3 空间严重崩溃时剧烈震动)
            if (sideWallRenderers != null && _initialWallPositions != null)
            {
                float physIntensity = phaseProgress > 0.70f ? Mathf.InverseLerp(0.70f, 1.0f, phaseProgress) * physicalWarpIntensity : 0f;
                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        Vector3 waveOffset = new Vector3(
                            Mathf.Sin(time * 3.5f + i) * 0.12f,
                            Mathf.Cos(time * 3.1f + i) * 0.12f,
                            Mathf.Sin(time * 2.8f + i) * 0.08f
                        ) * physIntensity;

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
                // Phase 1 (0.0 -> 0.35): 必须看完 25 个以上视频，每次展示至少 2.2 秒防偷跑
                bool readyForNextClick = _mediaPlayTimer >= 2.2f && !_isTransitioning;

                if (playerClicked && readyForNextClick)
                {
                    _mediaPlayTimer = 0f;
                    _phase1VideoCount++;
                    TriggerNextMediaSwitch();

                    float p = Mathf.Clamp01((float)_phase1VideoCount / Mathf.Max(1, requiredPhase1Videos)) * 0.35f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;
                }
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2 (0.35 -> 0.70): 算法半自动渐进推移 (40 步，每步 5~8s，偏好权重从 10% 逐步增加到 90%)
                _switchTimer += Time.deltaTime;
                bool timerExpired = _switchTimer >= _currentInterval;

                if ((playerClicked || timerExpired) && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    _phase2StepCount++;
                    TriggerNextMediaSwitch();

                    float p = 0.35f + Mathf.Clamp01((float)_phase2StepCount / Mathf.Max(1, requiredPhase2Steps)) * 0.35f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;
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

                // 倒计时更新：如果玩家不动，崩坏计时归零回到 Phase 1 循环
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
                    Debug.Log($"[抗争机制] 玩家点击抗争 +1，当前累计: {_resistanceClickCount}/{requiredResistanceClicks}");

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
                if (mainWallTex != null)
                {
                    if (_wallMat.HasProperty("_MainTex")) _wallMat.SetTexture("_MainTex", mainWallTex);
                    if (_wallMat.HasProperty("_BaseMap")) _wallMat.SetTexture("_BaseMap", mainWallTex);
                    _wallMat.mainTexture = mainWallTex;
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
