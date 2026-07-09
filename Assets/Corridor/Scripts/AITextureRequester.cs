using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

// 不需要 API Key (完全免费 & 无限生成)
// 这个服务直接通过 URL 参数生成 AI 图片，非常适合快速测试

public class AITextureRequester : MonoBehaviour
{
    [Header("画框设置 (解决贴图拉伸/破碎问题)")]
    public Renderer wallRenderer; // 需要更换贴图的目标物体
    // 是否生成悬浮画框? True: 生成新 Quad; False: 直接贴墙上
    public bool useFloatingCanvas = true; 
    // 画框相对于墙的位置偏移 (默认向前移动一点点，防止闪烁)
    public Vector3 canvasOffset = new Vector3(0, 0, -0.05f); 
    // 画框旋转 (如果画反了，改这里)
    public Vector3 canvasRotation = new Vector3(0, 180, 0); 
    // 画框大小 (默认 2x2 米)
    public Vector3 canvasScale = new Vector3(2, 2, 1);
    
    // 内部使用的画框对象
    private GameObject _currentCanvas;

    [Header("高级设置 (可选)")]
    // 如果你有 Pollinations.ai 的 API Key，填在这里 (可以解锁顶级 Flux 模型 & 无限制)
    // 获取地址: https://pollinations.ai/
    public string apiKey = "";

    [Header("幻灯片设置")]
    [Tooltip("每隔多少秒换一张画")]
    public float changeInterval = 45f;

    [Header("提示词配置")]
    [TextArea]
    public string promptBase = "Oil painting by Egon Schiele, expressionism art style, raw emotional lines, masterpiece, high quality, 8k, detailed face";
    public string[] randomKeywords = { 
        "portrait of a twisted man", 
        "skeletal figure sitting", 
        "melancholic self portrait", 
        "woman with red hair", 
        "abstract anatomy study", 
        "couple embracing in despair", 
        "hands reaching out", 
        "distorted body lines"
    };

    void Start()
    {
        // 如果填了 API Key，我们可以大胆一点：用更好的模型，并且切得快一点
        if (!string.IsNullOrEmpty(apiKey))
        {
            Debug.Log("[AITextureRequester] 检测到 API Key！启用高级模式 (Flux 模型 + 高速生成)");
            changeInterval = 15f; // 有 Key 的话，15秒一张没压力
        }
        else
        {
            Debug.Log("[AITextureRequester] 未检测到 API Key。使用免费模式 (Turbo 模型 + 慢速生成)");
            changeInterval = 45f; // 没 Key 只能慢点，而且用 Turbo
        }

        // 如果开启了悬浮画框模式，先生成画框
        if (useFloatingCanvas)
        {
            CreateFloatingCanvas();
        }
        else
        {
            // 否则尝试自动获取 Renderer
            if (wallRenderer == null)
            {
                wallRenderer = GetComponent<Renderer>();
                if (wallRenderer == null)
                {
                    Debug.LogError($"[AITextureRequester] {gameObject.name} 上找不到 Renderer 组件，脚本已停用。", gameObject);
                    return;
                }
            }
        }

        // 启动幻灯片播放协程
        StartCoroutine(SlideShowRoutine());
    }

    // 生成一个专用的 Cube (双面可见) 来展示画作，避开 Quad 的背面剔除不可见问题
    void CreateFloatingCanvas()
    {
        if (_currentCanvas != null) return;

        // 改成 Cube，这样不管你在正面还是反面都能看见它！
        _currentCanvas = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _currentCanvas.name = "ArtCanvas_" + gameObject.name;
        
        // 设置父子关系
        _currentCanvas.transform.SetParent(transform, false);
        
        // 应用偏移、旋转、缩放
        // 现在真实读取面板上配置的参数，而不是写死 0
        _currentCanvas.transform.localPosition = canvasOffset; 
        
        // 关键修复：图片是倒着的，绕 Z 轴旋转 180 度把它正过来
        _currentCanvas.transform.localRotation = Quaternion.Euler(canvasRotation); 
        
        // 缩放：宽2米，高2米，厚度0.05米（像一块真正的画布板子）
        // 还要检查父物体是不是缩放是负数？不管了，读取面板配置
        _currentCanvas.transform.localScale = new Vector3(canvasScale.x, canvasScale.y, 0.05f);
        
        // 获取新画框的 Renderer
        wallRenderer = _currentCanvas.GetComponent<Renderer>();
        
        // 关键修改：检查当前渲染管线，用通用性最好的 Shader
        // 如果是 URP 就用 URP/Unlit，否则用 Legacy Diffuse，保证肯定能看见
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit"); // 尝试 URP Lit
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit"); // 尝试 URP Simple Lit
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse"); // 最后的救命稻草

        if (shader == null)
        {
            Debug.LogError($"[AITextureRequester] 严重错误：找不到任何可用的 Shader！画框无法生成。请检查项目设置里的 Shader Striping。");
            Destroy(_currentCanvas);
            return;
        }

        wallRenderer.material = new Material(shader)
        {
            color = Color.magenta // 给个显眼的洋红色作为占位符，如果看见洋红色方块就说明画框生成了
        };

        // 移除碰撞体 (防止挡路)
        Destroy(_currentCanvas.GetComponent<Collider>());
        
        Debug.Log($"[AITextureRequester] 已生成悬浮画框(Cube): {_currentCanvas.name}，位置: {_currentCanvas.transform.position}");
    }

