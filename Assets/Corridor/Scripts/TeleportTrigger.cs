using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
{
    // 只要有东西撞进来，就打印名字
    Debug.Log("【物理层检测成功】撞到了物体: " + other.name); 

    // 检查标签（注意：这里用你的小写 player）
    if (other.CompareTag("Player")) 
    {
        Debug.Log("【逻辑层匹配成功】确认是玩家，准备传送！");
        
        if (CorridorManager.Instance != null)
        {
            CorridorManager.Instance.TeleportPlayer(other.transform);
        }
        else
        {
            Debug.LogError("【严重错误】找不到 CorridorManager 实例！你挂脚本了吗？");
        }
    }
    else
    {
        Debug.Log("【逻辑层拦截】撞到的是 " + other.name + "，标签是 " + other.tag + "，不是 player。");
    }
} // Added this closing brace to fix the scope of OnTriggerEnter
    // 专门用于检测 Character Controller 进入
private void OnControllerColliderHit(ControllerColliderHit hit)
{
    if (hit.gameObject.CompareTag("Player"))
    {
        Debug.Log("【控制器检测】确认是玩家！");
        CorridorManager.Instance.TeleportPlayer(hit.gameObject.transform);
    }
}
}