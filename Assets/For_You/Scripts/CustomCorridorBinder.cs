using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 手工 3D 走廊流体绑定器 (Custom 3D Corridor Binder)
    /// 将玩家在 Unity 编辑器场景中手动搭建的 4 面墙 Cube 与 1 面尽头 Quad，
    /// 自动绑定上 CorridorWallShader 和 CardMediaDatabase 的动态流体图像！
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

        [Header("流动切换参数")]
        public float imageSwitchInterval = 2.5f;

        private Material _frontMat;
        private Material _wallMat;

        private Texture2D _currentTex;
        private Texture2D _nextTex;
        private float _switchTimer = 0f;

        private void Start()
        {
            // 自动禁用 ContentCardSpawner 散落卡片生成
            ContentCardSpawner spawner = FindObjectOfType<ContentCardSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
                Debug.Log("[CustomCorridorBinder] 已自动禁用散落卡片生成器，全面使用手动 3D 走廊！");
            }

            // 初始化材质
            Shader wallShader = Shader.Find("Wakeup/CorridorWallShader");
            if (wallShader == null) wallShader = Shader.Find("Universal Render Pipeline/Unlit");
            _wallMat = new Material(wallShader);

            Shader frontShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (frontShader == null) frontShader = Shader.Find("Unlit/Texture");
            _frontMat = new Material(frontShader);

            // 应用材质到手工搭建的墙体
            if (frontWallRenderer != null) frontWallRenderer.material = _frontMat;

            if (sideWallRenderers != null)
            {
                foreach (var r in sideWallRenderers)
                {
                    if (r != null) r.material = _wallMat;
                }
            }

            // 加载初始图像
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

            // 2. 定时交替图像
            _switchTimer += Time.deltaTime;
            if (_switchTimer >= imageSwitchInterval)
            {
                _switchTimer = 0f;
                _currentTex = _nextTex;
                PickNextTexture();
                ApplyTexturesToWalls();
            }

            // 3. 动态驱动墙面流体与 Glitch 强度
            if (_wallMat != null)
            {
                float speed = 1.0f + phaseProgress * 2.0f;
                float glitch = phaseProgress > 0.35f ? Mathf.InverseLerp(0.35f, 0.96f, phaseProgress) * 0.8f : 0f;
                _wallMat.SetFloat("_FlowSpeed", speed);
                _wallMat.SetFloat("_GlitchAmount", glitch);
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
                if (_frontMat.HasProperty("_BaseMap")) _frontMat.SetTexture("_BaseMap", _currentTex);
                _frontMat.mainTexture = _currentTex;
            }

            if (_wallMat != null)
            {
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
