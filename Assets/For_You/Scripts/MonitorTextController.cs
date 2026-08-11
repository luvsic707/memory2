using System.Collections;
using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 在 Office (87) 的 Glowing Screen 上按点击次数显示逐渐失控的公司邮件文字。
    /// 挂载到任意 GameObject，脚本会自动在 Glowing Screen 子物体上创建 World Space Canvas 并显示 TMP 文字。
    /// </summary>
    public class MonitorTextController : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("Glowing Screen 物体（Office (87) 的子物体），若为空则自动按名称查找")]
        public Transform glowingScreen;

        [Header("文字序列（双重声音：系统指令 vs 潜意识低语）")]
        [TextArea(2, 4)]
        public string[] textSequence = new string[]
        {
            "SYSTEM: Please complete daily task report #7741.",
            "SYSTEM: Reminder: Q3 deadline is TODAY.",
            "...did you hear that noise outside your cubicle?",
            "SYSTEM: Disregard noise.\nFocus on Q3 review.",
            "URGENT: Re: Re: Please re-review Q3 report.",
            "SYSTEM: Notice: Wall distance optimized.",
            "...the walls are closing in. Look at the doorway.",
            "SYSTEM: DO NOT LOOK AWAY FROM THE SCREEN.",
            "...there is nothing left to type. Step away.",
            "SYSTEM: Mandatory Overtime Initiated.",
            "...your desk is crushed.",
            "S̶Y̶S̶T̶E̶M̶: OVERTIME MANDATORY. KEEP TYPING.",
            "...stop listening to the machine. WALK OUT.",
            "E̵R̵R̵O̵R̵: WORKPLACE BOUNDARY DISSOLVED.",
            "...the office is a skin you outgrew.\nLeak to next layer ->"
        };

        [Tooltip("每次点击切换到下一条文字的点击间隔")]
        public int clicksPerMessage = 1;

        [Tooltip("文字打字机效果速度（字/秒）")]
        public float typeSpeed = 35f;

        [Header("崩坏乱码配置")]
        [Tooltip("当点击超过预设文案后，乱码崩坏强度的增长速率")]
        [Range(0f, 1f)] public float glitchIntensityRate = 0.15f;

        [Header("3D 屏幕自适应偏置")]
        [Tooltip("微调 Canvas 在屏幕上的位置偏置（相对于屏幕 local 空间）")]
        public Vector3 positionOffset = new Vector3(0f, 0f, 0.02f);

        [Tooltip("微调 Canvas 的旋转偏置（度），用于修正屏幕朝向和镜像反字问题")]
        public Vector3 rotationOffset = new Vector3(0f, 180f, 0f);

        [Tooltip("是否自动缩放 Canvas 以匹配 Screen Mesh 的边界大小")]
        public bool autoScaleToScreen = true;

        private TextMeshProUGUI _tmp;
        private int _totalClicks = 0;
        private int _lastShownIndex = -1;
        private Coroutine _typeCoroutine;

        private static readonly string[] GlitchSymbols = new string[]
        {
            "░", "▒", "▓", "█", "§", "Ø", "Ψ", "Δ", "Ξ", "Ω", "µ", "≠", "ERR_0x87", "NULL", "[BROKEN]"
        };

        private void Start()
        {
            if (glowingScreen == null)
            {
                GameObject found = GameObject.Find("Glowing Screen");
                if (found != null) glowingScreen = found.transform;
            }

            if (glowingScreen == null)
            {
                Debug.LogWarning("[MonitorText] 找不到 Glowing Screen，请手动拖入引用。");
                return;
            }

            SetupCanvas();
            ShowMessage(0);
        }

        private void SetupCanvas()
        {
            GameObject canvasGO = new GameObject("MonitorCanvas");
            canvasGO.transform.SetParent(glowingScreen, false);

            Renderer r = glowingScreen.GetComponent<Renderer>();
            if (r == null) r = glowingScreen.GetComponentInChildren<Renderer>();

            Vector3 worldCenter = (r != null) ? r.bounds.center : glowingScreen.position;
            Vector3 worldSize = (r != null) ? r.bounds.size : Vector3.zero;

            canvasGO.transform.position = worldCenter + glowingScreen.TransformDirection(positionOffset);
            canvasGO.transform.rotation = glowingScreen.rotation * Quaternion.Euler(rotationOffset);

            float w = 0.5f;
            float h = 0.3f;
            if (r != null && worldSize.magnitude > 0.01f)
            {
                w = Mathf.Max(worldSize.x, worldSize.z);
                h = worldSize.y;
            }

            float scaleX = w / 160f;
            float scaleY = h / 100f;

            if (autoScaleToScreen)
            {
                Vector3 lossy = glowingScreen.lossyScale;
                float localScaleX = scaleX / (lossy.x == 0f ? 1f : lossy.x);
                float localScaleY = scaleY / (lossy.y == 0f ? 1f : lossy.y);
                canvasGO.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
            }
            else
            {
                canvasGO.transform.localScale = Vector3.one * 0.01f;
            }

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 100);

            GameObject textGO = new GameObject("MonitorText");
            textGO.transform.SetParent(canvasGO.transform, false);

            _tmp = textGO.AddComponent<TextMeshProUGUI>();
            _tmp.fontSize = 14;
            _tmp.color = new Color(0.2f, 1f, 0.4f);
            _tmp.alignment = TextAlignmentOptions.TopLeft;
            _tmp.text = "";
            _tmp.enableWordWrapping = true;

            RectTransform textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 8);
            textRt.offsetMax = new Vector2(-8, -8);
        }

        /// <summary>
        /// 由 Stage4Controller 在每次点击时调用（支持无限点击与渐进崩坏乱码）
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;
            int index = _totalClicks / clicksPerMessage;

            if (index < textSequence.Length)
            {
                if (index != _lastShownIndex)
                {
                    ShowMessage(index);
                }
            }
            else
            {
                // 超越固定数组长度：生成无限渐进崩坏乱码文字
                GenerateInfiniteGlitchMessage(index);
            }
        }

        private void ShowMessage(int index)
        {
            if (_tmp == null || index >= textSequence.Length) return;
            _lastShownIndex = index;
            SetCustomMessage(textSequence[index]);
        }

        private void GenerateInfiniteGlitchMessage(int overflowIndex)
        {
            // 从后半段潜意识/崩坏文本中轮询基准句
            int baseIdx = (overflowIndex % 5) + (textSequence.Length - 5);
            string baseMsg = textSequence[Mathf.Clamp(baseIdx, 0, textSequence.Length - 1)];

            // 计算崩坏层级
            int extraClicks = overflowIndex - textSequence.Length + 1;
            string glitched = ApplyGlitchEffect(baseMsg, extraClicks);
            SetCustomMessage(glitched);
        }

        /// <summary>
        /// 程序化字符崩坏注入算法
        /// </summary>
        private string ApplyGlitchEffect(string original, int glitchSeverity)
        {
            char[] chars = original.ToCharArray();
            int corruptCount = Mathf.Min(glitchSeverity * 2 + 1, chars.Length);

            for (int k = 0; k < corruptCount; k++)
            {
                int randIdx = Random.Range(0, chars.Length);
                if (chars[randIdx] != '\n' && chars[randIdx] != ' ')
                {
                    chars[randIdx] = GlitchSymbols[Random.Range(0, GlitchSymbols.Length)][0];
                }
            }

            string result = new string(chars);
            if (Random.value < 0.4f)
            {
                result += "\n" + GlitchSymbols[Random.Range(0, GlitchSymbols.Length)] + " LEAK INTO THE NEXT ITERATION ->";
            }
            return result;
        }

        /// <summary>
        /// 公共接口：外部脚本直接向 3D 屏幕写入指定的绿色终端文字
        /// </summary>
        public void SetCustomMessage(string message)
        {
            if (_tmp == null) return;
            if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
            _typeCoroutine = StartCoroutine(TypeText(message));
        }

        private IEnumerator TypeText(string message)
        {
            _tmp.text = "";
            foreach (char c in message)
            {
                _tmp.text += c;
                yield return new WaitForSeconds(1f / typeSpeed);
            }
        }
    }
}
