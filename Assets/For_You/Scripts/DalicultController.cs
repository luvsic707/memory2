using UnityEngine;
using System.Collections;

/// <summary>
/// Dalicult 物体控制器 (接收广播并执行移动演出)
/// 当收到特定事件广播后，使 Dalicult 显现并快速飘过，随后再次隐藏。
/// </summary>
public class DalicultController : MonoBehaviour
{
    [Header("订阅事件")]
    [Tooltip("监听的全局事件ID（需与 Phone 脚本发送的 eventId 一致）")]
    public string targetEventId = "PlayerPlayedPhone";

    [Header("视觉呈现")]
    [Tooltip("Dalicult 的视觉子物体根节点（用于显示/隐藏）。如果不拖拽，脚本会自动控制当前物体下的所有子物体显示/隐藏。")]
    public GameObject visualRoot;

    [Header("移动路径设置")]
    [Tooltip("开始飘过的起点标记")]
    public Transform startPoint;

    [Tooltip("结束飘过的终点标记")]
    public Transform endPoint;

    [Tooltip("如果没有配置起止点，物体会以此方向和距离作为位移")]
    public Vector3 fallbackDirection = Vector3.forward;
    public float fallbackDistance = 15f;

    [Header("飘过参数")]
    [Tooltip("快速飘过的持续时间（秒）")]
    public float dashDuration = 1.2f;

    [Tooltip("飘过时，如果检测到 DaliMelt 变形脚本，会在运动途中动态增加融化拉伸感（制造速度太快导致融化拖尾的幻觉）")]
    public float maxMeltStretch = 2.0f;

    private bool _isDashing = false;

    void Start()
    {
        // 1. 初始化时默认隐藏视觉内容，但保持当前 GameObject 激活，以便能够接收全局事件
        SetVisualsActive(false);

        // 2. 订阅解耦的全局事件总线
        NarrationAnnouncer.OnSceneEventTriggered += HandleSceneEvent;
    }

    void OnDestroy()
    {
        // 3. 及时取消订阅，防止内存泄漏
        NarrationAnnouncer.OnSceneEventTriggered -= HandleSceneEvent;
    }

    private void HandleSceneEvent(string eventId)
    {
        // 确认收到的事件是目标事件，且当前没有在执行运动
        if (eventId == targetEventId && !_isDashing)
        {
            StartCoroutine(DashRoutine());
        }
    }

    private IEnumerator DashRoutine()
    {
        _isDashing = true;
        Debug.Log($"[Dalicult] 接收到全局事件 '{targetEventId}'！开始执行快速飘过演出。");

        // 1. 确定起终点位置
        Vector3 startPos = startPoint != null ? startPoint.position : transform.position;
        Vector3 endPos = endPoint != null ? endPoint.position : transform.position + (fallbackDirection.normalized * fallbackDistance);

        // 2. 将物体瞬移到起点并显示视觉
        transform.position = startPos;
        SetVisualsActive(true);

        // 3. 检测物体本身或子物体上是否有 DaliMelt 变形脚本
        DaliMelt daliMelt = GetComponent<DaliMelt>();
        if (daliMelt == null) daliMelt = GetComponentInChildren<DaliMelt>();
        
        float originalMelt = daliMelt != null ? daliMelt.meltAmount : 0f;

        // 4. 插值移动
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / dashDuration);

            // 线性移动位置
            transform.position = Vector3.Lerp(startPos, endPos, percent);

            // 核心视觉增强：在运动中途 (正弦曲线最高点) 动态将达利融化度拉伸到极致，模拟肉体/形变拉伸的视觉拖尾
            if (daliMelt != null)
            {
                float stretchFactor = Mathf.Sin(percent * Mathf.PI); // 0 -> 1 -> 0
                daliMelt.meltAmount = originalMelt + (stretchFactor * maxMeltStretch);
            }

            yield return null;
        }

        // 5. 到达终点，重置状态并隐藏
        transform.position = endPos;
        if (daliMelt != null)
        {
            daliMelt.meltAmount = originalMelt;
        }

        SetVisualsActive(false);
        _isDashing = false;
        Debug.Log("[Dalicult] 演出完毕，已隐去。");
    }

    private void SetVisualsActive(bool active)
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(active);
        }
        else
        {
            // 如果没有配置 visualRoot，自动隐藏当前物体的所有子 Object
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
