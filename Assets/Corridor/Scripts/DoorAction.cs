using UnityEngine;
using UnityEngine.SceneManagement;

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

    // Q 键交互已统一由 UniversalPlayer 射线 + IInteractable 处理
    // OnTriggerEnter/Exit 保留用于未来的交互提示 UI

    // OnTriggerEnter/Exit 已移除：交互由 UniversalPlayer 射线 + IInteractable 统一处理

    // 这个方法就是 Action 的具体实现
    public void ExecutePortal()
    {
        if (string.IsNullOrEmpty(targetScene)) return;
        
        // 可以在这里加入转场特效的逻辑
        Debug.Log("Action 执行中：加载场景 " + targetScene);
        SceneManager.LoadScene(targetScene);
    }
}