    // 每一帧都根据 Inspector 的值更新画板，极大方便你运行时实时拖拽调光！
    void Update()
    {
        if (_currentCanvas != null)
        {
            _currentCanvas.transform.localPosition = canvasOffset;
            _currentCanvas.transform.localRotation = Quaternion.Euler(canvasRotation);
            _currentCanvas.transform.localScale = new Vector3(canvasScale.x, canvasScale.y, 0.05f);
        }
    }

    IEnumerator SlideShowRoutine()
    {
        // 只要开启，就一直循环
        while (true)
        {
            RequestNewHorrorTexture();
            yield return new WaitForSeconds(changeInterval);
            Debug.Log($"[AITextureRequester] 等待 {changeInterval} 秒后切换下一张...");
        }
    }

    [ContextMenu("手动切换下一张")]
    public void RequestNewHorrorTexture()
    {
        // 如果是在 Editor 里且没运行，协程跑不了，所以提醒一下
        if (!Application.isPlaying) 
        {
            Debug.LogWarning("请在运行模式(Play Mode)下测试，或者等待自动播放。");
            return;
        }

        string finalPrompt = BuildRandomPrompt();
        
        // 直接构建 URL
        string encodedPrompt = UnityWebRequest.EscapeURL(finalPrompt);
        // 加上超大随机数防止缓存，避免 Rate Limit
        int seed = Random.Range(100000, 9999999);
        
        string url = "";

        // 根据是否有 API Key 决定使用什么模型
        if (!string.IsNullOrEmpty(apiKey))
        {
            // 有 Key: 使用 Flux (画质最好)，不需要 nofeed 参数
            url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=1024&seed={seed}&nologo=true&model=flux";
        }
        else
        {
            // 无 Key: Turbo 模型似乎正在维护/升级，暂时全部切回 Flux 模型
            // 注意：Flux 免费版也有速率限制，所以必须保持 45秒 的间隔
            url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=1024&seed={seed}&nologo=true&model=flux&nofeed=true";
        }

        StartCoroutine(DownloadImage(url));
    }

    private string BuildRandomPrompt()
    {
        string keyword = randomKeywords[Random.Range(0, randomKeywords.Length)];
        return $"{promptBase}, {keyword}";
    }

    IEnumerator DownloadImage(string url)
    {
        Debug.Log($"[AITextureRequester] 正在生成 AI 图片 ({(string.IsNullOrEmpty(apiKey) ? "免费模式" : "高级模式")})，URL: {url}");
        
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            if (!string.IsNullOrEmpty(apiKey))
            {
                www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AITextureRequester] 主服务失败 ({www.error})，尝试备用图源...");
                
                // Fallback: 使用随机艺术照片
                int fallbackSeed = Random.Range(1, 99999);
                string fallbackUrl = $"https://picsum.photos/seed/{fallbackSeed}/1024/1024";
                
                using (UnityWebRequest fallback = UnityWebRequestTexture.GetTexture(fallbackUrl))
                {
                    yield return fallback.SendWebRequest();
                    
                    if (fallback.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("<color=yellow>[AITextureRequester] 使用备用图源成功 (Pollinations 暂时不可用)</color>");
                        ApplyTexture(DownloadHandlerTexture.GetContent(fallback));
                    }
                    else
                    {
                        Debug.LogError($"[AITextureRequester] 备用图源也失败了: {fallback.error}");
                    }
                }
            }
            else
            {
                ApplyTexture(DownloadHandlerTexture.GetContent(www));
            }
        }
    }

    private void ApplyTexture(Texture2D downloadedTexture)
    {
        if (wallRenderer == null) return;

        downloadedTexture.name = "GeneratedHorror";
        downloadedTexture.wrapMode = TextureWrapMode.Clamp;

        // 强制材质属性
        wallRenderer.material.color = Color.white;
        wallRenderer.material.mainTextureScale = new Vector2(1, 1);
        wallRenderer.material.mainTextureOffset = new Vector2(0, 0);

        // 自发光设置
        wallRenderer.material.EnableKeyword("_EMISSION");
        wallRenderer.material.SetTexture("_EmissionMap", downloadedTexture);
        wallRenderer.material.SetColor("_EmissionColor", Color.white * 0.7f);

        wallRenderer.material.mainTexture = downloadedTexture;
        Debug.Log("<color=green>[AITextureRequester] 贴图更换成功！</color>");
    }
}