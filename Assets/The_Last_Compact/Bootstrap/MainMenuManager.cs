using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastCompact.Core;

public class MainMenuManager : MonoBehaviour
{
    [Header("开始游戏后加载的场景")]
    public string firstLevelScene = "Wakeup_room";

    private void Start()
    {
        // 关键修复：确保在主菜单时鼠标是可见且解锁的
        CursorService.Unlock();
    }

    public void OnStartGameButton()
    {
        Debug.Log("开始游戏！正在加载 Wakeup 场景...");
        // 必须确保 Wakeup_room 已经加到了 Build Settings
        SceneManager.LoadScene(firstLevelScene);
    }

    public void OnQuitGameButton()
    {
        Debug.Log("退出游戏");
        Application.Quit();
    }
}
