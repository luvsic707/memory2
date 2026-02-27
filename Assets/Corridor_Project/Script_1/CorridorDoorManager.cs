using UnityEngine;
using TheLastCompact.Core;

/// <summary>
/// 走廊门扉管理器 (中介者)
/// 负责根据全局进度，批量开启/关闭门的 Collider
/// </summary>
public class CorridorDoorManager : MonoBehaviour
{
    [Header("Phase A Config (Doors 1-3)")]
    public Collider[] groupA_Colliders;

    [Header("Phase B Config (Doors 4-6)")]
    public Collider[] groupB_Colliders;

    void Start()
    {
        // 1. 订阅事件 (为了处理在当前场景内发生的突变)
        if (GlobalProgressManager.Instance != null)
        {
            GlobalProgressManager.Instance.OnPhaseACompleted += ActivatePhaseB;
            GlobalProgressManager.Instance.OnAllPhasesCompleted += OnAllPhasesDone;

            // 2. 关键修复：主动检查当前状态 (State Check)
            int count = GlobalProgressManager.Instance.currentMemoryCount;
            
            if (count >= 6)
            {
                OnAllPhasesDone(default);
            }
            else if (count >= 3)
            {
                // A 阶段已过，直接初始化为 B 阶段状态
                Debug.Log("[DoorManager] Detected Phase A already finished. Init Phase B.");
                SetGroupActive(groupA_Colliders, false);
                SetGroupActive(groupB_Colliders, true);
            }
            else
            {
                // 默认初始状态：只开 A，关 B
                SetGroupActive(groupA_Colliders, true);
                SetGroupActive(groupB_Colliders, false);
            }
        }
        else
        {
            // 如果没有 Manager (单独测试时)，默认 A 开 B 关
            SetGroupActive(groupA_Colliders, true);
            SetGroupActive(groupB_Colliders, false);
        }
    }

    void OnDestroy()
    {
        if (GlobalProgressManager.Instance != null)
        {
            GlobalProgressManager.Instance.OnPhaseACompleted -= ActivatePhaseB;
            GlobalProgressManager.Instance.OnAllPhasesCompleted -= OnAllPhasesDone;
        }
    }

    // 事件响应：阶段 A 结束 -> 切换到 B (V6: 接受载荷)
    private void ActivatePhaseB(PhaseCompletedPayload payload)
    {
        Debug.Log($"[DoorManager] Phase A Ended. {payload} Switching to Phase B...");
        
        // 关闭 A 组
        SetGroupActive(groupA_Colliders, false);
        
        // 开启 B 组
        SetGroupActive(groupB_Colliders, true);
    }

    // 事件响应：全部结束 (V6: 接受载荷)
    private void OnAllPhasesDone(PhaseCompletedPayload payload)
    {
        Debug.Log($"[DoorManager] All Phases Done. {payload} Shutting down all doors...");
        // 可选：全部关闭，或者保留 B 组，看需求。目前设为全部关闭。
        SetGroupActive(groupB_Colliders, false);

        // 核心修改：演出结束后，通知 GlobalProgressManager
        if (GlobalProgressManager.Instance != null)
        {
            GlobalProgressManager.Instance.CompleteDoorSequence();
        }
    }

    // 辅助方法：批量开关
    // 修改：不仅开关 Collider，而是直接开关整个 GameObject，实现“消失/出现”的效果
    private void SetGroupActive(Collider[] doors, bool isActive)
    {
        if (doors == null) return;
        foreach (var col in doors)
        {
            if (col != null)
            {
                // 如果 col 挂在门的主体上，这将隐藏整个门
                col.gameObject.SetActive(isActive);
            }
        }
    }
}
