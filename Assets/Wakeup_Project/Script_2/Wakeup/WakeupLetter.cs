using UnityEngine;
using UnityEngine.Events;
using TMPro;
using TheLastCompact.Core;

/// <summary>
/// Wakeup 场景中的信件交互物
/// 玩家阅读完信件后，触发游戏正式开始：
/// - GlobalMentalState 解冻
/// - MentalStatsUI 显示
/// </summary>
public class WakeupLetter : MonoBehaviour, IInteractable
{
    [Header("信件 UI")]
    [Tooltip("信件内容面板 (包含文字的 UI Panel)")]
    public GameObject letterPanel;

    [Tooltip("信件内容文本")]
    public TextMeshProUGUI letterText;

    [Header("信件内容")]
    [TextArea(5, 15)]
    public string letterContent = "亲爱的...\n\n这是一封来自记忆深处的信。\n\n当你读到这些文字的时候，一切都已经开始了。";

    [Header("读完后触发")]
    [Tooltip("信件被阅读后触发的事件 (比如：激活门、播放音效等)")]
    public UnityEvent OnLetterRead;

    [Header("设置")]
    [Tooltip("阅读后是否自动关闭")]
    public float autoCloseDelay = 0f; // 0 = 手动关闭

    private bool _hasBeenRead = false;
    private bool _isReading = false;

    public string InteractHint => "按 Q 阅读信件";

    private void Start()
    {
        // 确保信件面板初始隐藏
        if (letterPanel != null) letterPanel.SetActive(false);
    }

    /// <summary>
    /// 实现 IInteractable 接口
    /// </summary>
    public void Interact()
    {
        if (_isReading)
        {
            // 正在阅读中，按 Q 关闭信件
            CloseLetter();
            return;
        }

        OpenLetter();
    }

    private void OpenLetter()
    {
        _isReading = true;
        Debug.Log("[WakeupLetter] 玩家打开了信件。");

        // 显示信件面板
        if (letterPanel != null)
        {
            letterPanel.SetActive(true);

            if (letterText != null)
            {
                letterText.text = letterContent;
            }
        }

        // 释放鼠标 (方便阅读)
        CursorService.Unlock();
    }

    private void CloseLetter()
    {
        _isReading = false;
        Debug.Log("[WakeupLetter] 玩家关闭了信件。");

        // 隐藏信件面板
        if (letterPanel != null)
        {
            letterPanel.SetActive(false);
        }

        // 重新锁定鼠标
        CursorService.Lock();

        // 首次阅读完毕 → 触发游戏正式开始
        if (!_hasBeenRead)
        {
            _hasBeenRead = true;
            TriggerGameStart();
        }
    }

    /// <summary>
    /// 触发游戏正式开始
    /// 调用 GlobalUIManager.EnableGameplay() 来：
    /// 1. 触发 OnContractSigned → 重置 GlobalMentalState 数据
    /// 2. 触发 OnGameStarted → 解冻系统 + 显示 MentalStatsUI
    /// </summary>
    private void TriggerGameStart()
    {
        Debug.Log("<color=green>[WakeupLetter] 信件已阅读，触发游戏正式开始！</color>");

        // 触发自定义事件 (比如显示门)
        OnLetterRead?.Invoke();

        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.EnableGameplay();
        }
        else
        {
            Debug.LogError("[WakeupLetter] 找不到 GlobalUIManager！无法启动游戏。");
        }
    }
}
