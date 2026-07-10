using UnityEngine;
using UnityEngine.SceneManagement;

// 这个脚本专门用于开发测试
// 当你在非 Bootstrap 场景（比如 Wakeup_room）直接按播放时，
// 它会自动检测 GlobalManager 是否存在，如果不存在，就先把 Bootstrap 场景加载进来。
public class DevPreload : MonoBehaviour
{
#if UNITY_EDITOR
    // 当在 Unity 编辑器中按下 Play 时，这个静态方法会自动在加载第一个关卡场景之前执行！
    // 这样，不管当前打开并运行的是哪个关卡场景，它都会自动且零配置地把 Bootstrap 加载进来。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoPreloadBootstrap()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        // 如果是 Bootstrap 或者是主菜单，我们不需要做任何额外的预加载
        if (activeSceneName == "0_Bootstrap" || activeSceneName == "MainMenu")
        {
            return;
        }

        // 检查 0_Bootstrap 是否已经加载
        bool isBootstrapLoaded = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == "0_Bootstrap")
            {
                isBootstrapLoaded = true;
                break;
            }
        }

        if (!isBootstrapLoaded)
        {
            Debug.LogWarning($"[DevPreload] 检测到直接在编辑器中运行关卡场景 '{activeSceneName}'，正在自动预加载 0_Bootstrap 核心框架...");
            
            // 采用 Additive 模式加载 Bootstrap，使其与当前关卡合并
            SceneManager.LoadScene("0_Bootstrap", LoadSceneMode.Additive);
            
            // 注册场景加载后的清理逻辑，去除 Bootstrap 中的重复相机/灯光等物体
            SceneManager.sceneLoaded += OnBootstrapSceneLoaded;
        }
    }

    private static void OnBootstrapSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "0_Bootstrap")
        {
            SceneManager.sceneLoaded -= OnBootstrapSceneLoaded;

            // 清理 Bootstrap 中多余的摄像机、灯光和 EventSystem，避免与当前关卡场景冲突
            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go.name.Contains("EventSystem"))
                {
                    var es = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
                    if (es != null) es.enabled = false;
                    Destroy(go);
                    Debug.Log("[DevPreload] 已自动清理 Bootstrap 中重复的 EventSystem");
                }
                else if (go.name.Contains("Camera") || go.name.Contains("Light") || go.name.Contains("Directional Light"))
                {
                    Destroy(go);
                    Debug.Log($"[DevPreload] 已自动清理 Bootstrap 中重复的组件: {go.name}");
                }
            }
        }
    }
#endif

    void Awake()
    {
        // 兼容原有的挂载式 Awake 逻辑（若静态加载由于特殊原因未触发作为兜底）
        if (GlobalUIManager.Instance == null)
        {
            Debug.LogWarning("[DevPreload] Component Awake: 检测到全局系统未初始化，正在加载 Bootstrap...");
            SceneManager.LoadScene("0_Bootstrap", LoadSceneMode.Additive);
            StartCoroutine(CleanupBootstrap());
        }
    }

    System.Collections.IEnumerator CleanupBootstrap()
    {
        // 等待一帧，让场景加载完
        yield return null;

        var bootstrapScene = SceneManager.GetSceneByName("0_Bootstrap");
        if (bootstrapScene.IsValid())
        {
            var roots = bootstrapScene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go.name.Contains("EventSystem"))
                {
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
