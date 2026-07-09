using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using TheLastCompact.Narration; // 引入我们优雅配置过的独立事件总线

namespace TheLastCompact.Archive
{
    [System.Serializable]
    public class SpaceTriggerEvent
    {
        [Tooltip("当玩家积攒到了这个次数时触发（比如：第 1 次按空格播一段，第 3 次按空格播另一段并显示魔方）")]
        public int requiredPressCount = 1;

        [Tooltip("触发的旁白 Group ID（留空则不触发声音）")]
        public string narrationGroupId;

        [Tooltip("除了旁白，你还想在这里触发什么其他事件？（比如拖入 Cube 并调用 GameObject.SetActive 来让它出现）")]
        public UnityEvent onEventTriggered;

        [Tooltip("延迟几秒后再触发上面的事件？（例如：想等旁白播完再显示 Cube，就填入旁白的秒数即可）")]
        public float delayBeforeEvent = 0f;
        
        [HideInInspector]
        public bool hasTriggered = false;
    }

    /// <summary>
    /// Archive 房间的独立导演脚本：
    /// 专门监听玩家按下 / 按住 空格键的最多次数，
    /// 完全由配置驱动旁白与 Cube 的显隐，高度解耦。
    /// </summary>
    public class ArchiveSpaceSequence : MonoBehaviour
    {
        [Header("开场配置")]
        [Tooltip("刚进入房间时（不按空格）想触发的第一句旁白 Group ID，留空则不触发")]
        public string initialNarrationGroupId;

        [Tooltip("进入房间后等几秒钟再开始说第一句话（比如玩家刚走进去，让子弹飞一会儿）")]
        public float delayBeforeInitialNarration = 2f;

        [Header("触发配置")]
        [Tooltip("玩家需要每次按下/按住多长时间才算作 1 次有效的输入？（填 0 代表只要敲一下空格就算1次，填 1 代表必须按死 1 秒才算 1 次）")]
        public float requiredHoldTimePerPress = 0f;

        [Tooltip("配置你需要的次数节点")]
        public List<SpaceTriggerEvent> sequenceEvents;

        [Header("调试信息 (只读)")]
        [SerializeField] private int currentPressCount = 0;
        private float currentHoldTimer = 0f;
        private bool isCurrentPressValid = false;

        private void Start()
        {
            if (!string.IsNullOrEmpty(initialNarrationGroupId))
            {
                StartCoroutine(PlayInitialNarration());
            }
        }

        private System.Collections.IEnumerator PlayInitialNarration()
        {
            if (delayBeforeInitialNarration > 0)
                yield return new WaitForSeconds(delayBeforeInitialNarration);

            Debug.Log($"[ArchiveDirector] 广播开局默认旁白: {initialNarrationGroupId}");
            NarrationAnnouncer.Announce(initialNarrationGroupId);
        }

        void Update()
        {
            // 检测空格按键
            if (Input.GetKey(KeyCode.Space))
            {
                if (!isCurrentPressValid)
                {
                    currentHoldTimer += Time.deltaTime;

                    // 当按住的时间达到了规定的阈值时，计为 1 次有效计数！
                    if (currentHoldTimer >= requiredHoldTimePerPress)
                    {
                        isCurrentPressValid = true;
                        currentPressCount++;
                        Debug.Log($"[ArchiveDirector] 玩家完成 1 次有效空格积攒！当前总次数: {currentPressCount}");

                        CheckAndTriggerEvents();
                    }
                }
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                // 松开空格时重置当前那一轮的计时状态
                currentHoldTimer = 0f;
                isCurrentPressValid = false;
            }
        }

        private void CheckAndTriggerEvents()
        {
            foreach (var evt in sequenceEvents)
            {
                // 如果当前次数达标了，且这个节点还没被触发过
                if (!evt.hasTriggered && currentPressCount == evt.requiredPressCount)
                {
                    evt.hasTriggered = true;

                    // 1. 发送旁白广播（极其解耦：只负责喊，NarratorManager 会去放音乐）
                    if (!string.IsNullOrEmpty(evt.narrationGroupId))
                    {
                        Debug.Log($"[ArchiveDirector] 广播要求播放旁白: {evt.narrationGroupId}");
                        NarrationAnnouncer.Announce(evt.narrationGroupId);
                    }

                    // 2. 延迟触发物理事件（比如显示 Cube）
                    if (evt.delayBeforeEvent > 0)
                    {
                        StartCoroutine(TriggerEventCoroutine(evt.onEventTriggered, evt.delayBeforeEvent));
                    }
                    else
                    {
                        evt.onEventTriggered?.Invoke();
                    }
                }
            }
        }

        private System.Collections.IEnumerator TriggerEventCoroutine(UnityEvent evt, float delay)
        {
            yield return new WaitForSeconds(delay);
            evt?.Invoke();
        }
    }
}
