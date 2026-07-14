using System;

/// <summary>
/// 旁白事件总线 — 完全静态，零依赖。
/// 
/// 事件一览：
///   OnNarrationRequested(groupId)  — 某个触发源请求播放一组旁白
///   OnNarrationStarted(entryId)    — 某条旁白开始播放
///   OnNarrationEnded(entryId)      — 某条旁白播放完毕
/// 
/// 任何系统（Shader、动画、AI）只需订阅对应事件，无需引用 NarratorManager。
/// </summary>
public static class NarrationAnnouncer
{
    // ── 触发层 ──────────────────────────────────────────
    /// <summary>触发源调用此事件请求播放旁白组</summary>
    public static event Action<string> OnNarrationRequested;

    // ── 播放层回调 ───────────────────────────────────────
    /// <summary>旁白开始播放时由 NarratorManager 触发。参数 = entryId</summary>
    public static event Action<string> OnNarrationStarted;

    /// <summary>旁白播放完毕时由 NarratorManager 触发。参数 = entryId</summary>
    public static event Action<string> OnNarrationEnded;

    // ── 公开 API ────────────────────────────────────────

    /// <summary>场景触发源调用：请求播放某个旁白组</summary>
    public static void Announce(string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return;
        OnNarrationRequested?.Invoke(groupId);
    }

    /// <summary>由 NarratorManager 内部调用：通知旁白已开始</summary>
    public static void NotifyStarted(string entryId)
    {
        OnNarrationStarted?.Invoke(entryId);
    }

    /// <summary>由 NarratorManager 内部调用：通知旁白已结束</summary>
    public static void NotifyEnded(string entryId)
    {
        OnNarrationEnded?.Invoke(entryId);
    }

    // ── 通用场景事件（用于完全解耦 Shader / 动画 等触发） ──
    public static event Action<string> OnSceneEventTriggered;

    /// <summary>场景控制器调用：触发某个自定义事件（如 "StartMelt"）</summary>
    public static void TriggerSceneEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        OnSceneEventTriggered?.Invoke(eventId);
    }
}
