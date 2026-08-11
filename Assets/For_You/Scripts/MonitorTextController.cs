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

        private Transform _canvasTransform;
        private Vector3 _originalCanvasScale;
        private Coroutine _juiceCoroutine;

        /// <summary>
        /// 由 Stage4Controller 在每次点击时调用（支持无限点击、特效冲击与渐进崩坏乱码）
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;

            // 1. 触发屏幕弹性冲击与磷光亮闪
            TriggerScreenImpact();

            int index = _totalClicks / clicksPerMessage;

            if (index < textSequence.Length)
            {
                if (index != _lastShownIndex)
                {
                    ShowMessage(index);
                }
                else
                {
                    // 即使在同一条文字期间点击，也有概率触发瞬时特效
                    TriggerClickJuiceEffect();
                }
            }
            else
            {
                // 超越固定数组长度：生成无限渐进崩坏乱码文字并触发高频特效
                GenerateInfiniteGlitchMessage(index);
                TriggerClickJuiceEffect();
            }
        }

        private void TriggerScreenImpact()
        {
            if (_tmp == null) return;
            if (_canvasTransform == null && _tmp.canvas != null)
            {
                _canvasTransform = _tmp.canvas.transform;
                _originalCanvasScale = _canvasTransform.localScale;
            }

            // 瞬时荧光亮闪与微幅弹跳
            StartCoroutine(ImpactPulseRoutine());
        }

        private IEnumerator ImpactPulseRoutine()
        {
            _tmp.color = new Color(0.4f, 1.0f, 0.6f); // 亮绿色磷光
            if (_canvasTransform != null)
            {
                _canvasTransform.localScale = _originalCanvasScale * 1.06f;
            }

            yield return new WaitForSeconds(0.06f);

            _tmp.color = new Color(0.2f, 1.0f, 0.4f); // 恢复标准终端绿
            if (_canvasTransform != null)
            {
                _canvasTransform.localScale = _originalCanvasScale;
            }
        }

        private void TriggerClickJuiceEffect()
        {
            if (_tmp == null || _totalClicks < 3) return;

            if (_juiceCoroutine != null) StopCoroutine(_juiceCoroutine);

            // 随机切换：字母积木崩塌 vs 横向撕裂拉扯
            if (Random.value < 0.5f)
            {
                _juiceCoroutine = StartCoroutine(CrumbleCollapseEffectRoutine());
            }
            else
            {
                _juiceCoroutine = StartCoroutine(HorizontalStretchTearRoutine());
            }
        }

        /// <summary>
        /// 特效 1：字母如积木般一瞬间垮塌下坠 (Letter Block Crumble / Falling Bricks)
        /// </summary>
        private IEnumerator CrumbleCollapseEffectRoutine()
        {
            _tmp.ForceMeshUpdate();
            TMP_TextInfo textInfo = _tmp.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) yield break;

            float duration = 0.45f;
            float elapsed = 0f;

            // 为每个字符生成随机坍塌速度与旋转偏置
            int charCount = textInfo.characterCount;
            float[] dropSpeeds = new float[charCount];
            float[] rotSpeeds = new float[charCount];
            for (int i = 0; i < charCount; i++)
            {
                dropSpeeds[i] = Random.Range(30f, 90f);
                rotSpeeds[i] = Random.Range(-45f, 45f);
            }

            // 保存初始顶点快照
            Vector3[][] origVertices = new Vector3[textInfo.meshInfo.Length][];
            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                origVertices[m] = (Vector3[])textInfo.meshInfo[m].vertices.Clone();
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                _tmp.ForceMeshUpdate();
                textInfo = _tmp.textInfo;

                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int matIdx = charInfo.materialReferenceIndex;
                    int vertIdx = charInfo.vertexIndex;

                    Vector3[] destinationVertices = textInfo.meshInfo[matIdx].vertices;
                    Vector3[] cachedOrig = origVertices[matIdx];

                    // 计算字符中心点
                    Vector3 charCenter = (cachedOrig[vertIdx + 0] + cachedOrig[vertIdx + 2]) * 0.5f;

                    // 计算垮塌位移 (向下掉落)
                    float fallY = -dropSpeeds[i] * progress;
                    float rotZ = rotSpeeds[i] * progress;
                    Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

                    for (int v = 0; v < 4; v++)
                    {
                        Vector3 origPos = cachedOrig[vertIdx + v];
                        Vector3 relPos = origPos - charCenter;
                        Vector3 rotated = rot * relPos;
                        destinationVertices[vertIdx + v] = charCenter + rotated + new Vector3(0f, fallY, 0f);
                    }
                }

                // 提交更新顶点 Mesh
                for (int m = 0; m < textInfo.meshInfo.Length; m++)
                {
                    textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
                    _tmp.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
                }

                yield return null;
            }

            // 恢复顶点位置
            _tmp.ForceMeshUpdate();
        }

        /// <summary>
        /// 特效 2：文字一瞬间横向撕裂与拉扯 (Horizontal Scanline Stretch & Tear)
        /// </summary>
        private IEnumerator HorizontalStretchTearRoutine()
        {
            _tmp.ForceMeshUpdate();
            TMP_TextInfo textInfo = _tmp.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) yield break;

            float duration = 0.22f;
            float elapsed = 0f;

            // 保存初始顶点快照
            Vector3[][] origVertices = new Vector3[textInfo.meshInfo.Length][];
            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                origVertices[m] = (Vector3[])textInfo.meshInfo[m].vertices.Clone();
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float glitchFactor = Mathf.Sin(elapsed * Mathf.PI / duration);

                _tmp.ForceMeshUpdate();
                textInfo = _tmp.textInfo;

                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int matIdx = charInfo.materialReferenceIndex;
                    int vertIdx = charInfo.vertexIndex;

                    Vector3[] destinationVertices = textInfo.meshInfo[matIdx].vertices;
                    Vector3[] cachedOrig = origVertices[matIdx];

                    // 行号奇偶判定撕裂方向
                    int lineNo = charInfo.lineNumber;
                    float shiftX = (lineNo % 2 == 0 ? 1f : -1f) * 18f * glitchFactor;
                    float stretchX = 1f + (0.5f * glitchFactor);

                    Vector3 charCenter = (cachedOrig[vertIdx + 0] + cachedOrig[vertIdx + 2]) * 0.5f;

                    for (int v = 0; v < 4; v++)
                    {
                        Vector3 origPos = cachedOrig[vertIdx + v];
                        Vector3 relPos = origPos - charCenter;
                        relPos.x *= stretchX; // 横向拉扯
                        destinationVertices[vertIdx + v] = charCenter + relPos + new Vector3(shiftX, 0f, 0f);
                    }
                }

                for (int m = 0; m < textInfo.meshInfo.Length; m++)
                {
                    textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
                    _tmp.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
                }

                yield return null;
            }

            _tmp.ForceMeshUpdate();
        }

        private void ShowMessage(int index)
        {
            if (_tmp == null || index >= textSequence.Length) return;
            _lastShownIndex = index;
            SetCustomMessage(textSequence[index]);
        }

        private void GenerateInfiniteGlitchMessage(int overflowIndex)
        {
            int baseIdx = (overflowIndex % 5) + (textSequence.Length - 5);
            string baseMsg = textSequence[Mathf.Clamp(baseIdx, 0, textSequence.Length - 1)];

            int extraClicks = overflowIndex - textSequence.Length + 1;
            string glitched = ApplyGlitchEffect(baseMsg, extraClicks);
            SetCustomMessage(glitched);
        }

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
