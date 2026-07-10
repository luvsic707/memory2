using UnityEngine;

/// <summary>
/// Corridor Room 1 专用 - 完全独立
/// 计时器触发：delay 秒后瞬间禁用 Act One，启用 Act Two。
/// 不依赖任何其他系统，挂在场景任意 GameObject 上即可。
/// </summary>
public class ActTransitionController : MonoBehaviour
{
    [Header("Act 根节点")]
    [Tooltip("幕一的根节点 GameObject")]
    public GameObject actOneRoot;

    [Tooltip("幕二的根节点 GameObject")]
    public GameObject actTwoRoot;

    [Header("计时设置")]
    [Tooltip("多少秒后从 Act One 切换到 Act Two")]
    public float delay = 30f;

    private float timer = 0f;
    private bool transitioned = false;

    void Start()
    {
        // 初始状态：幕一开，幕二关
        if (actOneRoot != null) actOneRoot.SetActive(true);
        if (actTwoRoot != null) actTwoRoot.SetActive(false);
    }

    void Update()
    {
        if (transitioned) return;

        timer += Time.deltaTime;

        if (timer >= delay)
        {
            transitioned = true;
            SwitchToActTwo();
        }
    }

    private void SwitchToActTwo()
    {
        if (actOneRoot != null) actOneRoot.SetActive(false);
        if (actTwoRoot != null) actTwoRoot.SetActive(true);

        Debug.Log("[ActTransition] 切换完成：Act One → Act Two");
    }

    /// <summary>
    /// 也可以从外部（比如 UnityEvent、按钮）手动触发切换
    /// </summary>
    public void TriggerTransitionNow()
    {
        if (transitioned) return;
        transitioned = true;
        SwitchToActTwo();
    }
}
