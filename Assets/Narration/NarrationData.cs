using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Narration
{
    // ═══════════════════════════════════════════════════════════════
    // 1. 旁白条件 — 判断某条旁白是否应该播放
    // ═══════════════════════════════════════════════════════════════

    public enum ConditionType
    {
        HasVisitedRoom,     // 去过某个房间
        NotVisitedRoom,     // 没去过某个房间
        HasCollectedMemory, // 收集了某个记忆碎片
        NotCollectedMemory, // 没收集某个记忆碎片
        MemoryCountAtLeast, // 已收集的记忆数量 >= N
        MemoryCountLessThan,// 已收集的记忆数量 < N
        GamePhase,          // 当前游戏阶段 (PhaseA / PhaseB / AllCompleted)
        NarrationPlayed,    // 某条旁白已经播放过
        NarrationNotPlayed, // 某条旁白没播放过
    }

    [System.Serializable]
    public class NarrationCondition
    {
        public ConditionType type;

        [Tooltip("目标 ID (房间ID、记忆ID、旁白ID 等)")]
        public string targetId;

        [Tooltip("数值参数 (用于 MemoryCountAtLeast 等)")]
        public int intValue;
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. 旁白条目 — 一条完整的旁白
    // ═══════════════════════════════════════════════════════════════

    [System.Serializable]
    public class NarrationEntry
    {
        [Tooltip("唯一标识符")]
        public string id;

        [Tooltip("旁白音频")]
        public AudioClip audioClip;

        [Tooltip("字幕文本 (可选)")]
        [TextArea(2, 5)]
        public string subtitleText;

        [Tooltip("字幕显示时长 (0 = 跟随音频时长)")]
        public float subtitleDuration = 0f;

        [Tooltip("播放此旁白前的延迟")]
        public float delayBefore = 0f;

        [Tooltip("所有条件都满足时才播放")]
        public List<NarrationCondition> conditions = new List<NarrationCondition>();

        [Tooltip("优先级 (多条旁白满足条件时，播优先级高的)")]
        public int priority = 0;

        [Tooltip("是否只播一次")]
        public bool playOnce = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. 旁白组 — 一组相关旁白 (同一个触发点的多种变体)
    // ═══════════════════════════════════════════════════════════════

    [System.Serializable]
    public class NarrationGroup
    {
        [Tooltip("组名 (用于调试)")]
        public string groupName;

        [Tooltip("该触发点对应的所有旁白变体 (按优先级选择第一个满足条件的)")]
        public List<NarrationEntry> entries = new List<NarrationEntry>();
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. 旁白数据库 — ScriptableObject，你的内容全在这里编辑
    // ═══════════════════════════════════════════════════════════════

    [CreateAssetMenu(fileName = "NewNarrationDatabase", menuName = "Narration/Database")]
    public class NarrationDatabase : ScriptableObject
    {
        [Tooltip("所有旁白组")]
        public List<NarrationGroup> groups = new List<NarrationGroup>();

        /// <summary>
        /// 根据组名查找旁白组
        /// </summary>
        public NarrationGroup FindGroup(string groupName)
        {
            return groups.Find(g => g.groupName == groupName);
        }

        /// <summary>
        /// 根据 ID 查找单条旁白 (跨组查找)
        /// </summary>
        public NarrationEntry FindEntry(string id)
        {
            foreach (var group in groups)
            {
                var entry = group.entries.Find(e => e.id == id);
                if (entry != null) return entry;
            }
            return null;
        }
    }
}
