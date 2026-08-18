using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage5 阶段播报总线 (Stage5 Announce Bus)
    ///
    /// 目的：让音频（Stage5Controller）、视觉（Stage5PostProcessingController）、
    /// 走廊形变（CustomCorridorBinder）、Phase4 献祭系统等互相不直接引用彼此的实例/私有字段，
    /// 全部通过这个静态总线读写共享状态，保持低耦合、可插拔——任何一个子系统都可以订阅或发布，
    /// 删掉/替换某个子系统不会牵连其他系统的编译或运行。
    ///
    /// 提供两类接口：
    /// 1. 离散事件 OnPhaseEntered(int phaseIndex 1~4) —— 阶段边界被跨越时广播一次
    ///    （无论是正常推进、还是被 Phase4 崩坏机制重置回 Phase1），用于一次性触发
    ///    （音效强调、状态重置等），避免每个子系统各自重复散落 0.35/0.70/0.96 这几个阈值判断。
    /// 2. 连续广播值 VisualChaosIntensity (0~1) —— 由视觉系统每帧发布，代表当前画面偏离
    ///    "干净原始状态"的程度（不管是过饱和还是接近黑白，都算"偏离"）。音频系统读取这个值
    ///    来调制失真/低通等参数强度，做到"音频跟着画面一起炸"，而不需要引用视觉系统的具体实现。
    ///
    /// 连续性的强度曲线插值依然由各子系统直接读取 Stage5Controller.phaseProgress 独立完成
    /// (PhaseCurveParam 系统不变)，这个总线只负责离散通知 + 一个共享强度信号，职责单一。
    /// </summary>
    public static class Stage5AnnounceBus
    {
        /// <summary>阶段边界被跨越时触发，参数为新进入的阶段索引 (1~4)。</summary>
        public static event System.Action<int> OnPhaseEntered;

        private static int _lastAnnouncedPhase = -1;

        /// <summary>当前视觉混沌强度 (0~1)，由 Stage5PostProcessingController 每帧写入，其余系统只读。</summary>
        public static float VisualChaosIntensity { get; private set; } = 0f;

        /// <summary>
        /// 由 Stage5Controller 每帧调用：根据当前 phaseProgress 判定阶段索引，
        /// 若与上次不同则广播一次 OnPhaseEntered。
        /// </summary>
        public static void AnnouncePhase(float phaseProgress)
        {
            int phase = ComputePhaseIndex(phaseProgress);
            if (phase != _lastAnnouncedPhase)
            {
                _lastAnnouncedPhase = phase;
                OnPhaseEntered?.Invoke(phase);
            }
        }

        /// <summary>由视觉系统每帧调用，发布当前的视觉混沌强度供音频等系统读取。</summary>
        public static void PublishVisualChaosIntensity(float intensity)
        {
            VisualChaosIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>与全项目其余 Phase 判定逻辑保持一致的固定断点 (0.35 / 0.70 / 0.96)。</summary>
        public static int ComputePhaseIndex(float phaseProgress)
        {
            if (phaseProgress < 0.35f) return 1;
            if (phaseProgress < 0.70f) return 2;
            if (phaseProgress < 0.96f) return 3;
            return 4;
        }

        /// <summary>场景重新加载/编辑器域重载时调用，清空静态状态，避免跨场景残留。</summary>
        public static void ResetState()
        {
            _lastAnnouncedPhase = -1;
            VisualChaosIntensity = 0f;
        }
    }
}
