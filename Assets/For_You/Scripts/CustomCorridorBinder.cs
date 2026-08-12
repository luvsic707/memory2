using System.Collections.Generic;
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

        [Tooltip("是否显示正面 Quad 墙面 (默认 false 彻底隐藏遮挡视野的巨型绿墙)")]
        public bool showFrontWall = false;

        [Tooltip("走廊四周的 4 面墙体 Cube（左、右、天花板、地面）")]
        public Renderer[] sideWallRenderers;

        [Header("保留与拆解四周 Cube 墙体外壳 (Keep Solid Box Shell)")]
        [Tooltip("保留周围 4 面 Cube 墙体来承载主要视频内容 (设置为 false 完整保留 Scene 里的 Cube 墙面)")]
        public bool disableSolidBoxShell = false;

        [Header("多宫格 3D 视频矩阵走廊 (Multi-Panel Video Matrix Corridor)")]
        [Tooltip("启用 3D 多宫格错落视频画廊墙（模仿参考视频中贴满走廊四周的多画面排列效果）")]
        public bool enableMultiPanelVideoMatrix = true;

        [Tooltip("走廊四周多宫格视频/图像面板的数量 (默认 16 块错落贴于左、右、天花板、地面)")]
        public int multiPanelCount = 16;

        private List<GameObject> _matrixPanels = new List<GameObject>();

        [Header("走廊穿梭流动性控制 (Continuous Forward Flow Fly)")]
        [Tooltip("启用走廊无限向前平滑穿梭流动（模仿参考视频中的无缝推进感）")]
        public bool enableContinuousForwardFly = true;

        [Tooltip("向前穿梭流动的基础速度 (米/秒，Inspector 自由调速)")]
        public float forwardFlySpeed = 2.2f;

        [Header("管道弯曲动势与倾斜控制 (Curved Tunnel Flow)")]
        [Tooltip("启用走廊向 Player 涌现时的拐弯与 S 曲线动势（平滑微弯，极高可读性）")]
        public bool enableTunnelCurvingTrends = true;

        [Tooltip("走廊拐弯弯曲振幅 (米，保持平缓高可读性)")]
        [Range(0f, 1.2f)] public float tunnelCurveAmplitude = 0.35f;

        [Tooltip("走廊轻微扭曲角度 (度)")]
        [Range(0f, 15f)] public float tunnelTwistAngle = 4.0f;

        private Vector3[] _basePanelPositions;
        private Quaternion[] _basePanelRotations;

        [Header("物理墙体震颤")]
        [Tooltip("物理墙体在 Phase 3 的震颤强度 (降低强度保持画面平稳)")]
        public float physicalWarpIntensity = 0.05f;

        [Header("Shader 视觉特效强度手动配置 (Inspector 自由调校)")]
        [Tooltip("漩涡扭曲强度上限 (设置为 0 彻底关闭漩涡拉扯)")]
        [Range(0f, 1.5f)] public float maxVortexAmount = 0.0f;

        [Tooltip("油彩弧形抹平强度上限 (默认 0.06 保持画面平整)")]
        [Range(0f, 1.5f)] public float maxOilSmearArc = 0.06f;

        [Tooltip("极速拖尾模糊强度上限 (设置为 0 彻底关闭模糊重影)")]
        [Range(0f, 1.5f)] public float maxSpeedTrails = 0.0f;

        [Tooltip("Glitch 像素故障块强度上限")]
        [Range(0f, 1.0f)] public float maxGlitchAmount = 0.08f;

        [Tooltip("RGB 色偏分色强度上限")]
        [Range(0f, 0.05f)] public float maxRgbShift = 0.006f;

        [Tooltip("波浪弯曲扭曲强度上限")]
        [Range(0f, 1.5f)] public float maxWaveWarp = 0.12f;

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
        private Vector3 _initialFrontScale = Vector3.one;

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
            if (mediaDatabase == null && Stage5Controller.Instance != null)
            {
                mediaDatabase = Stage5Controller.Instance.mediaDatabase;
            }
            if (mediaDatabase == null)
            {
                mediaDatabase = FindObjectOfType<CardMediaDatabase>();
            }

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

            if (frontWallRenderer != null)
            {
                frontWallRenderer.material = _frontMat;
                _initialFrontScale = frontWallRenderer.transform.localScale;
                frontWallRenderer.enabled = showFrontWall;

                if (!showFrontWall)
                {
                    Vector3 p = frontWallRenderer.transform.position;
                    p.z = 80.0f;
                    frontWallRenderer.transform.position = p;
                }
            }
            // 🌟 强力搜寻场景中所有的 Cube (包含 Cube, Cube (1) ~ Cube (5) 全部 6 个墙体)
            List<Renderer> foundRenderers = new List<Renderer>();
            GameObject[] sceneGos = FindObjectsOfType<GameObject>();
            foreach (var go in sceneGos)
            {
                if (go != null && go.name.StartsWith("Cube") && go.name != "Invisible_Static_Safety_Floor")
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null) foundRenderers.Add(r);
                }
            }
            sideWallRenderers = foundRenderers.ToArray();
            Debug.Log($"<color=cyan>[CustomCorridorBinder] 强力搜寻并注入场景中全套 {sideWallRenderers.Length} 个 Cube 墙面！(包含 Cube, Cube (1)~Cube (5))</color>");

            // 🌟 核心修正：自动测算 Player 位置与走廊 Z 轴偏差，将全套 Cube 墙体对齐包裹住 Player！
            Transform playerT = Camera.main != null ? Camera.main.transform : null;
            if (playerT != null && sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                float minZ = float.MaxValue;
                foreach (var r in sideWallRenderers)
                {
                    if (r != null)
                    {
                        float z = r.transform.position.z;
                        if (z < minZ) minZ = z;
                    }
                }

                float zShift = (playerT.position.z - 2.0f) - minZ;
                if (Mathf.Abs(zShift) > 1.0f)
                {
                    foreach (var r in sideWallRenderers)
                    {
                        if (r != null)
                        {
                            Vector3 p = r.transform.position;
                            p.z += zShift;
                            r.transform.position = p;
                        }
                    }
                    Debug.Log($"<color=green>[CustomCorridorBinder] 成功将场景中偏远 {minZ:F1}m 的 Cube 墙体全自动吸附对齐到 Player 身旁 (平移 {zShift:F1}m)！</color>");
                }
            }

            if (sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                _initialWallPositions = new Vector3[sideWallRenderers.Length];
                _initialWallRotations = new Quaternion[sideWallRenderers.Length];
                _initialWallScales = new Vector3[sideWallRenderers.Length];

                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
                if (unlitShader == null) unlitShader = Shader.Find("Standard");

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        Material wallMatInst = new Material(unlitShader);
                        Texture tex = (mediaDatabase != null) ? mediaDatabase.GetEntertainmentTexture() : null;
                        if (tex != null)
                        {
                            if (wallMatInst.HasProperty("_MainTex")) wallMatInst.SetTexture("_MainTex", tex);
                            if (wallMatInst.HasProperty("_BaseMap")) wallMatInst.SetTexture("_BaseMap", tex);
                            wallMatInst.mainTexture = tex;
                        }
                        if (wallMatInst.HasProperty("_Color")) wallMatInst.SetColor("_Color", Color.white);
                        if (wallMatInst.HasProperty("_BaseColor")) wallMatInst.SetColor("_BaseColor", Color.white);

                        sideWallRenderers[i].material = wallMatInst;
                        _initialWallPositions[i] = sideWallRenderers[i].transform.localPosition;
                        _initialWallRotations[i] = sideWallRenderers[i].transform.localRotation;
                        _initialWallScales[i] = sideWallRenderers[i].transform.localScale;

                        sideWallRenderers[i].enabled = true;
                    }
                }
                Debug.Log("<color=green>[CustomCorridorBinder] 成功为场景中所有 Cube 墙面强力投影 mediaDatabase 媒体素材！</color>");
            }

            CreateInvisibleGroundFloor();
            CreateFarEndCapWall();
            CreatePerfectCorridorPlanes();
            CreateMultiPanelVideoMatrix();

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
            ApplyTexturesToWalls();

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

                // 实时联动 Inspector 手动配置的 6 大 Shader 特效滑块！
                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * maxGlitchAmount : 0f;
                float borderFade = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.20f : 0f;

                bool isP1 = phaseProgress < 0.35f;
                float p2R = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float p3R = Mathf.InverseLerp(0.70f, 1.00f, phaseProgress);

                // 正面墙：实时响应 Inspector 中的油彩抹平与极速拖尾滑块
                float frontOilSmear = isP1 ? 0.02f : (0.02f + p2R * (maxOilSmearArc * 0.5f) + p3R * maxOilSmearArc);
                float frontSpeedTrails = isP1 ? 0f : (p2R * (maxSpeedTrails * 0.5f) + p3R * maxSpeedTrails);

                _frontMat.SetFloat("_TransitionProgress", progress);
                _frontMat.SetFloat("_TransitionMode", (float)_currentModeIndex);
                _frontMat.SetFloat("_GlitchIntensity", glitch);
                _frontMat.SetFloat("_BorderFade", borderFade);
                _frontMat.SetFloat("_OilSmearArc", frontOilSmear);
                _frontMat.SetFloat("_ExplosiveRadialTrails", frontSpeedTrails);
            }

            // 正面墙 (Front Wall) 严格保持其原本封闭尽头的大尺寸，并基于初始 Scale 进行微呼吸！
            if (frontWallRenderer != null)
            {
                frontWallRenderer.enabled = showFrontWall;
                if (showFrontWall)
                {
                    float breathe = Mathf.Sin(time * 1.5f) * 0.02f;
                    frontWallRenderer.transform.localScale = _initialFrontScale * (1.0f + breathe);
                }
            }

            // 5. 驱动 Side Walls 四周墙面多维流体 (实时相应 Inspector 滑块)
            if (_wallMat != null)
            {
                bool isPhase1 = phaseProgress < 0.35f;

                float p2Ratio = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float p3Ratio = Mathf.InverseLerp(0.70f, 1.00f, phaseProgress);

                float jelly = isPhase1 ? 0.2f : Mathf.Lerp(0.2f, 0.05f, p2Ratio);
                float speed = isPhase1 ? 0.4f : (0.4f + p2Ratio * 0.4f + p3Ratio * 0.6f);

                // 实时联动 Inspector 中的 6 大 Shader 滑块
                float glitch = isPhase1 ? 0.02f : (0.02f + p2Ratio * (maxGlitchAmount * 0.5f) + p3Ratio * maxGlitchAmount);
                float rgbShift = isPhase1 ? 0.002f : (0.002f + p2Ratio * (maxRgbShift * 0.5f) + p3Ratio * maxRgbShift);
                float waveWarp = isPhase1 ? 0.05f : (0.05f + p2Ratio * (maxWaveWarp * 0.5f) + p3Ratio * maxWaveWarp);

                float angle = isPhase1 ? 0.05f : (0.05f + Mathf.Sin(time * 0.3f) * 0.1f);
                float vortex = isPhase1 ? 0f : (p2Ratio * (maxVortexAmount * 0.5f) + p3Ratio * maxVortexAmount);
                float sliceShift = isPhase1 ? 0.02f : (0.02f + p2Ratio * 0.04f + p3Ratio * 0.08f);
                
                float borderFade = isPhase1 ? 0f : (p2Ratio * 0.05f + p3Ratio * 0.15f);
                float oilSmear = isPhase1 ? 0.02f : (0.02f + p2Ratio * (maxOilSmearArc * 0.5f) + p3Ratio * maxOilSmearArc);
                float speedTrails = isPhase1 ? 0f : (p2Ratio * (maxSpeedTrails * 0.5f) + p3Ratio * maxSpeedTrails);

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
                // 🌟 大幅减轻 Phase 3 物理变形抖动，保持画面平稳高可读性
                float physIntensity = phaseProgress > 0.70f ? Mathf.InverseLerp(0.70f, 1.0f, phaseProgress) * (physicalWarpIntensity * 0.2f) : 0f;

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        // 1. 严格基于你在 Scene 里搭好的初始 Scale 进行 5% 微小软呼吸
                        Vector3 baseScale = _initialWallScales[i];
                        Vector3 jellyScale = baseScale;
                        if (phaseProgress < 0.70f)
                        {
                            float breathe = Mathf.Sin(time * 1.5f + i * 0.8f) * 0.02f;
                            jellyScale = new Vector3(baseScale.x * (1.0f + breathe), baseScale.y * (1.0f - breathe), baseScale.z * (1.0f + breathe * 0.5f));
                        }
                        sideWallRenderers[i].transform.localScale = jellyScale;

                        // 2. 极轻微的平稳浮动（绝无剧烈晃动）
                        Vector3 waveOffset = new Vector3(
                            Mathf.Sin(time * 1.5f + i) * 0.015f,
                            Mathf.Cos(time * 1.2f + i) * 0.015f,
                            Mathf.Sin(time * 1.0f + i) * 0.010f
                        ) * (isPhase1 ? 0.2f : (0.2f + physIntensity));

                        sideWallRenderers[i].transform.localPosition = _initialWallPositions[i] + waveOffset;
                        float angleOffset = Mathf.Sin(time * 2.0f + i) * 0.4f * physIntensity;
                        sideWallRenderers[i].transform.localRotation = _initialWallRotations[i] * Quaternion.Euler(angleOffset, 0f, angleOffset);
                    }
                }
            }

            // 🌟 核心突破：真正无缝、绝对无闪烁复位的无限隧道推进 (True Seamless Infinite Tunnel Flow)
            if (enableContinuousForwardFly)
            {
                float flySpeed = forwardFlySpeed * (1.0f + phaseProgress * 0.7f);
                _cumulativeFlyZ += flySpeed * Time.deltaTime;

                // 摄像机零闪烁、零跳变，依靠墙面动态 UV 贴图与流体 Shader 在视觉上形成 100% 顺滑无限延伸推进！
                if (_wallMat != null)
                {
                    _wallMat.SetFloat("_FlowSpeed", flySpeed * 0.6f);
                }
            }

            // 🌟 驱动多宫格 3D 视频面板沿着走廊四周流畅后退流逝，并带有弯曲拐弯 S 曲线动势！
            if (enableMultiPanelVideoMatrix && enableContinuousForwardFly)
            {
                Camera mainCam = Camera.main;
                float streamSpeed = forwardFlySpeed * (1.0f + phaseProgress * 0.7f);

                for (int i = 0; i < _matrixPanels.Count; i++)
                {
                    if (_matrixPanels[i] != null)
                    {
                        Vector3 currentPos = _matrixPanels[i].transform.position;
                        currentPos.z -= streamSpeed * Time.deltaTime;

                        // 管道弯曲 S 曲线动势与微扭曲 (Curved & Bending Tunnel Trend - 极高可读性)
                        if (enableTunnelCurvingTrends)
                        {
                            float z = currentPos.z;
                            float curveX = Mathf.Sin(time * 0.7f + z * 0.15f) * tunnelCurveAmplitude;
                            float curveY = Mathf.Cos(time * 0.5f + z * 0.12f) * (tunnelCurveAmplitude * 0.6f);
                            float twist = Mathf.Sin(time * 0.4f + z * 0.10f) * tunnelTwistAngle;

                            Vector3 baseP = (_basePanelPositions != null && i < _basePanelPositions.Length) ? _basePanelPositions[i] : currentPos;
                            currentPos.x = baseP.x + curveX;
                            currentPos.y = baseP.y + curveY;

                            Quaternion baseR = (_basePanelRotations != null && i < _basePanelRotations.Length) ? _basePanelRotations[i] : Quaternion.identity;
                            _matrixPanels[i].transform.rotation = baseR * Quaternion.Euler(0f, 0f, twist);
                        }

                        _matrixPanels[i].transform.position = currentPos;

                        if (mainCam != null && _matrixPanels[i].transform.position.z < mainCam.transform.position.z - 3.5f)
                        {
                            Vector3 p = _matrixPanels[i].transform.position;
                            p.z += (multiPanelCount / 4) * 4.5f; // 无缝重新排列到前方 18~20 米深处！
                            _matrixPanels[i].transform.position = p;

                            // 动态换上一张全新的媒体视频/图片素材！
                            MeshRenderer mr = _matrixPanels[i].GetComponent<MeshRenderer>();
                            if (mr != null && mediaDatabase != null)
                            {
                                Texture nextTex = mediaDatabase.GetEntertainmentTexture();
                                if (nextTex != null)
                                {
                                    if (mr.material.HasProperty("_MainTex")) mr.material.SetTexture("_MainTex", nextTex);
                                    if (mr.material.HasProperty("_BaseMap")) mr.material.SetTexture("_BaseMap", nextTex);
                                }
                            }
                        }
                    }
                }
            }
        }

        private Renderer _farEndCapRenderer;
        private Renderer[] _perfectWallPlanes;

        /// <summary>
        /// 在走廊极尽头 (Z = 13.8m, Y = 1.0m) 创建 10x10 巨型高清媒体封底墙，彻底无缝封死尽头黑洞！
        /// </summary>
        private void CreateFarEndCapWall()
        {
            GameObject endCapGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            endCapGo.name = "Tunnel_FarEndCap_Wall";
            endCapGo.transform.SetParent(transform, false);

            endCapGo.transform.position = new Vector3(0f, 1.0f, 13.8f);
            endCapGo.transform.rotation = Quaternion.identity;
            endCapGo.transform.localScale = new Vector3(10.0f, 10.0f, 1.0f);

            Collider col = endCapGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _farEndCapRenderer = endCapGo.GetComponent<MeshRenderer>();
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
            if (unlitShader == null) unlitShader = Shader.Find("Standard");

            Material mat = new Material(unlitShader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            _farEndCapRenderer.material = mat;
            Debug.Log("<color=green>[CustomCorridorBinder] 成功创建走廊极尽头高清 10x10 媒体封底墙，零缝隙封死尽头！</color>");
        }

        /// <summary>
        /// 创建 4 面绝对吻合、方向端正、高清平整的 3D 走廊墙面 (左、右、顶、地)
        /// </summary>
        private void CreatePerfectCorridorPlanes()
        {
            GameObject wallRoot = new GameObject("PerfectCorridorPlanes_Root");
            wallRoot.transform.SetParent(transform, false);

            _perfectWallPlanes = new Renderer[4];

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");

            for (int i = 0; i < 4; i++)
            {
                GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plane.name = $"PerfectWallPlane_{i}";
                plane.transform.SetParent(wallRoot.transform, false);

                Vector3 pos = Vector3.zero;
                Quaternion rot = Quaternion.identity;
                Vector3 scale = new Vector3(16.0f, 4.0f, 1.0f);

                switch (i)
                {
                    case 0: // 左墙
                        pos = new Vector3(-2.45f, 1.0f, 6.0f);
                        rot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                    case 1: // 右墙
                        pos = new Vector3(2.45f, 1.0f, 6.0f);
                        rot = Quaternion.Euler(0f, -90f, 0f);
                        break;
                    case 2: // 天花板
                        pos = new Vector3(0f, 3.0f, 6.0f);
                        rot = Quaternion.Euler(90f, 0f, 0f);
                        scale = new Vector3(4.9f, 16.0f, 1.0f);
                        break;
                    case 3: // 地面
                    default:
                        pos = new Vector3(0f, -1.0f, 6.0f);
                        rot = Quaternion.Euler(-90f, 0f, 0f);
                        scale = new Vector3(4.9f, 16.0f, 1.0f);
                        break;
                }

                plane.transform.position = pos;
                plane.transform.rotation = rot;
                plane.transform.localScale = scale;

                Collider col = plane.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = plane.GetComponent<MeshRenderer>();
                Material mat = new Material(unlitShader);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

                mr.material = mat;
                _perfectWallPlanes[i] = mr;
            }
            Debug.Log("<color=green>[CustomCorridorBinder] 成功构建 4 面绝对吻合、方向端正的 3D 走廊墙面！</color>");
        }

        /// <summary>
        /// 模仿参考视频：将 16+ 块视频/图像面板错落贴在走廊左、右、天花板、地面四周！
        /// </summary>
        private void CreateMultiPanelVideoMatrix()
        {
            GameObject matrixRoot = new GameObject("MultiPanelVideoMatrix_Root");
            matrixRoot.transform.SetParent(transform, false);

            _basePanelPositions = new Vector3[multiPanelCount];
            _basePanelRotations = new Quaternion[multiPanelCount];

            for (int i = 0; i < multiPanelCount; i++)
            {
                GameObject panelGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panelGo.name = $"VideoPanel_Tile_{i}";
                panelGo.transform.SetParent(matrixRoot.transform, false);

                int side = i % 4;
                float zPos = 1.0f + (i / 4) * 2.2f + Random.Range(-0.3f, 0.3f);

                Vector3 pos = Vector3.zero;
                Quaternion rot = Quaternion.identity;
                Vector3 scale = new Vector3(2.2f, 1.25f, 1f); // 16:9 标准高清无变形比例

                switch (side)
                {
                    case 0: // 左墙贴片
                        pos = new Vector3(-2.42f, Random.Range(-0.8f, 0.8f), zPos);
                        rot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                    case 1: // 右墙贴片
                        pos = new Vector3(2.42f, Random.Range(-0.8f, 0.8f), zPos);
                        rot = Quaternion.Euler(0f, -90f, 0f);
                        break;
                    case 2: // 天花板贴片
                        pos = new Vector3(Random.Range(-0.8f, 0.8f), 2.42f, zPos);
                        rot = Quaternion.Euler(90f, 0f, 0f);
                        scale = new Vector3(2.2f, 1.25f, 1f);
                        break;
                    case 3: // 地面贴片
                    default:
                        pos = new Vector3(Random.Range(-0.8f, 0.8f), -2.42f, zPos);
                        rot = Quaternion.Euler(-90f, 0f, 0f);
                        scale = new Vector3(2.2f, 1.25f, 1f);
                        break;
                }

                panelGo.transform.position = pos;
                panelGo.transform.rotation = rot;
                panelGo.transform.localScale = scale;

                Collider col = panelGo.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = panelGo.GetComponent<MeshRenderer>();
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
                Material mat = new Material(unlitShader);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

                if (mediaDatabase != null)
                {
                    Texture tex = null;
                    int r = i % 5;
                    if (r == 0) tex = mediaDatabase.GetEntertainmentTexture();
                    else if (r == 1) tex = mediaDatabase.GetThemeTexture("banana");
                    else if (r == 2) tex = mediaDatabase.GetThemeTexture("prayer");
                    else if (r == 3) tex = mediaDatabase.GetThemeTexture("push");
                    else tex = mediaDatabase.GetThemeTexture("work");

                    if (tex != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        mat.mainTexture = tex;
                    }
                }

                mr.material = mat;
                _matrixPanels.Add(panelGo);
                _basePanelPositions[i] = pos;
                _basePanelRotations[i] = rot;
            }
            Debug.Log($"<color=cyan>[MultiPanelMatrix] 成功搭建 16 宫格 3D 走廊错落视频画廊墙（具备 S 曲线管道动势）！</color>");
        }

        private float _cumulativeFlyZ = 0f;

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
                // Phase 3: 大幅放缓切屏频率至 3.2s ~ 2.5s，确保画质与内容的可读性
                _currentInterval = Mathf.Lerp(3.2f, 2.5f, Mathf.InverseLerp(0.70f, 1.00f, phaseProgress));
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
                if (texA == null && mediaDatabase != null) texA = mediaDatabase.GetEntertainmentTexture();
                if (texB == null && mediaDatabase != null) texB = mediaDatabase.GetEntertainmentTexture();

                if (_frontMat.HasProperty("_MainTex") && texA != null) _frontMat.SetTexture("_MainTex", texA);
                if (_frontMat.HasProperty("_NextTex") && texB != null) _frontMat.SetTexture("_NextTex", texB);
                if (_frontMat.HasProperty("_BaseMap") && texA != null) _frontMat.SetTexture("_BaseMap", texA);
                _frontMat.mainTexture = texA;
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

            // 🌟 强力为四周 6 个 Cube 墙体与悬浮视频贴片赋上高清视频与画报 Texture，确保四周 100% 满布动态媒体画面！
            if (sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        sideWallRenderers[i].enabled = true;
                        Material mat = sideWallRenderers[i].material;
                        if (mat != null)
                        {
                            Texture targetTex = null;
                            if (mediaDatabase != null)
                            {
                                switch (i % 5)
                                {
                                    case 0: targetTex = mediaDatabase.GetEntertainmentTexture(); break;
                                    case 1: targetTex = mediaDatabase.GetThemeTexture("banana"); break;
                                    case 2: targetTex = mediaDatabase.GetThemeTexture("prayer"); break;
                                    case 3: targetTex = mediaDatabase.GetThemeTexture("push"); break;
                                    case 4: default: targetTex = mediaDatabase.GetThemeTexture("work"); break;
                                }
                            }
                            if (targetTex == null) targetTex = (i % 2 == 0) ? texA : texB;

                            if (targetTex != null)
                            {
                                if (mat.HasProperty("_MainTex"))
                                {
                                    mat.SetTexture("_MainTex", targetTex);
                                    mat.SetTextureScale("_MainTex", Vector2.one);
                                }
                                if (mat.HasProperty("_BaseMap"))
                                {
                                    mat.SetTexture("_BaseMap", targetTex);
                                    mat.SetTextureScale("_BaseMap", Vector2.one);
                                }
                                mat.mainTexture = targetTex;
                            }
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                        }
                    }
                }
            }

            if (_farEndCapRenderer != null)
            {
                Texture endCapTex = texA != null ? texA : (texB != null ? texB : (mediaDatabase != null ? mediaDatabase.GetEntertainmentTexture() : null));
                if (endCapTex != null)
                {
                    Material mat = _farEndCapRenderer.material;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", endCapTex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", endCapTex);
                        mat.mainTexture = endCapTex;
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    }
                }
            }

            if (_perfectWallPlanes != null && _perfectWallPlanes.Length > 0)
            {
                for (int i = 0; i < _perfectWallPlanes.Length; i++)
                {
                    if (_perfectWallPlanes[i] != null)
                    {
                        Material mat = _perfectWallPlanes[i].material;
                        if (mat != null)
                        {
                            Texture targetTex = null;
                            if (mediaDatabase != null)
                            {
                                switch (i)
                                {
                                    case 0: targetTex = mediaDatabase.GetEntertainmentTexture(); break;
                                    case 1: targetTex = mediaDatabase.GetThemeTexture("work"); break;
                                    case 2: targetTex = mediaDatabase.GetThemeTexture("push"); break;
                                    case 3: default: targetTex = mediaDatabase.GetThemeTexture("banana"); break;
                                }
                            }
                            if (targetTex == null) targetTex = (i % 2 == 0) ? texA : texB;

                            if (targetTex != null)
                            {
                                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", targetTex);
                                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", targetTex);
                                mat.mainTexture = targetTex;
                            }
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                        }
                    }
                }
            }
        }

        private void EnsureResistanceUI()
        {
            if (_resistanceUiGo == null && frontWallRenderer != null)
            {
                _resistanceUiGo = new GameObject("Phase3_ResistanceUI");
                _resistanceUiGo.transform.SetParent(frontWallRenderer.transform, false);
                _resistanceUiGo.transform.localPosition = new Vector3(0f, 0.45f, -0.1f);

                _resistanceTmp = _resistanceUiGo.AddComponent<TextMeshPro>();
                _resistanceTmp.fontSize = 0.28f;
                _resistanceTmp.alignment = TextAlignmentOptions.Center;
                _resistanceTmp.color = new Color(1.0f, 0.9f, 0.4f, 0.75f);
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
