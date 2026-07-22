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

        [Header("Phase B/C 4大偏好推送表")]
        public string[] themeStrings_Banana = {
            "10 Ways to Eat More Bananas Today! 🍌",
            "Why Your Brain CRAVES Potassium & Sugar 🍌",
            "Return to Monkey: Ultimate Dopamine Guide 🐒",
            "Top 5 Fresh Banana Smoothie Recipes 🥤",
            "Instant Gratification: Why Wait? 🍌",
            "Yellow Dopamine: The Primal Instinct 🍌"
        };
        public string[] themeStrings_Prayer = {
            "Daily Meditation: Manifesting Abundance ✨",
            "How to Open Your Third Eye in 5 Mins 🔮",
            "Signs the Universe is Talking to You ⛩️",
            "Finding Peace in Suffering & Prayer 🙏",
            "Cosmic Energy & Divine Guidance ✨",
            "Spiritual Awakening: Trust the Process 🔮"
        };
        public string[] themeStrings_Push = {
            "The Sisyphus Mindset: Embrace the Grind 🗿",
            "Never Stop Pushing: No Pain No Gain 💪",
            "Why Hard Times Create Strong People ⛰️",
            "10,000 Hours of Repetition Mastery 🏋️",
            "The Hill Never Ends: Keep Climbing 🧗",
            "Pain is Temporary, Glory is Eternal 🗿"
        };
        public string[] themeStrings_Work = {
            "Top 10 Office Slacking & Dodging Hacks 💻",
            "How to Look Busy When Boss Walks By ☕",
            "10x Productivity: Typewriter Masterclass ⚡",
            "Overcoming Workplace Burnout & Fatigue 📊",
            "Corporate Ladder Survival Guide 💼",
            "Work-Life Balance: Quiet Quitting 101 ☕"
        };

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
        }

        private void Update()
        {
            Transform pTransform = GetPlayerTransform();
            if (pTransform == null) return;

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

        private Shader FindCardShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("URP/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private void SpawnCard()
        {
            Transform pTransform = GetPlayerTransform();
            if (pTransform == null) return;

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

            // 3. 设置材质
            Renderer rend = cardGo.GetComponent<Renderer>();
            Shader shader = FindCardShader();
            Material mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);   // Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
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

        private static readonly string[] _phaseATags = { "[ 🔥 FOR YOU ]", "[ ✨ DISCOVER ]", "[ 🌸 VIBES ]", "[ 🍿 TRENDING ]", "[ 💎 POPULAR ]", "[ 🎵 REEL ]" };
        private static readonly string[] _phaseAIcons = { "✨", "🦄", "🔮", "🌸", "🍩", "🌈", "🎵", "💫", "🎨", "🍭", "🦋", "💖" };

        private void ConfigurePhaseA(ContentCard card)
        {
            // 高饱和度随机彩虹色
            float hue = Random.Range(0f, 1f);
            card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.85f, 1f));
            card.cardColor = new Color(card.cardColor.r, card.cardColor.g, card.cardColor.b, 0.92f);

            // 富视觉结构
            card.categoryTag = _phaseATags[Random.Range(0, _phaseATags.Length)];
            card.cardIcon = _phaseAIcons[Random.Range(0, _phaseAIcons.Length)];
            card.cardHeadline = _randomContent[Random.Range(0, _randomContent.Length)];
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

            if (bananas + prayers + pushes + works == 0)
            {
                bananas = 8; prayers = 15; pushes = 3; works = 45;
            }

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
            float narrowT = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);

            if (max == bananas)
            {
                card.categoryTag = Random.value < 0.5f ? "[ 🍌 BANANA OBSESSED ]" : "[ 🎯 99.4% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.12f, 0.16f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.9f, 1f));
                card.cardIcon = Random.value < 0.7f ? "🍌" : "🐒";
                card.cardHeadline = themeStrings_Banana[Random.Range(0, themeStrings_Banana.Length)];
            }
            else if (max == prayers)
            {
                card.categoryTag = Random.value < 0.5f ? "[ 🔮 SPIRITUAL SEEKER ]" : "[ 🎯 98.9% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.52f, 0.58f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1f));
                card.cardIcon = Random.value < 0.7f ? "🧘" : "🔮";
                card.cardHeadline = themeStrings_Prayer[Random.Range(0, themeStrings_Prayer.Length)];
            }
            else if (max == pushes)
            {
                card.categoryTag = Random.value < 0.5f ? "[ 🗿 SISYPHUS HARDCORE ]" : "[ 🎯 99.1% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.06f, 0.10f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.85f, 1f));
                card.cardIcon = Random.value < 0.7f ? "🗿" : "💪";
                card.cardHeadline = themeStrings_Push[Random.Range(0, themeStrings_Push.Length)];
            }
            else
            {
                card.categoryTag = Random.value < 0.5f ? "[ 💻 WORKAHOLIC COG ]" : "[ 🎯 99.8% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.97f, 1.02f) % 1f, narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.95f), Random.Range(0.85f, 1f));
                card.cardIcon = Random.value < 0.7f ? "💻" : "⌨️";
                card.cardHeadline = themeStrings_Work[Random.Range(0, themeStrings_Work.Length)];
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
                // 步骤1: 推荐标签
                card.categoryTag = "[ 📡 ALGORITHM EVAL ]";
                card.cardIcon = "📊";
                int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
                string[] pool = max == bananas ? themeStrings_Banana
                              : max == prayers ? themeStrings_Prayer
                              : max == pushes ? themeStrings_Push
                              : themeStrings_Work;
                card.cardHeadline = pool[Random.Range(0, pool.Length)];
                card.cardColor = new Color(0.95f, 0.95f, 1f, 0.95f);
            }
            else if (cProgress < 0.66f)
            {
                // 步骤2: 行为关联物
                card.categoryTag = "[ 👁️ BEHAVIOR TRACKED ]";
                card.cardIcon = "🔍";
                string[] behaviorHints = {
                    "A familiar yellow shape...",
                    "That rock you pushed on the hill",
                    "The sound of keys clicking in office",
                    "A stone idol waiting in silence",
                    "You've been here before...",
                };
                card.cardHeadline = behaviorHints[Random.Range(0, behaviorHints.Length)];
                card.cardColor = new Color(1f, 0.98f, 0.93f, 0.97f);
                card.isPrivateDataCard = true;
            }
            else
            {
                // 步骤3: 核爆私密数据
                _phaseCDataIndex++;
                card.categoryTag = "[ ⚠️ OBSERVER DATA LOG ]";
                card.cardIcon = "👁️";
                string[] privateData = {
                    $"You picked up {bananas} bananas.",
                    $"You prayed {prayers} times.",
                    $"You pushed the rock {pushes} times.",
                    $"You typed that sentence {works} times.",
                    "We have been watching\nsince the first banana.",
                    $"banana: {bananas} | prayer: {prayers}\npush: {pushes} | work: {works}",
                    "There was never a moment\nwithout an observer.",
                    "The feed knows. It always knew.",
                };
                card.cardHeadline = privateData[(_phaseCDataIndex - 1) % privateData.Length];
                card.cardColor = new Color(1f, 1f, 1f, 0.98f);
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
