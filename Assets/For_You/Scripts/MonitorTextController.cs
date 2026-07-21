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
            "Reminder: Deadline is approaching.",
            "Re: Please review the Q3 report again.",
            "URGENT: Re: Re: Please re-review the Q3 report.",
            "Your performance review is scheduled for review.",
            "Note: The review of your review has been reviewed.",
            "TASK_7741: PROCESSING...",
            "TASK_7741: PROCESSING... TASK_7741: PROCESSING...",
            "ERROR: STACK OVERFLOW — TASK_7741",
            "You have been here before.\nYou will be here again.",
            "There is no exit in this directory.",
            "TASK_7741: RUNNING\nTASK_7741: RUNNING\nTASK_7741: RUNNING",
            "CRITICAL: Your absence has been noted.\nYour presence has also been noted.",
            "The report is due.\nThe report is always due.",
            "> _",
        };

        [Tooltip("每次点击切换到下一条文字的点击间隔")]
        public int clicksPerMessage = 4;

        [Tooltip("文字打字机效果速度（字/秒）")]
        public float typeSpeed = 25f;

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
            canvasGO.transform.localPosition = Vector3.zero;
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = Vector3.one * 0.01f; // 缩放到适合 3D 屏幕的大小

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
            if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
            _typeCoroutine = StartCoroutine(TypeText(textSequence[index]));
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
