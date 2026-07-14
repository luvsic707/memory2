using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastCompact.Core;

public class DoorAction : MonoBehaviour, IInteractable
{
    [Header("目标场景")]
    public string targetScene = "archive_room"; 

    [Header("Interaction Settings")]
    public bool isLocked = false;

    // IInteractable 实现
    public string InteractHint => isLocked ? "门已锁定" : "按 Q 进入 " + targetScene;

    public void Interact()
    {
        if (!isLocked)
        {
            ExecutePortal();
        }
        else
        {
            Debug.Log("[DoorAction] 门被锁了，无法进入。");
        }
    }

    public void ExecutePortal()
    {
        if (string.IsNullOrEmpty(targetScene)) return;

        Debug.Log("[DoorAction] 请求场景切换 → " + targetScene);

        // 事件驱动：交给 GlobalProgressManager 统一处理
        // 它负责 ScreenFader 淡入淡出 + 异步加载，避免主线程卡顿
        if (GlobalProgressManager.Instance != null)
        {
            GlobalProgressManager.Instance.RequestSceneTransition(targetScene);
        }
        else
        {
            // Fallback：单独测试场景时直接加载
            Debug.LogWarning("[DoorAction] GlobalProgressManager 不存在，使用直接加载（仅测试用）");
            SceneManager.LoadScene(targetScene);
        }
    }
}