using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第 2 阶段（上帝已死 2_God）总控制器
    /// 1. 采用单例模式控制关卡节奏。
    /// 2. 石台（浮岛）随时间推移晃动加剧（X/Z轴平移偏差与旋转倾斜）。
    /// 3. 玩家通过点击鼠标左键（或按Q）跪拜祈祷，每次点击会显著降低晃动，并在短时间内稳定石台。如果停止点击则快速恶化。
    /// 4. 实时把晃动强度同步给 RockFractureDistorter，驱动顶点的物理崩裂。
    /// 5. 检测玩家高度 Y 值，一旦坠落深渊，延时 3 秒转场进入下一场景 3_Rock（西西弗斯推石）。
    /// </summary>
    public class Stage2Controller : MonoBehaviour
    {
        public static Stage2Controller Instance { get; private set; }

        [Header("石台与玩家引用")]
        [Tooltip("悬浮石台 GameObject（如果为空，将自动寻找名字包含 island 的物体）")]
        public GameObject floatingIsland;

        [Tooltip("玩家物体 GameObject（如果为空，将自动寻找 Player 标签或脚本）")]
        public GameObject player;

        [Header("平衡平衡参数")]
        [Tooltip("每秒晃动值增长速度")]
        public float shakeGrowthRate = 0.08f;

        [Tooltip("每次点击鼠标祈祷减少的晃动值")]
        public float shakeReductionPerClick = 0.12f;

        [Tooltip("最大晃动值上限")]
        public float maxShakeIntensity = 1.2f;

        [Tooltip("每次点击后石台保持绝对稳定的冷却时间（秒）")]
        public float stabilizationCooldown = 0.5f;

        [Header("坠落检测")]
        [Tooltip("坠落高度阈值（低于石台初始高度多少米算作坠落）")]
        public float fallThresholdDistance = 8f;

        [Header("BGM 背景音乐")]
        [Tooltip("Stage 2 背景音乐 AudioClip（如果留空，系统会自动加载备用背景音乐）")]
        public AudioClip bgmClip;

        [Tooltip("BGM 音量 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float bgmVolume = 0.5f;

        [Tooltip("是否循环播放 BGM")]
        public bool loopBgm = true;

        private AudioSource _bgmAudioSource;
        private float currentShakeIntensity = 0f;
        private float cooldownTimer = 0f;

        private Vector3 islandBasePosition;
        private Quaternion islandBaseRotation;
        private float islandBaseY;

        private bool hasFallen = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 自动配置 BGM 背景音乐
            SetupBGM();

            // 自动装配 Stage 2 Juice 效果组件
            if (Stage2JuiceEffects.Instance == null && GetComponent<Stage2JuiceEffects>() == null)
            {
                gameObject.AddComponent<Stage2JuiceEffects>();
            }

            // 自动寻找石台
            if (floatingIsland == null)
            {
                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (go.name.ToLower().Contains("island"))
                    {
                        floatingIsland = go;
                        break;
                    }
                }
            }

            // 自动寻找玩家
            if (player == null)
            {
                player = GameObject.FindWithTag("Player");
                if (player == null)
                {
                    var pc = FindObjectOfType<UniversalPlayer>();
                    if (pc != null) player = pc.gameObject;
                }
            }

            if (floatingIsland != null)
            {
                islandBasePosition = floatingIsland.transform.position;
                islandBaseRotation = floatingIsland.transform.rotation;
                islandBaseY = islandBasePosition.y;

                // 自动装配网格形变器
                RockFractureDistorter distorter = floatingIsland.GetComponent<RockFractureDistorter>();
                if (distorter == null) distorter = floatingIsland.GetComponentInChildren<RockFractureDistorter>();
                if (distorter == null)
                {
                    distorter = floatingIsland.AddComponent<RockFractureDistorter>();
                }
                Debug.Log($"[Stage2] 石台 '{floatingIsland.name}' 基础高度记录为: {islandBaseY}，网格变形组件已就绪。");
            }
            else
            {
                Debug.LogError("[Stage2] 找不到悬浮石台 (floatingIsland)！无法执行晃动效果。");
            }
        }

        private void SetupBGM()
        {
            // 关掉之前残留的对话旁白系统 NarratorManager
            if (NarratorManager.Instance != null)
            {
                NarratorManager.Instance.StopCurrent();
                NarratorManager.Instance.gameObject.SetActive(false);
            }

            _bgmAudioSource = gameObject.AddComponent<AudioSource>();
            _bgmAudioSource.loop = loopBgm;
            _bgmAudioSource.volume = bgmVolume;
            _bgmAudioSource.spatialBlend = 0f;

            if (bgmClip != null)
            {
                _bgmAudioSource.clip = bgmClip;
                _bgmAudioSource.Play();
                Debug.Log($"[Stage2] 播放指定的 BGM: {bgmClip.name}");
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            // 调试模式下支持按 P 键直接跳过本关
            if (Input.GetKeyDown(KeyCode.P) && FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.Log("[Debug] 玩家在单关测试模式下按下了 P 键，正在手动跳过当前关卡...");
                PerformSceneTransition();
                return;
            }
#endif

            if (hasFallen || floatingIsland == null) return;

            // 1. 监测鼠标左键交互（祈祷）
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q))
            {
                // 减少晃动值
                currentShakeIntensity -= shakeReductionPerClick;
                currentShakeIntensity = Mathf.Max(currentShakeIntensity, 0f);
                
                // 进入稳定冷静期
                cooldownTimer = stabilizationCooldown;

                // 增加祈祷数据计数器
                if (PlayerBehaviorData.Instance != null)
                {
                    PlayerBehaviorData.Instance.AddPrayer();
                }

                // 触发 Stage 2 Juice 反馈（神圣光环 + 视角扣击 + 低沉神钟音效）
                if (Stage2JuiceEffects.Instance != null)
                {
                    Vector3 origin = player != null ? player.transform.position : transform.position;
                    Stage2JuiceEffects.Instance.TriggerPrayerJuice(origin);
                }

                Debug.Log($"[Stage2] 玩家祈祷稳定石台。当前晃动度: {currentShakeIntensity}");
            }

            // 2. 冷却时间与晃动值递增
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
            else
            {
                currentShakeIntensity += shakeGrowthRate * Time.deltaTime;
                currentShakeIntensity = Mathf.Min(currentShakeIntensity, maxShakeIntensity);
            }

            // 3. 执行石台物理晃动偏移（正弦平移 + XZ轴偏角旋转）
            float t = Time.time;
            Vector3 posOffset = new Vector3(
                Mathf.Sin(t * 18f),
                Mathf.Cos(t * 22f) * 0.1f, // 垂直轻微晃动
                Mathf.Cos(t * 16f)
            ) * currentShakeIntensity * 0.25f; // 位移振幅

            Vector3 rotOffset = new Vector3(
                Mathf.Sin(t * 14f),
                0f,
                Mathf.Cos(t * 12f)
            ) * currentShakeIntensity * 15f; // 倾斜角抖动偏移（最大倾角约18度，这会导致玩家滑落）

            floatingIsland.transform.position = islandBasePosition + posOffset;
            floatingIsland.transform.rotation = islandBaseRotation * Quaternion.Euler(rotOffset);

            // 4. 将强度同步给网格变形器，展现石头崩裂
            RockFractureDistorter distorter = floatingIsland.GetComponent<RockFractureDistorter>();
            if (distorter == null) distorter = floatingIsland.GetComponentInChildren<RockFractureDistorter>();
            if (distorter != null)
            {
                distorter.currentIntensity = currentShakeIntensity;
            }

            // 5. 模拟倾斜导致的玩家向边缘滑落 (由于 CharacterController 默认不继承平台物理倾斜，我们需要手动进行受力推挤)
            if (player != null && currentShakeIntensity > 0.15f)
            {
                Vector3 playerPos = player.transform.position;
                Vector3 pushDirection = playerPos - islandBasePosition;
                pushDirection.y = 0f; // 仅在水平面推动

                if (pushDirection.sqrMagnitude < 0.01f)
                {
                    pushDirection = player.transform.forward; // 默认防零向量
                }
                pushDirection.Normalize();

                // 滑落速度随着晃动值呈二次方递增，晃动越剧烈，滑出越快
                float slideSpeed = currentShakeIntensity * currentShakeIntensity * 7.5f;

                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc == null) cc = player.GetComponentInChildren<CharacterController>();

                if (cc != null)
                {
                    cc.Move(pushDirection * slideSpeed * Time.deltaTime);
                }
                else
                {
                    player.transform.position += pushDirection * slideSpeed * Time.deltaTime;
                }
            }

            // 6. 监测玩家是否跌落深渊
            if (player != null)
            {
                if (player.transform.position.y < (islandBaseY - fallThresholdDistance))
                {
                    hasFallen = true;
                    StartCoroutine(HandlePlayerFallSequence());
                }
            }
        }

        /// <summary>
        /// 玩家落空坠落协程：跌落 3 秒后执行切关
        /// </summary>
        private IEnumerator HandlePlayerFallSequence()
        {
            Debug.Log("<color=red>[Stage2] 检测到玩家坠落深渊！启动 3 秒倒计时转场...</color>");
            
            // 触发跌落通告与失重 FOV 视角动画
            EventBus.RaiseAnnouncement("You lost balance and fell from the grace of divinity...");
            if (Stage2JuiceEffects.Instance != null)
            {
                Stage2JuiceEffects.Instance.TriggerFallEuphoriaSequence();
            }

            yield return new WaitForSeconds(3.0f);

            Debug.Log("[Stage2] 倒计时结束，执行场景跳转加载下一阶段 3...");
            PerformSceneTransition();
        }

        private void PerformSceneTransition()
        {
            EventBus.RaiseSceneComplete();

            // 额外防御：如果在编辑器中独立运行当前场景（没有通过 0_Bootstrap 启动），则没有 SceneTransitionManager。
            // 为了让单关测试顺畅，我们直接在此处通过 SceneManager 载入下一个场景！
#if UNITY_EDITOR
            if (FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning("[Stage2Controller] 未检测到 SceneTransitionManager（可能是单关测试模式）。正在自动载入 3_Rock 场景进行单关测试过渡。");
                UnityEngine.SceneManagement.SceneManager.LoadScene("3_Rock");
            }
#endif
        }
    }
}
