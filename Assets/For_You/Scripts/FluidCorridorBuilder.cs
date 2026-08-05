using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 3D 几何包裹隧道构建器 (Brandon Eversole 真实 3D Corridor 实验)
    /// 生成由 5 面墙（前尽头墙 + 上下左右 4 面流体墙）组成的真实 3D 图像隧道，
    /// 彻底干掉散落方块卡片，让玩家置身于图 3 那种由图像向外拖拽拉伸流动的沉浸式通道中。
    /// </summary>
    public class FluidCorridorBuilder : MonoBehaviour
    {
        [Header("媒体数据库")]
        public CardMediaDatabase mediaDatabase;

        [Header("隧道几何尺寸")]
        public float tunnelWidth = 6f;
        public float tunnelHeight = 6f;
        public float tunnelDepth = 15f;

        [Header("流动控制")]
        public float imageSwitchInterval = 2.5f;

        private Transform _player;
        private GameObject _frontWall;
        private Renderer _frontRenderer;
        private Renderer[] _wallRenderers = new Renderer[4]; // Left, Right, Top, Bottom

        private Material _frontMat;
        private Material _wallMat;

        private Texture2D _currentTex;
        private Texture2D _nextTex;
        private float _switchTimer = 0f;
        private float _blendTimer = 0f;
        private bool _isBlending = false;

        private void Start()
        {
            _player = Camera.main != null ? Camera.main.transform : null;

            // 1. 禁用原有的 ContentCardSpawner（不再生成散落方块）
            ContentCardSpawner spawner = FindObjectOfType<ContentCardSpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
                Debug.Log("[FluidCorridorBuilder] 已自动禁用 ContentCardSpawner 散落卡片生成。");
            }

            // 2. 搭建 3D 走廊几何框架
            Build3DCorridor();

            // 3. 初始加载图像
            PickNextTexture();
            _currentTex = _nextTex;
            PickNextTexture();
            ApplyTexturesToWalls();
        }

        private void Build3DCorridor()
        {
            GameObject container = new GameObject("3D_FluidCorridorContainer");
            container.transform.SetParent(transform, false);

            Shader wallShader = Shader.Find("Wakeup/CorridorWallShader");
            if (wallShader == null) wallShader = Shader.Find("Universal Render Pipeline/Unlit");
            _wallMat = new Material(wallShader);

            Shader frontShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (frontShader == null) frontShader = Shader.Find("Unlit/Texture");
            _frontMat = new Material(frontShader);

            // A. 前尽头墙 (Front Wall)
            _frontWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _frontWall.name = "FrontEndWall";
            _frontWall.transform.SetParent(container.transform, false);
            _frontWall.transform.localPosition = new Vector3(0f, 0f, tunnelDepth);
            _frontWall.transform.localScale = new Vector3(tunnelWidth, tunnelHeight, 1f);
            _frontRenderer = _frontWall.GetComponent<Renderer>();
            _frontRenderer.material = _frontMat;

            // 保持 Collider 开启，支持注视进度推进

            // B. 左墙 (Left Wall)
            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Quad);
            left.name = "LeftWall";
            left.transform.SetParent(container.transform, false);
            left.transform.localPosition = new Vector3(-tunnelWidth * 0.5f, 0f, tunnelDepth * 0.5f);
            left.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            left.transform.localScale = new Vector3(tunnelDepth, tunnelHeight, 1f);
            _wallRenderers[0] = left.GetComponent<Renderer>();
            _wallRenderers[0].material = _wallMat;

            // C. 右墙 (Right Wall)
            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Quad);
            right.name = "RightWall";
            right.transform.SetParent(container.transform, false);
            right.transform.localPosition = new Vector3(tunnelWidth * 0.5f, 0f, tunnelDepth * 0.5f);
            right.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            right.transform.localScale = new Vector3(tunnelDepth, tunnelHeight, 1f);
            _wallRenderers[1] = right.GetComponent<Renderer>();
            _wallRenderers[1].material = _wallMat;

            // D. 天花板 (Top Wall)
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Quad);
            top.name = "TopWall";
            top.transform.SetParent(container.transform, false);
            top.transform.localPosition = new Vector3(0f, tunnelHeight * 0.5f, tunnelDepth * 0.5f);
            top.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            top.transform.localScale = new Vector3(tunnelWidth, tunnelDepth, 1f);
            _wallRenderers[2] = top.GetComponent<Renderer>();
            _wallRenderers[2].material = _wallMat;

            // E. 地板 (Bottom Wall)
            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bottom.name = "BottomWall";
            bottom.transform.SetParent(container.transform, false);
            bottom.transform.localPosition = new Vector3(0f, -tunnelHeight * 0.5f, tunnelDepth * 0.5f);
            bottom.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            bottom.transform.localScale = new Vector3(tunnelWidth, tunnelDepth, 1f);
            _wallRenderers[3] = bottom.GetComponent<Renderer>();
            _wallRenderers[3].material = _wallMat;
        }

        private void Update()
        {
            if (_player != null)
            {
                // 3D 通道始终围绕玩家位置与视角对齐
                transform.position = _player.position;
                transform.rotation = Quaternion.LookRotation(_player.forward);
            }

            float phaseProgress = Stage5Controller.Instance != null
                ? Stage5Controller.Instance.phaseProgress
                : 0f;

            // 0. 注视尽头画面推进 Phase 进度
            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, tunnelDepth + 5f))
                {
                    if (hit.transform == _frontWall.transform && Stage5Controller.Instance != null)
                    {
                        Stage5Controller.Instance.phaseProgress += 0.005f * Time.deltaTime * 1.5f;
                        Stage5Controller.Instance.phaseProgress = Mathf.Clamp01(Stage5Controller.Instance.phaseProgress);
                    }
                }
            }

            // 1. 定时交替图像
            _switchTimer += Time.deltaTime;
            if (_switchTimer >= imageSwitchInterval)
            {
                _switchTimer = 0f;
                _currentTex = _nextTex;
                PickNextTexture();
                ApplyTexturesToWalls();
            }

            // 2. 动态调节墙体流体流动与 Phase 状态
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
