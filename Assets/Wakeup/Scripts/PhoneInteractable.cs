using UnityEngine;

/// <summary>
/// 手机交互与接触检测脚本 (触发事件源)
/// 当玩家与手机发生接触（Trigger 碰撞）或交互（Interact）时，发出全局广播。
/// 完全解耦：它不知道谁会响应这个事件，只管发送广播。
/// </summary>
public class PhoneInteractable : MonoBehaviour, IInteractable
{
    [Header("事件设置")]
    [Tooltip("被触发时发送的全局事件ID")]
    public string eventId = "PlayerPlayedPhone";

    [Tooltip("是否只触发一次")]
    public bool triggerOnce = true;

    [Header("交互提示 (可选)")]
    [SerializeField] private string interactHint = "玩手机";

    private bool _hasTriggered = false;

    // 实现 IInteractable 接口的属性
    public string InteractHint => interactHint;

    void Awake()
    {
        // 自动防御：如果物体上没有 Collider，射线交互将完全无法命中！
        // 我们在运行时自动为其添加 BoxCollider。
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"[Phone] 监测到 '{gameObject.name}' 上没有 Collider！已自动添加 BoxCollider，以确保 Q 键射线检测可以正常工作。");
        }
    }

    // 实现 IInteractable 接口的方法：当玩家看准手机并按下交互键（如 Q 键射线交互）时触发
    public void Interact()
    {
        TriggerEvent();
    }

    private void TriggerEvent()
    {
        if (triggerOnce && _hasTriggered) return;

        _hasTriggered = true;
        Debug.Log($"<color=cyan>[Phone] 玩家通过【Q键射线交互】激活了手机！向全局总线广播事件：{eventId}</color>");

        // 核心解耦：直接使用全局事件总线发送事件
        NarrationAnnouncer.TriggerSceneEvent(eventId);
    }
}
