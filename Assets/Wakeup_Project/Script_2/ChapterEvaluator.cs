using UnityEngine;
using TheLastCompact.Core; 

namespace TheLastCompact.Core
{
    /// <summary>
    /// 章节评价器 (The Judge)
    /// 负责在章节结束时计算由于 Entropy 和 Debt 对心智结构造成的不可逆损伤
    /// </summary>
    public class ChapterEvaluator : MonoBehaviour
    {
        [Header("Settings")]
        public bool autoSubscribe = true;

        [Header("Configuration")]
        [Tooltip("拖入 GameBalance 资产")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        [Tooltip("拖入 EndingData 资产")]
        [SerializeField] private EndingData endingData;

        void Start()
        {
            if (autoSubscribe && GlobalProgressManager.Instance != null)
            {
                GlobalProgressManager.Instance.OnDoorSequenceFinished += EvaluateChapter;
            }
        }

        void OnDestroy()
        {
            if (GlobalProgressManager.Instance != null)
            {
                GlobalProgressManager.Instance.OnDoorSequenceFinished -= EvaluateChapter;
            }
        }

        /// <summary>
        /// 核心结算逻辑 (V6: 接受载荷)
        /// </summary>
        public void EvaluateChapter(DoorSequencePayload payload)
        {
            Debug.Log($"[ChapterEvaluator] Received: {payload}");

            if (GlobalMentalState.Instance == null)
            {
                Debug.LogError("[ChapterEvaluator] 无法结算：GlobalMentalState 缺失！");
                return;
            }

            // 0. 立即冻结系统状态，确保数据定格
            GlobalMentalState.Instance.FreezeSystem();

            var model = GlobalMentalState.Instance.Model;
            
            // 1. 获取基础数据
            float psyche = model.Psyche;
            float debt = model.TotalDebt;
            float entropy = model.AccumulatedDebt;

            // 2. 计算结构稳定性指数 (S)
            float denominator = Mathf.Sqrt((debt * entropy) + 1f);
            float stabilityIndex = psyche / denominator;

            // 3. 判定结局分支 (V8：阈值从配置读取)
            float stableThresh = balanceConfig != null ? balanceConfig.stableThreshold : 1.5f;
            float unstableThresh = balanceConfig != null ? balanceConfig.unstableThreshold : 0.8f;
            
            Debug.Log("-----------------[ 章节结算 ]-----------------");
            Debug.Log($"[数据] Psyche: {psyche:F1} | Debt: {debt:F1} | Entropy: {entropy:F1}");
            Debug.Log($"[公式] S = {psyche:F1} / sqrt({debt:F1} * {entropy:F1} + 1)");
            Debug.Log($"[结果] Stability Index (S): <color=yellow>{stabilityIndex:F2}</color>");

            // V10: 结局文本从 EndingData 读取
            EndingEntry ending;
            if (stabilityIndex > stableThresh)
            {
                ending = endingData != null ? endingData.stableEnding 
                    : new EndingEntry { title = "Stable Ending", description = "结构稳固", debugLabel = "结构稳固" };
                Debug.Log($">> 播放演出：[Dora 面容清晰] ({ending.debugLabel})");
            }
            else if (stabilityIndex > unstableThresh)
            {
                ending = endingData != null ? endingData.unstableEnding 
                    : new EndingEntry { title = "Unstable Ending", description = "结构震荡", debugLabel = "结构震荡" };
                Debug.Log($">> 播放演出：[Dora 面容模糊] ({ending.debugLabel})");
            }
            else
            {
                ending = endingData != null ? endingData.collapseEnding 
                    : new EndingEntry { title = "Collapse Ending", description = "结构崩塌", debugLabel = "结构崩塌" };
                Debug.Log($">> 播放演出：[Dora 沦为黑影] ({ending.debugLabel})");
            }

            Debug.Log($"[判定] 结局走向: <color=cyan>{ending.title}</color>");
            Debug.Log("---------------------------------------------");

            if (GlobalUIManager.Instance != null)
            {
                GlobalUIManager.Instance.ShowEnding(ending.title, ending.description);
            }
        }
    }
}
