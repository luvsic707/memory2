using UnityEngine;

/// <summary>
/// 当此物体被启用时（比如被 SequenceManager 设为 active），
/// 自动瞬移到玩家的正前方。
/// </summary>
public class TeleportToPlayerOnEnable : MonoBehaviour
{
    [Header("玩家引用 (不填则自动找名字叫 player 的物体)")]
    public Transform playerTransform;

    [Header("位置设置")]
    [Tooltip("出现在玩家正前方的距离（单位：米）")]
    public float distanceInFront = 10f;
    
    [Tooltip("是否强行把 Y 轴高度设为跟玩家一样（平行视线）")]
    public bool keepSameY = true;
    
    [Tooltip("Y轴的额外微调偏移量")]
    public float yOffset = 0f;

    [Header("朝向设置")]
    [Tooltip("是否让这个物体自动转身，正脸对着玩家")]
    public bool lookAtPlayer = true;

    void OnEnable()
    {
        // 优先使用主相机（玩家眼睛真正的朝向）来判断“正前方”
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
        else if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("[Teleport] 找不到玩家或相机，无法瞬移！");
            return;
        }

        // 获取玩家的面朝方向，但强行抹平 Y 轴（防止玩家低头看地板导致门刷在地底下）
        Vector3 flatForward = playerTransform.forward;
        flatForward.y = 0;
        if (flatForward.sqrMagnitude < 0.01f) flatForward = playerTransform.up; // 极小概率玩家100%垂直看地
        flatForward.Normalize();

        Vector3 targetPos = playerTransform.position + flatForward * distanceInFront;

        // 处理 Y 轴保持平行
        if (keepSameY)
        {
            targetPos.y = playerTransform.position.y + yOffset;
        }
        
        transform.position = targetPos;

        if (lookAtPlayer)
        {
            Vector3 lookDirection = playerTransform.position - transform.position;
            lookDirection.y = 0;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
        
        Debug.Log($"[TeleportToPlayer] 成功将 {gameObject.name} 瞬移到了玩家面前 {distanceInFront} 米处！坐标: {targetPos}");
    }
}
