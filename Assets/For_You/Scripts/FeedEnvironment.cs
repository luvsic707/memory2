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

        private void Start()
        {
            _player = Camera.main != null ? Camera.main.transform : null;
            _mpb = new MaterialPropertyBlock();

            CreateStarParticles();
            CreateTunnel();
        }

        private void Update()
        {
            if (_player == null) return;

            UpdateTunnel();
            UpdateStars();
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

        /// <summary>
        /// 创建隧道壁圆柱（初始完全透明）
        /// </summary>
        private void CreateTunnel()
        {
            _tunnelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _tunnelGo.name = "FeedTunnel";
            _tunnelGo.transform.SetParent(transform, false);

            // 移除碰撞体
            Collider col = _tunnelGo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 设置透明材质
            _tunnelRenderer = _tunnelGo.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", 1); // Front cull: 从内部看
            mat.renderQueue = 2900;
            _tunnelRenderer.material = mat;

            // 初始完全透明
            _tunnelRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", new Color(0.1f, 0.15f, 0.3f, 0f));
            _tunnelRenderer.SetPropertyBlock(_mpb);
        }

        private void UpdateTunnel()
        {
            if (_tunnelGo == null || _player == null) return;

            // 隧道跟随玩家且面朝前方
            _tunnelGo.transform.position = _player.position + _player.forward * (tunnelLength * 0.4f);
            _tunnelGo.transform.rotation = Quaternion.LookRotation(_player.forward) * Quaternion.Euler(90f, 0f, 0f);

            // Phase A (0~0.35): 隧道不可见
            // Phase B (0.35~0.70): 隧道渐显，半径从大缩小
            // Phase C (0.70~1.0): 隧道最窄
            float tunnelVisibility = 0f;
            float radius = maxTunnelRadius;

            if (phaseProgress > 0.35f)
            {
                float t = Mathf.InverseLerp(0.35f, 1.0f, phaseProgress);
                tunnelVisibility = Mathf.Lerp(0f, maxTunnelAlpha, t);
                radius = Mathf.Lerp(maxTunnelRadius, minTunnelRadius, t);
            }

            _tunnelGo.transform.localScale = new Vector3(radius, tunnelLength * 0.5f, radius);

            // 颜色：深蓝到深紫渐变
            float hue = Mathf.Lerp(0.62f, 0.72f, phaseProgress);
            Color tunnelColor = Color.HSVToRGB(hue, 0.4f, 0.2f);
            tunnelColor.a = tunnelVisibility;

            _tunnelRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", tunnelColor);
            _tunnelRenderer.SetPropertyBlock(_mpb);
        }

        private void UpdateStars()
        {
            if (_starParticles == null) return;

            // Phase A: 星点明亮。Phase B→C: 星点逐渐消失（被隧道取代）
            var main = _starParticles.main;
            float starAlpha = Mathf.Lerp(0.5f, 0f, Mathf.InverseLerp(0.3f, 0.6f, phaseProgress));
            main.startColor = new Color(0.8f, 0.85f, 1f, starAlpha);

            // 跟随玩家
            _starParticles.transform.position = _player.position;
        }
    }
}
