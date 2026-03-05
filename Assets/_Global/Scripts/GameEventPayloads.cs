namespace TheLastCompact.Core
{
    /// <summary>
    /// 事件载荷定义
    /// 每个事件广播都必须携带载荷，方便调试和追踪
    /// </summary>

    /// <summary>记忆收集事件载荷</summary>
    public struct MemoryCollectedPayload
    {
        public string MemoryID;
        public int TotalCollected;
        public int RequiredForNextPhase;

        public override string ToString()
            => $"[Memory] ID:{MemoryID} Total:{TotalCollected}/{RequiredForNextPhase}";
    }

    /// <summary>阶段完成事件载荷</summary>
    public struct PhaseCompletedPayload
    {
        public int PhaseIndex;       // 1 = PhaseA, 2 = AllPhases
        public int MemoriesCollected;

        public override string ToString()
            => $"[Phase] Index:{PhaseIndex} Memories:{MemoriesCollected}";
    }

    /// <summary>门序列结束事件载荷</summary>
    public struct DoorSequencePayload
    {
        public int DoorsOpened;
        public int DoorsClosed;

        public override string ToString()
            => $"[DoorSeq] Opened:{DoorsOpened} Closed:{DoorsClosed}";
    }
}
