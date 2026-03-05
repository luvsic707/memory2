using UnityEngine;

/// <summary>
/// 交互高亮组件
/// 挂在任何 IInteractable 物体上，当玩家准星对准时产生边缘发光效果
/// 使用 MaterialPropertyBlock 实现，不会创建新材质实例
/// </summary>
public class InteractHighlight : MonoBehaviour
{
    [Header("高亮设置")]
    [Tooltip("高亮颜色")]
    public Color highlightColor = new Color(0.6f, 0.85f, 1f, 1f); // 冷蓝色微光

    [Tooltip("发光强度")]
    [Range(0f, 3f)]
    public float glowIntensity = 1.2f;

    [Tooltip("高亮淡入淡出速度")]
    public float fadeSpeed = 8f;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private float _currentIntensity = 0f;
    private bool _isHighlighted = false;

    // Shader 属性 ID 缓存
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionKeyword = Shader.PropertyToID("_EMISSION");

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 启用高亮
    /// </summary>
    public void EnableHighlight()
    {
        _isHighlighted = true;
    }

    /// <summary>
    /// 关闭高亮
    /// </summary>
    public void DisableHighlight()
    {
        _isHighlighted = false;
    }

    void Update()
    {
        // 平滑过渡
        float target = _isHighlighted ? glowIntensity : 0f;
        _currentIntensity = Mathf.Lerp(_currentIntensity, target, Time.deltaTime * fadeSpeed);

        // 性能优化：接近 0 时直接归零
        if (!_isHighlighted && _currentIntensity < 0.01f)
        {
            _currentIntensity = 0f;
        }

        // 应用到所有 Renderer
        if (_renderers == null) return;

        Color emissionColor = highlightColor * _currentIntensity;

        foreach (var rend in _renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(_propBlock);

            if (_currentIntensity > 0.01f)
            {
                // 开启自发光
                _propBlock.SetColor(EmissionColorProp, emissionColor);
                rend.SetPropertyBlock(_propBlock);

                // 确保 emission keyword 开启
                foreach (var mat in rend.materials)
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
            else
            {
                // 关闭自发光
                _propBlock.SetColor(EmissionColorProp, Color.black);
                rend.SetPropertyBlock(_propBlock);
            }
        }
    }
}
