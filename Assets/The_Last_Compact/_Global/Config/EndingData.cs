using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 结局数据资产 (ScriptableObject)
    /// 结局文本从代码中剥离，存入外部数据资产
    /// 在 Unity Editor 中 Create → Config → EndingData 即可生成
    /// </summary>
    [CreateAssetMenu(fileName = "EndingData", menuName = "Config/EndingData")]
    public class EndingData : ScriptableObject
    {
        public EndingEntry stableEnding = new EndingEntry
        {
            title = "Stable Ending",
            description = "Dora's memory core has maintained remarkable integrity.\n\nPerhaps in this cold digital world, there is still a trace of warmth worth preserving.",
            debugLabel = "结构稳固"
        };

        public EndingEntry unstableEnding = new EndingEntry
        {
            title = "Unstable Ending",
            description = "The memory data remains intact, but irreversible fractures have appeared in the core structure.\n\nWill she still recognize who you are?",
            debugLabel = "结构震荡"
        };

        public EndingEntry collapseEnding = new EndingEntry
        {
            title = "Collapse Ending",
            description = "Entropy levels critical. System collapse.\n\nEverything has dissolved into the void of data noise.",
            debugLabel = "结构崩塌"
        };
    }

    [System.Serializable]
    public class EndingEntry
    {
        public string title;
        [TextArea(3, 10)]
        public string description;
        [Tooltip("调试日志用标签")]
        public string debugLabel;
    }
}
