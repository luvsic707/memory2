using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// View 层: 驱动 3D 模型材质
    /// 增强版：同步所有动态参数（包括噪声缩放和抖动速度）
    /// </summary>
    public class CausalView : MonoBehaviour
    {
        [Header("Target Rendering")]
        [SerializeField] private Renderer targetModelRenderer;
        [SerializeField] private CausalController controller;

        [Header("Visual Mapping (Read Only)")]
        [Range(0, 1)] public float glitchIntensity;

        // --- Shader 属性 ID 缓存 ---
        private static readonly int DistortionProp = Shader.PropertyToID("_Distortion");
        private static readonly int ErosionProp = Shader.PropertyToID("_Erosion");
        private static readonly int EntropyColorProp = Shader.PropertyToID("_EntropyColor");
        private static readonly int NoiseScaleProp = Shader.PropertyToID("_NoiseScale");
        private static readonly int SpeedProp = Shader.PropertyToID("_DistortionSpeed");

        private MaterialPropertyBlock _propBlock;
        private bool _isSubscribed = false;

        void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
        }

        void Start()
        {
            TrySubscribe();
        }

        void OnEnable()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_isSubscribed) return;

            if (controller != null && controller.Model != null)
            {
                controller.Model.OnStateChanged += SyncWithModel;
                _isSubscribed = true;
                SyncWithModel();
                Debug.Log("<color=green>CausalView: 动态参数链路已激活！</color>");
            }
        }

        private void SyncWithModel()
        {
            if (controller == null || controller.Model == null) return;

            var model = controller.Model;
            glitchIntensity = model.GlitchIntensity;

            if (targetModelRenderer != null)
            {
                // 读取当前属性块
                targetModelRenderer.GetPropertyBlock(_propBlock);

                // 1. 核心形变与溶解 (映射 0.11 的限制可以在 Model 层调，也可以这里直接乘系数)
                // 如果觉得扭曲太丑，这里可以乘一个 0.2
                _propBlock.SetFloat(DistortionProp, glitchIntensity * 0.5f);
                _propBlock.SetFloat(ErosionProp, glitchIntensity);

                // 2. 动态同步 Noise Scale 和 Speed (让腐烂变细碎的关键)
                _propBlock.SetFloat(NoiseScaleProp, model.NoiseScale);
                _propBlock.SetFloat(SpeedProp, model.DistortionSpeed);

                // 3. 颜色偏移
                Color entropyCol = Color.Lerp(Color.white, new Color(0.4f, 0.1f, 0.5f), glitchIntensity);
                _propBlock.SetColor(EntropyColorProp, entropyCol);

                // 应用属性块
                targetModelRenderer.SetPropertyBlock(_propBlock);
            }
        }

        void OnDisable()
        {
            if (_isSubscribed && controller != null && controller.Model != null)
            {
                controller.Model.OnStateChanged -= SyncWithModel;
                _isSubscribed = false;
            }
        }
    }
}