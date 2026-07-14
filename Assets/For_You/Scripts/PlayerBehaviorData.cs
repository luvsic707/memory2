using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 跨场景持久化行为数据中心 (Player Behavior Data Tracker)
    /// 作为一个 DontDestroyOnLoad 的单例，用于在玩家不知道的情况下暗中统计各章节中无奖励、重复性交互行为的次数，
    /// 用于在最后一章生成个性化的算法式用户画像推送内容。
    /// </summary>
    public class PlayerBehaviorData : MonoBehaviour
    {
        public static PlayerBehaviorData Instance { get; private set; }

        [Header("交互计数器 (行为特征收集)")]
        [Tooltip("场景 1 (丛林/猿)：捡起或交互香蕉的总次数")]
        public int bananaCount = 0;

        [Tooltip("场景 2 (神殿/神)：跪拜或祈祷的总次数")]
        public int prayerCount = 0;

        [Tooltip("场景 3 (荒诞/西西弗斯)：将巨石推上山坡的总次数")]
        public int pushCount = 0;

        private void Awake()
        {
            // 单例持久化模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("<color=green>[PlayerBehaviorData] 持久化行为数据中心已成功初始化，跨场景不销毁。</color>");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 增加香蕉交互次数 (场景 1 触发)
        /// </summary>
        public void AddBanana()
        {
            bananaCount++;
            Debug.Log($"<color=yellow>[BehaviorTracker] 香蕉交互计数 +1。当前总数: {bananaCount}</color>");
        }

        /// <summary>
        /// 增加祈祷交互次数 (场景 2 触发)
        /// </summary>
        public void AddPrayer()
        {
            prayerCount++;
            Debug.Log($"<color=yellow>[BehaviorTracker] 祈祷/跪拜计数 +1。当前总数: {prayerCount}</color>");
        }

        /// <summary>
        /// 增加推石交互次数 (场景 3 触发)
        /// </summary>
        public void AddPush()
        {
            pushCount++;
            Debug.Log($"<color=yellow>[BehaviorTracker] 西西弗斯推石/重置计数 +1。当前总数: {pushCount}</color>");
        }

        /// <summary>
        /// 重置所有收集到的行为数据
        /// </summary>
        public void ResetData()
        {
            bananaCount = 0;
            prayerCount = 0;
            pushCount = 0;
            Debug.Log("[PlayerBehaviorData] 所有的玩家行为追踪数据已被重置归零。");
        }
    }
}
