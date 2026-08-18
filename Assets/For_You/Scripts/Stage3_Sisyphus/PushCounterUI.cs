using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 简单的推石次数计数 UI
    /// 实时从 PlayerBehaviorData 中读取推石总数并显示在 UI 文本上。
    /// </summary>
    public class PushCounterUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("用于显示推石计数的 TextMeshProUGUI 组件")]
        public TextMeshProUGUI counterText;

        [Header("文本格式")]
        [Tooltip("显示的文本格式，{0} 会被替换为推石数量")]
        public string textFormat = "🪨 推石次数: {0}";

        private void Start()
        {
            if (counterText == null)
            {
                counterText = GetComponent<TextMeshProUGUI>();
            }

            if (counterText == null)
            {
                Debug.LogWarning($"[PushCounterUI] 未在 '{gameObject.name}' 上找到 TextMeshProUGUI 组件，请手动从 Inspector 赋值。");
            }
        }

        private void Update()
        {
            if (counterText != null && PlayerBehaviorData.Instance != null)
            {
                counterText.text = string.Format(textFormat, PlayerBehaviorData.Instance.pushCount);
            }
        }
    }
}
