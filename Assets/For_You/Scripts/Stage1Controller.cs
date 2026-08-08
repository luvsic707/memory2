using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第 1 阶段（猩猩关卡 1_Ape）总控制器
    /// 1. 采用单例模式，协调关卡流程。
    /// 2. 追踪吃香蕉进度，随交互次数增加，分裂数量递增（每4个多生一只，最多6倍）。
    /// 3. 大多数新生香蕉外观正常（轻微扭曲随计数增长）。
    ///    约 mutantChance（默认15%）的新生香蕉为"变异香蕉"：色彩/尺寸/形状夸张。
    /// 4. 吃满 exitBananaMinCount 后，变异香蕉有概率成为 Exit 香蕉（isGlowingBanana=true）。
    ///    玩家与 Exit 香蕉交互 → 触发通关。无固定触发时机，完全由玩家探索发现。
    /// </summary>
    public class Stage1Controller : MonoBehaviour
    {
        public static Stage1Controller Instance { get; private set; }

        // ── 关卡参数 ──────────────────────────────────────────────────────
        [Header("关卡参数")]
        [Tooltip("正常香蕉的扭曲度在此数量时达到最大（与通关无关，仅影响普通香蕉变形程度）")]
        public int distortionMaxCount = 50;

        [Tooltip("正常香蕉最大扭曲度上限（保持在 0.6 以下避免形变过于夸张）")]
        public float maxDistortionFactor = 0.6f;

        // ── 变异香蕉系统 ──────────────────────────────────────────────────
        [Header("🧬 变异香蕉系统")]
        [Tooltip("每次生成新香蕉时，出现变异香蕉的概率 (0.15 表示 15% 几率变异。只要玩家与任意变异香蕉交互，即可通关进入 Stage 2)")]
        [Range(0f, 1f)] public float mutantChance = 0.15f;

        // ── Prefab ────────────────────────────────────────────────────────
        [Header("香蕉 Prefab")]
        [Tooltip("普通香蕉预制体（分裂生成与 Exit 香蕉共用此 Prefab）")]
        public GameObject bananaPrefab;

        // ── Juice 参数（集中控制，推送到所有 BananaJuice 组件） ─────────
        [Header("🍌 香蕉 Juice 参数（统一控制所有香蕉的手感）")]
        [Tooltip("Y 轴压缩幅度")]
        [Range(0f, 0.5f)] public float juiceSquashY = 0.28f;

        [Tooltip("弹回时的超出倍率")]
        [Range(1f, 1.5f)] public float juiceOvershoot = 1.12f;

        [Tooltip("随机晃动角度（度）")]
        [Range(0f, 45f)] public float juiceWobbleAngle = 18f;

        [Tooltip("摄像机震屏强度")]
        [Range(0f, 0.1f)] public float juiceShakeIntensity = 0.025f;

        [Tooltip("横向弹出位移")]
        [Range(0f, 0.15f)] public float juicePunchDistance = 0.04f;

        // ── 内部状态 ──────────────────────────────────────────────────────
        private int   eatenCount           = 0;
        private bool  hasSpawnedExitBanana = false;
        private Vector3 templateScale      = Vector3.one;

        /// <summary>变异香蕉视觉类型</summary>
        private enum MutantType { Color, ScaleBig, ScaleSmall, Distortion }

        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // 自动为场景主相机挂载第一视角手臂抓取系统
            if (FirstPersonArmController.Instance == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam.GetComponent<FirstPersonArmController>() == null)
                    mainCam.gameObject.AddComponent<FirstPersonArmController>();
            }

            // 缓存场景里第一个香蕉的原本缩放，避免生成的香蕉比例错误
            BananaInteractable initialBanana = FindObjectOfType<BananaInteractable>();
            if (initialBanana != null) templateScale = initialBanana.transform.lossyScale;

            // 将 Juice 参数推送给场景中所有香蕉
            PushJuiceSettingsToAllBananas();
        }

        private void Update()
        {
#if UNITY_EDITOR
            // 单关测试模式：按 P 键直接跳过该关
            if (Input.GetKeyDown(KeyCode.P) && FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.Log("[Debug] 玩家在单关测试模式下按下了 P 键，正在手动跳过当前关卡...");
                PerformSceneTransition();
            }
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API

        /// <summary>获取当前正常香蕉的扭曲度因子</summary>
        public float GetCurrentDistortionFactor()
        {
            if (distortionMaxCount <= 0) return 0f;
            float ratio = Mathf.Min((float)eatenCount / distortionMaxCount, 1f);
            return ratio * maxDistortionFactor;
        }

        /// <summary>查询是否已产生过 Exit 香蕉</summary>
        public bool HasSpawnedExitBanana() => hasSpawnedExitBanana;

        /// <summary>把 Inspector 上的 Juice 参数同步推送给场景内所有 BananaJuice 组件</summary>
        public void PushJuiceSettingsToAllBananas()
        {
            BananaJuice[] allJuice = FindObjectsOfType<BananaJuice>(true);
            foreach (var j in allJuice)
            {
                j.squashY        = juiceSquashY;
                j.overshoot      = juiceOvershoot;
                j.wobbleAngle    = juiceWobbleAngle;
                j.shakeIntensity = juiceShakeIntensity;
                j.punchDistance  = juicePunchDistance;
            }
        }

        /// <summary>每次普通香蕉被交互/吃掉时调用（由 BananaInteractable 触发）</summary>
        public void OnBananaEaten(BananaInteractable eatenBanana)
        {
            eatenCount++;
            Debug.Log($"[Stage1] 吃掉香蕉。当前计数：{eatenCount}");

            // 有丝分裂：随计数增多，每次生成数量增加（每4个+1，上限6）
            int mitosisCount = Mathf.Clamp(eatenCount / 4 + 1, 1, 6);
            for (int i = 0; i < mitosisCount; i++)
            {
                SpawnBananaFromSource(eatenBanana);
            }
        }

        /// <summary>
        /// 玩家吃掉 Exit 香蕉（isGlowingBanana=true）时触发通关
        /// 保持与 BananaInteractable.ExecuteBananaEatLogic() 中调用的方法名一致
        /// </summary>
        public void EatGlowingBanana()
        {
            Debug.Log("<color=green>[Stage1] 玩家成功吃下 Exit 变异香蕉！触发转场加载阶段 2...</color>");
            EventBus.RaiseAnnouncement("The strange banana tasted wrong... You lost consciousness.");
            Invoke("PerformSceneTransition", 1.2f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Internal: Spawn Logic

        /// <summary>
        /// 在被吃香蕉附近生成一个新香蕉。
        /// 按 mutantChance 决定是否为变异香蕉，再按 exitBananaChance 决定是否为 Exit 香蕉。
        /// </summary>
        private void SpawnBananaFromSource(BananaInteractable source)
        {
            GameObject prefab = source.bananaPrefab != null ? source.bananaPrefab : bananaPrefab;
            if (prefab == null)
            {
                Debug.LogError("[Stage1] 未指定 bananaPrefab！无法生成新香蕉。");
                return;
            }

            // 随机偏移位置（沿用 source 的 spawnRadius/spawnHeightOffset）
            Vector3 offset = new Vector3(
                Random.Range(-source.spawnRadius, source.spawnRadius),
                source.spawnHeightOffset,
                Random.Range(-source.spawnRadius, source.spawnRadius)
            );
            Vector3 spawnPos  = source.transform.position + offset;

            // ── 决定变异 ──────────────────────────────────────────────────
            bool isMutant = Random.value < mutantChance;

            // ── 实例化 ───────────────────────────────────────────────────
            GameObject newBanana = Instantiate(prefab, spawnPos, Random.rotation);
            newBanana.name = isMutant ? "Banana_Mutant" : "Banana_Normal";

            // 基础缩放（使用缓存的场景原始香蕉缩放作为参考）
            newBanana.transform.localScale = templateScale;

            if (isMutant)
            {
                // 变异香蕉：应用夸张视觉效果
                ApplyMutantVisuals(newBanana);

                // 🌟 核心规则：只要是变异香蕉，吃掉即触发通关进入 Stage 2！
                BananaInteractable bi = newBanana.GetComponent<BananaInteractable>();
                if (bi == null)
                {
                    bi = newBanana.AddComponent<BananaInteractable>();
                    bi.bananaPrefab       = prefab;
                    bi.spawnAsInteractive = true;
                }
                bi.isGlowingBanana = true;

                Debug.Log("<color=yellow>[Stage1] 生成了一只变异香蕉！与其交互即可通关进入 Stage 2。</color>");
            }
            else
            {
                // 正常香蕉：轻微扭曲随计数增长
                float distFactor = GetCurrentDistortionFactor();
                if (distFactor > 0.05f)
                {
                    BananaDistorter d = newBanana.GetComponent<BananaDistorter>() ?? newBanana.AddComponent<BananaDistorter>();
                    d.distortionFactor = distFactor;
                }
                newBanana.transform.localScale = templateScale * (1f + distFactor * 0.2f);

                if (!source.spawnAsInteractive)
                {
                    var bi = newBanana.GetComponent<BananaInteractable>();
                    if (bi != null) Destroy(bi);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Internal: Mutant Visual System

        /// <summary>
        /// 对变异香蕉应用随机夸张视觉效果。
        /// 从四种变异类型中随机选一种：色彩、极大、极小、高度扭曲。
        /// </summary>
        private void ApplyMutantVisuals(GameObject banana)
        {
            MutantType type = (MutantType)Random.Range(0, System.Enum.GetValues(typeof(MutantType)).Length);
            Renderer[] renderers = banana.GetComponentsInChildren<Renderer>(true);

            switch (type)
            {
                // ── 色彩异变：极度饱和的随机彩色 ──────────────────────────
                case MutantType.Color:
                {
                    Color vibrant = Color.HSVToRGB(Random.value, 0.95f, 1.0f);
                    foreach (var r in renderers)
                    {
                        r.material.color = vibrant;
                        // 尝试添加自发光（URP/Standard 均兼容）
                        if (r.material.HasProperty("_EmissionColor"))
                        {
                            r.material.EnableKeyword("_EMISSION");
                            r.material.SetColor("_EmissionColor", vibrant * 0.35f);
                        }
                    }
                    break;
                }

                // ── 尺寸异变：极度放大（3~5x） ─────────────────────────────
                case MutantType.ScaleBig:
                {
                    banana.transform.localScale = templateScale * Random.Range(3f, 5.5f);
                    // 同时加一点颜色区分
                    Color bigColor = Color.HSVToRGB(Random.value, 0.7f, 1f);
                    foreach (var r in renderers) r.material.color = bigColor;
                    break;
                }

                // ── 尺寸异变：极度缩小（8%~25%） ───────────────────────────
                case MutantType.ScaleSmall:
                {
                    banana.transform.localScale = templateScale * Random.Range(0.08f, 0.25f);
                    // 鲜艳小香蕉
                    Color smallColor = Color.HSVToRGB(Random.value, 0.9f, 1f);
                    foreach (var r in renderers) r.material.color = smallColor;
                    break;
                }

                // ── 形状异变：极度扭曲 + 诡异配色 ─────────────────────────
                case MutantType.Distortion:
                {
                    BananaDistorter d = banana.GetComponent<BananaDistorter>() ?? banana.AddComponent<BananaDistorter>();
                    d.distortionFactor = Random.Range(2.5f, 5f);
                    d.twistRate        = Random.Range(8f,  20f);
                    d.bendRate         = Random.Range(0.8f, 2f);

                    Color distortColor = Color.HSVToRGB(Random.value, 0.85f, 0.9f);
                    foreach (var r in renderers) r.material.color = distortColor;
                    break;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Scene Transition

        private void PerformSceneTransition()
        {
            EventBus.RaiseSceneComplete();

#if UNITY_EDITOR
            if (FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning("[Stage1Controller] 未检测到 SceneTransitionManager，自动载入 2_God。");
                UnityEngine.SceneManagement.SceneManager.LoadScene("2_God");
            }
#endif
        }
    }
}
