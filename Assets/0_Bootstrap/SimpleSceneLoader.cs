using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneLoader : MonoBehaviour
{
    [Header("要加载的游戏场景名称")]
    public string sceneToLoad = "MainMenu";

    [Header("延迟几秒加载 (可选)")]
    public float delay = 1.0f;

    void Start()
    {
        // 只有当当前场景确实是 Bootstrap 时才加载下一个场景
        // 如果是从其他场景通过 DevPreload 加载进来的，就不要跳转了
        if (SceneManager.GetActiveScene().name == "0_Bootstrap")
        {
            // 延迟一秒加载，确保 UI 和 GlobalState 都初始化完毕
            Invoke("LoadGameScene", delay);
        }
    }

    void LoadGameScene()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
