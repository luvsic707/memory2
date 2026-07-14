using System;
using UnityEngine;

namespace TheLastCompact.Core
{
    public class CausalModel
    {
        // ═══ 封装后的核心状态 (外部只读) ═══
        public float AccumulatedDebt { get; private set; }
        public float EntropyThreshold { get; private set; }
        public float TotalDebt { get; private set; }

        // ═══ 动态视觉参数 (外部只读) ═══
        public float NoiseScale { get; private set; }
        public float DistortionSpeed { get; private set; }

        // ═══ 计算属性 ═══
        public float GlitchIntensity => Mathf.Clamp01(AccumulatedDebt / EntropyThreshold);
        public float ResolutionScale => 1.0f - (GlitchIntensity * 0.7f);
        /// <summary>
        /// 精神值 = 阈值 - 累积熵 (单一事实来源，禁止外部自行计算)
        /// </summary>
        public float Psyche => EntropyThreshold - AccumulatedDebt;

        public event Action OnStateChanged;

        // ═══ 配置参数 (构造时注入，运行时不变) ═══
        private readonly float _loanRecoveryRate;
        private readonly float _loanBaseCost;
        private readonly float _loanDurationPenalty;
        private readonly float _naturalDecayRate;
        private readonly float _debtPenaltyCoeff;
        private readonly float _debtCapMultiplier;
        private readonly float _noiseScaleBase;
        private readonly float _noiseScaleGlitchMul;
        private readonly float _distortSpeedBase;
        private readonly float _distortSpeedGlitchMul;

        /// <summary>
        /// 使用 GameBalanceConfig 构造 (推荐)
        /// </summary>
        public CausalModel(GameBalanceConfig config)
        {
            EntropyThreshold = config.initialThreshold <= 0 ? 500f : config.initialThreshold;
            _loanRecoveryRate = config.loanRecoveryRate;
            _loanBaseCost = config.loanBaseCost;
            _loanDurationPenalty = config.loanDurationPenalty;
            _naturalDecayRate = config.naturalDecayRate;
            _debtPenaltyCoeff = config.debtPenaltyCoeff;
            _debtCapMultiplier = config.debtCapMultiplier;
            _noiseScaleBase = config.noiseScaleBase;
            _noiseScaleGlitchMul = config.noiseScaleGlitchMul;
            _distortSpeedBase = config.distortSpeedBase;
            _distortSpeedGlitchMul = config.distortSpeedGlitchMul;

            NoiseScale = _noiseScaleBase;
            DistortionSpeed = _distortSpeedBase;
        }

        /// <summary>
        /// 重置所有运行时状态 (保留配置参数和订阅者)
        /// 解决 V12：不再 new，订阅者不会丢失
        /// </summary>
        public void Reset()
        {
            AccumulatedDebt = 0f;
            TotalDebt = 0f;
            NoiseScale = _noiseScaleBase;
            DistortionSpeed = _distortSpeedBase;
        }

        /// <summary>
        /// 唯一的外部写入通道：注入环境压力 (全局债务)
        /// CausalController 通过此方法同步全局气候，禁止直接赋值
        /// </summary>
        public void InjectEnvironmentPressure(float globalDebt)
        {
            TotalDebt = globalDebt;
        }

        public void Step(float deltaTime, bool isLoanActive, float duration)
        {
            if (isLoanActive)
            {
                // 【借贷中】
                AccumulatedDebt = Mathf.Lerp(AccumulatedDebt, 0, deltaTime * _loanRecoveryRate);

                NoiseScale = Mathf.Lerp(NoiseScale, _noiseScaleBase, deltaTime * 0.2f);
                DistortionSpeed = Mathf.Lerp(DistortionSpeed, _distortSpeedBase, deltaTime * 0.2f);

                // 贷款代价：增加总负债
                TotalDebt += _loanBaseCost * (1f + duration * _loanDurationPenalty) * deltaTime;
            }
            else
            {
                // 【腐烂中】
                float penalty = 1f + (TotalDebt * _debtPenaltyCoeff);
                float naturalDecay = _naturalDecayRate * penalty;

                // 限制 accumulatedDebt 不要无止境飙升
                if (AccumulatedDebt < EntropyThreshold * _debtCapMultiplier)
                {
                    AccumulatedDebt += naturalDecay * deltaTime;
                }

                // 视觉随腐烂程度自动变得细碎
                NoiseScale = _noiseScaleBase + (GlitchIntensity * _noiseScaleGlitchMul);
                DistortionSpeed = _distortSpeedBase + (GlitchIntensity * _distortSpeedGlitchMul);
            }
            OnStateChanged?.Invoke();
        }

        public void RecordHistory() { }
    }
}