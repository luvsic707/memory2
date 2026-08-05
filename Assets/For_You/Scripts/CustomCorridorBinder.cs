using UnityEngine;
using UnityEngine.Video;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 高级多维 3D 走廊绑定器 (纯交互与内容驱动版 - 彻底取消死板时间限制)
    /// 整体体验时长不再硬编码，而是 100% 由玩家交互频次和数据库里的素材内容数量决定！
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
        public float physicalWarpIntensity = 0.15f;

        [Header("内容交互驱动配置 (已大幅拉长体验)")]
        [Tooltip("Phase 1 必须刷完的视频总数（默认 20 个视频）")]
        public int requiredPhase1Videos = 20;

        [Tooltip("Phase 2 算法控制切屏的总内容张数（默认 35 张，大幅拉长）")]
        public int requiredPhase2Steps = 35;

        [Tooltip("Phase 3 狂乱抽搐霸屏的总内容张数（默认 40 张，大幅拉长）")]
        public int requiredPhase3Steps = 40;

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
        private int _phase3StepCount = 0;
        private int _lastVideoIndex = -1;

        private Vector3[] _initialWallPositions;
        private Quaternion[] _initialWallRotations;

        // Phase 4 抉择终端组件
        private GameObject _choiceContainer;
        private GameObject _stayOptionGo;
        private GameObject _continueOptionGo;
        private TextMeshPro _stayTmp;
        private TextMeshPro _continueTmp;
        private float _gazeChoiceTimer = 0f;
        private string _hoveredChoice = "";

        private void Start()
        {
            ContentCardSpawner spawner = FindObjectOfType<ContentCardSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
                Debug.Log("[CustomCorridorBinder] 已自动禁用散落卡片生成器，全面使用高级 3D 走廊！");
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

            // 注视检测 (Phase 4 选择分支)
            Camera cam = Camera.main;
            if (cam != null && phaseProgress >= 0.96f)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                HandlePhase4ChoiceGaze(ray);
            }

            // Phase 4 抉择界面
            if (phaseProgress >= 0.96f)
            {
                if (_choiceContainer == null) CreatePhase4ChoiceTerminals();
                if (_choiceContainer != null) _choiceContainer.SetActive(true);
            }
            else
            {
                if (_choiceContainer != null) _choiceContainer.SetActive(false);
            }

            // 驱动 Front Wall 极速艺术过渡 (0.35s 极速快切)
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

                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f : 0f;
                _frontMat.SetFloat("_TransitionProgress", progress);
                _frontMat.SetFloat("_TransitionMode", (float)_currentModeIndex);
                _frontMat.SetFloat("_GlitchIntensity", glitch);
            }

            // 驱动 Side Walls 四周墙面多维流体
            if (_wallMat != null)
            {
                bool isPhase1 = phaseProgress < 0.35f;

                float speed = isPhase1 ? 0.8f : (1.0f + phaseProgress * 2.5f);
                float glitch = isPhase1 ? 0f : (Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f);
                float rgbShift = isPhase1 ? 0f : (Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.04f);
                float waveWarp = isPhase1 ? 0f : (Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 1.5f);

                float angle = isPhase1 ? 0f : (Mathf.Sin(time * 0.4f) * 1.2f);
                float vortex = isPhase1 ? 0f : (Mathf.InverseLerp(0.35f, 1.0f, phaseProgress) * 1.5f);
                float sliceShift = isPhase1 ? 0f : (Mathf.InverseLerp(0.35f, 1.0f, phaseProgress) * 0.9f);

                _wallMat.SetFloat("_FlowSpeed", speed);
                _wallMat.SetFloat("_GlitchAmount", glitch);
                _wallMat.SetFloat("_RGBShift", rgbShift);
                _wallMat.SetFloat("_WaveWarp", waveWarp);

                _wallMat.SetFloat("_FlowAngle", angle);
                _wallMat.SetFloat("_VortexAmount", vortex);
                _wallMat.SetFloat("_SliceOffset", sliceShift);
            }

            // 物理墙面震颤
            if (sideWallRenderers != null && _initialWallPositions != null)
            {
                float physIntensity = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 1.0f, phaseProgress) * physicalWarpIntensity : 0f;
                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        Vector3 waveOffset = new Vector3(
                            Mathf.Sin(time * 2.5f + i) * 0.08f,
                            Mathf.Cos(time * 2.1f + i) * 0.08f,
                            Mathf.Sin(time * 1.8f + i) * 0.05f
                        ) * physIntensity;

                        sideWallRenderers[i].transform.localPosition = _initialWallPositions[i] + waveOffset;
                        float angleOffset = Mathf.Sin(time * 3.0f + i) * 1.5f * physIntensity;
                        sideWallRenderers[i].transform.localRotation = _initialWallRotations[i] * Quaternion.Euler(angleOffset, 0f, angleOffset);
                    }
                }
            }
        }

        /// <summary>
        /// 核心：纯交互与内容驱动逻辑（彻底取代死板的时间倒计时）
        /// 进度从 0.0 -> 1.0 完全取决于玩家刷出的内容步数！
        /// </summary>
        private float _mediaPlayTimer = 0f; // 当前视频/素材在屏展现计时器

        private void HandleControlModeAndInput(float phaseProgress)
        {
            _mediaPlayTimer += Time.deltaTime;
            bool playerClicked = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

            if (phaseProgress < 0.35f)
            {
                // Phase 1: 防偷跑锁，每个视频必须至少稳定展现 2.2 秒以上才允许点击切下一个！
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
                // Phase 2 (0.35 -> 0.70): 算法半自动控制，每次内容推移增加一个 step
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
            else if (phaseProgress < 0.96f)
            {
                // Phase 3 (0.70 -> 0.96): 狂乱失控，按高频步数推进
                _switchTimer += Time.deltaTime;
                if (_switchTimer >= _currentInterval && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    _phase3StepCount++;
                    TriggerNextMediaSwitch();

                    float p = 0.70f + Mathf.Clamp01((float)_phase3StepCount / Mathf.Max(1, requiredPhase3Steps)) * 0.26f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;
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
                _currentInterval = 4.0f;
            }
            else if (phaseProgress < 0.70f)
            {
                _currentInterval = Mathf.Lerp(4.5f, 7.0f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                _currentInterval = Mathf.Lerp(1.5f, 0.8f, Mathf.InverseLerp(0.70f, 0.96f, phaseProgress));
            }
        }

        private void PickNextMedia()
        {
            if (mediaDatabase == null) return;

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            string theme = GetDominantTheme();

            VideoClip[] videoPool = null;
            if (phaseProgress < 0.35f)
            {
                videoPool = mediaDatabase.entertainmentVideos;
            }
            else
            {
                videoPool = GetThemeVideoPool(theme);
                if (videoPool == null || videoPool.Length == 0) videoPool = mediaDatabase.entertainmentVideos;
            }

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

            if (phaseProgress < 0.35f)
            {
                _nextTex = mediaDatabase.GetEntertainmentTexture();
            }
            else if (phaseProgress < 0.70f)
            {
                _nextTex = Random.value < 0.5f
                    ? mediaDatabase.GetThemeTexture(theme)
                    : mediaDatabase.GetEntertainmentTexture();
            }
            else
            {
                _nextTex = mediaDatabase.GetThemeTexture(theme) ?? mediaDatabase.GetEntertainmentTexture();
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

        private void CreatePhase4ChoiceTerminals()
        {
            if (frontWallRenderer == null) return;

            _choiceContainer = new GameObject("Phase4_ChoiceTerminals");
            _choiceContainer.transform.SetParent(frontWallRenderer.transform, false);
            _choiceContainer.transform.localPosition = new Vector3(0f, 0f, -0.1f);

            _stayOptionGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _stayOptionGo.name = "StayOptionQuad";
            _stayOptionGo.transform.SetParent(_choiceContainer.transform, false);
            _stayOptionGo.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
            _stayOptionGo.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            SetQuadColor(_stayOptionGo, new Color(0.1f, 0.75f, 1.0f, 0.95f));

            GameObject stayTextGo = new GameObject("StayText");
            stayTextGo.transform.SetParent(_stayOptionGo.transform, false);
            stayTextGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _stayTmp = stayTextGo.AddComponent<TextMeshPro>();
            _stayTmp.text = "[ INFINITE LOOP ]\nSTAY HERE";
            _stayTmp.alignment = TextAlignmentOptions.Center;
            _stayTmp.fontSize = 0.35f;
            _stayTmp.color = Color.white;

            _continueOptionGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _continueOptionGo.name = "ContinueOptionQuad";
            _continueOptionGo.transform.SetParent(_choiceContainer.transform, false);
            _continueOptionGo.transform.localPosition = new Vector3(0.25f, 0f, 0f);
            _continueOptionGo.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            SetQuadColor(_continueOptionGo, new Color(1.0f, 0.5f, 0.1f, 0.95f));

            GameObject continueTextGo = new GameObject("ContinueText");
            continueTextGo.transform.SetParent(_continueOptionGo.transform, false);
            continueTextGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _continueTmp = continueTextGo.AddComponent<TextMeshPro>();
            _continueTmp.text = "[ BREAK THE LOOP ]\nFACE THE FUTURE";
            _continueTmp.alignment = TextAlignmentOptions.Center;
            _continueTmp.fontSize = 0.35f;
            _continueTmp.color = Color.white;
        }

        private void HandlePhase4ChoiceGaze(Ray ray)
        {
            RaycastHit hit;
            string hovered = "";

            if (Physics.Raycast(ray, out hit, 50f))
            {
                if (_stayOptionGo != null && (hit.transform == _stayOptionGo.transform || hit.transform.IsChildOf(_stayOptionGo.transform)))
                {
                    hovered = "stay";
                }
                else if (_continueOptionGo != null && (hit.transform == _continueOptionGo.transform || hit.transform.IsChildOf(_continueOptionGo.transform)))
                {
                    hovered = "continue";
                }
            }

            if (!string.IsNullOrEmpty(hovered))
            {
                if (_hoveredChoice != hovered)
                {
                    _hoveredChoice = hovered;
                    _gazeChoiceTimer = 0f;
                }

                _gazeChoiceTimer += Time.deltaTime;

                if (hovered == "stay" && _stayOptionGo != null)
                    _stayOptionGo.transform.localScale = Vector3.Lerp(_stayOptionGo.transform.localScale, new Vector3(0.48f, 0.48f, 1f), Time.deltaTime * 10f);
                if (hovered == "continue" && _continueOptionGo != null)
                    _continueOptionGo.transform.localScale = Vector3.Lerp(_continueOptionGo.transform.localScale, new Vector3(0.48f, 0.48f, 1f), Time.deltaTime * 10f);

                if (_gazeChoiceTimer >= 0.5f)
                {
                    TriggerChoiceAction(hovered);
                }
            }
            else
            {
                _hoveredChoice = "";
                _gazeChoiceTimer = 0f;
                if (_stayOptionGo != null) _stayOptionGo.transform.localScale = Vector3.Lerp(_stayOptionGo.transform.localScale, new Vector3(0.40f, 0.40f, 1f), Time.deltaTime * 8f);
                if (_continueOptionGo != null) _continueOptionGo.transform.localScale = Vector3.Lerp(_continueOptionGo.transform.localScale, new Vector3(0.40f, 0.40f, 1f), Time.deltaTime * 8f);
            }
        }

        private void TriggerChoiceAction(string action)
        {
            if (Stage5Controller.Instance != null)
            {
                if (action == "continue")
                {
                    Debug.Log("[CustomCorridorBinder] 玩家看中 [BREAK THE LOOP]！进入 Stage 6！");
                    Stage5Controller.Instance.StartCoroutine("TransitionSequence");
                }
                else if (action == "stay")
                {
                    Debug.Log("[CustomCorridorBinder] 玩家看中 [INFINITE LOOP]！重置进度回到 Phase 1 循环！");
                    Stage5Controller.Instance.phaseProgress = 0.05f;
                    _hoveredChoice = "";
                    _gazeChoiceTimer = 0f;
                    _phase1VideoCount = 0;
                    _phase2StepCount = 0;
                    _phase3StepCount = 0;
                }
            }
        }

        private void SetQuadColor(GameObject quad, Color color)
        {
            Renderer rend = quad.GetComponent<Renderer>();
            if (rend == null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            Material m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            rend.material = m;
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
