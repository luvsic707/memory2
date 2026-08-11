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

        [Header("文字序列（按点击次数索引）")]
        [TextArea(2, 4)]
        public string[] textSequence = new string[]
        {
            "Please complete your daily task report...",
            "Reminder: Deadline is TODAY.",
            "Re: Please review Q3 report again.",
            "URGENT: Re: Re: Please re-review Q3 report.",
            "Note: Your review has been reviewed.",
            "WARNING: Structural Integrity Failing...\nWalls are closing in.",
            "ERROR: Workplace boundary collapsing.",
            "Notice: Desk space reduced to 25%.",
            "CRITICAL: System Collapsed.\nWhy are you still typing?",
            "[SYSTEM ALERT]:\nThe office has completely collapsed.",
            "[GUIDANCE]:\nSTOP WORKING.\nStep away from your desk.",
            "[GUIDANCE]:\nWalk out of the ruins to enter Stage 5 ->"
        };

        [Tooltip("每次点击切换到下一条文字的点击间隔")]
        public int clicksPerMessage = 1;

        [Tooltip("文字打字机效果速度（字/秒）")]
        public float typeSpeed = 35f;

        [Header("3D 屏幕自适应偏置")]
        [Tooltip("微调 Canvas 在屏幕上的位置偏置（相对于屏幕 local 空间）")]
        public Vector3 positionOffset = new Vector3(0f, 0f, 0.02f); // 默认往前方稍微偏出一点，防止跟屏幕 Z-fighting 闪烁

        [Tooltip("微调 Canvas 的旋转偏置（度），用于修正屏幕朝向和镜像反字问题")]
        public Vector3 rotationOffset = new Vector3(0f, 180f, 0f); // 默认 180 度翻转来解决常见 Mirror 反字

        [Tooltip("是否自动缩放 Canvas 以匹配 Screen Mesh 的边界大小")]
        public bool autoScaleToScreen = true;

        private TextMeshProUGUI _tmp;
        private int _totalClicks = 0;
        private int _lastShownIndex = -1;
        private Coroutine _typeCoroutine;

        private void Start()
        {
            // 自动查找 Glowing Screen
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

            // 在 Glowing Screen 上创建 World Space Canvas + TMP
            SetupCanvas();

            // 显示第一条文字
            ShowMessage(0);
        }

        private void SetupCanvas()
        {
            GameObject canvasGO = new GameObject("MonitorCanvas");
            canvasGO.transform.SetParent(glowingScreen, false);

            // 获取 MeshRenderer 边界中心作为实际 3D 屏幕位置
            Renderer r = glowingScreen.GetComponent<Renderer>();
            if (r == null) r = glowingScreen.GetComponentInChildren<Renderer>();

            Vector3 worldCenter = (r != null) ? r.bounds.center : glowingScreen.position;
            Vector3 worldSize = (r != null) ? r.bounds.size : Vector3.zero;

            // 应用位置和 Z-Fighting 偏移（在屏幕 local 空间移动）
            canvasGO.transform.position = worldCenter + glowingScreen.TransformDirection(positionOffset);

            // 应用世界旋转与微调偏置
            canvasGO.transform.rotation = glowingScreen.rotation * Quaternion.Euler(rotationOffset);

            // 动态自适应屏幕网格尺寸，计算合理的缩放
            float w = 0.5f;
            float h = 0.3f;
            if (r != null && worldSize.magnitude > 0.01f)
            {
                // 屏幕可能朝向 X 或 Z，宽度取二者最大值
                w = Mathf.Max(worldSize.x, worldSize.z);
                h = worldSize.y;
            }

            float scaleX = w / 160f;
            float scaleY = h / 100f;

            if (autoScaleToScreen)
            {
                // 对抗父物体缩放，确保 World Space 尺寸绝对精确
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
            rt.sizeDelta = new Vector2(160, 100); // 匹配屏幕比例

            GameObject textGO = new GameObject("MonitorText");
            textGO.transform.SetParent(canvasGO.transform, false);

            _tmp = textGO.AddComponent<TextMeshProUGUI>();
            _tmp.fontSize = 14;
            _tmp.color = new Color(0.2f, 1f, 0.4f); // 绿色终端字体
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
        /// 由 Stage4Controller 在每次点击时调用
        /// </summary>
        public void OnClick()
        {
            _totalClicks++;
            int targetIndex = Mathf.Min(_totalClicks / clicksPerMessage, textSequence.Length - 1);
            if (targetIndex != _lastShownIndex)
            {
                ShowMessage(targetIndex);
            }
        }

        private void ShowMessage(int index)
        {
            if (_tmp == null || index >= textSequence.Length) return;
            _lastShownIndex = index;
            SetCustomMessage(textSequence[index]);
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
