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
        public int maxCards = 45; // 稍降低数量，防止过于臃肿

        [Header("Phase B/C 4大偏好推送表（无 Emoji 纯净版）")]
        public string[] themeStrings_Banana = {
            "10 Ways to Eat More Bananas Today",
            "Why Your Brain CRAVES Potassium & Sugar",
            "Return to Monkey: Ultimate Dopamine Guide",
            "Top 5 Fresh Banana Smoothie Recipes",
            "Instant Gratification: Why Wait?",
            "Yellow Dopamine: The Primal Instinct"
        };
        public string[] themeStrings_Prayer = {
            "Daily Meditation: Manifesting Abundance",
            "How to Open Your Third Eye in 5 Mins",
            "Signs the Universe is Talking to You",
            "Finding Peace in Suffering & Prayer",
            "Cosmic Energy & Divine Guidance",
            "Spiritual Awakening: Trust the Process"
        };
        public string[] themeStrings_Push = {
            "The Sisyphus Mindset: Embrace the Grind",
            "Never Stop Pushing: No Pain No Gain",
            "Why Hard Times Create Strong People",
            "10,000 Hours of Repetition Mastery",
            "The Hill Never Ends: Keep Climbing",
            "Pain is Temporary, Glory is Eternal"
        };
        public string[] themeStrings_Work = {
            "Top 10 Office Slacking & Dodging Hacks",
            "How to Look Busy When Boss Walks By",
            "10x Productivity: Typewriter Masterclass",
            "Overcoming Workplace Burnout & Fatigue",
            "Corporate Ladder Survival Guide",
            "Work-Life Balance: Quiet Quitting 101"
        };

        // Phase A 4 大热点类型词库 (按照 窥探 / 变好的承诺 / 猎奇震惊 / 轻松的笑 分类)
        private readonly string[] _phaseA_VoyeurTags = { "[ VOYEUR ]", "[ DAY IN LIFE ]", "[ EXPOSED ]", "[ STORYTIME ]", "[ PRIVATE STORY ]" };
        private readonly string[] _phaseA_VoyeurHeadlines = {
            "GRWM: My Unfiltered Morning Routine",
            "What I Eat In A Day As A Model",
            "Exposing My Ex's Secret Messages",
            "Spill The Tea: The Whole Story",
            "Silent NYC Apartment Tour",
            "Main Character Energy: Room Reveal",
            "I Snuck Into A Private Party",
            "Behind The Scenes: What They Hide"
        };

        private readonly string[] _phaseA_GlowUpTags = { "[ GLOW UP ]", "[ MONK MODE ]", "[ 10X SELF ]", "[ DETOX ]", "[ MINDSET ]" };
        private readonly string[] _phaseA_GlowUpHeadlines = {
            "5 AM Club: My Monk Mode Journey",
            "100 Days Glow Up Transformation",
            "How I Made $10,000/Mo At 20",
            "Dopamine Detox: Reclaim Your Focus",
            "Atomic Habits: 1% Better Everyday",
            "That Girl Routine: Unlock Full Potential",
            "Reset Your Life In 7 Days",
            "Stop Wasting Time: Hard Truths"
        };

        private readonly string[] _phaseA_ShockTags = { "[ SHOCK NOVELTY ]", "[ UNEXPLAINED ]", "[ BIZARRE ]", "[ 100% REAL ]", "[ DEEP WEB ]" };
        private readonly string[] _phaseA_ShockHeadlines = {
            "YOU WON'T BELIEVE WHAT HAPPENED",
            "Hydraulic Press Vs Rare Gemstone",
            "Surviving 24 Hours Buried Alive",
            "Oddly Satisfying Crushing Test",
            "Found Footage From Abandoned Lab",
            "Cursed Artifact Mystery Solved",
            "Destroying A $10,000 Gaming Rig",
            "Creepypasta Real Life Encounter"
        };

        private readonly string[] _phaseA_LaughTags = { "[ EASY LAUGHS ]", "[ MEME CENTRAL ]", "[ PET CHAOS ]", "[ RELATABLE ]", "[ SHITPOST ]" };
        private readonly string[] _phaseA_LaughHeadlines = {
            "Brainrot Meme Compilation 2026",
            "Golden Retriever Energy Unleashed",
            "Try Not To Laugh Challenge",
            "Wait For The Ending",
            "Relatable Workplace Fails",
            "Derpy Cat Reacts To Cucumbers",
            "Unexpected Plot Twist At The End",
            "Me Thinking About Life At 3 AM"
        };

        private void ConfigurePhaseA(ContentCard card)
        {
            float hue = Random.Range(0f, 1f);
            card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.85f, 1f));
            card.cardColor = new Color(card.cardColor.r, card.cardColor.g, card.cardColor.b, 0.92f);
            card.cardIcon = "";

            int categoryIndex = Random.Range(0, 4);
            switch (categoryIndex)
            {
                case 0: // 窥探 (Voyeurism)
                    card.categoryTag = _phaseA_VoyeurTags[Random.Range(0, _phaseA_VoyeurTags.Length)];
                    card.cardHeadline = _phaseA_VoyeurHeadlines[Random.Range(0, _phaseA_VoyeurHeadlines.Length)];
                    break;
                case 1: // 变好的承诺 (Glow Up / Monk Mode)
                    card.categoryTag = _phaseA_GlowUpTags[Random.Range(0, _phaseA_GlowUpTags.Length)];
                    card.cardHeadline = _phaseA_GlowUpHeadlines[Random.Range(0, _phaseA_GlowUpHeadlines.Length)];
                    break;
                case 2: // 猎奇/震惊 (Shock Novelty)
                    card.categoryTag = _phaseA_ShockTags[Random.Range(0, _phaseA_ShockTags.Length)];
                    card.cardHeadline = _phaseA_ShockHeadlines[Random.Range(0, _phaseA_ShockHeadlines.Length)];
                    break;
                case 3: // 轻松的笑 (Easy Laughs / Memes)
                    card.categoryTag = _phaseA_LaughTags[Random.Range(0, _phaseA_LaughTags.Length)];
                    card.cardHeadline = _phaseA_LaughHeadlines[Random.Range(0, _phaseA_LaughHeadlines.Length)];
                    break;
            }
        }

        // 内部状态
        private Transform _player;
        private float _spawnTimer = 0f;
        private float _currentInterval = 1.0f;
        private List<ContentCard> _activeCards = new List<ContentCard>();
        private int _phaseCDataIndex = 0;

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

            // 计算当前节奏间隔（整体节奏放慢 30%）
            _currentInterval = CalculateSpawnInterval();

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _currentInterval && _activeCards.Count < maxCards)
            {
                _spawnTimer = 0f;

                // Phase C 私密数据卡出现时的静默暂停
                if (phaseProgress > 0.85f && IsPrivateDataMoment())
                {
                    _spawnTimer = -2.5f; // 停 2.5 秒制造沉寂
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

            // 碰撞体
            Collider col = cardGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            BoxCollider box = cardGo.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // 3. 设置材质
            Renderer rend = cardGo.GetComponent<Renderer>();
            Shader shader = FindCardShader();
            Material mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            rend.material = mat;

            // 4. 配置 ContentCard 组件（旋钮2: 内容）
            ContentCard card = cardGo.AddComponent<ContentCard>();
            ConfigureCardContent(card);

            // 5. 漂移速度微调（放慢 30%，漂移更平缓优雅）
            card.driftSpeed = Mathf.Lerp(0.85f, 1.75f, phaseProgress);

            _activeCards.Add(card);
            OnCardSpawned?.Invoke(card);
        }

        private Vector3 CalculateSpawnPosition()
        {
            float radius = Mathf.Lerp(spawnRadius, minSpawnRadius, phaseProgress);

            if (phaseProgress < 0.35f)
            {
                return _player.position + Random.onUnitSphere * radius;
            }
            else if (phaseProgress < 0.70f)
            {
                float narrowT = Mathf.InverseLerp(0.35f, 0.70f, phaseProgress);
                float maxAngle = Mathf.Lerp(180f, 45f, narrowT);
                Vector3 dir = RandomConeDirection(_player.forward, maxAngle);
                return _player.position + dir * radius;
            }
            else
            {
                float narrowT = Mathf.InverseLerp(0.70f, 1.0f, phaseProgress);
                float maxAngle = Mathf.Lerp(45f, 12f, narrowT);
                Vector3 dir = RandomConeDirection(_player.forward, maxAngle);
                return _player.position + dir * radius;
            }
        }

        /// <summary>
        /// 旋钮3: 节奏（放慢 30%）
        /// </summary>
        private float CalculateSpawnInterval()
        {
            if (phaseProgress < 0.35f)
            {
                if (Random.value < 0.12f)
                    return Random.Range(0.2f, 0.4f);
                return Random.Range(0.65f, 2.0f);
            }
            else if (phaseProgress < 0.70f)
            {
                return 0.85f; // 匀速放慢
            }
            else
            {
                return 0.58f;
            }
        }

        private void ConfigureCardContent(ContentCard card)
        {
            if (phaseProgress < 0.35f)
            {
                ConfigurePhaseA(card);
            }
            else if (phaseProgress < 0.70f)
            {
                ConfigurePhaseB(card);
            }
            else
            {
                ConfigurePhaseC(card);
            }
        }

        private void ConfigurePhaseB(ContentCard card)
        {
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
                card.categoryTag = Random.value < 0.5f ? "[ BANANA OBSESSED ]" : "[ 99.4% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.12f, 0.16f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.9f, 1f));
                card.cardIcon = "";
                card.cardHeadline = themeStrings_Banana[Random.Range(0, themeStrings_Banana.Length)];
            }
            else if (max == prayers)
            {
                card.categoryTag = Random.value < 0.5f ? "[ SPIRITUAL SEEKER ]" : "[ 98.9% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.52f, 0.58f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1f));
                card.cardIcon = "";
                card.cardHeadline = themeStrings_Prayer[Random.Range(0, themeStrings_Prayer.Length)];
            }
            else if (max == pushes)
            {
                card.categoryTag = Random.value < 0.5f ? "[ SISYPHUS HARDCORE ]" : "[ 99.1% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.06f, 0.10f), narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.7f, 0.95f), Random.Range(0.85f, 1f));
                card.cardIcon = "";
                card.cardHeadline = themeStrings_Push[Random.Range(0, themeStrings_Push.Length)];
            }
            else
            {
                card.categoryTag = Random.value < 0.5f ? "[ WORKAHOLIC COG ]" : "[ 99.8% ALGORITHM MATCH ]";
                float hue = Mathf.Lerp(Random.Range(0f, 1f), Random.Range(0.97f, 1.02f) % 1f, narrowT);
                card.cardColor = Color.HSVToRGB(hue, Random.Range(0.6f, 0.95f), Random.Range(0.85f, 1f));
                card.cardIcon = "";
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
                card.categoryTag = "[ ALGORITHM EVAL ]";
                card.cardIcon = "";
                int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
                string[] pool = max == bananas ? themeStrings_Banana
                              : max == prayers ? themeStrings_Prayer
                              : max == pushes ? themeStrings_Push
                              : themeStrings_Work;
                card.cardHeadline = pool[Random.Range(0, pool.Length)];
                card.cardColor = new Color(0.2f, 0.75f, 1.0f, 0.95f); // 霓虹青紫外包边
            }
            else if (cProgress < 0.66f)
            {
                card.categoryTag = "[ BEHAVIOR TRACKED ]";
                card.cardIcon = "";
                string[] behaviorHints = {
                    "A familiar yellow shape...",
                    "That rock you pushed on the hill",
                    "The sound of keys clicking in office",
                    "A stone idol waiting in silence",
                    "You've been here before...",
                };
                card.cardHeadline = behaviorHints[Random.Range(0, behaviorHints.Length)];
                card.cardColor = new Color(1.0f, 0.65f, 0.15f, 0.97f); // 警示琥珀橙外包边
                card.isPrivateDataCard = true;
            }
            else
            {
                _phaseCDataIndex++;
                card.categoryTag = "[ OBSERVER DATA LOG ]";
                card.cardIcon = "";
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
                card.cardColor = new Color(1.0f, 0.25f, 0.4f, 0.98f); // 高科技警示绯红外包边
                card.isPrivateDataCard = true;
            }
        }

        private bool IsPrivateDataMoment()
        {
            float cProgress = Mathf.InverseLerp(0.70f, 1.0f, phaseProgress);
            return cProgress > 0.5f && Random.value < 0.3f;
        }

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
