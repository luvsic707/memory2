using UnityEngine;

public class AutoStartGame : MonoBehaviour
{
    [Header("延迟几秒开始游戏（模拟对话时间）")]
    public float startDelay = 0.1f; // 几乎立即开始

    void Start()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // 安全检查：绝对不可以在 Bootstrap 和 MainMenu 里自动开始
        if (currentScene == "0_Bootstrap" || currentScene == "MainMenu")
        {
            Debug.LogWarning("AutoStartGame 不应该放在系统场景里！已自动停止。");
            return;
        }

        // 关键修复：如果场景里有叙事管理器 (WakeupSequenceManager)，说明需要演戏，不要自动跳过！
        if (FindAnyObjectByType<TheLastCompact.Wakeup.WakeupSequenceManager>() != null)
        {
            Debug.LogWarning("检测到 WakeupSequenceManager，AutoStartGame 自动停止，以免跳过剧情。");
            return;
        }

        // 关键修复：如果游戏已经开始过了（比如从 Wakeup 过来），不要再次触发 EnableGameplay
        // 否则会重置 CausalModel 数据！
        if (GlobalUIManager.Instance != null && GlobalUIManager.Instance.isGameplayActive)
        {
            Debug.Log("[AutoStartGame] 游戏已在进行中，跳过重复启动。");
            return;
        }

        Invoke("StartGameLogic", startDelay);
    }

    void StartGameLogic()
    {
        if (GlobalUIManager.Instance != null)
        {
            Debug.Log("自动触发游戏开始流程...");
            // 使用新的全局 UI 启动序列 (仅开启玩法，不切场景)
            GlobalUIManager.Instance.EnableGameplay();
        }
        else
        {
            Debug.LogError("找不到 GlobalUIManager，无法自动开始游戏！请从 Bootstrap 启动。");
        }
    }
}
