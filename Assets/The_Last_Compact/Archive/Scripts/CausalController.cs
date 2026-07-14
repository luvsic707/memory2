using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// Controller 层 (Ex-CausalController)
    /// 重构：现在改为 LocalEntropyReactor (局部熵反应堆)
    /// 1. 不再自己检测输入，而是监听 GlobalMentalState
    /// 2. 拥有独立的腐烂速度 (DecayMultiplier)
    /// </summary>
    public class CausalController : MonoBehaviour
    {
        [Header("Local Constitution")]
        [Tooltip("个体体质系数：< 1 代表比环境更坚强，> 1 代表更脆弱")]
        [SerializeField] private float decayMultiplier = 1.0f;
        
        [Header("Configuration")]
        [Tooltip("拖入 GameBalance 资产")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        private CausalModel _model;
        public CausalModel Model => _model;

        // 缓存的全局"气候"数据
        private float _cachedGlobalDebt;
        private bool _cachedLoanActive;

        void Awake()
        {
            if (balanceConfig == null)
            {
                Debug.LogWarning("[CausalController] GameBalanceConfig 未赋值！使用默认值。");
                balanceConfig = ScriptableObject.CreateInstance<GameBalanceConfig>();
            }
            _model = new CausalModel(balanceConfig);
        }

        void Start()
        {
            // 订阅全局事件
            if (GlobalMentalState.Instance != null && GlobalMentalState.Instance.Model != null)
            {
                GlobalMentalState.Instance.Model.OnStateChanged += SyncWithGlobalClimate;
                // 初始化一次
                SyncWithGlobalClimate();
            }
        }

        void OnDestroy()
        {
            // 防止内存泄漏，取消订阅
            if (GlobalMentalState.Instance != null && GlobalMentalState.Instance.Model != null)
            {
                GlobalMentalState.Instance.Model.OnStateChanged -= SyncWithGlobalClimate;
            }
        }

        /// <summary>
        /// 事件回调：当全局状态发生变化时，更新本地的气候参数
        /// </summary>
        private void SyncWithGlobalClimate()
        {
            if (GlobalMentalState.Instance == null) return;

            // 1. 获取已持续的贷款状态
            _cachedLoanActive = GlobalMentalState.Instance.IsLoanActive;

            // 2. 获取全球总因果债 (这是环境压力)
            _cachedGlobalDebt = GlobalMentalState.Instance.Model.TotalDebt;
        }

        void Update()
        {
            if (_model == null) return;

            // --- 核心逻辑：本地模拟 ---
            
            // 1. 注入环境压力 (通过封装方法，禁止直接赋值)
            _model.InjectEnvironmentPressure(_cachedGlobalDebt);

            // 2. 步进本地模型
            // 关键点：使用 decayMultiplier 缩放时间，实现个体的快慢差异
            // 传入 0f 作为 duration，因为我们不想在本地重复计算贷款时长的叠加惩罚（那已经是 Global 的职责了）
            _model.Step(Time.deltaTime * decayMultiplier, _cachedLoanActive, 0f);
        }
    }
}