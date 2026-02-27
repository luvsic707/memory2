using UnityEngine;
using TheLastCompact.Narration;
using TheLastCompact.Core;

/// <summary>
/// 旁白触发区域
/// 挂在场景中的触发区上，玩家进入时播放对应旁白组
/// </summary>
[RequireComponent(typeof(Collider))]
public class NarrationTrigger : MonoBehaviour
{
    [Header("旁白设置")]
    [Tooltip("要播放的旁白组名 (对应 NarrationDatabase 里的 groupName)")]
    public string narrationGroupName;

    [Header("触发行为")]
    [Tooltip("是否只触发一次")]
    public bool triggerOnce = true;

    [Tooltip("是否在旁白正在播放时打断 (false = 等当前旁白播完)")]
    public bool interruptCurrent = false;

    [Tooltip("触发延迟 (进入区域后等几秒再播)")]
    public float triggerDelay = 0f;

    [Header("高级")]
    [Tooltip("要追踪的房间 ID (进入此触发区时自动标记为已访问)")]
    public string roomIdToTrack;

    private bool _hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered && triggerOnce) return;
        if (!other.CompareTag("Player")) return;

        _hasTriggered = true;

        // 追踪房间访问
        if (!string.IsNullOrEmpty(roomIdToTrack))
        {
            if (GlobalProgressManager.Instance != null)
            {
                GlobalProgressManager.Instance.VisitRoom(roomIdToTrack);
            }
        }

        // 播放旁白
        if (NarratorManager.Instance != null)
        {
            if (interruptCurrent && NarratorManager.Instance.IsPlaying)
            {
                NarratorManager.Instance.StopCurrent();
            }

            if (triggerDelay > 0f)
            {
                Invoke(nameof(PlayNarration), triggerDelay);
            }
            else
            {
                PlayNarration();
            }
        }
    }

    private void PlayNarration()
    {
        if (NarratorManager.Instance != null && !string.IsNullOrEmpty(narrationGroupName))
        {
            Debug.Log($"[NarrationTrigger] 触发旁白组: {narrationGroupName}");
            NarratorManager.Instance.PlayGroup(narrationGroupName);
        }
    }

    // 可选：允许手动重置 (比如回到走廊时重新触发)
    public void ResetTrigger()
    {
        _hasTriggered = false;
    }
}
