using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 旁白与场景事件响应器。
/// 订阅 NarrationAnnouncer 的事件，在 Inspector 里把 ID 绑定到任意 UnityEvent。
/// </summary>
public class NarrationEventResponder : MonoBehaviour
{
    // ── 数据结构 ─────────────────────────────────────────
    [System.Serializable]
    public class NarrationResponse
    {
        [Tooltip("要监听的旁白 entryId")]
        public string narrationEntryId;
        public UnityEvent onStarted;
        public UnityEvent onEnded;
    }

    [System.Serializable]
    public class SceneEventResponse
    {
        [Tooltip("要监听的自定义场景事件 ID（如 StartMelt）")]
        public string eventId;
        public UnityEvent onTriggered;
    }

    // ── Inspector 配置 ──────────────────────────────────
    [Header("旁白响应绑定")]
    public List<NarrationResponse> responses = new List<NarrationResponse>();

    [Header("通用场景事件绑定")]
    public List<SceneEventResponse> sceneEventResponses = new List<SceneEventResponse>();

    // ── 生命周期 ────────────────────────────────────────
    void OnEnable()
    {
        NarrationAnnouncer.OnNarrationStarted += HandleStarted;
        NarrationAnnouncer.OnNarrationEnded   += HandleEnded;
        NarrationAnnouncer.OnSceneEventTriggered += HandleSceneEvent;
    }

    void OnDisable()
    {
        NarrationAnnouncer.OnNarrationStarted -= HandleStarted;
        NarrationAnnouncer.OnNarrationEnded   -= HandleEnded;
        NarrationAnnouncer.OnSceneEventTriggered -= HandleSceneEvent;
    }

    // ── 内部处理 ────────────────────────────────────────
    private void HandleStarted(string entryId)
    {
        foreach (var r in responses)
            if (r.narrationEntryId == entryId) r.onStarted?.Invoke();
    }

    private void HandleEnded(string entryId)
    {
        foreach (var r in responses)
            if (r.narrationEntryId == entryId) r.onEnded?.Invoke();
    }

    private void HandleSceneEvent(string eventId)
    {
        foreach (var r in sceneEventResponses)
        {
            if (!string.IsNullOrEmpty(r.eventId) && string.Equals(r.eventId.Trim(), eventId.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[EventResponder] 收到广播 → 触发事件: {eventId}");
                r.onTriggered?.Invoke();
            }
        }
    }
}
