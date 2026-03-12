using UnityEngine;
using TheLastCompact.Core;

public class GlobalMentalState : MonoBehaviour
{
    public static GlobalMentalState Instance { get; private set; }
    public CausalModel Model { get; private set; }

    [Header("平衡配置")]
    [Tooltip("拖入 GameBalance 资产")]
    public GameBalanceConfig balanceConfig;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保证 A-B-C 跳转不销毁

            if (balanceConfig == null)
            {
                Debug.LogWarning("[MentalState] GameBalanceConfig 未赋值！使用默认值。请在 Inspector 中拖入配置资产。");
                balanceConfig = ScriptableObject.CreateInstance<GameBalanceConfig>();
            }
            Model = new CausalModel(balanceConfig);
        }
        else { Destroy(gameObject); }
    }

    // 公开属性供观察者读取，避免重复 Input 检测
    public bool IsLoanActive { get; private set; }
    private float _currentLoanDuration = 0f;

    // 系统冻结标志 (用于结算时刻定格数据)
    // 默认为 true，等待对话结束后触发 OnGameStarted 事件解冻
    private bool _isFrozen = true;

    /// <summary>
    /// 冻结系统状态 (停止任何数值变化)
    /// </summary>
    public void FreezeSystem()
    {
        _isFrozen = true;
        Debug.Log("<color=red>[MentalState] System Frozen. All updates stopped.</color>");
    }

    private void Start()
    {
        // 订阅全局事件
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnContractSigned += HandleContractSigned;
            GlobalUIManager.Instance.OnGameStarted += HandleGameStarted;
        }
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnContractSigned -= HandleContractSigned;
            GlobalUIManager.Instance.OnGameStarted -= HandleGameStarted;
        }
    }

    /// <summary>
    /// 处理契约签订事件：重置数值 (V12 修复：用 Reset 而非 new，保留订阅者)
    /// </summary>
    private void HandleContractSigned()
    {
        Model.Reset();
        Debug.Log("[MentalState] Contract Signed. Model Reset (subscribers preserved).");
    }

    /// <summary>
    /// 处理游戏开始事件：解冻系统
    /// </summary>
    private void HandleGameStarted()
    {
        _isFrozen = false;
        Debug.Log("[MentalState] Game Started. System Unfrozen.");
    }

    void Update()
    {
        // 如果被冻结，直接跳过所有逻辑
        if (_isFrozen) return;
        if (Model == null) return;

        // 1. 统一处理输入
        IsLoanActive = Input.GetKey(KeyCode.Space);

        // 2. 计算按住时长 (用于计算借贷惩罚)
        if (IsLoanActive)
        {
            _currentLoanDuration += Time.deltaTime;
        }
        else
        {
            _currentLoanDuration = 0f;
        }

        // 3. 驱动核心模型
        Model.Step(Time.deltaTime, IsLoanActive, _currentLoanDuration);

        // 监控日志 (每秒打印一次)
        _logTimer += Time.deltaTime;
        if (_logTimer >= 1.0f)
        {
//            Debug.Log($"[MentalState] Entropy: {Model.AccumulatedDebt:F1} / {Model.EntropyThreshold:F0} | Psyche: {Model.Psyche:F1} | Total Debt: {Model.TotalDebt:F1}");
            _logTimer = 0f;
        }
    }

    private float _logTimer = 0f;
}