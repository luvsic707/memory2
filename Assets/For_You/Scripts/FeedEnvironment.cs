using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 空间变形控制器。
    /// 用 phaseProgress 驱动环境从无垠虚空 → 半透明隧道 → 极窄管道。
    /// 运行时动态生成隧道壁和粒子系统。
    /// </summary>
    public class FeedEnvironment : MonoBehaviour
    {
        [Header("隧道参数")]
        [Tooltip("隧道圆柱的最大半径（Phase A 不可见）")]
        public float maxTunnelRadius = 12f;

        [Tooltip("隧道圆柱的最小半径（Phase C 极窄）")]
        public float minTunnelRadius = 3f;

        [Tooltip("隧道长度")]
        public float tunnelLength = 80f;

        [Tooltip("隧道壁透明度上限")]
        public float maxTunnelAlpha = 0.15f;

        [Header("星点粒子")]
        [Tooltip("Phase A 远处微弱星点数量")]
        public int starCount = 200;

        // 由 Stage5Controller 设置
        [HideInInspector] public float phaseProgress = 0f;

        private Transform _player;
        private GameObject _tunnelGo;
        private Renderer _tunnelRenderer;
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _starParticles;

        private Transform GetPlayerTransform()
        {
            if (_player != null) return _player;
            if (Camera.main != null) _player = Camera.main.transform;
            if (_player == null)
            {
#if UNITY_2023_1_OR_NEWER
                UniversalPlayer p = FindAnyObjectByType<UniversalPlayer>();
#else
                UniversalPlayer p = FindObjectOfType<UniversalPlayer>();
#endif
                if (p != null) _player = p.transform;
            }
            return _player;
        }

        private void Start()
        {
            _player = GetPlayerTransform();
            _mpb = new MaterialPropertyBlock();

            CreateStarParticles();
            CreateTunnel();
        }

        private void Update()
        {
            Transform pTransform = GetPlayerTransform();
            if (pTransform == null) return;

            UpdateTunnel();
            UpdateStars();
        }

        private Shader FindTunnelShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("URP/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        /// <summary>
        /// 创建远处微弱星点粒子（Phase A 的无垠虚空感）
        /// </summary>
        private void CreateStarParticles()
        {
            GameObject starGo = new GameObject("StarParticles");
            starGo.transform.SetParent(transform, false);

            _starParticles = starGo.AddComponent<ParticleSystem>();

            var main = _starParticles.main;
            main.maxParticles = starCount;
            main.startLifetime = 20f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startColor = new Color(0.8f, 0.85f, 1f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = true;

            var shape = _starParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 40f;

            var emission = _starParticles.emission;
            emission.rateOverTime = starCount / 20f;

            // 微弱闪烁
            var colorOverLifetime = _starParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            // 禁用默认渲染器的阴影
            ParticleSystemRenderer psRenderer = starGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
        }

        private void UpdateStars()
        {
            if (_starParticles == null) return;
            Transform pTransform = GetPlayerTransform();
            if (pTransform == null) return;

            // Phase A: 星点明亮。Phase B→C: 星点逐渐消失（被隧道取代）
            var main = _starParticles.main;
            float starAlpha = Mathf.Lerp(0.5f, 0f, Mathf.InverseLerp(0.3f, 0.6f, phaseProgress));
            main.startColor = new Color(0.8f, 0.85f, 1f, starAlpha);

            // 跟随玩家
            _starParticles.transform.position = pTransform.position;
        }

        /// <summary>
        /// 创建隧道壁圆柱（初始完全透明）
        /// </summary>
        private void CreateTunnel()
        {
            // 彻底禁用遮挡视野的巨型 FeedTunnel 圆柱遮挡体，保持镜头通透高可读性
            if (_tunnelGo != null) _tunnelGo.SetActive(false);
        }

        private void UpdateTunnel()
        {
            Transform pTransform = GetPlayerTransform();
            if (_tunnelGo == null || pTransform == null) return;

            // 隧道跟随玩家且面朝前方
            _tunnelGo.transform.position = pTransform.position + pTransform.forward * (tunnelLength * 0.4f);
            _tunnelGo.transform.rotation = Quaternion.LookRotation(pTransform.forward) * Quaternion.Euler(90f, 0f, 0f);

            // Phase A (0~0.35): 虚空感，带有极其微弱的星流
            // Phase B (0.35~0.70): 隧道渐显，有机的流动脉冲
            // Phase C (0.70~1.0): 极窄收拢
            float tunnelVisibility = 0.04f; // 保持微弱的虚空氛围感
            float radius = maxTunnelRadius;

            if (phaseProgress > 0.35f)
            {
                float t = Mathf.InverseLerp(0.35f, 1.0f, phaseProgress);
                tunnelVisibility = Mathf.Lerp(0.04f, maxTunnelAlpha, t);
                radius = Mathf.Lerp(maxTunnelRadius, minTunnelRadius, t);
            }

            // 添加有机的微弱呼吸/波纹动效（像流体隧道在蠕动/流动）
            float organicPulse = Mathf.Sin(Time.time * 1.8f) * 0.15f * radius;
            _tunnelGo.transform.localScale = new Vector3(radius + organicPulse, tunnelLength * 0.5f, radius - organicPulse);

            // 色彩随时间有机的缓慢漂移（从洋红到电青色）
            float dynamicHue = (Mathf.Sin(Time.time * 0.5f) * 0.08f + Mathf.Lerp(0.60f, 0.75f, phaseProgress)) % 1.0f;
            Color tunnelColor = Color.HSVToRGB(dynamicHue, 0.5f, 0.25f);
            tunnelColor.a = tunnelVisibility;

            _tunnelRenderer.GetPropertyBlock(_mpb);
            _tunnelRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", tunnelColor);
            _mpb.SetColor("_Color", tunnelColor);
            _tunnelRenderer.SetPropertyBlock(_mpb);
        }
    }
}
