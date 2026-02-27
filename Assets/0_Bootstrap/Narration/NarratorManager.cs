using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TheLastCompact.Narration;
using TheLastCompact.Core;

/// <summary>
/// 旁白管理器 (全局单例)
/// 负责播放画外音、显示字幕、管理播放状态
/// 类似 Stanley Parable 的旁白系统
/// </summary>
public class NarratorManager : MonoBehaviour
{
    public static NarratorManager Instance { get; private set; }

    [Header("数据")]
    [Tooltip("旁白数据库 (ScriptableObject)")]
    public NarrationDatabase database;

    [Header("音频")]
    [Tooltip("旁白用的 AudioSource")]
    public AudioSource narrationAudio;

    [Header("字幕 UI (可选)")]
    [Tooltip("字幕文本组件")]
    public TextMeshProUGUI subtitleText;
    [Tooltip("字幕面板")]
    public GameObject subtitlePanel;

    [Header("设置")]
    [Tooltip("旁白之间的最小间隔 (秒)")]
    public float cooldownBetweenNarrations = 1f;

    // 内部状态
    private HashSet<string> _playedNarrationIds = new HashSet<string>();
    private Queue<NarrationEntry> _queue = new Queue<NarrationEntry>();
    private bool _isPlaying = false;
    private float _lastPlayTime = -999f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        // 自动创建 AudioSource (如果没在 Inspector 里指定)
        if (narrationAudio == null)
        {
            narrationAudio = gameObject.AddComponent<AudioSource>();
            narrationAudio.playOnAwake = false;
            narrationAudio.spatialBlend = 0f; // 2D 音效 (画外音)
        }

        // 初始隐藏字幕
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    // 公开 API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 播放指定旁白组 (自动选择满足条件的最高优先级条目)
    /// </summary>
    public void PlayGroup(string groupName)
    {
        if (database == null)
        {
            Debug.LogWarning("[Narrator] 没有设置数据库！");
            return;
        }

        var group = database.FindGroup(groupName);
        if (group == null)
        {
            Debug.LogWarning($"[Narrator] 找不到旁白组: {groupName}");
            return;
        }

        // 找到满足所有条件的最高优先级条目
        NarrationEntry bestEntry = null;
        int bestPriority = int.MinValue;

        foreach (var entry in group.entries)
        {
            // 跳过已播放且只播一次的
            if (entry.playOnce && _playedNarrationIds.Contains(entry.id))
                continue;

            // 检查所有条件
            if (!EvaluateConditions(entry.conditions))
                continue;

            // 选优先级最高的
            if (entry.priority > bestPriority)
            {
                bestPriority = entry.priority;
                bestEntry = entry;
            }
        }

        if (bestEntry != null)
        {
            EnqueueNarration(bestEntry);
        }
    }

    /// <summary>
    /// 直接播放指定 ID 的旁白 (忽略条件)
    /// </summary>
    public void PlayById(string narrationId)
    {
        if (database == null) return;

        var entry = database.FindEntry(narrationId);
        if (entry == null)
        {
            Debug.LogWarning($"[Narrator] 找不到旁白: {narrationId}");
            return;
        }

        if (entry.playOnce && _playedNarrationIds.Contains(entry.id))
            return;

        EnqueueNarration(entry);
    }

    /// <summary>
    /// 停止当前旁白
    /// </summary>
    public void StopCurrent()
    {
        StopAllCoroutines();
        if (narrationAudio != null && narrationAudio.isPlaying)
        {
            narrationAudio.Stop();
        }
        HideSubtitle();
        _isPlaying = false;
    }

    /// <summary>
    /// 当前是否正在播放旁白
    /// </summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// 某条旁白是否已经播放过
    /// </summary>
    public bool HasPlayed(string narrationId)
    {
        return _playedNarrationIds.Contains(narrationId);
    }

    // ═══════════════════════════════════════════════════════════════
    // 内部逻辑
    // ═══════════════════════════════════════════════════════════════

    private void EnqueueNarration(NarrationEntry entry)
    {
        _queue.Enqueue(entry);

        if (!_isPlaying)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        _isPlaying = true;

        while (_queue.Count > 0)
        {
            // 冷却检查
            float elapsed = Time.unscaledTime - _lastPlayTime;
            if (elapsed < cooldownBetweenNarrations)
            {
                yield return new WaitForSecondsRealtime(cooldownBetweenNarrations - elapsed);
            }

            var entry = _queue.Dequeue();

            // 播放前延迟
            if (entry.delayBefore > 0f)
            {
                yield return new WaitForSecondsRealtime(entry.delayBefore);
            }

            // 播放音频
            if (entry.audioClip != null)
            {
                narrationAudio.clip = entry.audioClip;
                narrationAudio.Play();
                Debug.Log($"[Narrator] 播放旁白: {entry.id} ({entry.audioClip.name})");
            }

            // 显示字幕
            if (!string.IsNullOrEmpty(entry.subtitleText))
            {
                ShowSubtitle(entry.subtitleText);
            }

            // 标记为已播放
            _playedNarrationIds.Add(entry.id);

            // 等待播放完毕
            float waitTime = entry.audioClip != null
                ? entry.audioClip.length
                : (entry.subtitleDuration > 0f ? entry.subtitleDuration : 3f);

            yield return new WaitForSecondsRealtime(waitTime);

            // 隐藏字幕
            HideSubtitle();

            _lastPlayTime = Time.unscaledTime;
        }

        _isPlaying = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // 条件评估
    // ═══════════════════════════════════════════════════════════════

    private bool EvaluateConditions(List<NarrationCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0) return true;

        foreach (var cond in conditions)
        {
            if (!EvaluateSingle(cond)) return false;
        }
        return true;
    }

    private bool EvaluateSingle(NarrationCondition cond)
    {
        var progress = GlobalProgressManager.Instance;
        if (progress == null) return false;

        switch (cond.type)
        {
            case ConditionType.HasVisitedRoom:
                return progress.HasVisitedRoom(cond.targetId);
            case ConditionType.NotVisitedRoom:
                return !progress.HasVisitedRoom(cond.targetId);
            case ConditionType.HasCollectedMemory:
                return progress.IsMemoryCollected(cond.targetId);
            case ConditionType.NotCollectedMemory:
                return !progress.IsMemoryCollected(cond.targetId);
            case ConditionType.MemoryCountAtLeast:
                return progress.CollectedCount >= cond.intValue;
            case ConditionType.MemoryCountLessThan:
                return progress.CollectedCount < cond.intValue;
            case ConditionType.NarrationPlayed:
                return _playedNarrationIds.Contains(cond.targetId);
            case ConditionType.NarrationNotPlayed:
                return !_playedNarrationIds.Contains(cond.targetId);
            default:
                Debug.LogWarning($"[Narrator] 未处理的条件类型: {cond.type}");
                return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 字幕
    // ═══════════════════════════════════════════════════════════════

    private void ShowSubtitle(string text)
    {
        if (subtitleText != null) subtitleText.text = text;
        if (subtitlePanel != null) subtitlePanel.SetActive(true);
    }

    private void HideSubtitle()
    {
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        if (subtitleText != null) subtitleText.text = "";
    }
}
