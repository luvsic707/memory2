using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 单张内容卡片的行为控制。
    /// 负责：漂移运动、注视吸引、愉悦反馈音效、Phase C 声音抽走、多层富文本视觉呈现。
    /// 由 ContentCardSpawner 生成并配置。
    /// </summary>
    public class ContentCard : MonoBehaviour
    {
        [Header("运动参数")]
        [Tooltip("缓慢漂向玩家的基础速度")]
        public float driftSpeed = 1.5f;

        [Tooltip("被注视时的吸引加速度")]
        public float gazeAttractSpeed = 8f;

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

        // 回调：通知 Stage5Controller 发生了注视交互
        public System.Action<ContentCard> OnGazeInteract;
        // 回调：通知选择卡被激活
        public System.Action<string> OnChoiceSelected;

        private Vector3 _baseScale;

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;

            // 找到玩家摄像机
            _player = Camera.main != null ? Camera.main.transform : null;

            // 创建内嵌深色卡片背景（制造精美的边框与层级感）
            CreateInnerCardFrame();

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
        }

        /// <summary>
        /// 由 Stage5Controller 在射线命中时调用
        /// </summary>
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

        /// <summary>
        /// 注视离开
        /// </summary>
        public void OnGazeExit()
        {
            _isBeingGazed = false;
        }

        private void CreateInnerCardFrame()
        {
            GameObject innerGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            innerGo.name = "InnerFrame";
            innerGo.transform.SetParent(transform, false);
            innerGo.transform.localPosition = new Vector3(0f, 0f, -0.01f); // 在外框前面 (-0.01)，在文字后面 (-0.02)
            innerGo.transform.localScale = new Vector3(0.90f, 0.90f, 1f); // 留出 10% 彩色发光外边框

            Collider col = innerGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _innerFrameRend = innerGo.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material innerMat = new Material(shader);
            if (innerMat.HasProperty("_Surface")) innerMat.SetFloat("_Surface", 0); // Opaque 保证不透光
            _innerFrameRend.material = innerMat;

            // 内部背景统一使用极具质感的高对比度暗夜黑灰（#0E101A）
            Color innerBg = isPrivateDataCard ? new Color(0.04f, 0.05f, 0.08f, 0.98f) : new Color(0.07f, 0.08f, 0.13f, 0.96f);
            if (isChoiceCard) innerBg = new Color(0.09f, 0.08f, 0.15f, 0.98f);

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", innerBg);
            mpb.SetColor("_Color", innerBg);
            _innerFrameRend.SetPropertyBlock(mpb);
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
            string mainText = !string.IsNullOrEmpty(cardHeadline) ? cardHeadline : cardText;
            if (!string.IsNullOrEmpty(mainText))
            {
                GameObject mainGo = new GameObject("CardMainText");
                mainGo.transform.SetParent(transform, false);
                float yOffset = !string.IsNullOrEmpty(cardIcon) ? -0.18f : -0.05f;
                if (string.IsNullOrEmpty(categoryTag) && string.IsNullOrEmpty(cardIcon)) yOffset = 0f;

                mainGo.transform.localPosition = new Vector3(0f, yOffset, -0.02f);
                mainGo.transform.localRotation = Quaternion.identity;

                _tmpMain = mainGo.AddComponent<TextMeshPro>();
                _tmpMain.text = mainText;
                _tmpMain.enableAutoSizing = true;
                _tmpMain.fontSizeMin = 0.25f;
                _tmpMain.fontSizeMax = 0.75f;
                _tmpMain.alignment = TextAlignmentOptions.Center;
                _tmpMain.color = Color.white;
                _tmpMain.enableWordWrapping = true;
                _tmpMain.fontStyle = FontStyles.Bold;

                RectTransform rtM = _tmpMain.GetComponent<RectTransform>();
                rtM.sizeDelta = new Vector2(0.82f, 0.55f);
            }
        }

        private void ApplyColor(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_mpb);

            if (_renderer.material != null && _renderer.material.HasProperty("_Color"))
            {
                _renderer.material.color = c;
            }
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

            // 内框淡出
            if (_innerFrameRend != null)
            {
                Color innerBg = isPrivateDataCard ? new Color(0.05f, 0.06f, 0.1f, 0.95f * a) : new Color(0.08f, 0.09f, 0.14f, 0.92f * a);
                if (isChoiceCard) innerBg = new Color(0.12f, 0.1f, 0.18f, 0.95f * a);
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", innerBg);
                mpb.SetColor("_Color", innerBg);
                _innerFrameRend.SetPropertyBlock(mpb);
            }

            // 所有文字层淡出
            if (_tmpMain != null) { Color tc = _tmpMain.color; tc.a = a; _tmpMain.color = tc; }
            if (_tmpHeader != null) { Color tc = _tmpHeader.color; tc.a = a * 0.75f; _tmpHeader.color = tc; }
            if (_tmpIcon != null) { Color tc = _tmpIcon.color; tc.a = a; _tmpIcon.color = tc; }
        }
    }
}
