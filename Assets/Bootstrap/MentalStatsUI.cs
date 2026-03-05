using UnityEngine;
using TMPro; // 记得引用这个，处理文字

public class MentalStatsUI : MonoBehaviour
{
    [Header("UI 文本引用")]
    public TextMeshProUGUI entropyText;
    public TextMeshProUGUI psycheText;
    public TextMeshProUGUI debtText;

    // 单例模式 (可选，如果其他脚本需要访问 UI 组件的话)
    public static MentalStatsUI Instance;

    // 控制 UI 显隐
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        // 关键修复：不再管理整个 UI System 的生死
        // 也不再需要 DontDestroyOnLoad，因为 GlobalUIManager 已经带着我们一起飞了
        
        if (Instance == null)
        {
            Instance = this;
            
            // 初始化 CanvasGroup (如果没有就加上)
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            HideUI();
        }
        else
        {
            // 如果真的有重复，只毁灭这个组件所在的物体，绝不毁灭 Root
            // 但理论上 GlobalUIManager 不会让我们重复
            Debug.LogWarning("[MentalStatsUI] Duplicate found, destroying self.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnGameStarted += ShowUI;
        }
    }

    private void OnDestroy()
    {
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnGameStarted -= ShowUI;
        }
    }

    public void ShowUI()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true; // 可交互
        }
    }

    public void HideUI()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false; // 不可交互
        }
    }

    void Update()
    {
        // 暂时放在 Update 里实时刷新，方便你调试
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        // 1. 安全检查：确保 GlobalMentalState 已经初始化
        if (GlobalMentalState.Instance == null || GlobalMentalState.Instance.Model == null)
        {
            // V11 修复：null guard
            if (entropyText != null) entropyText.text = "Waiting for System...";
            return;
        }

        // 2. 从全局状态获取核心数据 (V9：所有值从 Model 读取，UI 不做任何计算)
        var model = GlobalMentalState.Instance.Model;

        // 3. 更新 UI 显示
        if (entropyText != null) 
            entropyText.text = $"ENTROPY: {model.AccumulatedDebt:F1} / {model.EntropyThreshold:F0}";
            
        if (psycheText != null) 
            psycheText.text = $"PSYCHE: {model.Psyche:F1}";
            
        if (debtText != null) 
            debtText.text = $"DEBT: ${model.TotalDebt:F0}";
    }
    
    // 移除 UpdateDebt 方法，因为数据流是单向的 (UI 只负责显示，不负责修改)
    // 任何修改都应该通过 GlobalMentalState 去操作 CausalModel
}