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

    // 实现 IInteractable 接口的方法：当玩家按下交互键时触发
    public void Interact()
    {
        TriggerEvent("按键交互");
    }

    // 触发器检测：当玩家身体碰到手机时直接触发
    private void OnTriggerEnter(Collider other)
    {
        // 支持按 Tag 或者名字识别玩家
        if (other.CompareTag("Player") || other.name.Contains("player") || other.name.Contains("Player"))
        {
            TriggerEvent("碰撞接触");
        }
    }

    private void TriggerEvent(string triggerSource)
    {
        if (triggerOnce && _hasTriggered) return;

        _hasTriggered = true;
        Debug.Log($"<color=cyan>[Phone] 玩家通过【{triggerSource}】激活了手机！向全局总线广播事件：{eventId}</color>");

        // 核心解耦：直接使用全局事件总线发送事件
        NarrationAnnouncer.TriggerSceneEvent(eventId);
    }
}
