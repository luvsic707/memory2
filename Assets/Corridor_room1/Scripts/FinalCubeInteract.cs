using UnityEngine;

/// <summary>
/// Act Final 专属交互脚本：
/// 挂在出现的 Cube 上，玩家靠近后按 Q 键，呼叫出隐藏的 Door。
/// </summary>
public class FinalCubeInteract : MonoBehaviour
{
    [Header("交互设置")]
    [Tooltip("按 Q 键时要显示出来的门")]
    public GameObject doorToReveal;

    [Tooltip("交互距离（玩家必须在这个距离内按 Q 才有效，填 15 的话基本上只要出现在 10 米外就能直接按了）")]
    public float interactDistance = 15f;

    void Update()
    {
        // 监听玩家是否按下了 Q 键
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // 获取玩家（相机）离这个 Cube 的距离
            float dist = Vector3.Distance(Camera.main.transform.position, transform.position);
            
            if (dist <= interactDistance)
            {
                if (doorToReveal != null)
                {
                    // 让门显形
                    doorToReveal.SetActive(true);
                    Debug.Log("[FinalCubeInteract] 玩家与 Cube 交互成功，大门出现！");
                    
                    // 交互完后把这个脚本关掉，防止疯狂按 Q 触发多次
                    this.enabled = false;
                }
                else
                {
                    Debug.LogWarning("[FinalCubeInteract] 你忘记把 Door 拖进槽位里啦！");
                }
            }
            else
            {
                Debug.Log($"[FinalCubeInteract] 按了 Q，但离得太远了 ({dist} 米)！");
            }
        }
    }
}
