using UnityEngine;

/// <summary>
/// Corridor Room 1 — 玩家进入区域时触发旁白。
/// 
/// 使用方式：
///   1. 给一个 GameObject 挂上 Collider，勾选 Is Trigger。
///   2. 把本脚本挂到同一 GameObject。
///   3. 在 Inspector 中填写 groupId（对应 NarrationDatabase 的 groupName）。
///   4. 玩家进入时，自动通过 NarrationAnnouncer 广播，无需引用 NarratorManager。
/// </summary>
[RequireComponent(typeof(Collider))]
public class NarrationZoneTrigger : MonoBehaviour
{
    [Header("旁白设置")]
    [Tooltip("对应 NarrationDatabase 里的 groupName")]
    public string groupId;

    [Tooltip("只触发一次（推荐开启）")]
    public bool fireOnce = true;

    [Header("玩家识别")]
    [Tooltip("玩家的 Tag（默认 Player）")]
    public string playerTag = "Player";

    private bool _fired = false;

    void Start()
    {
        // 强制 Collider 成为 Trigger
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && fireOnce) return;
        if (!other.CompareTag(playerTag)) return;

        _fired = true;
        NarrationAnnouncer.Announce(groupId);
        Debug.Log($"[NarrationZone] 玩家进入区域 → 广播旁白组: {groupId}");

        // 触发一次后禁用 Collider（省去每帧检查 _fired）
        if (fireOnce)
            GetComponent<Collider>().enabled = false;
    }

    /// <summary>
    /// 重置触发状态（调试或重新游玩用）
    /// </summary>
    public void Reset()
    {
        _fired = false;
        GetComponent<Collider>().enabled = true;
    }
}
