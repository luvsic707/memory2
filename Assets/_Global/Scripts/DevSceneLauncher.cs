using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 开发测试专用 - 完全独立，与任何游戏系统无关。
/// 在 Play Mode 里显示一个浮动面板，点击即可跳转任意场景。
/// 打包时自动无效（只在 UNITY_EDITOR 或 DEVELOPMENT_BUILD 下激活）。
/// 把这个脚本挂在随便一个 GameObject 上，或者直接放在每个场景里，方便测试。
/// </summary>
public class DevSceneLauncher : MonoBehaviour
{
    // ── 可测试的场景列表（与 Build Settings 里的名称保持一致）──
    private readonly string[] scenes = new[]
    {
        "0_Bootstrap",
        "MainMenu",
        "Wakeup_room",
        "Corridor",
        "Corridor_room1",
        "Corridor_room2",
        "Corridor_room3",
        "Corridor_room4",
        "Corridor_room5",
        "Archive_room",
        "testEmptyScene"
    };

    // ── UI 控制 ──
    private bool isVisible = false;
    private Vector2 scrollPos;

    private GUIStyle panelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle titleStyle;
    private bool stylesInitialized = false;

    void Update()
    {
        // F1 键切换面板显示
        if (Input.GetKeyDown(KeyCode.F1))
        {
            isVisible = !isVisible;
        }
    }

    void OnGUI()
    {
        // 只在编辑器或 Development Build 里显示
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif
        InitStyles();

        // 右上角小提示
        if (!isVisible)
        {
            GUI.Label(new Rect(Screen.width - 160, 8, 155, 24), "[ F1 ] Dev Scene Launcher");
            return;
        }

        // 半透明面板
        Rect panelRect = new Rect(Screen.width - 230, 10, 220, Mathf.Min(scenes.Length * 46 + 80, Screen.height - 20));
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        GUILayout.BeginArea(panelRect);
        GUILayout.Space(10);
        GUILayout.Label("🎬 Dev Scene Launcher", titleStyle);
        GUILayout.Label($"当前场景: {SceneManager.GetActiveScene().name}", GUILayout.ExpandWidth(true));
        GUILayout.Space(6);

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        foreach (string sceneName in scenes)
        {
            bool isCurrent = SceneManager.GetActiveScene().name == sceneName;
            GUI.enabled = !isCurrent;

            if (GUILayout.Button(isCurrent ? $"▶ {sceneName}" : sceneName, buttonStyle))
            {
                LoadScene(sceneName);
            }
            GUI.enabled = true;
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("✕ 关闭", buttonStyle))
        {
            isVisible = false;
        }
        GUILayout.EndArea();
    }

    private void LoadScene(string sceneName)
    {
        Debug.Log($"[DevLauncher] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        // 面板背景
        Texture2D panelTex = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.05f, 0.88f));
        panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = panelTex }
        };

        // 按钮
        Texture2D btnTex    = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.3f, 1f));
        Texture2D btnHover  = MakeTex(1, 1, new Color(0.35f, 0.35f, 0.55f, 1f));
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal  = { background = btnTex,   textColor = Color.white },
            hover   = { background = btnHover, textColor = Color.white },
            active  = { background = btnHover, textColor = Color.yellow },
            fontSize = 13,
            padding  = new RectOffset(8, 8, 6, 6),
            margin   = new RectOffset(4, 4, 2, 2)
        };

        // 标题
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.cyan },
            alignment = TextAnchor.MiddleCenter
        };
    }

    private Texture2D MakeTex(int w, int h, Color col)
    {
        Texture2D tex = new Texture2D(w, h);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }
}
