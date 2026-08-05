using UnityEngine;
using UnityEngine.Video;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 高级多维 3D 走廊绑定器 (双 VideoPlayer 双缓冲零白屏 + 5 种 @elfilter_a 艺术过渡)
    /// 实现视频/图片无缝双缓冲（Double-Buffering），提前静默预加载 VideoClip，彻底解决切换白屏卡顿！
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

        // 双 VideoPlayer 预加载双缓冲系统 (彻底消灭白屏间断)
        private VideoPlayer _videoPlayerA;
        private VideoPlayer _videoPlayerB;
        private RenderTexture _renderTexA;
        private RenderTexture _renderTexB;
        private bool _activePlayerIsA = true;

        private Texture _currentTex;
        private Texture _nextTex;

        private float _switchTimer = 0f;
        private float _currentInterval = 1.8f;
        private float _transTimer = 0f;
        private bool _isTransitioning = false;
        private int _currentModeIndex = 0; // 0:Grid, 1:Strips, 2:Fluid, 3:Portal, 4:Data

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
            _currentTex = _nextTex;
            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void SetupDoubleBufferedVideoPlayers()
        {
            // Player A
            GameObject vpGoA = new GameObject("CorridorVideoPlayer_A");
            vpGoA.transform.SetParent(transform, false);
            _videoPlayerA = vpGoA.AddComponent<VideoPlayer>();
            _videoPlayerA.playOnAwake = false;
            _videoPlayerA.isLooping = true;
            _videoPlayerA.renderMode = VideoRenderMode.RenderTexture;
            _renderTexA = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _renderTexA.Create();
            _videoPlayerA.targetTexture = _renderTexA;

            // Player B
            GameObject vpGoB = new GameObject("CorridorVideoPlayer_B");
            vpGoB.transform.SetParent(transform, false);
            _videoPlayerB = vpGoB.AddComponent<VideoPlayer>();
            _videoPlayerB.playOnAwake = false;
            _videoPlayerB.isLooping = true;
            _videoPlayerB.renderMode = VideoRenderMode.RenderTexture;
            _renderTexB = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
            _renderTexB.Create();
            _videoPlayerB.targetTexture = _renderTexB;
        }

        private void Update()
        {
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            float time = Time.time;

            UpdateRhythmTempo(phaseProgress);
            HandleControlModeAndInput(phaseProgress);

            // 注视检测
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit hit;

                if (phaseProgress >= 0.35f && phaseProgress < 0.96f)
                {
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
                    HandlePhase4ChoiceGaze(ray);
                }
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

            // 驱动 Front Wall 过渡
            if (_frontMat != null)
            {
                float progress = 0f;
                if (_isTransitioning)
                {
                    _transTimer += Time.deltaTime;
                    float transDur = phaseProgress > 0.70f ? 0.3f : 0.75f;
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

        private void HandleControlModeAndInput(float phaseProgress)
        {
            bool playerClicked = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

            if (phaseProgress < 0.35f)
            {
                if (playerClicked && !_isTransitioning)
                {
                    TriggerNextMediaSwitch();

                    if (Stage5Controller.Instance != null)
                    {
                        Stage5Controller.Instance.phaseProgress += 0.35f / 24f;
                        Stage5Controller.Instance.phaseProgress = Mathf.Clamp01(Stage5Controller.Instance.phaseProgress);
                    }
                }
            }
            else if (phaseProgress < 0.70f)
            {
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

            // 随机从 5 种 @elfilter_a 艺术模式中抽取
            _currentModeIndex = Random.Range(0, 5);
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

        /// <summary>
        /// 双缓冲无缝预加载系统 (Double-Buffered Seamless Video Preloader)
        /// 轮流使用 VideoPlayer A 和 VideoPlayer B，彻底消灭视频解压加载造成的白屏空档！
        /// </summary>
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
                    // 切换备用 VideoPlayer 预加载播放
                    _activePlayerIsA = !_activePlayerIsA;
                    VideoPlayer activePlayer = _activePlayerIsA ? _videoPlayerA : _videoPlayerB;
                    RenderTexture activeTex = _activePlayerIsA ? _renderTexA : _renderTexB;

                    activePlayer.clip = clip;
                    activePlayer.Play();

                    _nextTex = activeTex;
                    return;
                }
            }

            // 静态图片素材无缝加载
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
