using UnityEngine;
using UnityEngine.Events;
using TheLastCompact.Core;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 记忆碎片物品
    /// 功能：被交互后通知总管计数 +1，并触发自定义事件 (OnCollected)
    /// </summary>
    public class MemoryItem : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [Tooltip("必须为每个场景设置唯一的 ID (如 Room1, Room2)")]
        public string memoryID = "Room_Unique_ID";

        [Header("Event Hooks")]
        public UnityEvent OnCollected;

        private bool _isCollected = false;

        // IInteractable 实现
        public string InteractHint => "按 Q 收集记忆碎片";

        public void Interact()
        {
            Collect();
        }

        void Start()
        {
            // 检查之前是否已经收集过了 (跨场景回来时恢复状态)
            if (GlobalProgressManager.Instance != null)
            {
                if (GlobalProgressManager.Instance.IsMemoryCollected(memoryID))
                {
                    Debug.Log($"[MemoryItem] ID '{memoryID}' already collected. Auto-triggering events and disabling.");
                    OnCollected?.Invoke();
                    gameObject.SetActive(false);
                    _isCollected = true;
                }
            }
        }

        // 统一收集逻辑
        public void Collect()
        {
            if (_isCollected) return;
            
            if (GlobalProgressManager.Instance == null)
            {
                Debug.LogError("[MemoryItem] 找不到 GlobalProgressManager!");
                return;
            }

            _isCollected = true;

            // 1. 通知总管 (带 ID) — 轻量操作，立即执行
            GlobalProgressManager.Instance.CollectMemory(memoryID);

            // 2. 用渐黑过渡遮盖后续重操作
            if (ScreenFader.Instance != null)
            {
                Debug.Log("[MemoryItem] 开始收集过渡动画...");
                ScreenFader.Instance.FadeOutAndIn(() =>
                {
                    // 黑屏期间执行所有重操作
                    OnCollected?.Invoke();
                    gameObject.SetActive(false);
                    Debug.Log("[MemoryItem] 收集完成，门已激活");
                });
            }
            else
            {
                // 没有 ScreenFader 时走原有逻辑
                Debug.LogWarning("[MemoryItem] 没有找到 ScreenFader，直接执行");
                OnCollected?.Invoke();
                gameObject.SetActive(false);
            }
        }
    }
}
