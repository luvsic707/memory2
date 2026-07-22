using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 内容卡片生成器。根据三个旋钮参数持续生成 ContentCard。
    /// 旋钮1: spawnDirection (球→隧道→贴脸)
    /// 旋钮2: contentSource (随机→过滤→私密数据)
    /// 旋钮3: spawnRhythm (惊喜→机械→静默)
    /// 所有旋钮由外部 phaseProgress (0→1) 驱动。
    /// </summary>
    public class ContentCardSpawner : MonoBehaviour
    {
        [Header("生成参数")]
        [Tooltip("卡片生成的基础半径（距离玩家）")]
        public float spawnRadius = 20f;

        [Tooltip("最小生成半径")]
        public float minSpawnRadius = 12f;

        [Tooltip("卡片大小范围")]
        public Vector2 cardSizeRange = new Vector2(0.8f, 2.0f);

        [Tooltip("同时存在的最大卡片数")]
        public int maxCards = 60;

        [Header("Phase C 私密数据内容")]
        [Tooltip("Phase C 第一步：主题推送文案（根据数据自动生成）")]
        public string[] themeStrings_Banana = { "Organic Living Tips", "Wild Foraging Guide", "Primal Instinct Unlocked", "Return to Nature 🌿" };
        public string[] themeStrings_Prayer = { "Find Inner Peace", "Daily Meditation Guide", "Spiritual Awakening ✨", "Faith & Healing 🙏" };
        public string[] themeStrings_Push = { "Endurance Training Plan", "Sisyphus Workout 💪", "Embrace the Grind", "Repetition is Mastery" };
        public string[] themeStrings_Work = { "Productivity Hacks", "10x Your Output ⚡", "Office Wellness Tips", "Burnout Prevention Guide" };

        // Phase A 的随机缤纷文字池
        private readonly string[] _randomContent = {
            "✨", "🌈", "🎵", "💫", "🌸", "🎪", "🦋", "🍭",
            "dream big", "infinite scroll", "new for you", "trending now",
            "you might like this", "recommended", "just for you", "explore",
            "discover", "✿", "♡", "☆", "→", "∞", "◇", "△",
            "vibes", "aesthetic", "mood", "slay", "iconic", "manifesting",
            "healing energy", "self care", "growth mindset", "blessed",
            "ASMR", "satisfying", "oddly satisfying", "POV:", "story time",
        };

        // 内部状态
        private Transform _player;
        private float _spawnTimer = 0f;
        private float _currentInterval = 0.8f;
        private List<ContentCard> _activeCards = new List<ContentCard>();
        private int _phaseCDataIndex = 0; // Phase C 递进步骤计数
        private bool _hasShownBanana = false;
        private bool _hasShownPrivateData = false;

        // 由 Stage5Controller 设置
        [HideInInspector] public float phaseProgress = 0f;

        // 回调
        public System.Action<ContentCard> OnCardSpawned;

        private void Start()
        {
            _player = Camera.main != null ? Camera.main.transform : null;
        }

        private void Update()
        {
            if (_player == null) return;

            // 清理已销毁的卡片引用
            _activeCards.RemoveAll(c => c == null);

            // 计算当前节奏间隔
            _currentInterval = CalculateSpawnInterval();

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _currentInterval && _activeCards.Count < maxCards)
            {
                _spawnTimer = 0f;

                // Phase C 私密数据卡出现时的静默暂停
                if (phaseProgress > 0.85f && IsPrivateDataMoment())
                {
                    _spawnTimer = -2.0f; // 停 2 秒再继续
                }

                SpawnCard();
            }
        }

        private void SpawnCard()
        {
            // 1. 计算生成位置（旋钮1: 方向）
            Vector3 spawnPos = CalculateSpawnPosition();

            // 2. 创建 Quad
            GameObject cardGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cardGo.name = "FeedCard";
            cardGo.transform.position = spawnPos;

            // 随机大小
            float size = Random.Range(cardSizeRange.x, cardSizeRange.y);
            cardGo.transform.localScale = Vector3.one * size;

            // 移除碰撞体（我们用射线检测 Renderer bounds）
            Collider col = cardGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            // 添加一个用于射线检测的 BoxCollider
            BoxCollider box = cardGo.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // 3. 设置材质为 URP Unlit 透明
            Renderer rend = cardGo.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            rend.material = mat;

            // 4. 配置 ContentCard 组件（旋钮2: 内容）
            ContentCard card = cardGo.AddComponent<ContentCard>();
            ConfigureCardContent(card);

            // 5. 漂移速度微调
            card.driftSpeed = Mathf.Lerp(1.2f, 2.5f, phaseProgress);

            _activeCards.Add(card);
            OnCardSpawned?.Invoke(card);
        }

        /// <summary>
        /// 旋钮1: 生成方向。Phase A 球形 → Phase B 前方收窄 → Phase C 贴脸
        /// </summary>
        private Vector3 CalculateSpawnPosition()
        {
            float radius = Mathf.Lerp(spawnRadius, minSpawnRadius, phaseProgress);

            if (phaseProgress < 0.35f)
            {
                // Phase A: 全球面随机
                return _player.position + Random.onUnitSphere * radius;
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase B: 逐渐收窄到前方锥形
                float narrowT = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float maxAngle = Mathf.Lerp(180f, 45f, narrowT);

                Vector3 dir = RandomConeDirection(_player.forward, maxAngle);
                return _player.position + dir * radius;
            }
            else
            {
                // Phase C: 前方极窄锥 ±15°
                float narrowT = Mathf.InverseLerp(0.70f, 1.0f, phaseProgress);
                float maxAngle = Mathf.Lerp(45f, 12f, narrowT);

                Vector3 dir = RandomConeDirection(_player.forward, maxAngle);
                return _player.position + dir * radius;
            }
        }

        /// <summary>
        /// 旋钮3: 节奏。Phase A 随机惊喜 → Phase B 机械匀速 → Phase C 紧密+静默
        /// </summary>
        private float CalculateSpawnInterval()
        {
            if (phaseProgress < 0.35f)
            {
                // Phase A: 随机间隔，偶尔密集涌入
                if (Random.value < 0.15f)
                    return Random.Range(0.1f, 0.25f); // 惊喜密集波
                return Random.Range(0.4f, 1.5f);
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase B: 机械匀速
                return 0.6f;
            }
            else
            {
                // Phase C: 更紧密
                return 0.4f;
            }
        }

        /// <summary>
        /// 旋钮2: 内容来源。Phase A 随机缤纷 → Phase B 过滤偏好 → Phase C 私密数据
        /// </summary>
        private void ConfigureCardContent(ContentCard card)
        {
            if (phaseProgress < 0.35f)
            {
                // Phase A: 纯随机缤纷
                ConfigurePhaseA(card);
            }
            else if (phaseProgress < 0.70f)
            {
                // Phase B: 按数据过滤，但外表仍好看
                ConfigurePhaseB(card);
            }
            else
            {
                // Phase C: 私密数据递进
                ConfigurePhaseC(card);
            }
        }

        private void ConfigurePhaseA(ContentCard card)
        {
            // 高饱和度随机色
            float hue = Random.Range(0f, 1f);
            card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.95f), Random.Range(0.85f, 1f));
            card.cardColor = new Color(card.cardColor.r, card.cardColor.g, card.cardColor.b, 0.92f);

            // 随机文字
            card.cardText = _randomContent[Random.Range(0, _randomContent.Length)];
        }

        private void ConfigurePhaseB(ContentCard card)
        {
            // 读取 PlayerBehaviorData，收窄到偏好色系
            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes = PlayerBehaviorData.Instance.pushCount;
                works = PlayerBehaviorData.Instance.workCount;
            }

            // 如果全是 0（调试模式），用模拟数据
            if (bananas + prayers + pushes + works == 0)
            {
                bananas = 8; prayers = 15; pushes = 3; works = 45;
            }

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));

            // 色系收窄
            float narrowT = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);

            if (max == bananas)
            {
                // 黄色系
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.10f, 0.18f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1f));
                card.cardText = Random.value < narrowT
                    ? themeStrings_Banana[Random.Range(0, themeStrings_Banana.Length)]
                    : _randomContent[Random.Range(0, _randomContent.Length)];
            }
            else if (max == prayers)
            {
                // 青蓝色系
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.50f, 0.60f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.5f, 0.8f), Random.Range(0.85f, 1f));
                card.cardText = Random.value < narrowT
                    ? themeStrings_Prayer[Random.Range(0, themeStrings_Prayer.Length)]
                    : _randomContent[Random.Range(0, _randomContent.Length)];
            }
            else if (max == pushes)
            {
                // 橙色系
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.06f, 0.12f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1f));
                card.cardText = Random.value < narrowT
                    ? themeStrings_Push[Random.Range(0, themeStrings_Push.Length)]
                    : _randomContent[Random.Range(0, _randomContent.Length)];
            }
            else
            {
                // 红色系
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.97f, 1.03f) % 1f, narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.5f, 0.85f), Random.Range(0.85f, 1f));
                card.cardText = Random.value < narrowT
                    ? themeStrings_Work[Random.Range(0, themeStrings_Work.Length)]
                    : _randomContent[Random.Range(0, _randomContent.Length)];
            }

            card.cardColor = new Color(card.cardColor.r, card.cardColor.g, card.cardColor.b, 0.92f);
        }

        private void ConfigurePhaseC(ContentCard card)
        {
            float cProgress = Mathf.InverseLerp(0.70f, 1.0f, phaseProgress);

            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes = PlayerBehaviorData.Instance.pushCount;
                works = PlayerBehaviorData.Instance.workCount;
            }
            if (bananas + prayers + pushes + works == 0)
            {
                bananas = 8; prayers = 15; pushes = 3; works = 45;
            }

            if (cProgress < 0.33f)
            {
                // 步骤1: 推主题（眼熟但不点破）
                int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
                string[] pool = max == bananas ? themeStrings_Banana
                              : max == prayers ? themeStrings_Prayer
                              : max == pushes ? themeStrings_Push
                              : themeStrings_Work;
                card.cardText = pool[Random.Range(0, pool.Length)];
                card.cardColor = new Color(0.95f, 0.95f, 1f, 0.95f); // 精致白，依然好看
            }
            else if (cProgress < 0.66f)
            {
                // 步骤2: 推具体行为物件——用文字描述唤起回忆
                string[] behaviorHints = {
                    "🍌",
                    "A familiar yellow shape...",
                    "That rock you pushed",
                    "The sound of keys clicking",
                    "A stone idol, waiting",
                    "All work and no play...",
                    "The hill never ends",
                    "You've seen this before",
                };
                card.cardText = behaviorHints[Random.Range(0, behaviorHints.Length)];
                card.cardColor = new Color(1f, 0.98f, 0.93f, 0.97f); // 暖白，更精致更贴心
                card.isPrivateDataCard = true;
            }
            else
            {
                // 步骤3: 核爆——直接吐私密数据
                _phaseCDataIndex++;
                string[] privateData = {
                    $"You picked up {bananas} bananas.",
                    $"You prayed {prayers} times.",
                    $"You pushed the rock {pushes} times.",
                    $"You typed that sentence {works} times.",
                    "We have been watching\nsince the first banana.",
                    $"banana: {bananas}\nprayer: {prayers}\npush: {pushes}\nwork: {works}",
                    "There was never a moment\nwithout an observer.",
                    "The feed knows.\nIt always knew.",
                };
                card.cardText = privateData[(_phaseCDataIndex - 1) % privateData.Length];
                card.cardColor = new Color(1f, 1f, 1f, 0.98f); // 纯白——最可怕的是它没变凶
                card.isPrivateDataCard = true;
            }
        }

        private bool IsPrivateDataMoment()
        {
            float cProgress = Mathf.InverseLerp(0.70f, 1.0f, phaseProgress);
            return cProgress > 0.5f && Random.value < 0.3f;
        }

        /// <summary>
        /// 在朝向 forward 的锥形范围内生成随机方向
        /// </summary>
        private Vector3 RandomConeDirection(Vector3 forward, float maxAngleDeg)
        {
            float angleRad = maxAngleDeg * Mathf.Deg2Rad;
            float z = Random.Range(Mathf.Cos(angleRad), 1f);
            float phi = Random.Range(0f, 2f * Mathf.PI);
            float sinTheta = Mathf.Sqrt(1f - z * z);
            Vector3 localDir = new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), z);

            Quaternion rotation = Quaternion.LookRotation(forward);
            return rotation * localDir;
        }
    }
}
