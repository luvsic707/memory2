using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject — 存储一组时间驱动的旁白 Cue。
/// 创建方式：Assets → Create → Narration → Timeline Data
/// 
/// 可在多个场景的 NarrationTimeline 组件里共用同一份数据资产。
/// </summary>
[CreateAssetMenu(fileName = "NewNarrationTimeline", menuName = "Narration/Timeline Data")]
public class NarrationTimelineData : ScriptableObject
{
    [System.Serializable]
    public class Cue
    {
        [Tooltip("从场景开始计时多少秒后触发")]
        public float triggerTime;

        [Tooltip("对应 NarrationDatabase 里的 groupName")]
        public string groupId;

        [Tooltip("备注（仅编辑器参考，不影响运行）")]
        public string note;
    }

    [Tooltip("按时间升序填写所有旁白 Cue")]
    public List<Cue> cues = new List<Cue>();
}
