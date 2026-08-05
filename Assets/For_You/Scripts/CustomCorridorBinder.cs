using UnityEngine;
using UnityEngine.Video;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 高级多维 3D 走廊绑定器 (主动点击掌控 -> 掌控力丧失 -> 彻底失控 完整机制)
    /// Phase 1: 玩家主动点击鼠标左键/Trigger 才能刷下一条 Feed（主动掌控感）
    /// Phase 2: 算法开始剥夺控制权，即使不点击也会自动强制切屏，玩家点击开始卡顿/失灵
    /// Phase 3: 玩家彻底失去控制权，点击完全无效，Feed 不受控制地狂乱抽搐！
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

        private Material _frontMat;
        private Material _wallMat;

        // 视频播放组件
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRenderTexture;

        private Texture _currentTex;
        private Texture _nextTex;
        private bool _isNextMediaVideo = false;

        private float _switchTimer = 0f;
        private float _currentInterval = 1.8f;
        private float _transTimer = 0f;
        private bool _isTransitioning = false;
        private int _currentModeIndex = 0; // 0: SlitScan, 1: Burst, 2: Feedback

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

            SetupVideoPlayer();

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
            _currentTex = _nextTex;
            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void SetupVideoPlayer()
        {
            GameObject vpGo = new GameObject("CorridorVideoPlayer");
            vpGo.transform.SetParent(transform, false);
            _videoPlayer = vpGo.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            _videoRenderTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _videoRenderTexture.Create();
            _videoPlayer.targetTexture = _videoRenderTexture;
        }

        private void Update()
        {
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            float time = Time.time;

            // 动态调节内容交替节奏
            UpdateRhythmTempo(phaseProgress);

            // 1. 鼠标点击与主动/被动切屏逻辑 (Phase 1 必须点击 -> Phase 2 半自动 -> Phase 3 完全失控)
            HandleControlModeAndInput(phaseProgress);

            // 2. 注视检测 (Phase 1 仅靠玩家手动点击，禁用自动加进度；Phase 2-3 启用注视与时间自动推进)
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit hit;

                if (phaseProgress >= 0.35f && phaseProgress < 0.96f)
                {
                    // Phase 2->3: 注视尽头画面推进 Phase 进度
                    if (frontWallRenderer != null && Physics.Raycast(ray, out hit, 50f))
                    {
                        if (hit.transform == frontWallRenderer.transform && Stage5Controller.Instance != null)
                        {
                            Stage5Controller.Instance.phaseProgress += Stage5Controller.Instance.progressPerSecond * Time.deltaTime * 1.5f;
                            Stage5Controller.Instance.phaseProgress = Mathf.Clamp01(Stage5Controller.Instance.phaseProgress);
                        }
                    }
                }
                else if (phaseProgress >= 0.96f)
                {
                    // Phase 4 抉择时刻：检测玩家注视哪个选择终端
                    HandlePhase4ChoiceGaze(ray);
                }
            }

            // 3. Phase 4 抉择界面
            if (phaseProgress >= 0.96f)
            {
                if (_choiceContainer == null) CreatePhase4ChoiceTerminals();
                if (_choiceContainer != null) _choiceContainer.SetActive(true);
            }
            else
            {
                if (_choiceContainer != null) _choiceContainer.SetActive(false);
            }

            // 4. 驱动 Front Wall 过渡与 Glitch
            if (_frontMat != null)
            {
                float progress = 0f;
                if (_isTransitioning)
                {
                    _transTimer += Time.deltaTime;
                    float transDur = phaseProgress > 0.70f ? 0.3f : 0.85f;
                    progress = Mathf.Clamp01(_transTimer / transDur);
                    if (_transTimer >= transDur) _isTransitioning = false;
                }

                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f : 0f;
                _frontMat.SetFloat("_TransitionProgress", progress);
                _frontMat.SetFloat("_TransitionMode", (float)_currentModeIndex);
                _frontMat.SetFloat("_GlitchIntensity", glitch);
            }

            // 5. 驱动 Side Walls 四周墙面多维流体 (Phase 1 绝对纯净 0 扭曲，Phase 2 逐渐加速侵蚀)
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

            // 6. 物理墙面震颤
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
        /// 核心掌控力剥夺逻辑
        /// Phase 1 (0 ~ 0.35): 必须按鼠标左键切屏（玩家完全掌控 Feed）
        /// Phase 2 (0.35 ~ 0.70): 即使不按，计时器到期也会自动强制切屏，玩家按键反应迟钝
        /// Phase 3 (0.70 ~ 0.96): 玩家按键彻底被忽略失灵，Feed 自动狂乱快切！
        /// </summary>
        private void HandleControlModeAndInput(float phaseProgress)
        {
            bool playerClicked = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

            if (phaseProgress < 0.35f)
            {
                // Phase 1：玩家掌控，点击才切屏
                if (playerClicked && !_isTransitioning)
                {
                    TriggerNextMediaSwitch();

                    // 每次主动刷屏，进度增加 1/24 (恰好点击 24 次进入 Phase 2，延长 200%)
                    if (Stage5Controller.Instance != null)
                    {
                        Stage5Controller.Instance.phaseProgress += 0.35f / 24f; // 约 0.01458f 每击
                        Stage5Controller.Instance.phaseProgress = Mathf.Clamp01(Stage5Controller.Instance.phaseProgress);
                    }
                }
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2：半失控模式——支持点击，但即使不点也会自动倒计时强制切屏
                _switchTimer += Time.deltaTime;
                bool timerExpired = _switchTimer >= _currentInterval;

                if ((playerClicked || timerExpired) && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    TriggerNextMediaSwitch();
                }
            }
            else
            {
                // Phase 3：完全失控模式——彻底忽略玩家点击！系统按狂乱节奏自动强切！
                _switchTimer += Time.deltaTime;
                if (_switchTimer >= _currentInterval && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    TriggerNextMediaSwitch();
                }
            }
        }

        private void TriggerNextMediaSwitch()
        {
            _isTransitioning = true;
            _transTimer = 0f;

            _currentModeIndex = Random.Range(0, 3);
            _currentTex = _nextTex;
            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void UpdateRhythmTempo(float phaseProgress)
        {
            if (phaseProgress < 0.35f)
            {
                _currentInterval = 3.2f;
            }
            else if (phaseProgress < 0.70f)
            {
                _currentInterval = Mathf.Lerp(4.5f, 7.5f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                _currentInterval = Mathf.Lerp(1.2f, 0.7f, Mathf.InverseLerp(0.70f, 0.96f, phaseProgress));
            }
        }

        private void PickNextMedia()
        {
            if (mediaDatabase == null) return;

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            string theme = GetDominantTheme();

            bool wantVideo = phaseProgress < 0.35f ? (Random.value < 0.45f) : (Random.value < 0.20f);

            if (wantVideo)
            {
                VideoClip clip = phaseProgress < 0.35f
                    ? mediaDatabase.GetEntertainmentVideo()
                    : mediaDatabase.GetThemeVideo(theme);

                if (clip != null)
                {
                    _videoPlayer.clip = clip;
                    _videoPlayer.Play();
                    _nextTex = _videoRenderTexture;
                    _isNextMediaVideo = true;
                    return;
                }
            }

            _isNextMediaVideo = false;
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
        }

        private void ApplyTexturesToWalls()
        {
            if (_currentTex == null) return;

            if (_frontMat != null)
            {
                if (_frontMat.HasProperty("_MainTex")) _frontMat.SetTexture("_MainTex", _currentTex);
                if (_frontMat.HasProperty("_NextTex")) _frontMat.SetTexture("_NextTex", _nextTex);
            }

            if (_wallMat != null)
            {
                if (_wallMat.HasProperty("_MainTex")) _wallMat.SetTexture("_MainTex", _currentTex);
                if (_wallMat.HasProperty("_BaseMap")) _wallMat.SetTexture("_BaseMap", _currentTex);
                _wallMat.mainTexture = _currentTex;
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
