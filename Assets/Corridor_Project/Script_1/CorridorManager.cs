using UnityEngine;

public class CorridorManager : MonoBehaviour
{
    public static CorridorManager Instance { get; private set; }

    [Header("传送配置")]
    public Transform startAnchor; 
    public Transform endAnchor;   
    public float offsetX; // 改为 X 轴偏移

    void Awake()
    {
        if (Instance == null) Instance = this;
        
        // 计算 X 轴上的物理距离
        if (startAnchor != null && endAnchor != null)
        {
            offsetX = endAnchor.position.x - startAnchor.position.x;
        }
    }

    public void TeleportPlayer(Transform playerTransform)
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>();

        // 1. 禁用控制器，拿回坐标控制权
        if (cc != null) cc.enabled = false; 

        // 2. 在 X 轴上执行平移
        Vector3 currentPos = playerTransform.position;
        // 减去 offsetX，让玩家跳回到起点
        playerTransform.position = new Vector3(currentPos.x - offsetX, currentPos.y, currentPos.z);
        
        // 3. 恢复控制器
        if (cc != null) cc.enabled = true;

        Debug.Log($"<color=yellow>【轴向修正】X轴重定向成功！偏移量：{offsetX}</color>");
    }
}