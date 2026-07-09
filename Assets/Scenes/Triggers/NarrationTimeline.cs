using UnityEngine;

/// <summary>
/// Corridor Room 1 — 按时间序列触发旁白。
/// 挂在任意场景根节点 GameObject 上。
/// 
/// Cue 数据来自 NarrationTimelineData ScriptableObject（可跨场景共用）。
/// 触发通过 NarrationAnnouncer 广播，完全无需持有 NarratorManager 的引用。
/// </summary>
public class NarrationTimeline : MonoBehaviour
{
    [Header("时间线数据（ScriptableObject）")]
    [Tooltip("从 Assets 拖入 NarrationTimelineData 资产")]
    public NarrationTimelineData data;

    [Header("设置")]
    [Tooltip("从 Start 后开始计时（false）还是从场景加载后计时（true）")]
    public bool useTimeSinceLevelLoad = false;

    private bool[] _fired;
    private float _timer = 0f;

    void Start()
    {
        if (data != null)
            _fired = new bool[data.cues.Count];
    }

    void Update()
    {
        if (data == null || _fired == null) return;

        if (!useTimeSinceLevelLoad)
            _timer += Time.deltaTime;
        else
            _timer = Time.timeSinceLevelLoad;

        for (int i = 0; i < data.cues.Count; i++)
        {
            if (_fired[i]) continue;
            var cue = data.cues[i];
            if (_timer >= cue.triggerTime)
            {
                _fired[i] = true;
                NarrationAnnouncer.Announce(cue.groupId);
                Debug.Log($"[NarrationTimeline] t={_timer:F1}s → 广播旁白组: {cue.groupId}");
            }
        }
    }

    /// <summary>调试：重置所有 Cue 的触发状态</summary>
    public void ResetAll()
    {
        _timer = 0f;
        if (_fired != null)
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
    }
}
