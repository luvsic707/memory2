using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 打字机交互组件 (阶段 4: 现代/办公室)
    /// 模拟《闪灵》经典名场面：
    /// 1. 玩家每次按下 Q 键交互，都会让后台 workCount 增加，并往屏幕上打出《闪灵》著名的重复段落。
    /// 2. 带有轻微的随机拼写错误（模拟 Jack Torrance 逐渐发疯的状态）。
    /// 3. 智能机制：如果场景中找不到配置的 UI，脚本会在运行时自动动态生成一个电影级复古字纸 UI 覆盖在屏幕中央，完全免去手动部署 UI 的麻烦。
    /// </summary>
    public class TypewriterInteractable : MonoBehaviour, IInteractable
    {
        [Header("UI 绑定 (可选，为空时会自动在场景中搜寻/动态生成)")]
        [Tooltip("用于显示打字内容的 UI 面板（可复用 Letter_Panel）")]
        public GameObject letterPanel;

        [Tooltip("用于显示打字内容的 TextMeshProUGUI 组件（可复用 Letter_Text）")]
        public TextMeshProUGUI letterText;

        [Header("打字机音效 (可选)")]
        [Tooltip("打字按键音效库")]
        public AudioClip[] keySounds;
        [Tooltip("打字机换行/回车音效")]
        public AudioClip carriageReturnSound;

        [Header("打字参数配置")]
        [Tooltip("每次按 Q 交互时，新增打出的字符个数 (推荐 2-3，可以让打字显得不至于过慢)")]
        public int charsPerPress = 3;

        [Tooltip("是否允许偶尔产生拼写错误以还原《闪灵》细节")]
        public bool allowTypos = true;

        [Tooltip("拼写错误的概率")]
        [Range(0f, 0.2f)] public float typoProbability = 0.04f;

        [Header("交互提示文本")]
        [SerializeField] private string interactHint = "使用打字机工作";

        private AudioSource audioSource;
        private string targetText = "All work and no play makes Jack a dull boy.\n";
        private string currentTypedContent = "";
        private int currentCharIndex = 0;
        private bool isTypingActive = false;

        // 实现 IInteractable 接口的属性
        public string InteractHint => interactHint;

        private void Awake()
        {
            // 自动配置音频组件
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // 自动配置并适配 BoxCollider (防御 100 倍缩放等外部导入问题)
            AutoFitCollider();

            // 智能搜寻场景中已有的 Letter_Panel
            if (letterPanel == null)
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject go in allObjects)
                {
                    if (go.name == "Letter_Panel" && !go.hideFlags.HasFlag(HideFlags.HideInHierarchy))
                    {
                        letterPanel = go;
                        break;
                    }
                }
            }

            // 智能搜寻 TextMeshProUGUI
            if (letterText == null && letterPanel != null)
            {
                letterText = letterPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            // 防御编程：如果场景里真的找不到 Letter_Panel，运行时自动动态生成一个美丽的《闪灵》打字机字纸 UI！
            if (letterPanel == null)
            {
                CreateDynamicUI();
            }
        }

        private void AutoFitCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            // 获取所有子物体渲染器的包围盒，计算出世界坐标系下的总大小
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // 如果没有任何渲染器，退回到默认大小
                box.size = Vector3.one;
                box.center = Vector3.zero;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // 将世界坐标下的包围盒中心和大小转换为本地坐标系（以消除父物体 100 倍缩放等问题）
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);

            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            box.center = localCenter;
            box.size = localSize;

            Debug.Log($"[Typewriter] 自动适配了 BoxCollider！本地中心: {box.center}，本地大小: {box.size}");
        }

        /// <summary>
        /// 动态在 Canvas 下生成打字机字纸 UI，免去用户手动配置 UI 面板的麻烦
        /// </summary>
        private void CreateDynamicUI()
        {
            // 1. 寻找或自动创建 Canvas
            Canvas canvas = null;
#if UNITY_2023_1_OR_NEWER
            canvas = FindAnyObjectByType<Canvas>();
#else
            canvas = FindObjectOfType<Canvas>();
#endif
            GameObject canvasGo = null;
            if (canvas == null)
            {
                canvasGo = new GameObject("TypewriterCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            else
            {
                canvasGo = canvas.gameObject;
            }

            // 2. 创建字纸面板 (TypewriterPaperPanel)
            letterPanel = new GameObject("TypewriterPaperPanel");
            letterPanel.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = letterPanel.AddComponent<RectTransform>();
            // 屏幕居中定位，宽 750，高 900 (完美的竖版信纸比例)
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(750, 900);
            panelRect.anchoredPosition = Vector2.zero;

            // 添加复古纸张底色 Image 组件
            UnityEngine.UI.Image paperImage = letterPanel.AddComponent<UnityEngine.UI.Image>();
            paperImage.color = new Color(0.96f, 0.95f, 0.91f, 0.98f); // 复古泛黄牛皮信纸暖白底色

            // 3. 创建极细黑色边框以增强纸张纸质感
            GameObject borderGo = new GameObject("Border");
            borderGo.transform.SetParent(letterPanel.transform, false);
            RectTransform borderRect = borderGo.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderRect.anchoredPosition = Vector2.zero;
            UnityEngine.UI.Outline borderOutline = borderGo.AddComponent<UnityEngine.UI.Outline>();
            borderOutline.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.35f);
            borderOutline.effectDistance = new Vector2(2, -2);

            // 4. 创建打字文本框 (TypewriterText)
            GameObject textGo = new GameObject("TypewriterText");
            textGo.transform.SetParent(letterPanel.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            // 设置页边距 (上下左右留空 50 像素)
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-100, -100); 
            textRect.anchoredPosition = Vector2.zero;

            letterText = textGo.AddComponent<TextMeshProUGUI>();
            letterText.color = new Color(0.12f, 0.12f, 0.12f, 1f); // 墨碳黑色打字油墨痕迹
            letterText.fontSize = 25f;
            letterText.alignment = TextAlignmentOptions.TopLeft;
            letterText.enableWordWrapping = true;
            letterText.text = "";

            Debug.Log("[Typewriter] 已成功在场景中动态部署了电影级打字纸 UI！");
        }

        private void Start()
        {
            // 初始隐藏 UI
            if (letterPanel != null)
            {
                letterPanel.SetActive(false);
            }
        }

        // 实现 IInteractable 接口的交互方法
        public void Interact()
        {
            // 首次交互或 UI 被关闭时，打开 UI 面板
            if (letterPanel != null && !letterPanel.activeSelf)
            {
                letterPanel.SetActive(true);
                isTypingActive = true;
                if (letterText != null)
                {
                    letterText.text = currentTypedContent;
                }
            }

            // 1. 跨场景行为数据增加
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddWork();
            }
            else
            {
                Debug.LogWarning("[Typewriter] 找不到 PlayerBehaviorData 实例！无法记录工作计数。");
            }

            // 2. 模拟打字机打入字符
            TypeNextCharacters();

            // 3. 播放随机打字声
            PlayTypewriterSound();
        }

        private void TypeNextCharacters()
        {
            for (int i = 0; i < charsPerPress; i++)
            {
                char nextChar = targetText[currentCharIndex];

                // 随机制造拼写错误以体现《闪灵》狂躁的氛围 (仅针对字母进行替换)
                if (allowTypos && Random.value < typoProbability && char.IsLetter(nextChar))
                {
                    // 随机替换为另一个小写字母
                    nextChar = (char)Random.Range('a', 'z' + 1);
                }

                currentTypedContent += nextChar;

                // 换行音效触发
                if (nextChar == '\n')
                {
                    if (carriageReturnSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(carriageReturnSound);
                    }
                }

                // 循环索取目标字符串
                currentCharIndex = (currentCharIndex + 1) % targetText.Length;
            }

            // 限制页面文本总字符数以防止 UI 溢出崩溃 (超出时滑动裁切，保留后 1000 字符)
            if (currentTypedContent.Length > 1000)
            {
                currentTypedContent = currentTypedContent.Substring(currentTypedContent.Length - 1000);
            }

            // 更新显示文本
            if (letterText != null)
            {
                letterText.text = currentTypedContent;
            }
        }

        private void PlayTypewriterSound()
        {
            if (audioSource == null) return;

            // 如果正在播放换行音效，暂不打断它
            if (carriageReturnSound != null && audioSource.isPlaying && audioSource.clip == carriageReturnSound)
            {
                return;
            }

            if (keySounds != null && keySounds.Length > 0)
            {
                AudioClip randomKey = keySounds[Random.Range(0, keySounds.Length)];
                audioSource.PlayOneShot(randomKey);
            }
        }

        private void Update()
        {
            // 如果玩家按下 ESC，关闭打字机字纸显示
            if (isTypingActive && letterPanel != null && letterPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    letterPanel.SetActive(false);
                    isTypingActive = false;
                }
            }
        }
    }
}
