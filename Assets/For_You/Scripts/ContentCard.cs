using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 单张内容卡片的行为控制。
    /// 
    /// 负责：漂移运动、注视吸引、愉悦反馈音效、Phase C 声音抽走、多层富文本视觉呈现。
    /// 新增：图片/纹理显示系统 + 程序化 Glitch 视觉扭曲系统（随 phaseProgress 从清晰→撕裂）。
    /// 
    /// 由 ContentCardSpawner 生成并配置。
    /// </summary>
    public class ContentCard : MonoBehaviour
    {
        [Header("运动参数")]
        [Tooltip("向玩家飞近的速度")]
        public float driftSpeed = 0.68f; // 放慢 20%，运动更加从容平缓

        [Tooltip("注视时向玩家靠近的加速倍率")]
        public float gazeAttractSpeed = 1.2f;

        [Tooltip("到达此距离后开始淡出销毁")]
        public float fadeStartDistance = 2.5f;

        [Tooltip("到达此距离后立刻销毁")]
        public float destroyDistance = 1.2f;

        [Tooltip("存活时间上限（秒），超过自动销毁")]
        public float maxLifetime = 25f;

        [Header("视觉扩展")]
        public Color cardColor = Color.white;
        public string cardText = "";
        public string categoryTag = "";
        public string cardIcon = "";
        public string cardHeadline = "";
        public string cardSubtext = "";

        [Tooltip("此卡片显示的图片纹理（由 ContentCardSpawner 从 CardMediaDatabase 中赋值）")]
        public Texture2D assignedTexture;

        [Header("类型标记")]
        [Tooltip("是否是 Phase C 的私密数据卡")]
        public bool isPrivateDataCard = false;

        [Tooltip("是否是结尾选择卡（留下/继续）")]
        public bool isChoiceCard = false;
        public string choiceAction = ""; // "stay" or "continue"

        // 内部状态
        private Transform _player;
        private bool _isBeingGazed = false;
        private float _lifetime = 0f;
        private Renderer _renderer;
        private TextMeshPro _tmpMain;
        private TextMeshPro _tmpHeader;
        private TextMeshPro _tmpIcon;
        private Renderer _innerFrameRend;
        private MaterialPropertyBlock _mpb;
        private float _alpha = 1f;

        // 媒体与 Glitch 系统
        private Material _innerCardMat;          // 内框独立 Material（用于纹理 + UV 抖动）
        private Renderer _glitchOverlayRend;     // 叠加在最前的 Glitch 色块 Quad
        private float _glitchTimer = 0f;
        private float _glitchUpdateInterval = 0.1f;
        private float _currentGlitch = 0f;       // 当前帧的 Glitch 强度 (0~1)

        // 回调：通知 Stage5Controller 发生了注视交互
        public System.Action<ContentCard> OnGazeInteract;
        // 回调：通知选择卡被激活
        public System.Action<string> OnChoiceSelected;

        private Vector3 _baseScale;

        public void SetBaseScale(Vector3 customBaseScale)
        {
            _baseScale = customBaseScale;
        }

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
            }

            // 找到玩家摄像机
            _player = Camera.main != null ? Camera.main.transform : null;

            // 创建内嵌深色卡片背景（制造精美的边框与层级感）
            CreateInnerCardFrame();

            // 在内框之上创建 Glitch 叠加层
            CreateGlitchOverlay();

            // 构建富文本排版层（顶部标签 + 中央大图标 + 底部文案）
            BuildRichCardLayout();

            // 应用初始颜色
            ApplyColor(cardColor);
        }

        private void Update()
        {
            _lifetime += Time.deltaTime;
            if (_lifetime > maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_player == null) return;

            // 漂移运动
            float speed = _isBeingGazed ? gazeAttractSpeed : driftSpeed;
            Vector3 toPlayer = (_player.position - transform.position).normalized;
            transform.position += toPlayer * speed * Time.deltaTime;

            // 始终平行对齐玩家视角（完美的 Billboard，彻底解决文字左右镜像反转问题）
            transform.rotation = _player.rotation;

            // 被注视时放大反馈
            float targetScaleMult = _isBeingGazed ? 1.25f : 1.0f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * targetScaleMult, Time.deltaTime * 8f);

            // 距离检测
            float dist = Vector3.Distance(transform.position, _player.position);

            // 淡出
            if (dist < fadeStartDistance)
            {
                float fadeT = Mathf.InverseLerp(fadeStartDistance, destroyDistance, dist);
                _alpha = 1f - fadeT;
                ApplyAlpha(_alpha);
            }

            // 销毁
            if (dist < destroyDistance)
            {
                Destroy(gameObject);
            }

            // 程序化 Glitch 视觉更新（限速：不每帧执行，节省性能）
            UpdateGlitch();
        }

        /// <summary>由 Stage5Controller 在射线命中时调用</summary>
        public void OnGazeEnter()
        {
            if (_isBeingGazed) return;
            _isBeingGazed = true;

            // 通知外部
            OnGazeInteract?.Invoke(this);

            // 如果是选择卡，触发选择
            if (isChoiceCard && !string.IsNullOrEmpty(choiceAction))
            {
                OnChoiceSelected?.Invoke(choiceAction);
            }
        }

        /// <summary>注视离开</summary>
        public void OnGazeExit()
        {
            _isBeingGazed = false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 视觉构建
        // ─────────────────────────────────────────────────────────────────────────

        private void CreateInnerCardFrame()
        {
            // 创建后置软辉光/尾迹层（环境渗透感）
            GameObject trailGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            trailGo.name = "AmbientGlowTrail";
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0f, 0.02f); // 放在卡片正后方
            trailGo.transform.localScale = new Vector3(1.25f, 1.25f, 1f); // 比主卡片宽 25%，作为柔和羽化尾痕

            Collider trailCol = trailGo.GetComponent<Collider>();
            if (trailCol != null) DestroyImmediate(trailCol);

            Renderer trailRend = trailGo.GetComponent<Renderer>();
            Shader shader = FindCardShader();
            Material trailMat = new Material(shader);

            if (trailMat.HasProperty("_Surface")) trailMat.SetFloat("_Surface", 1); // Transparent
            if (trailMat.HasProperty("_SrcBlend")) trailMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (trailMat.HasProperty("_DstBlend")) trailMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive 辉光融合
            if (trailMat.HasProperty("_ZWrite")) trailMat.SetInt("_ZWrite", 0);
            trailMat.renderQueue = 2999;

            // 给尾迹渲染器赋予半透明发光
            Color glowColor = cardColor;
            glowColor.a = 0.35f;
            if (trailMat.HasProperty("_BaseColor")) trailMat.SetColor("_BaseColor", glowColor);
            if (trailMat.HasProperty("_Color")) trailMat.SetColor("_Color", glowColor);
            trailRend.material = trailMat;

            // 创建内嵌深色/图像卡片主体（满格呈现，消除硬框积木感）
            GameObject innerGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            innerGo.name = "InnerFrame";
            innerGo.transform.SetParent(transform, false);
            innerGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            innerGo.transform.localScale = new Vector3(0.98f, 0.98f, 1f); // 98% 满格显示

            Collider col = innerGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            _innerFrameRend = innerGo.GetComponent<Renderer>();
            _innerCardMat = new Material(shader);

            // 配置透明度混合
            if (_innerCardMat.HasProperty("_Surface")) _innerCardMat.SetFloat("_Surface", 1); // Alpha Blend
            if (_innerCardMat.HasProperty("_SrcBlend")) _innerCardMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_innerCardMat.HasProperty("_DstBlend")) _innerCardMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_innerCardMat.HasProperty("_ZWrite")) _innerCardMat.SetInt("_ZWrite", 0);
            _innerCardMat.renderQueue = 3000;

            // ── 贴上图片纹理 ──
            if (assignedTexture != null)
            {
                if (_innerCardMat.HasProperty("_BaseMap"))
                    _innerCardMat.SetTexture("_BaseMap", assignedTexture);
                _innerCardMat.mainTexture = assignedTexture;

                if (_innerCardMat.HasProperty("_BaseColor"))
                    _innerCardMat.SetColor("_BaseColor", Color.white);
                if (_innerCardMat.HasProperty("_Color"))
                    _innerCardMat.SetColor("_Color", Color.white);
            }
            else
            {
                // 【关键修改】无纹理时彻底消灭半透明大色块！只保留几乎透明的非常柔和的深底，绝不填巨大的亮色方块
                Color innerBg = isPrivateDataCard
                    ? new Color(0.04f, 0.05f, 0.08f, 0.4f)
                    : new Color(0.05f, 0.05f, 0.08f, 0.2f);
                if (isChoiceCard) innerBg = new Color(0.09f, 0.08f, 0.15f, 0.95f);
                if (_innerCardMat.HasProperty("_BaseColor")) _innerCardMat.SetColor("_BaseColor", innerBg);
                if (_innerCardMat.HasProperty("_Color"))     _innerCardMat.SetColor("_Color",     innerBg);
            }

            _innerFrameRend.material = _innerCardMat;
        }

        private void CreateGlitchOverlay()
        {
            // 极薄的半透明有色 Quad，叠在内框正前方，用于 Glitch 色块/扫描线闪烁效果
            GameObject overlayGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayGo.name = "GlitchOverlay";
            overlayGo.transform.SetParent(transform, false);
            overlayGo.transform.localPosition = new Vector3(0f, 0f, -0.015f); // 在内框（-0.01）和文字（-0.02）之间
            overlayGo.transform.localScale = new Vector3(0.90f, 0.90f, 1f);

            Collider col = overlayGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            _glitchOverlayRend = overlayGo.GetComponent<Renderer>();
            Shader shader = FindCardShader();
            Material overlayMat = new Material(shader);

            if (overlayMat.HasProperty("_Surface")) overlayMat.SetFloat("_Surface", 1); // Transparent
            if (overlayMat.HasProperty("_SrcBlend")) overlayMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (overlayMat.HasProperty("_DstBlend")) overlayMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (overlayMat.HasProperty("_ZWrite")) overlayMat.SetInt("_ZWrite", 0);
            overlayMat.renderQueue = 3001;
            _glitchOverlayRend.material = overlayMat;

            // 初始完全透明
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", Color.clear);
            mpb.SetColor("_Color",     Color.clear);
            _glitchOverlayRend.SetPropertyBlock(mpb);
        }

        private Shader FindCardShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("URP/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private void BuildRichCardLayout()
        {
            // 1. 顶部 Header Category 标签
            if (!string.IsNullOrEmpty(categoryTag))
            {
                GameObject headerGo = new GameObject("CardHeader");
                headerGo.transform.SetParent(transform, false);
                headerGo.transform.localPosition = new Vector3(0f, 0.35f, -0.02f);
                headerGo.transform.localRotation = Quaternion.identity;

                _tmpHeader = headerGo.AddComponent<TextMeshPro>();
                _tmpHeader.text = categoryTag;
                _tmpHeader.enableAutoSizing = true;
                _tmpHeader.fontSizeMin = 0.2f;
                _tmpHeader.fontSizeMax = 0.55f;
                _tmpHeader.alignment = TextAlignmentOptions.Center;
                _tmpHeader.color = new Color(1f, 1f, 1f, 0.85f);
                _tmpHeader.fontStyle = FontStyles.Bold;

                RectTransform rtH = _tmpHeader.GetComponent<RectTransform>();
                rtH.sizeDelta = new Vector2(0.85f, 0.22f);
            }

            // 2. 中央大图标 (如果非空)
            if (!string.IsNullOrEmpty(cardIcon))
            {
                GameObject iconGo = new GameObject("CardIcon");
                iconGo.transform.SetParent(transform, false);
                float yOffset = string.IsNullOrEmpty(categoryTag) ? 0.05f : 0.10f;
                iconGo.transform.localPosition = new Vector3(0f, yOffset, -0.02f);
                iconGo.transform.localRotation = Quaternion.identity;

                _tmpIcon = iconGo.AddComponent<TextMeshPro>();
                _tmpIcon.text = cardIcon;
                _tmpIcon.enableAutoSizing = true;
                _tmpIcon.fontSizeMin = 0.5f;
                _tmpIcon.fontSizeMax = 1.2f;
                _tmpIcon.alignment = TextAlignmentOptions.Center;
                _tmpIcon.color = Color.white;

                RectTransform rtI = _tmpIcon.GetComponent<RectTransform>();
                rtI.sizeDelta = new Vector2(0.85f, 0.35f);
            }

            // 3. 底部 / 主要标题文本
            // 有图片时，文字缩小并偏移到底部，避免遮挡图片主体
            string mainText = !string.IsNullOrEmpty(cardHeadline) ? cardHeadline : cardText;
            if (!string.IsNullOrEmpty(mainText))
            {
                GameObject mainGo = new GameObject("CardMainText");
                mainGo.transform.SetParent(transform, false);

                float yOffset;
                if (assignedTexture != null)
                {
                    // 有图片时文字沉到底部
                    yOffset = -0.32f;
                }
                else if (!string.IsNullOrEmpty(cardIcon))
                {
                    yOffset = -0.18f;
                }
                else if (string.IsNullOrEmpty(categoryTag))
                {
                    yOffset = 0f;
                }
                else
                {
                    yOffset = -0.05f;
                }

                mainGo.transform.localPosition = new Vector3(0f, yOffset, -0.02f);
                mainGo.transform.localRotation = Quaternion.identity;

                _tmpMain = mainGo.AddComponent<TextMeshPro>();
                _tmpMain.text = mainText;
                _tmpMain.enableAutoSizing = true;
                _tmpMain.fontSizeMin = 0.18f;
                _tmpMain.fontSizeMax = assignedTexture != null ? 0.45f : 0.75f; // 有图时字体缩小
                _tmpMain.alignment = TextAlignmentOptions.Center;
                _tmpMain.color = Color.white;
                _tmpMain.enableWordWrapping = true;
                _tmpMain.fontStyle = FontStyles.Bold;

                RectTransform rtM = _tmpMain.GetComponent<RectTransform>();
                rtM.sizeDelta = new Vector2(0.82f, assignedTexture != null ? 0.30f : 0.55f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Glitch 视觉扭曲系统
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 程序化 Glitch 扭曲系统。
        /// Phase 1 (0.00~0.35): glitch=0，画面完全干净清晰。
        /// Phase 2 (0.35~0.70): glitch=0.1~0.4，轻度扫描线 + UV 偏移 + 色调漂移。
        /// Phase 3 (0.70~0.96): glitch=0.4~1.0，重度 UV 撕裂 + 颜色通道分裂 + 诡异色块闪烁。
        /// 更新频率被限速（不每帧执行），节省 CPU 性能。
        /// </summary>
        private void UpdateGlitch()
        {
            // 从 Stage5Controller 单例读取当前进度（与 FeedEnvironment 采用同样方式）
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            // 计算当前 Glitch 强度
            if (phaseProgress < 0.35f)
            {
                _currentGlitch = 0f;
            }
            else if (phaseProgress < 0.70f)
            {
                _currentGlitch = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress) * 0.4f; // 0 → 0.4
            }
            else
            {
                _currentGlitch = Mathf.Lerp(0.4f, 1.0f, Mathf.InverseLerp(0.70f, 0.96f, phaseProgress)); // 0.4 → 1.0
            }

            // Phase 1 完全跳过 Glitch 逻辑，保证干净
            if (_currentGlitch < 0.01f)
            {
                if (_innerCardMat != null && assignedTexture != null)
                {
                    _innerCardMat.mainTextureOffset = Vector2.zero;
                    _innerCardMat.mainTextureScale  = Vector2.one;
                }
                return;
            }

            // 限速：不每帧执行，产生自然的 Glitch 节律感
            _glitchTimer += Time.deltaTime;
            if (_glitchTimer < _glitchUpdateInterval) return;
            _glitchTimer = 0f;
            _glitchUpdateInterval = Random.Range(0.05f, 0.20f); // 不规则时间间隔，有机感

            // ── 1. UV 偏移扫描线撕裂 ──
            if (_innerCardMat != null && assignedTexture != null)
            {
                bool doShift = Random.value < _currentGlitch * 0.65f;
                if (doShift)
                {
                    float maxShift = _currentGlitch > 0.6f ? 0.18f : 0.04f;
                    _innerCardMat.mainTextureOffset = new Vector2(
                        Random.Range(-maxShift, maxShift),
                        Random.Range(-maxShift * 0.5f, maxShift * 0.5f)
                    );

                    // Phase 3 额外：局部缩放撕裂（图像像素块化感）
                    if (_currentGlitch > 0.6f && Random.value < 0.35f)
                    {
                        float sx = Random.Range(0.88f, 1.12f);
                        _innerCardMat.mainTextureScale = new Vector2(sx, 1f / sx);
                    }
                    else
                    {
                        _innerCardMat.mainTextureScale = Vector2.one;
                    }
                }
                else
                {
                    // 大多数时候回到正常，Glitch 是偶发性的
                    _innerCardMat.mainTextureOffset = Vector2.zero;
                    _innerCardMat.mainTextureScale  = Vector2.one;
                }
            }

            // ── 2. 内框诡异色调闪烁 ──
            if (_innerFrameRend != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                bool doColorGlitch = Random.value < _currentGlitch * 0.45f;
                if (doColorGlitch)
                {
                    // 随机的赛博朋克/恐怖色调：洋红、绿、青、红
                    float[] glitchHues = { 0.0f, 0.33f, 0.5f, 0.83f, 0.95f };
                    float h = glitchHues[Random.Range(0, glitchHues.Length)];
                    Color gc = Color.HSVToRGB(h, 0.85f, 0.55f);
                    gc.a = _currentGlitch * 0.55f * _alpha;
                    mpb.SetColor("_BaseColor", gc);
                    mpb.SetColor("_Color",     gc);
                }
                else
                {
                    // 正常状态：透明（让内框 material 直接显示纹理）
                    Color normal = new Color(0f, 0f, 0f, 0f);
                    mpb.SetColor("_BaseColor", normal);
                    mpb.SetColor("_Color",     normal);
                }
                _innerFrameRend.SetPropertyBlock(mpb);
            }

            // ── 3. Glitch 色块叠加层闪烁 ──
            if (_glitchOverlayRend != null)
            {
                bool doFlash = Random.value < _currentGlitch * 0.38f;
                MaterialPropertyBlock overlayMpb = new MaterialPropertyBlock();
                if (doFlash)
                {
                    // 赛博朋克系色系：洋红/青/蓝/红
                    float[] flashHues = { 0.0f, 0.5f, 0.55f, 0.85f };
                    float fh = flashHues[Random.Range(0, flashHues.Length)];
                    Color fc = Color.HSVToRGB(fh, 0.95f, 0.95f);
                    fc.a = Random.Range(0.08f, 0.30f) * _currentGlitch * _alpha;
                    overlayMpb.SetColor("_BaseColor", fc);
                    overlayMpb.SetColor("_Color",     fc);
                }
                else
                {
                    overlayMpb.SetColor("_BaseColor", Color.clear);
                    overlayMpb.SetColor("_Color",     Color.clear);
                }
                _glitchOverlayRend.SetPropertyBlock(overlayMpb);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 颜色 / 透明度
        // ─────────────────────────────────────────────────────────────────────────

        private void ApplyColor(Color c)
        {
            if (_renderer == null) return;
            
            // 【关键修改】隐藏底层标准的 Renderer（彻底消灭直角色彩大方块）
            _renderer.enabled = false;
        }

        private void ApplyAlpha(float a)
        {
            if (_renderer == null) return;
            Color c = cardColor;
            c.a = a;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_mpb);

            if (_renderer.material != null && _renderer.material.HasProperty("_Color"))
            {
                _renderer.material.color = c;
            }

            // 内框淡出（无纹理时显示纯色背景）
            if (_innerFrameRend != null && assignedTexture == null)
            {
                Color innerBg = isPrivateDataCard
                    ? new Color(0.05f, 0.06f, 0.1f,  0.95f * a)
                    : new Color(0.08f, 0.09f, 0.14f, 0.92f * a);
                if (isChoiceCard) innerBg = new Color(0.12f, 0.1f, 0.18f, 0.95f * a);
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", innerBg);
                mpb.SetColor("_Color",     innerBg);
                _innerFrameRend.SetPropertyBlock(mpb);
            }

            // 内框纹理有图时，只需降低 material 的整体 alpha 色
            if (_innerCardMat != null && assignedTexture != null)
            {
                Color tint = new Color(1f, 1f, 1f, a);
                if (_innerCardMat.HasProperty("_BaseColor")) _innerCardMat.SetColor("_BaseColor", tint);
                if (_innerCardMat.HasProperty("_Color"))     _innerCardMat.SetColor("_Color",     tint);
            }

            // Glitch 叠加层淡出（alpha 单独受 _alpha 影响，无需额外处理，UpdateGlitch 内已乘 _alpha）

            // 所有文字层淡出
            if (_tmpMain   != null) { Color tc = _tmpMain.color;   tc.a = a;          _tmpMain.color   = tc; }
            if (_tmpHeader != null) { Color tc = _tmpHeader.color; tc.a = a * 0.75f;  _tmpHeader.color = tc; }
            if (_tmpIcon   != null) { Color tc = _tmpIcon.color;   tc.a = a;          _tmpIcon.color   = tc; }
        }
    }
}
