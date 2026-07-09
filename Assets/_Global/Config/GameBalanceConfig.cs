using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 全局平衡配置资产
    /// 所有会变的数值都从这里读取，禁止在代码中硬编码
    /// 在 Unity Editor 中 Create → Config → GameBalance 即可生成
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "Config/GameBalance")]
    public class GameBalanceConfig : ScriptableObject
    {
        [Header("═══ Causal Model: 借贷 ═══")]
        [Tooltip("精神阈值上限 (Psyche 从这里开始递减)")]
        public float initialThreshold = 100f;

        [Tooltip("借贷恢复速率 (越高恢复越快)")]
        public float loanRecoveryRate = 0.4f;

        [Tooltip("每秒借贷基础代价")]
        public float loanBaseCost = 30f;

        [Tooltip("按住时长的惩罚系数")]
        public float loanDurationPenalty = 0.5f;

        [Header("═══ Causal Model: 自然腐烂 ═══")]
        [Tooltip("基础腐烂速率")]
        public float naturalDecayRate = 0.2f;

        [Tooltip("总负债对腐烂速率的加速系数")]
        public float debtPenaltyCoeff = 0.015f;

        [Tooltip("累积熵上限 = 阈值 × 此系数")]
        public float debtCapMultiplier = 1.2f;

        [Header("═══ Causal Model: 视觉参数 ═══")]
        [Tooltip("噪声缩放基础值")]
        public float noiseScaleBase = 12f;

        [Tooltip("噪声缩放随 glitch 的放大系数")]
        public float noiseScaleGlitchMul = 800f;

        [Tooltip("扭曲速度基础值")]
        public float distortSpeedBase = 5f;

        [Tooltip("扭曲速度随 glitch 的放大系数")]
        public float distortSpeedGlitchMul = 25f;

        [Header("═══ 章节结算 ═══")]
        [Tooltip("稳定结局阈值 (S > 此值 → Stable)")]
        public float stableThreshold = 1.5f;

        [Tooltip("不稳定结局阈值 (S > 此值 → Unstable, 否则 Collapse)")]
        public float unstableThreshold = 0.8f;

        [Header("═══ 进度管理 ═══")]
        [Tooltip("Phase A 所需记忆数量")]
        public int phaseAMemoryCount = 3;

        [Tooltip("全部完成所需记忆数量")]
        public int totalMemoryCount = 6;

        [Header("═══ 玩家 ═══")]
        [Tooltip("最低移速比 (glitch 满值时的移速系数)")]
        public float minSpeedRatio = 0.35f;

        [Tooltip("摄像机抖动触发阈值 (glitchIntensity > 此值开始抖)")]
        public float shakeStartThreshold = 0.4f;
    }
}
