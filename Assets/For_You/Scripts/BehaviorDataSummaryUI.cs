using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 行为特征总结与个性化推送诊断 UI (Behavior Data Summary UI)
    /// 1. 用于第五章（当代）：读取前四章的行为特征数据，并在 UI 上直观展示。
    /// 2. 支持语言选择 (防止默认字体不支持中文产生方块字/Tofu blocks)
    /// 3. 支持完全自定义 UI：如果在 Inspector 中拖入了自己摆放的 UI 文本，脚本会使用您的 UI，不会自动生成。
    /// </summary>
    public class BehaviorDataSummaryUI : MonoBehaviour
    {
        public enum Language
        {
            Chinese,
            English
        }

        [Header("语言设置 (LiberationSans SDF 默认字体不支持中文，建议选 English 避免方块字)")]
        [Tooltip("如果未导入中文字体，请选 English。若使用中文字体，可设为 Chinese。")]
        public Language displayLanguage = Language.English;

        [Header("UI 文本组件绑定 (手动绑定后将使用您自己设计的排版与字体)")]
        [Tooltip("香蕉交互次数展示文本")]
        public TextMeshProUGUI bananaCountText;
        [Tooltip("祈祷交互次数展示文本")]
        public TextMeshProUGUI prayerCountText;
        [Tooltip("推巨石次数展示文本")]
        public TextMeshProUGUI pushCountText;
        [Tooltip("办公室打字/工作次数展示文本")]
        public TextMeshProUGUI workCountText;

        [Header("算法画像报告文本")]
        [Tooltip("显示生成的个性化用户画像与推送广告建议")]
        public TextMeshProUGUI diagnosticReportText;

        [Header("测试模拟设置 (直接在当前关卡运行且前关数据为 0 时生效)")]
        [Tooltip("是否启用调试模拟数据")]
        public bool enableDebugSimulation = true;
        public int simBananaCount = 8;
        public int simPrayerCount = 15;
        public int simPushCount = 3;
        public int simWorkCount = 45;

        // 用于保存动态生成的 UI 面板引用以便控制隐藏/显示
        private GameObject letterPanel;
        private bool isPanelActive = true;

        private void Awake()
        {
            // 【核心设计】：如果用户没有手动拖拽绑定任何文本框，则由脚本动态生成高科技排版 UI
            if (bananaCountText == null && diagnosticReportText == null)
            {
                CreateDynamicUI();
            }
            else
            {
                // 如果是手动绑定模式，把挂载脚本的物体当作面板根节点 (如果有的话)
                letterPanel = gameObject;
            }
        }

        private void Start()
        {
            UpdateSummaryDisplay();

            // 如果显示了 UI，默认开启鼠标指针以方便用户交互/关闭
            if (letterPanel != null && letterPanel.activeSelf)
            {
                CursorService.Unlock();
            }
        }

        /// <summary>
        /// 刷新数据展示与算法分析画像
        /// </summary>
        public void UpdateSummaryDisplay()
        {
            // 1. 获取行为数据
            int bananas = 0;
            int prayers = 0;
            int pushes = 0;
            int works = 0;

            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes = PlayerBehaviorData.Instance.pushCount;
                works = PlayerBehaviorData.Instance.workCount;
            }

            // 2. 检测是否需要应用调试模拟数据 (当所有累积数据均为 0 时自动判定为调试运行)
            if (enableDebugSimulation && bananas == 0 && prayers == 0 && pushes == 0 && works == 0)
            {
                bananas = simBananaCount;
                prayers = simPrayerCount;
                pushes = simPushCount;
                works = simWorkCount;
                Debug.Log("<color=cyan>[BehaviorDataSummary] 前四关卡数据为空，已自动加载 Inspector 配置的模拟测试数据。</color>");
            }

            // 3. 更新基础数据 UI 显示
            if (bananaCountText != null)
            {
                if (prayerCountText == null)
                {
                    // 动态 UI 模式：采用分行整合显示，并使用富文本渲染
                    if (displayLanguage == Language.Chinese)
                    {
                        bananaCountText.text = $"🍌 香蕉采集次数:  <b><color=yellow>{bananas}</color></b> 次\n\n" +
                                               $"🙏 虔诚祈祷次数:  <b><color=cyan>{prayers}</color></b> 次\n\n" +
                                               $"🪨 巨石推山次数:  <b><color=orange>{pushes}</color></b> 次\n\n" +
                                               $"💼 办公室工作量:  <b><color=red>{works}</color></b> 次";
                    }
                    else
                    {
                        bananaCountText.text = $"Banana:  <b><color=yellow>{bananas}</color></b>\n\n" +
                                               $"Prayer:  <b><color=cyan>{prayers}</color></b>\n\n" +
                                               $"Push:  <b><color=orange>{pushes}</color></b>\n\n" +
                                               $"Work:  <b><color=red>{works}</color></b>";
                    }
                }
                else
                {
                    // 手动摆放模式：独立更新
                    if (displayLanguage == Language.Chinese)
                    {
                        bananaCountText.text = $"🍌 香蕉采集: {bananas} 次";
                        if (prayerCountText != null) prayerCountText.text = $"🙏 虔诚祈祷: {prayers} 次";
                        if (pushCountText != null) pushCountText.text = $"🪨 推石上山: {pushes} 次";
                        if (workCountText != null) workCountText.text = $"💼 机械劳动: {works} 次";
                    }
                    else
                    {
                        bananaCountText.text = $"Banana: {bananas}";
                        if (prayerCountText != null) prayerCountText.text = $"Prayer: {prayers}";
                        if (pushCountText != null) pushCountText.text = $"Push: {pushes}";
                        if (workCountText != null) workCountText.text = $"Work: {works}";
                    }
                }
            }

            // 4. 执行个性化用户算法画像分析
            if (diagnosticReportText != null)
            {
                diagnosticReportText.text = GenerateDiagnosticReport(bananas, prayers, pushes, works);
            }
        }

        /// <summary>
        /// 基于前四章的数据，计算并生成个性的用户算法画像
        /// </summary>
        private string GenerateDiagnosticReport(int bananas, int prayers, int pushes, int works)
        {
            int totalInteracts = bananas + prayers + pushes + works;
            if (totalInteracts == 0)
            {
                return displayLanguage == Language.Chinese
                    ? "【数据链路异常】\n系统暂未收集到您的行为习惯数据。"
                    : "【DATALINK ERROR】\nNo behavior logs detected from previous stages.";
            }

            string portrait = "";
            int maxVal = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));

            if (displayLanguage == Language.Chinese)
            {
                portrait = "<b>📊 【算法数据处理中心 - 诊断画像】</b>\n\n";

                if (maxVal == bananas)
                {
                    portrait += "<b>用户画像主标签</b>：<color=yellow>「返祖纯真者 (Primal ape)」</color>\n\n";
                    portrait += "<b>特征分析评估</b>：您对物质享受（采集香蕉）表现出极高热忱。符合灵长类最原始的反射弧，思维回路简单，容易受到即时物质奖励的引导与支配。\n\n";
                    portrait += "<b>🎯 智能算法精准推送</b>：\n- 🍌 智人牌熟透有机香蕉（买一送一）\n- 🌴 热带雨林沉浸式徒手求生体验入场券\n- 🦧 测一测您的猩猩基因纯度与血缘追溯";
                }
                else if (maxVal == prayers)
                {
                    portrait += "<b>用户画像主标签</b>：<color=cyan>「精神唯心者 (Spiritual dreamer)」</color>\n\n";
                    portrait += "<b>特征分析评估</b>：您执着于对虚无雕像进行没有科学反馈的机械祈祷。情绪波动大，精神处于极高敏感状态，极其容易接受外部强加的精神信仰与心理暗示。\n\n";
                    portrait += "<b>🎯 智能算法精准推送</b>：\n- 🙏 圣山开光金丝楠木防焦虑手串\n- 🧘 每日正念冥想自愈高级版 App 会员\n- 🏛️ 古代遗迹信仰之光探索路线门票";
                }
                else if (maxVal == pushes)
                {
                    portrait += "<b>用户画像主标签</b>：<color=orange>「荒诞受虐者 (Absurd Sisyphus)」</color>\n\n";
                    portrait += "<b>特征分析评估</b>：您深知巨石将重复滚回坡底，却仍旧乐此不疲地将其推向山顶。您的存在主义觉悟拉满，对无效劳动的耐受度极高，适合承担无回报的枯燥循环工作。\n\n";
                    portrait += "<b>🎯 智能算法精准推送</b>：\n- 🪨 重型举重及攀岩手套（专业耐磨防滑）\n- 📖 阿尔贝·加缪著作《西西弗斯神话》译本\n- 📦 减压指尖重力玩具（解构单调日常）";
                }
                else // works
                {
                    portrait += "<b>用户画像主标签</b>：<color=red>「系统最佳零件 (Systemic cog)」</color>\n\n";
                    portrait += "<b>特征分析评估</b>：您在办公室的旧打字机上敲出了庞大的行文记录。您对教条式、枯燥无意义的工作有着超乎常人的服从度。您是系统中最稳定、最受偏爱的螺丝钉。\n\n";
                    portrait += "<b>🎯 智能算法精准推送</b>：\n- ☕ 特浓重焙深度提神挂耳咖啡（包年订阅）\n- ⌨️ 静音茶轴机械键盘与高效润滑油脂\n- 🏥 颈椎舒缓康复治疗与失眠脑电波调理仪";
                }

                portrait += "\n\n<size=16><color=#666666><i>*系统声明：我们全天候保障数据隐私，推荐服务默认开启。</i></color></size>";
            }
            else
            {
                portrait = "<b>📊 【ALGORITHM PROFILE DIAGNOSTIC】</b>\n\n";

                if (maxVal == bananas)
                {
                    portrait += "<b>User Classification</b>: <color=yellow>「Primal Ape (Primal Ape)」</color>\n\n";
                    portrait += "<b>Behavior Analysis</b>: You show extreme enthusiasm for primal physical rewards (gathering bananas). Your neural responses resemble primal primates, making you highly susceptible to immediate material gratification.\n\n";
                    portrait += "<b>🎯 Target Recommendations</b>:\n- 🍌 Premium Ripe Bananas (Buy 1 Get 1 Free)\n- 🌴 Jungle Survival Immersive Experience Ticket\n- 🦧 Test Your Ape DNA Heritage & Ancestry";
                }
                else if (maxVal == prayers)
                {
                    portrait += "<b>User Classification</b>: <color=cyan>「Spiritual Dreamer (Spiritual Dreamer)」</color>\n\n";
                    portrait += "<b>Behavior Analysis</b>: You persist in mechanical prayers to a hollow statue with no scientific feedback. You are emotionally sensitive and highly susceptible to external spiritual suggestions and beliefs.\n\n";
                    portrait += "<b>🎯 Target Recommendations</b>:\n- 🙏 High-grade Meditation Beads (20% Off Today)\n- 🧘 Premium Mindfulness Meditation App Subscription\n- 🏛️ Holy Ruins & Ancient Faith Guided Tour Ticket";
                }
                else if (maxVal == pushes)
                {
                    portrait += "<b>User Classification</b>: <color=orange>「Absurd Sisyphus (Absurd Sisyphus)」</color>\n\n";
                    portrait += "<b>Behavior Analysis</b>: You push the rock up the hill repeatedly, knowing it will roll back down. Your existential endurance is fully maxed. You are perfect for repetitive, unrewarding loops of labor.\n\n";
                    portrait += "<b>🎯 Target Recommendations</b>:\n- 🪨 Heavy-duty Weightlifting & Climbing Gloves\n- 📖 'The Myth of Sisyphus' by Albert Camus\n- 📦 Gravity Fidget Toy (Deconstruct the monotony of life)";
                }
                else // works
                {
                    portrait += "<b>User Classification</b>: <color=red>「Perfect Systemic Cog (Systemic cog)」</color>\n\n";
                    portrait += "<b>Behavior Analysis</b>: You typed a massive amount of repetitive text in the office. Your compliance with monotonous, dogmatic labor is outstanding. You are the system's favorite reliable screw.\n\n";
                    portrait += "<b>🎯 Target Recommendations</b>:\n- ☕ Extra Strong Dark Roast Coffee Subscription\n- ⌨️ Silent Mechanical Keyboard and Lube Kit\n- 🏥 Spine Rehabilitation Therapy & Insomnia treatment";
                }

                portrait += "\n\n<size=16><color=#666666><i>*Privacy Agreement: We monitor your operations 24/7. Target recommendations are default enabled.</i></color></size>";
            }

            return portrait;
        }

        /// <summary>
        /// 动态在 Canvas 下生成高科技画像 UI 面板
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
                canvasGo = new GameObject("SummaryCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            else
            {
                canvasGo = canvas.gameObject;
            }

            // 2. 创建全屏底板
            GameObject bgPanel = new GameObject("SummaryBgPanel");
            bgPanel.transform.SetParent(canvasGo.transform, false);
            RectTransform bgRect = bgPanel.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;

            UnityEngine.UI.Image bgImage = bgPanel.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.05f, 0.06f, 0.08f, 0.98f); // 极客黑色调

            // 3. 主布局框架容器
            GameObject mainContainer = new GameObject("MainContainer");
            mainContainer.transform.SetParent(bgPanel.transform, false);
            RectTransform containerRect = mainContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.08f, 0.12f);
            containerRect.anchorMax = new Vector2(0.92f, 0.92f);
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;

            // 4. 左分栏：历史数据区
            GameObject leftPanel = new GameObject("LeftDataPanel");
            leftPanel.transform.SetParent(mainContainer.transform, false);
            RectTransform leftRect = leftPanel.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.42f, 1f);
            leftRect.sizeDelta = Vector2.zero;
            leftRect.anchoredPosition = Vector2.zero;

            // 左分栏科技描边
            UnityEngine.UI.Outline leftOutline = leftPanel.AddComponent<UnityEngine.UI.Outline>();
            leftOutline.effectColor = new Color(0.0f, 0.6f, 1.0f, 0.25f);
            leftOutline.effectDistance = new Vector2(1, -1);

            // 左分栏标题
            GameObject leftTitleGo = new GameObject("LeftTitle");
            leftTitleGo.transform.SetParent(leftPanel.transform, false);
            RectTransform leftTitleRect = leftTitleGo.AddComponent<RectTransform>();
            leftTitleRect.anchorMin = new Vector2(0.06f, 0.88f);
            leftTitleRect.anchorMax = new Vector2(0.94f, 0.96f);
            leftTitleRect.sizeDelta = Vector2.zero;
            leftTitleRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI leftTitle = leftTitleGo.AddComponent<TextMeshProUGUI>();
            leftTitle.text = "<b>📡 DATA INTEGRATION</b>";
            leftTitle.fontSize = 24f;
            leftTitle.color = new Color(0.0f, 0.8f, 1.0f, 1f);

            // 左分栏数值展示文本
            GameObject leftContentGo = new GameObject("LeftContentText");
            leftContentGo.transform.SetParent(leftPanel.transform, false);
            RectTransform leftContentRect = leftContentGo.AddComponent<RectTransform>();
            leftContentRect.anchorMin = new Vector2(0.06f, 0.05f);
            leftContentRect.anchorMax = new Vector2(0.94f, 0.82f);
            leftContentRect.sizeDelta = Vector2.zero;
            leftContentRect.anchoredPosition = Vector2.zero;

            bananaCountText = leftContentGo.AddComponent<TextMeshProUGUI>();
            bananaCountText.fontSize = 23f;
            bananaCountText.color = Color.white;
            bananaCountText.alignment = TextAlignmentOptions.TopLeft;

            // 5. 右分栏：诊断报告区
            GameObject rightPanel = new GameObject("RightReportPanel");
            rightPanel.transform.SetParent(mainContainer.transform, false);
            RectTransform rightRect = rightPanel.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.45f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.sizeDelta = Vector2.zero;
            rightRect.anchoredPosition = Vector2.zero;

            // 右分栏科技描边
            UnityEngine.UI.Outline rightOutline = rightPanel.AddComponent<UnityEngine.UI.Outline>();
            rightOutline.effectColor = new Color(0.0f, 0.9f, 0.5f, 0.25f);
            rightOutline.effectDistance = new Vector2(1, -1);

            // 右分栏标题
            GameObject rightTitleGo = new GameObject("RightTitle");
            rightTitleGo.transform.SetParent(rightPanel.transform, false);
            RectTransform rightTitleRect = rightTitleGo.AddComponent<RectTransform>();
            rightTitleRect.anchorMin = new Vector2(0.06f, 0.88f);
            rightTitleRect.anchorMax = new Vector2(0.94f, 0.96f);
            rightTitleRect.sizeDelta = Vector2.zero;
            rightTitleRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI rightTitle = rightTitleGo.AddComponent<TextMeshProUGUI>();
            rightTitle.text = "<b>🖥️ ALGORITHM ANALYSIS</b>";
            rightTitle.fontSize = 24f;
            rightTitle.color = new Color(0.0f, 1.0f, 0.6f, 1f);

            // 右分栏内容文本
            GameObject rightContentGo = new GameObject("RightContentText");
            rightContentGo.transform.SetParent(rightPanel.transform, false);
            RectTransform rightContentRect = rightContentGo.AddComponent<RectTransform>();
            rightContentRect.anchorMin = new Vector2(0.06f, 0.05f);
            rightContentRect.anchorMax = new Vector2(0.94f, 0.82f);
            rightContentRect.sizeDelta = Vector2.zero;
            rightContentRect.anchoredPosition = Vector2.zero;

            diagnosticReportText = rightContentGo.AddComponent<TextMeshProUGUI>();
            diagnosticReportText.fontSize = 22f;
            diagnosticReportText.color = Color.white;
            diagnosticReportText.alignment = TextAlignmentOptions.TopLeft;

            // 6. 底部 ESC 退出说明栏
            GameObject escTipGo = new GameObject("EscTipText");
            escTipGo.transform.SetParent(bgPanel.transform, false);
            RectTransform escRect = escTipGo.AddComponent<RectTransform>();
            escRect.anchorMin = new Vector2(0.1f, 0.03f);
            escRect.anchorMax = new Vector2(0.9f, 0.08f);
            escRect.sizeDelta = Vector2.zero;
            escRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI escTip = escTipGo.AddComponent<TextMeshProUGUI>();
            escTip.text = "Press <b>[ESC]</b> to toggle Algorithm Diagnostics Panel";
            escTip.fontSize = 18f;
            escTip.color = new Color(0.5f, 0.5f, 0.5f, 0.9f);
            escTip.alignment = TextAlignmentOptions.Center;

            // 保存面板引用
            letterPanel = bgPanel;

            Debug.Log("[BehaviorDataSummary] 已成功动态在场景中构建部署科幻风格算法诊断 UI！");
        }

        private void Update()
        {
            if (letterPanel != null)
            {
                // 检测 ESC 按键切换控制面板显隐
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    isPanelActive = !isPanelActive;
                    letterPanel.SetActive(isPanelActive);

                    if (isPanelActive)
                    {
                        CursorService.Unlock();
                    }
                    else
                    {
                        CursorService.Lock();
                    }
                }
            }
        }
    }
}
