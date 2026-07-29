using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 流体无限穿梭渲染器 (Brandon Eversole 风格实验)
    /// 在玩家视野正前方生成全屏/巨型视口流体 Quad，
    /// 不断融合 CardMediaDatabase 里的图像，并驱动 FluidTunnelShader 实现 Slit-scan 径向拉伸与流体无限沉入动效。
    /// </summary>
    public class FluidTunnelRenderer : MonoBehaviour
    {
        [Header("媒体数据库")]
        public CardMediaDatabase mediaDatabase;

        [Header("图像切换参数")]
        [Tooltip("每隔多少秒平滑融入下一张图片")]
        public float imageSwitchInterval = 2.5f;
        
        [Tooltip("图片融合过渡时长（秒）")]
        public float blendDuration = 0.8f;

        [Header("流体视觉参数")]
        public float baseTunnelSpeed = 1.0f;
        public float baseStretchIntensity = 1.8f;

        private Renderer _quadRenderer;
        private Material _fluidMaterial;
        private Transform _player;

        private Texture2D _currentTex;
        private Texture2D _nextTex;

        private float _switchTimer = 0f;
        private float _blendTimer = 0f;
        private bool _isBlending = false;

        private void Start()
        {
            _player = Camera.main != null ? Camera.main.transform : null;

            // 创建挂载在玩家正前方的全屏/巨型流体视口 Quad
            CreateFluidViewport();

            // 初始加载第一张图片
            PickNextTexture();
            _currentTex = _nextTex;
            PickNextTexture();
            UpdateMaterialTextures();
        }

        private void CreateFluidViewport()
        {
            GameObject quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "FluidViewportQuad";
            quadGo.transform.SetParent(transform, false);
            
            // 放置在玩家正前方 5 米处，缩放盖满视野
            quadGo.transform.localPosition = new Vector3(0f, 0f, 5f);
            quadGo.transform.localScale = new Vector3(12f, 8f, 1f);

            Collider col = quadGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            _quadRenderer = quadGo.GetComponent<Renderer>();

            Shader shader = Shader.Find("Wakeup/FluidTunnelShader");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            _fluidMaterial = new Material(shader);
            _quadRenderer.material = _fluidMaterial;
        }

        private void Update()
        {
            if (_player != null)
            {
                // 保持全屏视口始终跟随对齐玩家视角
                transform.position = _player.position;
                transform.rotation = _player.rotation;
            }

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            // 1. 定时触发图像融合
            _switchTimer += Time.deltaTime;
            if (_switchTimer >= imageSwitchInterval)
            {
                _switchTimer = 0f;
                _isBlending = true;
                _blendTimer = 0f;
                _currentTex = _nextTex;
                PickNextTexture();
                UpdateMaterialTextures();
            }

            // 2. 更新图像 Blend 进度
            float blendVal = 0f;
            if (_isBlending)
            {
                _blendTimer += Time.deltaTime;
                blendVal = Mathf.Clamp01(_blendTimer / blendDuration);
                if (_blendTimer >= blendDuration)
                {
                    _isBlending = false;
                }
            }

            // 3. 将 Phase 进度与材质参数实时同步
            if (_fluidMaterial != null)
            {
                _fluidMaterial.SetFloat("_BlendProgress", blendVal);
                _fluidMaterial.SetFloat("_PhaseProgress", phaseProgress);

                // Phase 1 (0~0.35): 清晰灵动的流体无限 Zoom
                // Phase 2 (0.35~0.70): 加入 Slit-scan 漩涡折叠与轻度 Glitch
                // Phase 3 (0.70~1.0): 强 Glitch 像素腐蚀
                float speed = baseTunnelSpeed + phaseProgress * 1.5f;
                float stretch = baseStretchIntensity + phaseProgress * 1.0f;
                float vortex = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.70f, phaseProgress) * 1.5f : 0f;
                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f : 0f;

                _fluidMaterial.SetFloat("_TunnelSpeed", speed);
                _fluidMaterial.SetFloat("_StretchIntensity", stretch);
                _fluidMaterial.SetFloat("_VortexDistortion", vortex);
                _fluidMaterial.SetFloat("_GlitchIntensity", glitch);
            }
        }

        private void PickNextTexture()
        {
            if (mediaDatabase == null) return;

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            string theme = GetDominantTheme();

            if (phaseProgress < 0.35f)
            {
                _nextTex = mediaDatabase.GetEntertainmentTexture();
            }
            else if (phaseProgress < 0.70f)
            {
                _nextTex = Random.value < 0.5f
                    ? mediaDatabase.GetThemeTexture(theme)
                    : mediaDatabase.GetEntertainmentTexture();
            }
            else
            {
                _nextTex = mediaDatabase.GetThemeTexture(theme) ?? mediaDatabase.GetEntertainmentTexture();
            }
        }

        private void UpdateMaterialTextures()
        {
            if (_fluidMaterial == null) return;
            if (_currentTex != null) _fluidMaterial.SetTexture("_MainTex", _currentTex);
            if (_nextTex != null)    _fluidMaterial.SetTexture("_NextTex", _nextTex);
        }

        private string GetDominantTheme()
        {
            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes  = PlayerBehaviorData.Instance.pushCount;
                works   = PlayerBehaviorData.Instance.workCount;
            }
            if (bananas + prayers + pushes + works == 0) works = 45;

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
            if (max == bananas) return "banana";
            if (max == prayers) return "prayer";
            if (max == pushes)  return "push";
            return "work";
        }
    }
}
