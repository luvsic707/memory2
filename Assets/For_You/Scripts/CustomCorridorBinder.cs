using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 丰富版 3D 走廊流体绑定器 (Brandon Eversole & Flying Lotus 风格)
    /// 驱动 Front Wall 在多重动画模式（Slit-scan 扫过、双重爆裂、残影融合）之间随机交替，
    /// 驱动四周 4 面 Side Walls 产生 RGB 色差拖尾、水波纹扭曲与脉动呼吸。
    /// </summary>
    public class CustomCorridorBinder : MonoBehaviour
    {
        [Header("媒体数据库")]
        public CardMediaDatabase mediaDatabase;

        [Header("手动搭建的走廊墙体")]
        [Tooltip("走廊尽头的正面墙/Quad（播放交替图像）")]
        public Renderer frontWallRenderer;

        [Tooltip("走廊四周的 4 面墙体 Cube（左、右、天花板、地面）")]
        public Renderer[] sideWallRenderers;

        [Header("动态与动画参数")]
        public float imageSwitchInterval = 2.2f;
        public float transitionDuration = 0.85f;

        private Material _frontMat;
        private Material _wallMat;

        private Texture2D _currentTex;
        private Texture2D _nextTex;
        
        private float _switchTimer = 0f;
        private float _transTimer = 0f;
        private bool _isTransitioning = false;
        private int _currentModeIndex = 0; // 0: SlitScan, 1: Burst, 2: Feedback

        private void Start()
        {
            ContentCardSpawner spawner = FindObjectOfType<ContentCardSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
                Debug.Log("[CustomCorridorBinder] 已自动禁用散落卡片生成器，全面使用高级 3D 走廊！");
            }

            Shader frontShader = Shader.Find("Wakeup/CorridorFrontShader");
            if (frontShader == null) frontShader = Shader.Find("Universal Render Pipeline/Unlit");
            _frontMat = new Material(frontShader);

            Shader wallShader = Shader.Find("Wakeup/CorridorWallShader");
            if (wallShader == null) wallShader = Shader.Find("Universal Render Pipeline/Unlit");
            _wallMat = new Material(wallShader);

            if (frontWallRenderer != null) frontWallRenderer.material = _frontMat;

            if (sideWallRenderers != null)
            {
                foreach (var r in sideWallRenderers)
                {
                    if (r != null) r.material = _wallMat;
                }
            }

            PickNextTexture();
            _currentTex = _nextTex;
            PickNextTexture();
            ApplyTexturesToWalls();
        }

        private void Update()
        {
            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            // 1. 注视尽头墙面推进 Phase 进度
            Camera cam = Camera.main;
            if (cam != null && frontWallRenderer != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 50f))
                {
                    if (hit.transform == frontWallRenderer.transform && Stage5Controller.Instance != null)
                    {
                        Stage5Controller.Instance.phaseProgress += Stage5Controller.Instance.progressPerSecond * Time.deltaTime * 1.5f;
                        Stage5Controller.Instance.phaseProgress = Mathf.Clamp01(Stage5Controller.Instance.phaseProgress);
                    }
                }
            }

            // 2. 定时触发图像多模式过渡
            _switchTimer += Time.deltaTime;
            if (_switchTimer >= imageSwitchInterval)
            {
                _switchTimer = 0f;
                _isTransitioning = true;
                _transTimer = 0f;

                // 随机轮换 3 种画幅融合过渡模式
                _currentModeIndex = Random.Range(0, 3);
                _currentTex = _nextTex;
                PickNextTexture();
                ApplyTexturesToWalls();
            }

            // 3. 驱动 Front Wall 图像过渡动画与像素 Glitch
            if (_frontMat != null)
            {
                float progress = 0f;
                if (_isTransitioning)
                {
                    _transTimer += Time.deltaTime;
                    progress = Mathf.Clamp01(_transTimer / transitionDuration);
                    if (_transTimer >= transitionDuration)
                    {
                        _isTransitioning = false;
                    }
                }

                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f : 0f;
                _frontMat.SetFloat("_TransitionProgress", progress);
                _frontMat.SetFloat("_TransitionMode", (float)_currentModeIndex);
                _frontMat.SetFloat("_GlitchIntensity", glitch);
            }

            // 4. 驱动 Side Walls 四周墙面的丰富流体动态
            if (_wallMat != null)
            {
                float speed = 1.0f + phaseProgress * 2.2f;
                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.85f : 0f;
                float rgbShift = 0.015f + phaseProgress * 0.03f;
                float waveWarp = 0.3f + phaseProgress * 1.2f;

                _wallMat.SetFloat("_FlowSpeed", speed);
                _wallMat.SetFloat("_GlitchAmount", glitch);
                _wallMat.SetFloat("_RGBShift", rgbShift);
                _wallMat.SetFloat("_WaveWarp", waveWarp);
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

        private void ApplyTexturesToWalls()
        {
            if (_currentTex == null) return;

            if (_frontMat != null)
            {
                if (_frontMat.HasProperty("_MainTex")) _frontMat.SetTexture("_MainTex", _currentTex);
                if (_frontMat.HasProperty("_NextTex")) _frontMat.SetTexture("_NextTex", _nextTex);
            }

            if (_wallMat != null)
            {
                if (_wallMat.HasProperty("_MainTex")) _wallMat.SetTexture("_MainTex", _currentTex);
                if (_wallMat.HasProperty("_BaseMap")) _wallMat.SetTexture("_BaseMap", _currentTex);
                _wallMat.mainTexture = _currentTex;
            }
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
