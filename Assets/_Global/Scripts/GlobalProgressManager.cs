using System;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 全局流程总管：负责管理游戏的宏观阶段进度
    /// </summary>
    public class GlobalProgressManager : MonoBehaviour
    {
        public static GlobalProgressManager Instance { get; private set; }

        // 事件定义 (V6：所有事件携带载荷)
        public event Action<MemoryCollectedPayload> OnMemoryCollected;
        public event Action<PhaseCompletedPayload> OnPhaseACompleted;
        public event Action<PhaseCompletedPayload> OnAllPhasesCompleted;
        public event Action<DoorSequencePayload> OnDoorSequenceFinished;

        [Header("Debug Controls")]
        [Tooltip("点击以模拟第一阶段结束")]
        public bool debugFinishPhaseA;
        [Tooltip("点击以模拟全部流程结束")]
        public bool debugFinishAll;

        [Header("Configuration")]
        [Tooltip("拖入 GameBalance 资产")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else { Destroy(gameObject); }
        }

        void Update()
        {
            // --- 模拟测试接口 ---
            if (debugFinishPhaseA)
            {
                debugFinishPhaseA = false;
                CompletePhaseA();
            }

            if (debugFinishAll)
            {
                debugFinishAll = false;
                CompleteAllPhases();
            }
        }

        public void CompletePhaseA()
        {
            Debug.Log("<color=yellow>[Progress] Phase A Completed! Signaling Door Manager...</color>");
            OnPhaseACompleted?.Invoke(new PhaseCompletedPayload
            {
                PhaseIndex = 1,
                MemoriesCollected = currentMemoryCount
            });
        }

        public void CompleteAllPhases()
        {
            Debug.Log("<color=yellow>[Progress] All Phases Completed! Preparing Settlement...</color>");
            OnAllPhasesCompleted?.Invoke(new PhaseCompletedPayload
            {
                PhaseIndex = 2,
                MemoriesCollected = currentMemoryCount
            });
        }

        public void CompleteDoorSequence()
        {
            Debug.Log("<color=yellow>[Progress] Door Sequence Finished. Ready for Evaluation.</color>");
            OnDoorSequenceFinished?.Invoke(new DoorSequencePayload
            {
                DoorsOpened = 0,
                DoorsClosed = currentMemoryCount
            });
        }

        // --- 记忆收集逻辑 ---
        [Header("Runtime Info")]
        public int currentMemoryCount = 0;
        
        // 新增：记录已收集的 ID (防止重复)
        public System.Collections.Generic.List<string> collectedIDs = new System.Collections.Generic.List<string>();

        // 新增：记录已访问的房间 (用于旁白条件系统)
        public System.Collections.Generic.List<string> visitedRoomIDs = new System.Collections.Generic.List<string>();

        /// <summary>
        /// 已收集的记忆数量 (供旁白系统条件判断用)
        /// </summary>
        public int CollectedCount => currentMemoryCount;

        /// <summary>
        /// 标记某个房间为已访问
        /// </summary>
        public void VisitRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return;
            if (visitedRoomIDs.Contains(roomId)) return;

            visitedRoomIDs.Add(roomId);
            Debug.Log($"[Progress] 房间已访问: {roomId} (累计: {visitedRoomIDs.Count})");
        }

        /// <summary>
        /// 检查某个房间是否已访问
        /// </summary>
        public bool HasVisitedRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return false;
            return visitedRoomIDs.Contains(roomId);
        }

        /// <summary>
        /// 检查某个 ID 是否已被收集
        /// </summary>
        public bool IsMemoryCollected(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return collectedIDs.Contains(id);
        }

        /// <summary>
        /// 收集记忆 (带 ID 校验)
        /// </summary>
        public void CollectMemory(string id)
        {
            // 如果这个 ID 已经拿过了，直接忽略（防止重复计数）
            if (!string.IsNullOrEmpty(id) && collectedIDs.Contains(id))
            {
                Debug.LogWarning($"[Progress] Memory '{id}' already collected. Ignoring.");
                return;
            }

            // 记录 ID
            if (!string.IsNullOrEmpty(id))
            {
                collectedIDs.Add(id);
            }

            currentMemoryCount++;
            Debug.Log($"[Progress] Memory Collected! ID: {id}, Total: {currentMemoryCount}");

            // 自动判断阶段 (V8：从配置读取阈值)
            int phaseACount = balanceConfig != null ? balanceConfig.phaseAMemoryCount : 3;
            int totalCount = balanceConfig != null ? balanceConfig.totalMemoryCount : 6;

            OnMemoryCollected?.Invoke(new MemoryCollectedPayload
            {
                MemoryID = id,
                TotalCollected = currentMemoryCount,
                RequiredForNextPhase = phaseACount
            });

            if (currentMemoryCount == phaseACount)
            {
                CompletePhaseA();
            }
            else if (currentMemoryCount == totalCount)
            {
                CompleteAllPhases();
            }
        }
    }
}
