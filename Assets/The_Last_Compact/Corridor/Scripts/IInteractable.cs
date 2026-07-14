/// <summary>
/// 通用交互接口
/// 任何可以被玩家交互的物体都应该实现此接口
/// 例如：门、信件、开关、NPC 等
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 执行交互行为
    /// </summary>
    void Interact();

    /// <summary>
    /// 交互提示文字 (可选，用于未来的 UI 提示)
    /// </summary>
    string InteractHint { get; }
}
