using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 数据驱动的四阶段强度曲线参数：Phase1 全程保持基线值，
    /// Phase2/3/4 各自的区间内从上一阶段数值平滑过渡到本阶段目标值。
    /// 断点固定在 0.35 / 0.70 / 0.96（与全项目其余 Phase 判定逻辑保持一致）。
    /// </summary>
    [System.Serializable]
    public class PhaseCurveParam
    {
        public float phase1 = 0f;
        public float phase2 = 0f;
        public float phase3 = 0f;
        public float phase4 = 0f;

        public float Evaluate(float progress)
        {
            if (progress < 0.35f) return phase1;
            if (progress < 0.70f) return Mathf.Lerp(phase1, phase2, Mathf.InverseLerp(0.35f, 0.70f, progress));
            if (progress < 0.96f) return Mathf.Lerp(phase2, phase3, Mathf.InverseLerp(0.70f, 0.96f, progress));
            return Mathf.Lerp(phase3, phase4, Mathf.InverseLerp(0.96f, 1.00f, progress));
        }
    }

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

        [Tooltip("是否显示正面走廊前墙 (true = 显示贴图媒体墙，false = 隐藏)")]
        public bool showFrontWall = true;

        [Tooltip("走廊四周的 4 面墙体 Cube（左、右、天花板、地面）")]
        public Renderer[] sideWallRenderers;

        [Header("保留与拆解四周 Cube 墙体外壳 (Keep Solid Box Shell)")]
        [Tooltip("保留周围 4 面 Cube 墙体来承载主要视频内容 (设置为 false 完整保留 Scene 里的 Cube 墙面)")]
        public bool disableSolidBoxShell = false;

        [Header("多宫格 3D 视频矩阵走廊 (Multi-Panel Video Matrix Corridor)")]
        [Tooltip("启用 3D 多宫格错落视频画廊墙（现已绑定 CorridorWallShader，默认开启以避免图片/视频卡片看起来像硬边相片框）")]
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

        [Header("走廊有机呼吸摆动 (Organic Breathing Sway - 让整条走廊像活的一样呼吸摇摆)")]
        [Tooltip("启用整条走廊的有机呼吸摆动（左右/上下漂移 + 轻微扭转 + 逐墙呼吸缩放），替代原本完全锁死的死板箱体")]
        public bool enableOrganicSway = true;

        [Tooltip("左右漂移振幅 (米)")]
        [Range(0f, 0.5f)] public float swayAmplitudeX = 0.24f;

        [Tooltip("上下漂移振幅 (米)")]
        [Range(0f, 0.5f)] public float swayAmplitudeY = 0.18f;

        [Tooltip("漂移摆动频率 (越小越缓慢梦幻)")]
        public float swayFrequency = 0.35f;

        [Tooltip("整体扭转角度振幅 (度)")]
        [Range(0f, 15f)] public float twistAmplitude = 8.0f;

        [Tooltip("扭转摆动频率")]
        public float twistFrequency = 0.22f;

        [Tooltip("每面墙独立呼吸缩放的幅度 (0.025 = ±2.5%)")]
        [Range(0f, 0.15f)] public float wallBreatheAmplitude = 0.06f;

        [Tooltip("每面墙呼吸缩放的频率")]
        public float wallBreatheFrequency = 0.6f;

        [Tooltip("向前推进速度的呼吸脉冲幅度 (0.18 = 速度在 ±18% 间波动)")]
        [Range(0f, 0.5f)] public float flowSpeedPulseAmplitude = 0.18f;

        [Tooltip("推进速度脉冲频率")]
        public float flowSpeedPulseFrequency = 0.5f;

        [Tooltip("每面墙/视频面板细分网格密度 (数值越大波浪起伏越平滑，顶点数量也越多，默认 10 对走廊墙面足够顺滑)")]
        [Range(2, 24)] public int wallMeshSubdivisions = 10;

        [Tooltip("Jelly 顶点波浪位移幅度 (米，直接决定壁面物理起伏的真实幅度，需要达到 0.1~0.5 米才能在米级走廊中看得见)")]
        [Range(0f, 1f)] public float jellyDisplacementScale = 0.4f;

        [Header("隧道飞驰速度流粒子 (Tunnel Speed Streaks — 已默认关闭，效果不好看，改用下方的分段循环隧道方案)")]
        [Tooltip("启用迎面而来的速度流粒子（拉丝效果）——默认已关闭，因为实际效果是一组硬直的放射线，不好看")]
        public bool enableSpeedStreaks = false;

        [Tooltip("同时存在的速度流粒子数量上限")]
        public int speedStreakCount = 140;

        [Tooltip("粒子从多远处生成（米，越大能看到粒子越早开始'接近'）")]
        public float speedStreakSpawnDistance = 16f;

        [Tooltip("粒子飞向摄像机的基础速度（米/秒）")]
        public float speedStreakBaseSpeed = 6f;

        [Tooltip("粒子拉丝长度倍率（速度越快拉丝越长，制造经典星际穿越感）")]
        [Range(0f, 3f)] public float speedStreakLengthScale = 1.2f;

        [Tooltip("粒子横向散布半径（米，大致对应走廊内部截面大小）")]
        public float speedStreakSpreadRadius = 1.8f;

        [Tooltip("粒子颜色（默认柔和白色，低不透明度不抢内容风头）")]
        public Color speedStreakColor = new Color(1f, 1f, 1f, 0.5f);

        private ParticleSystem _speedStreakSystem;

        [Header("🌟 真正的隧道穿梭错觉 (Segmented Recycling Tunnel — 实验性功能，默认已关闭)")]
        [Tooltip("启用分段循环走廊——默认已关闭，因为效果调试难度过高，暂时回退到固定 Cube 墙体方案（但 Jelly/Vortex 等 shader 改进依旧保留）")]
        public bool enableSegmentedTunnel = false;

        [Tooltip("循环走廊分成几段（越多越顺滑，但物体数量也越多）")]
        [Range(4, 24)] public int tunnelSegmentCount = 10;

        [Tooltip("每段走廊的长度（米）")]
        public float tunnelSegmentLength = 2.2f;

        [Tooltip("是否在创建分段循环走廊后，自动隐藏原本静止的 6 面大 Cube 墙体（仅关闭渲染，Collider 保留作为安全网）")]
        public bool hideOldWallsWhenSegmentedTunnelActive = true;

        private List<GameObject> _tunnelSegments = new List<GameObject>();
        private List<Material[]> _tunnelSegmentMaterials = new List<Material[]>();

        [Header("🛠️ 走廊墙体与封底 Inspector 手动微调参数 (Real-Time Manual Alignment)")]
        [Tooltip("走廊宽度半程 (米，默认 2.45 米精确贴合)")]
        public float wallHalfWidth = 2.45f;

        [Tooltip("走廊高度 offset (米，默认 Y=1.0 米)")]
        public float wallCenterY = 1.0f;

        [Tooltip("天花板高度 (米，默认 Y=3.0 米)")]
        public float ceilingY = 3.0f;

        [Tooltip("地面高度 (米，默认 Y=-1.0 米)")]
        public float floorY = -1.0f;

        [Tooltip("走廊整体 Z 轴偏移量 (米，默认 Z=6.0 米)")]
        public float wallCenterZ = 6.0f;

        [Tooltip("尽头封底墙 Z 轴位置 (米，默认 Z=13.8 米)")]
        public float endCapZ = 13.8f;

        [Tooltip("尽头封底墙尺寸 (米，默认 10.0 米)")]
        public float endCapSize = 10.0f;

        private Vector3[] _basePanelPositions;
        private Quaternion[] _basePanelRotations;

        [Header("🎛️ Phase 视觉强度曲线 (Data-Driven：每个参数独立设置 4 个阶段目标值，可随时在 Inspector 里重新设计整条曲线，下面这套新系统已完全取代旧的 maxXXX 单一上限方案)")]
        [Tooltip("Jelly 形变强度")]
        public PhaseCurveParam jellyCurve = new PhaseCurveParam { phase1 = 0.15f, phase2 = 0.5f, phase3 = 1.1f, phase4 = 1.6f };

        [Tooltip("涡旋形变强度")]
        public PhaseCurveParam vortexCurve = new PhaseCurveParam { phase1 = 0.0f, phase2 = 0.15f, phase3 = 0.7f, phase4 = 1.2f };

        [Tooltip("Glitch 像素故障块强度")]
        public PhaseCurveParam glitchCurve = new PhaseCurveParam { phase1 = 0.0f, phase2 = 0.08f, phase3 = 0.35f, phase4 = 0.75f };

        [Tooltip("RGB 色偏分色强度")]
        public PhaseCurveParam rgbShiftCurve = new PhaseCurveParam { phase1 = 0.0f, phase2 = 0.004f, phase3 = 0.02f, phase4 = 0.045f };

        [Tooltip("波浪弯曲形变强度")]
        public PhaseCurveParam waveWarpCurve = new PhaseCurveParam { phase1 = 0.05f, phase2 = 0.25f, phase3 = 0.7f, phase4 = 1.3f };

        [Tooltip("油彩弧形抹平强度")]
        public PhaseCurveParam oilSmearCurve = new PhaseCurveParam { phase1 = 0.02f, phase2 = 0.15f, phase3 = 0.45f, phase4 = 0.9f };

        [Tooltip("极速拖尾模糊强度")]
        public PhaseCurveParam speedTrailsCurve = new PhaseCurveParam { phase1 = 0.0f, phase2 = 0.05f, phase3 = 0.35f, phase4 = 0.8f };

        [Tooltip("边缘羽化强度 (Phase1 也保持温和羽化，避免硬边相片框效果)")]
        public PhaseCurveParam borderFadeCurve = new PhaseCurveParam { phase1 = 0.08f, phase2 = 0.15f, phase3 = 0.3f, phase4 = 0.5f };

        [Tooltip("画面切片错位强度")]
        public PhaseCurveParam sliceShiftCurve = new PhaseCurveParam { phase1 = 0.0f, phase2 = 0.05f, phase3 = 0.15f, phase4 = 0.35f };

        [Tooltip("UV 内容流动速度倍率 (独立于物理推进速度 forwardFlySpeed，控制墙面贴图滚动快慢)")]
        public PhaseCurveParam flowSpeedCurve = new PhaseCurveParam { phase1 = 0.4f, phase2 = 0.8f, phase3 = 1.4f, phase4 = 2.2f };

        [Header("Per-surface effect intensity multipliers (readability control)")]
        [Tooltip("Front wall (the one the player looks straight at) overall effect multiplier. Keep this lower than sideWallEffectIntensity so the content the player is focused on stays readable.")]
        [Range(0f, 2f)] public float frontWallEffectIntensity = 0.35f;

        [Tooltip("Side walls (Cube 1~4 around the player) overall effect multiplier, applied on top of the jelly/vortex/glitch curves. Can be pushed higher than the front wall since these are peripheral and can afford to be wilder/more playful.")]
        [Range(0f, 2f)] public float sideWallEffectIntensity = 1.4f;

        [Header("🌀 Phase4 彻底混沌节奏 (双模态：大部分时间催眠慢脉冲 + 偶发剧烈爆闪，制造仪式感)")]
        [Tooltip("催眠慢脉冲的呼吸频率 (越小越缓慢梦幻)")]
        public float phase4HypnoticFrequency = 0.8f;

        [Tooltip("剧烈爆闪的间隔周期(秒)")]
        public float phase4BurstInterval = 5f;

        [Tooltip("每次爆闪持续的时长(秒)")]
        public float phase4BurstDuration = 0.35f;

        [Tooltip("爆闪时刻所有参数的额外倍率")]
        [Range(1f, 4f)] public float phase4BurstIntensity = 2.2f;

        [Header("🍬 Phase1 感官过载但甜蜜 (靠切换节奏密度制造过载感，而非画面形变)")]
        [Tooltip("Phase1 每次点击切换视频后，最短需要等待多久才能再次生效(秒)——数值越小切换越密集，过载感越强")]
        public float phase1MinClickCooldown = 1.0f;

        [Tooltip("每次成功切换视频时，正面墙触发的反馈脉冲幅度")]
        public float clickDingPulseAmplitude = 0.06f;

        [Tooltip("反馈脉冲的衰减速度 (越大衰减越快，脉冲越短促)")]
        public float clickDingPulseDecay = 6f;

        private float _clickDingTimer = 0f;

        [Header("Phase2 掌控感逐渐流失 (Compulsive Control Loss)")]
        [Tooltip("Phase2 结束时，玩家点击被算法无声地吞掉/毫无反应的最大概率——制造失控感")]
        [Range(0f, 1f)] public float phase2MaxClickSwallowChance = 0.4f;

        [Header("节奏抖动 (让自动切换的间隔带一点不规律感)")]
        [Range(0f, 0.6f)] public float rhythmJitterAmount = 0.15f;

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
        public float phase3Duration = 25f;

        [Tooltip("Phase 3 空间崩坏倒计时")]
        public float collapseCountdown = 30f;

        [Tooltip("Phase 3 觉醒突破所需的疯狂连击鼠标次数 (连击 30 次打碎信息茧房)")]
        public int requiredResistanceClicks = 30;

        private Material _frontMat;
        private Material _wallMat;
        // 🌟 每面墙独立的 CorridorWallShader 材质实例（真正被渲染使用）。
        // 过去所有 Jelly/Vortex/Glitch/OilSmear 参数只写在 _wallMat 上，但 _wallMat 从未被赋给任何 Renderer，
        // 导致这些特效实际上从来没有在屏幕上生效过——这正是走廊看起来"死板"的根本原因之一。
        private Material[] _perWallMaterials;
        private List<Material> _matrixPanelMaterials = new List<Material>();

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

            // 压暗环境背景与天空盒，防止缝隙露出发白的透明虚空
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = Color.black;
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
                frontWallRenderer.enabled = true; // 始终显示正面墙
            }

            // 🌟 尊重你在 Inspector 里引用的 sideWallRenderers；仅在为空时在当前 Transform 父子层级中查找
            if (sideWallRenderers == null || sideWallRenderers.Length == 0)
            {
                List<Renderer> foundRenderers = new List<Renderer>();
                Transform rootSearch = transform.parent != null ? transform.parent : transform;
                Renderer[] childRenderers = rootSearch.GetComponentsInChildren<Renderer>(true);
                foreach (var r in childRenderers)
                {
                    if (r != null && r.gameObject.name.StartsWith("Cube") && r.gameObject.name != "Invisible_Static_Safety_Floor")
                    {
                        foundRenderers.Add(r);
                    }
                }
                if (foundRenderers.Count > 0)
                {
                    sideWallRenderers = foundRenderers.ToArray();
                }
            }
            Debug.Log($"<color=cyan>[CustomCorridorBinder] 已绑定 3D 走廊 {sideWallRenderers?.Length ?? 0} 个 Cube 墙面，位置 100% 保持 Scene 原始状态！</color>");

            if (sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                _initialWallPositions = new Vector3[sideWallRenderers.Length];
                _initialWallRotations = new Quaternion[sideWallRenderers.Length];
                _initialWallScales = new Vector3[sideWallRenderers.Length];
                _perWallMaterials = new Material[sideWallRenderers.Length];

                Shader wallCustomShader = Shader.Find("Wakeup/CorridorWallShader");
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
                if (unlitShader == null) unlitShader = Shader.Find("Standard");
                Shader wallShaderToUse = wallCustomShader != null ? wallCustomShader : unlitShader;

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        // 🌟 核心修复：过去这里一直用的是普通 Unlit 材质，现在改用真正携带 Jelly/Vortex/Glitch 等特效的 CorridorWallShader，
                        // 每面墙都拿到一份独立实例，确保真正被渲染使用。
                        Material wallMatInst = new Material(wallShaderToUse);
                        Texture tex = (mediaDatabase != null) ? mediaDatabase.GetEntertainmentTexture() : null;
                        if (tex != null)
                        {
                            if (wallMatInst.HasProperty("_MainTex")) wallMatInst.SetTexture("_MainTex", tex);
                            if (wallMatInst.HasProperty("_BaseMap")) wallMatInst.SetTexture("_BaseMap", tex);
                            wallMatInst.mainTexture = tex;
                        }
                        if (wallMatInst.HasProperty("_Color")) wallMatInst.SetColor("_Color", Color.white);
                        if (wallMatInst.HasProperty("_BaseColor")) wallMatInst.SetColor("_BaseColor", Color.white);
                        if (wallMatInst.HasProperty("_Cull")) wallMatInst.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                        if (wallMatInst.HasProperty("_CullMode")) wallMatInst.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);

                        sideWallRenderers[i].material = wallMatInst;
                        _perWallMaterials[i] = wallMatInst;

                        _initialWallPositions[i] = sideWallRenderers[i].transform.localPosition;
                        _initialWallRotations[i] = sideWallRenderers[i].transform.localRotation;
                        _initialWallScales[i] = sideWallRenderers[i].transform.localScale;

                        // 🌟 把原本只有 8 个顶点/每面 1 个四边形的低模 Cube 替换成细分网格，
                        // 这样 CorridorWallShader 顶点着色器里的 Jelly 波浪形变才有足够的顶点密度真正"起伏流动"，
                        // 而不是整片刚性平面僵硬地倾斜（这是走廊看起来死板的根本原因之一）。
                        if (wallCustomShader != null)
                        {
                            ApplyOrganicSubdivision(sideWallRenderers[i]);
                        }

                        sideWallRenderers[i].enabled = true;
                    }
                }
                Debug.Log($"<color=green>[CustomCorridorBinder] 成功为所有 Cube 墙面绑定 CorridorWallShader 并完成细分网格重建！(shader命中: {wallCustomShader != null})</color>");
            }

            CreateInvisibleGroundFloor();
            CreateMultiPanelVideoMatrix();
            CreateSpeedStreakSystem();
            CreateSegmentedTunnel();

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

        /// <summary>
        /// 核心新增：隧道飞驰速度流粒子系统 (Tunnel Speed Streaks)
        /// 不同于墙面形变（那只能让环境"有生命感"），这套系统才是真正制造"玩家在隧道里飞驰"主观感受的关键：
        /// 大量粒子从画面深处（摄像机前方远处）生成，沿直线飞向玩家，用 Stretched Billboard 拉丝渲染，
        /// 模拟经典的"星际穿越/超空间警速"视觉效果。粒子系统挂在 Camera 下，始终相对玩家视角"迎面而来"。
        /// </summary>
        private void CreateSpeedStreakSystem()
        {
            if (!enableSpeedStreaks) return;

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[CustomCorridorBinder] 未找到 Main Camera，无法创建速度流粒子系统。");
                return;
            }

            GameObject streakGo = new GameObject("TunnelSpeedStreaks");
            streakGo.transform.SetParent(mainCam.transform, false);
            streakGo.transform.localPosition = Vector3.zero;
            streakGo.transform.localRotation = Quaternion.identity;

            _speedStreakSystem = streakGo.AddComponent<ParticleSystem>();

            var main = _speedStreakSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            // 关键：Local 模拟空间，让粒子系统跟随摄像机视角旋转，永远从玩家看向的正前方迎面而来
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = speedStreakCount;
            float baseLifetime = speedStreakSpawnDistance / Mathf.Max(0.1f, speedStreakBaseSpeed);
            main.startLifetime = baseLifetime;
            main.startSpeed = speedStreakBaseSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = speedStreakColor;
            main.gravityModifier = 0f;

            var emission = _speedStreakSystem.emission;
            emission.rateOverTime = speedStreakCount / Mathf.Max(0.1f, baseLifetime);

            var shape = _speedStreakSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = speedStreakSpreadRadius;
            shape.radiusThickness = 1f; // 填实整个截面圆盘，而不只是边缘
            shape.position = new Vector3(0f, 0f, speedStreakSpawnDistance);
            shape.rotation = new Vector3(180f, 0f, 0f); // 掉头 180 度，让粒子从远处飞向摄像机方向（而不是背离）

            var renderer = _speedStreakSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = speedStreakLengthScale;
            renderer.lengthScale = 2f;
            renderer.alignment = ParticleSystemRenderSpace.View;

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null) particleShader = Shader.Find("Sprites/Default");
            Material streakMat = new Material(particleShader);
            if (streakMat.HasProperty("_Color")) streakMat.SetColor("_Color", speedStreakColor);
            if (streakMat.HasProperty("_BaseColor")) streakMat.SetColor("_BaseColor", speedStreakColor);
            // 开启混合透明，避免拉丝粒子遮住后方内容
            if (streakMat.HasProperty("_Surface")) streakMat.SetFloat("_Surface", 1f);
            if (streakMat.HasProperty("_Blend")) streakMat.SetFloat("_Blend", 0f);
            renderer.sharedMaterial = streakMat;

            Debug.Log("<color=cyan>[CustomCorridorBinder] 隧道飞驰速度流粒子系统已创建，这才是制造'玩家在隧道里飞驰'主观感受的核心视觉元素！</color>");
        }

        /// <summary>
        /// 🌟 根本解决方案：分段循环隧道 (Segmented Recycling Tunnel)
        /// 之前的固定大 Cube 盒子无论怎么滚 UV 都不会产生真正的视差——因为盒子本身在三维空间里从未移动。
        /// 这里把走廊拆成 N 段，每段都是一套完整的左/右/天花板/地面 4 个面，
        /// 每帧真实沿 Z 轴朝摄像机方向平移，划过摄像机后立即传送回最远端循环使用，
        /// 这才是真正产生视差/穿梭感的方法，与参考视频里隧道飞行的真实原理一致。
        /// </summary>
        private void CreateSegmentedTunnel()
        {
            if (!enableSegmentedTunnel) return;

            Shader wallCustomShader = Shader.Find("Wakeup/CorridorWallShader");
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Shader shaderToUse = wallCustomShader != null ? wallCustomShader : unlitShader;

            GameObject tunnelRoot = new GameObject("SegmentedTunnel_Root");
            // 🌟 关键修复：不能挂在 transform （本组件所在的 GameObject）下面！
            // 经排查发现，这个 GameObject 实际叫 "Stage5Controller"，它在世界空间里的位置是 (922, 635.5, 0)，
            // 距离真正的走廊/摄像机位置十万八千里！之前分段隧道全部被传送到了那里，导致玩家看到的是彻底黑屏。
            // 现在显式把走廊根节点放在世界原点（与 wallHalfWidth/ceilingY/floorY 等参数假设的坐标系一致），不要挂任何父级。
            tunnelRoot.transform.position = Vector3.zero;
            tunnelRoot.transform.rotation = Quaternion.identity;

            for (int i = 0; i < tunnelSegmentCount; i++)
            {
                GameObject segRoot = new GameObject($"TunnelSegment_{i}");
                segRoot.transform.SetParent(tunnelRoot.transform, false);
                float zPos = i * tunnelSegmentLength + 1.0f;
                segRoot.transform.localPosition = new Vector3(0f, 0f, zPos);

                Material[] segMats = new Material[4];

                // 0=左墙, 1=右墙, 2=天花板, 3=地面
                for (int f = 0; f < 4; f++)
                {
                    GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    plane.name = $"Seg{i}_Face{f}";
                    plane.transform.SetParent(segRoot.transform, false);

                    Vector3 localPos;
                    Quaternion localRot;
                    Vector3 scale;

                    switch (f)
                    {
                        case 0: // 左墙：面向走廊内部 (+X)
                            localPos = new Vector3(-wallHalfWidth, wallCenterY, 0f);
                            localRot = Quaternion.Euler(0f, 90f, 0f);
                            scale = new Vector3(ceilingY - floorY, tunnelSegmentLength * 1.05f, 1f);
                            break;
                        case 1: // 右墙
                            localPos = new Vector3(wallHalfWidth, wallCenterY, 0f);
                            localRot = Quaternion.Euler(0f, -90f, 0f);
                            scale = new Vector3(ceilingY - floorY, tunnelSegmentLength * 1.05f, 1f);
                            break;
                        case 2: // 天花板
                            localPos = new Vector3(0f, ceilingY, 0f);
                            localRot = Quaternion.Euler(90f, 0f, 0f);
                            scale = new Vector3(wallHalfWidth * 2f, tunnelSegmentLength * 1.05f, 1f);
                            break;
                        case 3: // 地面
                        default:
                            localPos = new Vector3(0f, floorY, 0f);
                            localRot = Quaternion.Euler(-90f, 0f, 0f);
                            scale = new Vector3(wallHalfWidth * 2f, tunnelSegmentLength * 1.05f, 1f);
                            break;
                    }

                    plane.transform.localPosition = localPos;
                    plane.transform.localRotation = localRot;
                    plane.transform.localScale = scale;

                    Collider col = plane.GetComponent<Collider>();
                    if (col != null) Destroy(col);

                    if (wallCustomShader != null)
                    {
                        MeshFilter mf = plane.GetComponent<MeshFilter>();
                        if (mf != null)
                        {
                            mf.mesh = GenerateSubdividedPlaneMesh(Mathf.Max(2, wallMeshSubdivisions / 2));
                        }
                    }

                    MeshRenderer mr = plane.GetComponent<MeshRenderer>();
                    Material mat = new Material(shaderToUse);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    if (mat.HasProperty("_CullMode")) mat.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);

                    Texture tex = GetRoundRobinTexture(i * 4 + f);
                    if (tex != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        mat.mainTexture = tex;
                    }

                    mr.material = mat;
                    segMats[f] = mat;
                }

                _tunnelSegments.Add(segRoot);
                _tunnelSegmentMaterials.Add(segMats);
            }

            // 生成了真实会滑动的新隧道后，隐藏原本静止的 6 面大 Cube 墙体（只关渲染，Collider 保留作为安全网）
            if (hideOldWallsWhenSegmentedTunnelActive && sideWallRenderers != null)
            {
                foreach (var r in sideWallRenderers)
                {
                    if (r != null) r.enabled = false;
                }
            }

            Debug.Log($"<color=green>[CustomCorridorBinder] 成功构建 {tunnelSegmentCount} 段可循环滑动的真实隧道分段，替代静止盒子的假流动！这才是真正产生视差的方法。</color>");
        }

        /// <summary>
        /// 根据索引轮询获取主题贴图（entertainment/banana/prayer/push/work 五选一），用于分段隧道多面多样化。
        /// </summary>
        private Texture GetRoundRobinTexture(int index)
        {
            if (mediaDatabase == null) return null;
            int r = index % 5;
            switch (r)
            {
                case 0: return mediaDatabase.GetEntertainmentTexture();
                case 1: return mediaDatabase.GetThemeTexture("banana");
                case 2: return mediaDatabase.GetThemeTexture("prayer");
                case 3: return mediaDatabase.GetThemeTexture("push");
                default: return mediaDatabase.GetThemeTexture("work");
            }
        }

        /// <summary>
        /// 用细分网格替换走廊墙面 Cube 原本的低模 Mesh（每个面只有 1 个四边形/4 个顶点），
        /// 让 CorridorWallShader 顶点着色器里的 Jelly 波浪形变有足够顶点密度真正"流动起伏"，
        /// 而不是整片刚性平面僵硬地倾斜。只替换 MeshFilter 的渲染网格，Collider 保持原样不受影响。
        /// </summary>
        private void ApplyOrganicSubdivision(Renderer wallRenderer)
        {
            MeshFilter mf = wallRenderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // 已经细分过就不要重复处理（防止重复调用时重复生成）
            if (mf.sharedMesh.name == "OrganicSubdividedWall") return;

            Mesh subdivided = GenerateSubdividedCubeMesh(wallMeshSubdivisions);
            mf.mesh = subdivided; // 实例化独立网格，不污染原始 Cube 资产
        }

        /// <summary>
        /// 生成细分网格版本的标准 1x1x1 立方体（每个面 subdivisions x subdivisions 个格子）。
        /// </summary>
        private Mesh GenerateSubdividedCubeMesh(int subdivisions)
        {
            subdivisions = Mathf.Max(1, subdivisions);
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

            foreach (Vector3 normal in faceNormals)
            {
                Vector3 tangent = new Vector3(normal.y, normal.z, normal.x);
                Vector3 bitangent = Vector3.Cross(normal, tangent);

                int baseIndex = verts.Count;
                for (int y = 0; y <= subdivisions; y++)
                {
                    for (int x = 0; x <= subdivisions; x++)
                    {
                        float u = (float)x / subdivisions;
                        float v = (float)y / subdivisions;
                        Vector3 pos = normal * 0.5f + tangent * (u - 0.5f) + bitangent * (v - 0.5f);
                        verts.Add(pos);
                        uvs.Add(new Vector2(u, v));
                    }
                }

                int rowLen = subdivisions + 1;
                for (int y = 0; y < subdivisions; y++)
                {
                    for (int x = 0; x < subdivisions; x++)
                    {
                        int i0 = baseIndex + y * rowLen + x;
                        int i1 = baseIndex + y * rowLen + x + 1;
                        int i2 = baseIndex + (y + 1) * rowLen + x;
                        int i3 = baseIndex + (y + 1) * rowLen + x + 1;

                        tris.Add(i0); tris.Add(i2); tris.Add(i1);
                        tris.Add(i1); tris.Add(i2); tris.Add(i3);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "OrganicSubdividedWall";
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 生成细分网格版本的单面 Quad（用于多宫格视频面板，与 Unity 内置 Quad 本地坐标系一致：
        /// XY 平面，顶点范围 -0.5..0.5，法线朝向 -Z）。
        /// </summary>
        private Mesh GenerateSubdividedPlaneMesh(int subdivisions)
        {
            subdivisions = Mathf.Max(1, subdivisions);
            int rowLen = subdivisions + 1;
            Vector3[] verts = new Vector3[rowLen * rowLen];
            Vector2[] uvs = new Vector2[rowLen * rowLen];

            for (int y = 0; y <= subdivisions; y++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    float u = (float)x / subdivisions;
                    float v = (float)y / subdivisions;
                    int idx = y * rowLen + x;
                    verts[idx] = new Vector3(u - 0.5f, v - 0.5f, 0f);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            List<int> tris = new List<int>();
            for (int y = 0; y < subdivisions; y++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int i0 = y * rowLen + x;
                    int i1 = y * rowLen + x + 1;
                    int i2 = (y + 1) * rowLen + x;
                    int i3 = (y + 1) * rowLen + x + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "OrganicSubdividedPanel";
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris.ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

                // Front wall keeps a lower effect load for readability (player looks straight at it),
                // scaled by frontWallEffectIntensity so it can be tuned independently from the side walls.
                float glitch = (phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * maxGlitchAmount : 0f) * frontWallEffectIntensity;
                float borderFade = (phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.20f : 0f) * frontWallEffectIntensity;

                bool isP1 = phaseProgress < 0.35f;
                float p2R = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float p3R = Mathf.InverseLerp(0.70f, 1.00f, phaseProgress);

                float frontOilSmear = (isP1 ? 0.02f : (0.02f + p2R * (maxOilSmearArc * 0.5f) + p3R * maxOilSmearArc)) * frontWallEffectIntensity;
                float frontSpeedTrails = (isP1 ? 0f : (p2R * (maxSpeedTrails * 0.5f) + p3R * maxSpeedTrails)) * frontWallEffectIntensity;

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
                frontWallRenderer.enabled = true; // 始终显示正面墙

                // 🍬 每次成功切换内容时的反馈脉冲，随时间衰减——制造 Phase1 甜蜜的多巴胺点击反馈
                if (_clickDingTimer > 0f)
                {
                    _clickDingTimer = Mathf.Max(0f, _clickDingTimer - Time.deltaTime * clickDingPulseDecay);
                }
                float dingPulse = _clickDingTimer * clickDingPulseAmplitude;

                float breathe = Mathf.Sin(time * 1.5f) * 0.02f + dingPulse;
                frontWallRenderer.transform.localScale = _initialFrontScale * (1.0f + breathe);
            }

            // 5. 驱动 Side Walls 四周墙面与多宫格面板多维流体 (实时相应 Inspector 滑块)
            // 🌟 核心修复：改为直接写入每面墙真正在渲染的材质实例 _perWallMaterials[i]（以及多宫格视频面板 _matrixPanelMaterials），
            // 而不是那个从未挂载到任何 Renderer 上的孤儿材质 _wallMat（过去所有特效因此从未生效过）。
            // 同时把 Phase 1 的基线值从"几乎完全归零"调整为"温和但持续存在"，
            // 避免走廊在安全阶段显得像一个死板的静态盒子（硬边相片框效果的元凶就是 borderFade 以前一直为 0）。
            if (_perWallMaterials != null || _matrixPanelMaterials.Count > 0 || _tunnelSegmentMaterials.Count > 0)
            {
                // 核心重构：所有强度参数改由 PhaseCurveParam 数据驱动评估，
                // 不再是散落各处、方向经常互相矛盾的临时 Lerp 公式——每个参数的四阶段目标值都能在 Inspector 里独立重新设计。
                float jelly = jellyCurve.Evaluate(phaseProgress);
                float glitch = glitchCurve.Evaluate(phaseProgress);
                float rgbShift = rgbShiftCurve.Evaluate(phaseProgress);
                float waveWarp = waveWarpCurve.Evaluate(phaseProgress);
                float vortex = vortexCurve.Evaluate(phaseProgress);
                float sliceShift = sliceShiftCurve.Evaluate(phaseProgress);
                float borderFade = borderFadeCurve.Evaluate(phaseProgress);
                float oilSmear = oilSmearCurve.Evaluate(phaseProgress);
                float speedTrails = speedTrailsCurve.Evaluate(phaseProgress);
                float angle = 0.05f + Mathf.Sin(time * 0.3f) * 0.1f;

                float chaosPulse = 1f;
                if (phaseProgress >= 0.96f)
                {
                    float hypnotic = 0.6f + 0.4f * Mathf.Sin(time * phase4HypnoticFrequency);
                    bool inBurst = phase4BurstInterval > 0.01f && (time % phase4BurstInterval) < phase4BurstDuration;
                    chaosPulse = hypnotic * (inBurst ? phase4BurstIntensity : 1f);
                }

                jelly *= chaosPulse;
                glitch *= chaosPulse;
                rgbShift *= chaosPulse;
                waveWarp *= chaosPulse;
                vortex *= chaosPulse;
                sliceShift *= chaosPulse;
                oilSmear *= chaosPulse;
                speedTrails *= chaosPulse;

                if (_perWallMaterials != null)
                {
                    // Side walls (Cube 1~4 around the player) get an independent intensity multiplier
                    // so they can be pushed wilder/more playful without affecting the front wall the
                    // player is directly focused on (see sideWallEffectIntensity / frontWallEffectIntensity).
                    float sideJelly = jelly * sideWallEffectIntensity;
                    float sideGlitch = glitch * sideWallEffectIntensity;
                    float sideRgbShift = rgbShift * sideWallEffectIntensity;
                    float sideWaveWarp = waveWarp * sideWallEffectIntensity;
                    float sideVortex = vortex * sideWallEffectIntensity;
                    float sideOilSmear = oilSmear * sideWallEffectIntensity;
                    float sideSpeedTrails = speedTrails * sideWallEffectIntensity;
                    float sideSliceShift = sliceShift * sideWallEffectIntensity;

                    for (int i = 0; i < _perWallMaterials.Length; i++)
                    {
                        Material m = _perWallMaterials[i];
                        if (m == null) continue;

                        m.SetFloat("_JellyAmount", sideJelly);
                        m.SetFloat("_JellyScale", jellyDisplacementScale);
                        m.SetFloat("_GlitchAmount", sideGlitch);
                        m.SetFloat("_RGBShift", sideRgbShift);
                        m.SetFloat("_WaveWarp", sideWaveWarp);
                        m.SetFloat("_BorderFade", borderFade);
                        m.SetFloat("_OilSmearArc", sideOilSmear);
                        m.SetFloat("_ExplosiveRadialTrails", sideSpeedTrails);

                        m.SetFloat("_FlowAngle", angle);
                        m.SetFloat("_VortexAmount", sideVortex);
                        m.SetFloat("_SliceOffset", sideSliceShift);
                    }
                }

                for (int i = 0; i < _matrixPanelMaterials.Count; i++)
                {
                    Material m = _matrixPanelMaterials[i];
                    if (m == null) continue;

                    m.SetFloat("_JellyAmount", jelly);
                    m.SetFloat("_JellyScale", jellyDisplacementScale * 0.5f);
                    m.SetFloat("_GlitchAmount", glitch);
                    m.SetFloat("_RGBShift", rgbShift);
                    m.SetFloat("_WaveWarp", waveWarp);
                    m.SetFloat("_BorderFade", borderFade);
                    m.SetFloat("_OilSmearArc", oilSmear);
                    m.SetFloat("_ExplosiveRadialTrails", speedTrails);

                    m.SetFloat("_FlowAngle", angle);
                    m.SetFloat("_VortexAmount", vortex);
                    m.SetFloat("_SliceOffset", sliceShift);
                }

                // 🌟 分段循环隧道也同步写入所有 Shader 形变参数，让滑动中的管段同样具备呼吸/果冻感
                for (int s = 0; s < _tunnelSegmentMaterials.Count; s++)
                {
                    Material[] segMats = _tunnelSegmentMaterials[s];
                    if (segMats == null) continue;
                    for (int f = 0; f < segMats.Length; f++)
                    {
                        Material m = segMats[f];
                        if (m == null) continue;

                        m.SetFloat("_JellyAmount", jelly);
                        m.SetFloat("_JellyScale", jellyDisplacementScale * 0.6f);
                        m.SetFloat("_GlitchAmount", glitch);
                        m.SetFloat("_RGBShift", rgbShift);
                        m.SetFloat("_WaveWarp", waveWarp);
                        m.SetFloat("_BorderFade", borderFade);
                        m.SetFloat("_OilSmearArc", oilSmear);
                        m.SetFloat("_ExplosiveRadialTrails", speedTrails);

                        m.SetFloat("_FlowAngle", angle);
                        m.SetFloat("_VortexAmount", vortex);
                        m.SetFloat("_SliceOffset", sliceShift);
                    }
                }
            }

            // 物理 Cube (1)~(5) 墙面：有机呼吸摆动 (Organic Breathing Sway)
            // 整条走廊作为一个刚体整体轻轻左右/上下漂移 + 轻微扭转，模拟"飞行穿越一条柔软有生命的管道"；
            // 每面墙再叠加独立相位的呼吸缩放脉冲，避免看起来像死板同步的机械箱体。
            if (sideWallRenderers != null && _initialWallPositions != null && _initialWallScales != null)
            {
                // 越往后阶段摆动越明显，呼应"沉浸感逐渐加剧/失控"的叙事节奏
                float phaseIntensityBoost = 1.0f + Mathf.InverseLerp(0.35f, 1.0f, phaseProgress) * 0.6f;

                Vector3 swayOffset = Vector3.zero;
                float twistAngle = 0f;

                if (enableOrganicSway)
                {
                    swayOffset = new Vector3(
                        Mathf.Sin(time * swayFrequency) * swayAmplitudeX,
                        Mathf.Sin(time * swayFrequency * 0.8f + 1.7f) * swayAmplitudeY,
                        0f
                    ) * phaseIntensityBoost;

                    twistAngle = Mathf.Sin(time * twistFrequency + 0.5f) * twistAmplitude * phaseIntensityBoost;
                }

                Quaternion twistRot = Quaternion.Euler(0f, 0f, twistAngle);

                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        // 整体共享的摆动/扭转（保持箱体结构完整，只是作为一个整体轻轻摇摆呼吸）
                        sideWallRenderers[i].transform.localPosition = _initialWallPositions[i] + swayOffset;
                        sideWallRenderers[i].transform.localRotation = twistRot * _initialWallRotations[i];

                        // 每面墙独立的呼吸缩放脉冲（黄金分割散开相位，避免所有墙同步显得机械）
                        float breathSeed = i * 1.618f;
                        float breathe = enableOrganicSway
                            ? Mathf.Sin(time * wallBreatheFrequency + breathSeed) * wallBreatheAmplitude * phaseIntensityBoost
                            : 0f;
                        sideWallRenderers[i].transform.localScale = _initialWallScales[i] * (1f + breathe);
                    }
                }
            }

            // 🌟 核心突破：真正无缝、绝对无闪烁复位的无限隧道推进 (True Seamless Infinite Tunnel Flow)
            // 加入速度呼吸脉冲，让推进感不再是冷冰冰的匀速直线，而像有生命的搏动式涌流
            if (enableContinuousForwardFly)
            {
                float speedPulse = enableOrganicSway
                    ? 1f + Mathf.Sin(time * flowSpeedPulseFrequency) * flowSpeedPulseAmplitude
                    : 1f;
                float flySpeed = forwardFlySpeed * flowSpeedCurve.Evaluate(phaseProgress) * speedPulse;
                _cumulativeFlyZ += flySpeed * Time.deltaTime;

                // 摄像机零闪烁、零跳变，依靠墙面动态 UV 贴图与流体 Shader 在视觉上形成 100% 顺滑无限延伸推进！
                float liveFlowSpeed = flySpeed * 0.6f;
                if (_perWallMaterials != null)
                {
                    foreach (var m in _perWallMaterials)
                    {
                        if (m != null) m.SetFloat("_FlowSpeed", liveFlowSpeed);
                    }
                }
                foreach (var m in _matrixPanelMaterials)
                {
                    if (m != null) m.SetFloat("_FlowSpeed", liveFlowSpeed);
                }

                foreach (var segMats in _tunnelSegmentMaterials)
                {
                    if (segMats == null) continue;
                    foreach (var m in segMats)
                    {
                        if (m != null) m.SetFloat("_FlowSpeed", liveFlowSpeed);
                    }
                }

                // 🌟 分段循环隧道的核心运动：真实沿 Z 轴朝摄像机方向平移，划过摄像机后立即传送回最远端循环，
                // 这才是真正产生视差/穿梭感的根本原因，而不是靠静止盒子表面滚 UV 假装流动
                if (enableSegmentedTunnel && _tunnelSegments.Count > 0)
                {
                    Camera segCam = Camera.main;
                    float segCamZ = segCam != null ? segCam.transform.position.z : 0f;
                    float totalTunnelLength = tunnelSegmentCount * tunnelSegmentLength;

                    for (int s = 0; s < _tunnelSegments.Count; s++)
                    {
                        GameObject seg = _tunnelSegments[s];
                        if (seg == null) continue;

                        Vector3 segPos = seg.transform.localPosition;
                        segPos.z -= flySpeed * Time.deltaTime;

                        // 划过摄像机后方一定距离，立即无缝传送回最远端循环（玩家看不到传送那一帧）
                        if (segPos.z < segCamZ - tunnelSegmentLength * 0.6f)
                        {
                            segPos.z += totalTunnelLength;

                            // 循环复位时刷新贴图，制造内容持续更新的隧道穿梭错觉
                            Material[] segMats = s < _tunnelSegmentMaterials.Count ? _tunnelSegmentMaterials[s] : null;
                            if (segMats != null)
                            {
                                for (int f = 0; f < segMats.Length; f++)
                                {
                                    if (segMats[f] == null) continue;
                                    Texture nextTex = GetRoundRobinTexture(Random.Range(0, 1000));
                                    if (nextTex != null)
                                    {
                                        if (segMats[f].HasProperty("_MainTex")) segMats[f].SetTexture("_MainTex", nextTex);
                                        if (segMats[f].HasProperty("_BaseMap")) segMats[f].SetTexture("_BaseMap", nextTex);
                                    }
                                }
                            }
                        }

                        seg.transform.localPosition = segPos;
                    }
                }
            }

            // 🌟 驱动多宫格 3D 视频面板沿着走廊四周流畅后退流逝，并带有弯曲拐弯 S 曲线动势！
            if (enableMultiPanelVideoMatrix && enableContinuousForwardFly)
            {
                Camera mainCam = Camera.main;
                float streamSpeed = forwardFlySpeed * flowSpeedCurve.Evaluate(phaseProgress);

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

                            // 动态换上一张全新的媒体视频/图片素材！改用阶段感知方法，确保 Phase3/4 循环复位时也能正确显示主导主题内容，而不是永远回到纯娱乐内容
                            MeshRenderer mr = _matrixPanels[i].GetComponent<MeshRenderer>();
                            if (mr != null && mediaDatabase != null)
                            {
                                Texture nextTex = GetPhaseAwareTexture(phaseProgress);
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
        /// 在走廊极尽头创建巨型高清媒体封底墙，彻底无缝封死尽头黑洞！
        /// </summary>
        private void CreateFarEndCapWall()
        {
            GameObject endCapGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            endCapGo.name = "Tunnel_FarEndCap_Wall";
            endCapGo.transform.SetParent(transform, false);

            endCapGo.transform.position = new Vector3(0f, wallCenterY, endCapZ);
            endCapGo.transform.rotation = Quaternion.identity;
            endCapGo.transform.localScale = new Vector3(endCapSize, endCapSize, 1.0f);

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
            Debug.Log("<color=green>[CustomCorridorBinder] 成功创建走廊极尽头高清媒体封底墙，零缝隙封死尽头！</color>");
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
                        pos = new Vector3(-wallHalfWidth, wallCenterY, wallCenterZ);
                        rot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                    case 1: // 右墙
                        pos = new Vector3(wallHalfWidth, wallCenterY, wallCenterZ);
                        rot = Quaternion.Euler(0f, -90f, 0f);
                        break;
                    case 2: // 天花板
                        pos = new Vector3(0f, ceilingY, wallCenterZ);
                        rot = Quaternion.Euler(90f, 0f, 0f);
                        scale = new Vector3(wallHalfWidth * 2.0f, 16.0f, 1.0f);
                        break;
                    case 3: // 地面
                    default:
                        pos = new Vector3(0f, floorY, wallCenterZ);
                        rot = Quaternion.Euler(-90f, 0f, 0f);
                        scale = new Vector3(wallHalfWidth * 2.0f, 16.0f, 1.0f);
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
            // 🌟 同样的修复：不能挂在 transform 下（该 GameObject 实际世界坐标在 (922, 635.5, 0)，距离真正走廊十万八千里），
            // 显式放在世界原点，与 sideWallRenderers 之前已经正确工作的位置基准保持一致。
            matrixRoot.transform.position = Vector3.zero;
            matrixRoot.transform.rotation = Quaternion.identity;

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

                // 🌟 核心修复：过去这些面板一直用普通 Unlit 材质 + Unity 内置 Quad（只有 4 个顶点），
                // 硬边矩形悬浮在空中看起来像一张张相片框，与四周环境完全脱节。
                // 现在改用与墙面相同的 CorridorWallShader + 细分网格，让它们也能跟着整体流动/边缘羽化，不再像硬生生贴上去的照片。
                MeshRenderer mr = panelGo.GetComponent<MeshRenderer>();
                Shader panelCustomShader = Shader.Find("Wakeup/CorridorWallShader");
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
                Shader panelShaderToUse = panelCustomShader != null ? panelCustomShader : unlitShader;
                Material mat = new Material(panelShaderToUse);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                if (mat.HasProperty("_CullMode")) mat.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);

                if (panelCustomShader != null)
                {
                    MeshFilter panelMf = panelGo.GetComponent<MeshFilter>();
                    if (panelMf != null)
                    {
                        panelMf.mesh = GenerateSubdividedPlaneMesh(Mathf.Max(2, wallMeshSubdivisions / 2));
                    }
                }

                if (mediaDatabase != null)
                {
                    // 核心修复：初始创建时不再按 i%5 固定轮询分配主题贴图（那会让 Phase1 一开局就有悬浮面板显示主题内容），
                    // 改用阶段感知方法：创建时 phaseProgress 必然接近 0（Start() 阶段），因此此时全部均为纯娱乐内容。
                    float initPhaseProgress = Stage5Controller.Instance != null ? Stage5Controller.Instance.phaseProgress : 0f;
                    Texture tex = GetPhaseAwareTexture(initPhaseProgress);

                    if (tex != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        mat.mainTexture = tex;
                    }
                }

                mr.material = mat;
                _matrixPanels.Add(panelGo);
                _matrixPanelMaterials.Add(mat);
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
                // Phase 1: 必须看完 40 个视频，每次展示至少 phase1MinClickCooldown 秒防偷跑
                bool readyForNextClick = _mediaPlayTimer >= phase1MinClickCooldown && !_isTransitioning;

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

                // 掌控感逐渐流失：随 Phase2 进度推进，玩家点击有概率被算法无声地吞掉/毫无反应
                float p2Ratio = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float swallowChance = p2Ratio * phase2MaxClickSwallowChance;
                bool clickSwallowed = playerClicked && Random.value < swallowChance;
                bool effectiveClick = playerClicked && !clickSwallowed;

                if ((effectiveClick || timerExpired) && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    _phase2StepCount++;
                    TriggerNextMediaSwitch();

                    float p = 0.35f + Mathf.Clamp01((float)_phase2StepCount / Mathf.Max(1, requiredPhase2Steps)) * 0.35f;
                    if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;

                    float themeWeight = Mathf.Lerp(10f, 90f, Mathf.InverseLerp(0.35f, 0.70f, p));
                    Debug.Log($"<color=yellow>[Phase 2 算法推流] 切屏步骤: {_phase2StepCount}/{requiredPhase2Steps} (偏好渗透率: {themeWeight:F0}%, Phase进度: {p * 100f:F1}%)</color>");
                }
                else if (clickSwallowed)
                {
                    Debug.Log("<color=gray>[Phase 2 掌控感流失] 玩家点击被算法无声地吞掉了，毫无反应...</color>");
                }
            }
            else if (phaseProgress < 0.96f)
            {
                // Phase 3: 完全被动的算法霸屏轮播——不需要点击，主题内容已经 100% 锁定最高 counter 类别，
                // 画面/音频在这一段本身就已经通过强度曲线变得猎奇疯狂；进度随时间自动推进，
                // 抓到 0.96 后才正式进入 Phase4 彻底混沌，在这之前不会激活任何点击抵抗/突破机制。
                _switchTimer += Time.deltaTime;
                if (_switchTimer >= _currentInterval && !_isTransitioning)
                {
                    _switchTimer = 0f;
                    TriggerNextMediaSwitch();
                }

                float p3Advance = Time.deltaTime / Mathf.Max(0.1f, phase3Duration) * (0.96f - 0.70f);
                float p = Mathf.Min(0.96f, phaseProgress + p3Advance);
                if (Stage5Controller.Instance != null) Stage5Controller.Instance.phaseProgress = p;
            }
            else
            {
                // Phase 4: 彻底混沌的 cult 仪式高潮！空间高频崩坏 + 玩家连击抗争机制只在这一阶段激活（之前误以为从 Phase3 就激活了，现在正式改为仅在 phaseProgress >= 0.96 才触发）
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
            _clickDingTimer = 1f;

            _currentModeIndex = Random.Range(0, 5);

            if (_nextTex != null) _currentTex = _nextTex;
            if (_currentTex != null) _lastValidTex = _currentTex;

            PickNextMedia();
            ApplyTexturesToWalls();
        }

        private void UpdateRhythmTempo(float phaseProgress)
        {
            float baseInterval;
            if (phaseProgress < 0.35f)
            {
                baseInterval = 4.5f;
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase 2: 漫长平缓，时间从 7.0 秒逐渐变化到 4.5 秒
                baseInterval = Mathf.Lerp(7.0f, 4.5f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                // Phase 3: 大幅放缓切屏频率至 3.2s ~ 2.5s，确保画质与内容的可读性
                baseInterval = Mathf.Lerp(3.2f, 2.5f, Mathf.InverseLerp(0.70f, 1.00f, phaseProgress));
            }

            float jitter = 1f + Random.Range(-rhythmJitterAmount, rhythmJitterAmount);
            _currentInterval = Mathf.Max(0.3f, baseInterval * jitter);
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
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

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
            // 🌟 如果分段循环隧道已启用并选择隐藏旧墙，则不再每帧强制重新启用这些 Renderer（否则会覆盖掉 CreateSegmentedTunnel() 里的 enabled=false）
            bool oldWallsShouldBeVisible = !(enableSegmentedTunnel && hideOldWallsWhenSegmentedTunnelActive);

            // Wall media sync mode across the four side walls, computed once per refresh (not per-wall):
            //  - Phase1 first 70% (progress < 0.245): fully unified, all walls show the exact same single texture.
            //  - Phase1 last 30% (0.245-0.35): two-texture alternating mix, easing into Phase2's algorithmic blend.
            //  - Phase2 through first half of Phase3 (0.35-0.83): each wall keeps picking independently (existing mixed behavior).
            //  - Second half of Phase3 onward (progress >= 0.83) through Phase4: fully unified again, but locked to a
            //    single deterministic dominant-theme texture -- "the algorithm now shows you only one thing, everywhere".
            Texture wallSyncTexA = null;
            Texture wallSyncTexB = null;
            bool wallSyncDualMix = false;

            if (phaseProgress < 0.245f)
            {
                // Entertainment category has no static texture pool in this database (video-driven only),
                // so GetPhaseAwareTexture() often returns null here -- fall back to the current shared video
                // frame (texA) rather than letting it cascade into the old per-wall alternating fallback.
                wallSyncTexA = GetPhaseAwareTexture(phaseProgress);
                if (wallSyncTexA == null) wallSyncTexA = texA;
                wallSyncTexB = wallSyncTexA;
            }
            else if (phaseProgress < 0.35f)
            {
                wallSyncTexA = GetPhaseAwareTexture(phaseProgress);
                if (wallSyncTexA == null) wallSyncTexA = texA;
                wallSyncTexB = GetPhaseAwareTexture(phaseProgress);
                if (wallSyncTexB == null) wallSyncTexB = texB;
                wallSyncDualMix = true;
            }
            else if (phaseProgress >= 0.83f && mediaDatabase != null)
            {
                string lockedTheme = GetDominantTheme();
                wallSyncTexA = mediaDatabase.GetThemeTexture(lockedTheme);
                wallSyncTexB = wallSyncTexA;
            }
            // else (0.35 <= progress < 0.83): wallSyncTexA stays null, each wall picks independently below (unchanged mixed behavior).

            if (sideWallRenderers != null && sideWallRenderers.Length > 0)
            {
                for (int i = 0; i < sideWallRenderers.Length; i++)
                {
                    if (sideWallRenderers[i] != null)
                    {
                        sideWallRenderers[i].enabled = oldWallsShouldBeVisible;
                        if (!oldWallsShouldBeVisible) continue;
                        Material mat = sideWallRenderers[i].material;
                        if (mat != null)
                        {
                            Texture targetTex;
                            if (wallSyncTexA != null)
                            {
                                targetTex = wallSyncDualMix ? (i % 2 == 0 ? wallSyncTexA : wallSyncTexB) : wallSyncTexA;
                            }
                            else
                            {
                                targetTex = GetPhaseAwareTexture(phaseProgress);
                            }
                            if (targetTex == null) targetTex = (i % 2 == 0) ? texA : texB;

                            if (targetTex != null)
                            {
                                Vector2 tileScale = Vector2.one;
                                Vector2 tileOffset = Vector2.zero;

                                // 自动矫正倒置的墙面与天花板/地面 Texture 姿态，确保图像 100% 正向清晰
                                string gName = sideWallRenderers[i].gameObject.name;
                                if (gName.Contains("(3)") || gName.Contains("(4)") || gName == "Cube")
                                {
                                    tileScale = new Vector2(1.0f, -1.0f);
                                    tileOffset = new Vector2(0.0f, 1.0f);
                                }

                                if (mat.HasProperty("_MainTex"))
                                {
                                    mat.SetTexture("_MainTex", targetTex);
                                    mat.SetTextureScale("_MainTex", tileScale);
                                    mat.SetTextureOffset("_MainTex", tileOffset);
                                }
                                if (mat.HasProperty("_BaseMap"))
                                {
                                    mat.SetTexture("_BaseMap", targetTex);
                                    mat.SetTextureScale("_BaseMap", tileScale);
                                    mat.SetTextureOffset("_BaseMap", tileOffset);
                                }
                                mat.mainTexture = targetTex;
                            }
                            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                            if (mat.HasProperty("_CullMode")) mat.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);
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

        /// <summary>
        /// 与 PickNextMedia() 完全一致的阶段判定逻辑，为四周墙面/悬浮面板统一选取贴图——
        /// Phase1 只呈现纯娱乐内容，Phase2 按 themeWeight 曲线逐步渗透主题内容，Phase3/4 完全被主导主题占领。
        /// 四周墙面、悬浮面板、正面墙必须统一走这个方法，避免之前那种各自为政、
        /// 四周墙面永远按 i%5 固定轮询导致 Phase1 就提前泄露主题内容的 bug。
        /// </summary>
        private Texture GetPhaseAwareTexture(float phaseProgress)
        {
            if (mediaDatabase == null) return null;

            if (phaseProgress < 0.35f)
            {
                return mediaDatabase.GetEntertainmentTexture();
            }

            string theme = GetDominantTheme();
            float themeWeight;
            if (phaseProgress < 0.70f)
            {
                themeWeight = Mathf.Lerp(0.10f, 0.90f, Mathf.InverseLerp(0.35f, 0.70f, phaseProgress));
            }
            else
            {
                themeWeight = 1.0f;
            }

            bool pickTheme = Random.value < themeWeight;
            Texture result = pickTheme ? mediaDatabase.GetThemeTexture(theme) : mediaDatabase.GetEntertainmentTexture();
            return result != null ? result : mediaDatabase.GetEntertainmentTexture();
        }
    }
}
