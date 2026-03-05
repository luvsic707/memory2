using UnityEngine;
using UnityEngine.SceneManagement;

// 这个脚本专门用于开发测试
// 当你在非 Bootstrap 场景（比如 Wakeup_room）直接按播放时，
// 它会自动检测 GlobalManager 是否存在，如果不存在，就先把 Bootstrap 场景加载进来。
public class DevPreload : MonoBehaviour
{
    void Awake()
    {
        // 检查全局管理器是否存在
        // 假设 GlobalUIManager 是挂在 GlobalManager 物体上的核心脚本之一
        if (GlobalUIManager.Instance == null)
        {
            Debug.LogWarning("检测到直接运行了游戏场景，正在自动加载 Bootstrap...");
            // 加载 Bootstrap 场景，但是是用 Additive 模式（叠加在当前场景上）
            // 这样既能保留当前场景，又能把 GlobalManager 带进来
            SceneManager.LoadScene("0_Bootstrap", LoadSceneMode.Additive);
            
            // 延迟一帧清理 Bootstrap 场景里多余的摄像机和 EventSystem
            // 因为当前场景（Wakeup）肯定已经有摄像机了，不需要 Bootstrap 的那个
            StartCoroutine(CleanupBootstrap());
        }
    }

    System.Collections.IEnumerator CleanupBootstrap()
    {
        // 等待一帧，让场景加载完
        yield return null;

        // 卸载 Bootstrap 场景里不需要的 Main Camera (如果有的话)
        // 注意：GlobalManager 和 UI 系统是 DontDestroyOnLoad 的，不会被卸载
        var bootstrapScene = SceneManager.GetSceneByName("0_Bootstrap");
        if (bootstrapScene.IsValid())
        {
            var roots = bootstrapScene.GetRootGameObjects();
            foreach (var go in roots)
            {
                // 如果这个物体不是 DontDestroyOnLoad 的（比如 Bootstrap 里的摄像机或者灯光）
                // 那就干掉它，避免和当前场景冲突
                // 简单的判断方法：名字包不包含 Camera 或者 EventSystem
                if (go.name.Contains("EventSystem"))
                {
                    // 先禁用 EventSystem 组件，防止正在处理事件时被销毁报错 (Assertion failed)
                    var es = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
                    if (es != null) es.enabled = false;
                    Destroy(go);
                }
                else if (go.name.Contains("Camera") || go.name.Contains("Light"))
                {
                    Destroy(go);
                }
            }
        }
    }
}
