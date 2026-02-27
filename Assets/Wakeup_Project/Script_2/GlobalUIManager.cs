using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI;
using TheLastCompact.Core;

public class GlobalUIManager : MonoBehaviour 
{
    public static GlobalUIManager Instance { get; private set; }

    [Header("UI 引用")]
    public GameObject pausePanel; 
    public GameObject endingPanel; // New: Reference to the Ending UI Panel
    public Text endingText;        // New: Reference to the Text component on the Ending Panel 

    [Header("当前状态")]
    public bool isPaused = false;
    public bool isGameplayActive = false; // 游戏是否已正式开始 (跨场景持久)

    void Awake()
    {
        // 健壮的单例模式：防止 Additive 加载时出现双份 UI
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 关键修复：必须保住整个 Canvas (Root)，而不仅仅是这个脚本所在的物体
        // 如果脚本挂在 Canvas 的子物体上，只保住子物体会导致它脱离 Canvas 而无法渲染
        DontDestroyOnLoad(transform.root.gameObject);
        
        // 确保开场时状态正确
        InitializeSystemState(); 
    }

    private void InitializeSystemState()
    {
        // 1. 强制关闭子物体 UI 面板，不管编辑器里是否勾选
        if (pausePanel != null) pausePanel.SetActive(false); 
        if (endingPanel != null) endingPanel.SetActive(false); // New: Hide ending panel on start 

        // 2. 显式重置状态变量
        isPaused = false; 

        // 3. 确保启动时时间是流动的
        Time.timeScale = 1f; 

        // 4. 确保开场时鼠标锁定
        CursorService.Lock();

        Debug.Log("系统初始化：状态已显式重置为 [Playing]");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        
        if (isPaused) { EnterPausedState(); }
        else { EnterPlayingState(); }
    }

    private void EnterPausedState()
    {
        Debug.Log("状态转换：[Playing] -> [Paused]");
        Time.timeScale = 0f;
        
        if (pausePanel != null) pausePanel.SetActive(true);
        
        CursorService.Unlock();
    }

    private void EnterPlayingState()
    {
        Debug.Log("状态转换：[Paused] -> [Playing]");
        Time.timeScale = 1f;
        
        if (pausePanel != null) pausePanel.SetActive(false);
        
        CursorService.Lock();
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene"); 
    }

    public void QuitGame()
    {
        Debug.Log("退出游戏");
        Application.Quit();
    }

    /// <summary>
    /// 正式开始游戏循环 (从 Wakeup -> Corridor)
    /// 该方法作为全局事件中心，协调各个模块
    /// </summary>
    // 定义全局事件
    public event System.Action OnContractSigned; // 契约签订 (数据重置)
    public event System.Action OnGameStarted;    // 游戏开始 (解冻/UI显示)

    /// <summary>
    /// 启动游戏序列 (Event-Driven)
    /// 1. 触发 OnContractSigned -> 重置数据
    /// 2. 加载场景
    /// 3. 场景加载完毕 -> 触发 OnGameStarted
    /// </summary>
    /// <summary>
    /// 仅开启游戏玩法 (在 Wakeup 场景内)
    /// - 重置数值
    /// - 显示 UI
    /// - 解锁玩家控制
    /// </summary>
    public void EnableGameplay()
    {
        Debug.Log("[GlobalUI] 开启游戏玩法 (在当前场景)...");
        
        // 标记游戏已开始 (跨场景持久)
        isGameplayActive = true;
        
        // 1. 重置数据
        OnContractSigned?.Invoke();
        
        // 2. 开始模拟 & 显示 UI
        OnGameStarted?.Invoke();

        // 3. 锁定鼠标
        CursorService.Lock();
    }

    /// <summary>
    /// 切换到走廊场景 (从 Wakeup 离开)
    /// </summary>
    public void LoadCorridorSequence()
    {
        Debug.Log("[GlobalUI] 切换到走廊场景...");
        StartCoroutine(LoadCorridorAsync());
    }

    private System.Collections.IEnumerator LoadCorridorAsync()
    {
        // 假设场景名为 "Corridor"
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Corridor");

        // 等待加载完成
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 场景加载完后，确保鼠标状态正确
        CursorService.Lock();
        
        Debug.Log("[GlobalUI] 走廊场景加载完毕。");
    }

    /// <summary>
    /// 显示结局 UI
    /// </summary>
    /// <param name="title">结局标题</param>
    /// <param name="description">结局描述</param>
    public void ShowEnding(string title, string description)
    {
        Debug.Log($"[UI] 显示结局: {title}");

        // 1. 激活面板
        if (endingPanel != null) 
        {
            endingPanel.SetActive(true);
            
            // 2. 设置文本
            if (endingText != null)
            {
                endingText.text = $"<size=40>{title}</size>\n\n<size=25>{description}</size>";
            }
        }
        else
        {
            Debug.LogWarning("[UI] GenerateEnding 被调用，但 endingPanel 未赋值！");
        }

        // 3. 释放鼠标
        CursorService.Unlock();

        // 4. (可选) 暂停游戏时间，防止背景还在动
        Time.timeScale = 0f;
    }
}