using UnityEngine;

[ExecuteAlways]  // 让 Update() 在 Editor 里也执行
/// <summary>
/// 配套 MeltDistortion.shader 使用。
/// 挂在要融化的 GameObject 上，自动随时间把 _MeltProgress 从 0 推到 1。
/// 完全独立，不依赖任何其他系统。
/// </summary>
public class MeltController : MonoBehaviour
{
    [Header("融化设置")]
    [Tooltip("是否在脚本启用后自动倒计时融化（受总导演控制时请取消勾选）")]
    public bool autoStart = false;

    [Tooltip("如果勾选了自动，几秒后开始融化")]
    public float startDelay = 5f;

    [Tooltip("融化总时长（秒）")]
    public float duration = 8f;

    [Header("调试")]
    [Range(0f, 1f)]
    [Tooltip("直接拖这个滑块预览效果（Editor 也立即生效）")]
    public float debugProgress = 0f;
    public bool useDebugProgress = false;

    private float timer = 0f;
    private bool started = false;
    private static readonly int GlobalMeltProgressID = Shader.PropertyToID("_GlobalMeltProgress");

    void Start()
    {
        // 初始设为0
        Shader.SetGlobalFloat(GlobalMeltProgressID, 0f);
    }

    void Update()
    {
        // 调试预览模式
        if (useDebugProgress)
        {
            Shader.SetGlobalFloat(GlobalMeltProgressID, debugProgress);
            return;
        }

        if (!started)
        {
            if (!autoStart) return; // 挂起，死等外部 StartMeltNow 唤醒

            timer += Time.deltaTime;
            if (timer < startDelay) return;
            
            started = true;
            timer = 0f;
        }
        else
        {
            timer += Time.deltaTime;
        }

        // 从 0 推到 1
        float progress = Mathf.Clamp01(timer / duration);
        Shader.SetGlobalFloat(GlobalMeltProgressID, progress);
    }

    void OnDisable()
    {
        // 脚本停止时复原
        Shader.SetGlobalFloat(GlobalMeltProgressID, 0f);
    }

    /// <summary>
    /// 外部调用：立即开始融化（跳过 delay）
    /// </summary>
    public void StartMeltNow()
    {
        started = true;
        timer = 0f;
    }
}
