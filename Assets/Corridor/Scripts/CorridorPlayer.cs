using UnityEngine;
using TheLastCompact.Core;

[RequireComponent(typeof(CharacterController))]
public class UniversalPlayer : MonoBehaviour
{
    private CharacterController _controller;
    private Camera _cam;
    private Vector3 _camOrigin;
    private float _xRot = 0f;

    [Header("移动参数")]
    public float moveSpeed = 5f;
    public float lookSensitivity = 2f;

    [Header("视觉反馈")]
    public float shakeAmount = 0.15f;

    [Header("交互设置")]
    [SerializeField] private float interactDistance = 4f;

    [Header("配置")]
    [Tooltip("拖入 GameBalance 资产")]
    [SerializeField] private GameBalanceConfig balanceConfig;

    private bool canControl = false;
    private InteractHighlight _currentHighlight;
    private int isInteracting = 0;

    void Awake() 
    {
        _controller = GetComponent<CharacterController>();
        _cam = GetComponentInChildren<Camera>();
    }

    void Start()
    {
        if (_cam != null) _camOrigin = _cam.transform.localPosition;
        
        // 初始锁定鼠标 (防止一开始就能动)
        // 注意：GlobalUIManager.InitializeSystemState 会重置这个，但为了保险起见
        // 如果是直接在 Wakeup 场景，Start 时还没触发 OnGameStarted
        // 如果是 Corridor 场景，AutoStartGame 会稍后触发 OnGameStarted
        
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnGameStarted += EnableControl;
            
            // 关键修复：如果游戏已经开始了（比如从 Wakeup 切到 Corridor），直接启用控制
            if (GlobalUIManager.Instance.isGameplayActive)
            {
                EnableControl();
                CursorService.Lock();
            }
        }
        else
        {
            // 如果没有全局管理器（非常规测试），直接允许控制
            canControl = true;
            Debug.LogWarning("GlobalUIManager missing in Player.Start. Defaulting to controllable.");
            CursorService.Lock();
        }

        if (GlobalMentalState.Instance == null) {
            // 使用新版 API (Unity 2023+)
            // var foundState = Object.FindFirstObjectByType<GlobalMentalState>(); // 旧版 Unity 可能无此 API，保留原样
            var foundState = FindAnyObjectByType<GlobalMentalState>();
            if (foundState != null)
            {
                Debug.LogWarning("注意：GlobalMentalState 实例未设置，但在场景中找到了对象。可能是初始化顺序问题。");
            }
            else
            {
                Debug.LogError("严重错误：场景中完全找不到 GlobalMentalState！请确保从 Bootstrap 或 MainMenu 启动游戏。");
            }
        }
    }

    private void OnDestroy()
    {
        if (GlobalUIManager.Instance != null)
        {
            GlobalUIManager.Instance.OnGameStarted -= EnableControl;
        }
    }

    public void EnableControl()
    {
        canControl = true;
        Debug.Log("[Player] Control Enabled.");
    }

    void Update()
    {
        // 如果无法控制，或者是暂停状态，都不动
        if (!canControl) return;
        if (GlobalUIManager.Instance != null && GlobalUIManager.Instance.isPaused) return;

        // 所有数据读取自全局单例，保证跨场景流畅性
        if (GlobalMentalState.Instance == null) return;
        if (GlobalMentalState.Instance.Model == null) return;

        HandleLook();
        HandleMove();
        HandleInteraction(); // 每帧射线检测 + 高亮 + Q键交互
        HandleVisuals();
    }

    void HandleMove()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = transform.right * h + transform.forward * v;

        // 核心逻辑：从全局模型读取崩坏强度，动态改变移速
        float intensity = GlobalMentalState.Instance.Model.GlitchIntensity;
        float minSpeed = balanceConfig != null ? balanceConfig.minSpeedRatio : 0.35f;
        float speedMod = Mathf.Lerp(1f, minSpeed, intensity);
        
        _controller.SimpleMove(dir * moveSpeed * speedMod);
    }

    void HandleInteraction()
    {
        // --- 每帧射线检测：高亮 + 交互 ---
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            InteractHighlight highlight = hit.collider.GetComponent<InteractHighlight>();

            // 1. 高亮管理
            if (highlight != null && highlight != _currentHighlight)
            {
                // 切换高亮目标：关闭旧的，开启新的
                if (_currentHighlight != null) _currentHighlight.DisableHighlight();
                _currentHighlight = highlight;
                _currentHighlight.EnableHighlight();
            }
            else if (highlight == null && _currentHighlight != null)
            {
                // 射线打到了非可交互物体，关闭高亮
                _currentHighlight.DisableHighlight();
                _currentHighlight = null;
            }

            // 2. Q 键交互
            if (interactable != null && Input.GetKeyDown(KeyCode.Q))
            {
                isInteracting++;
                Debug.Log("isInteracting: " + isInteracting);
                Debug.Log($"<color=green>Player:</color> 交互目标: {hit.collider.name}");
                interactable.Interact();
            }
        }
        else
        {
            // 射线未命中任何物体，关闭高亮
            if (_currentHighlight != null)
            {
                _currentHighlight.DisableHighlight();
                _currentHighlight = null;
            }
        }

        // --- 鼠标控制 ---
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CursorService.Unlock();
        }
        else if (Input.GetMouseButtonDown(0) && !CursorService.IsLocked)
        {
            CursorService.Lock();
        }
    }

    void HandleVisuals()
    {
        float intensity = GlobalMentalState.Instance.Model.GlitchIntensity;
        float shakeThresh = balanceConfig != null ? balanceConfig.shakeStartThreshold : 0.4f;
        
        // 随精神熵增产生的视角摇晃反馈
        if (intensity > shakeThresh) 
        {
            float shakeIn = (intensity - shakeThresh) / (1f - shakeThresh);
            _cam.transform.localPosition = _camOrigin + Random.insideUnitSphere * shakeAmount * shakeIn;
        }
        else
        {
            _cam.transform.localPosition = _camOrigin;
        }
    }
    
    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
        _xRot -= mouseY;
        _xRot = Mathf.Clamp(_xRot, -90f, 90f);

        if (_cam != null)
            _cam.transform.localRotation = Quaternion.Euler(_xRot, 0f, 0f);
            
        transform.Rotate(Vector3.up * mouseX);
    }
}