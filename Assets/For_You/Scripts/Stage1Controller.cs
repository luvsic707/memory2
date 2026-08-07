using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第 1 阶段（猩猩关卡 1_Ape）总控制器
    /// 1. 采用单例模式，协调关卡流程。
    /// 2. 追踪吃香蕉进度。随着吃蕉增多，扭曲因子（distortionFactor）不断提升，分裂（mitosis）出的香蕉数量和扭曲度递增。
    /// 3. 当计数达到目标值（约 20 次）时，将场上所有普通香蕉“灰质化”并禁用交互，同时在终点生成一个带红光光晕的特殊通关香蕉。
    /// 4. 吃掉特殊香蕉后广播事件并触发加载下一场景。
    /// </summary>
    public class Stage1Controller : MonoBehaviour
    {
        public static Stage1Controller Instance { get; private set; }

        [Header("关卡参数")]
        [Tooltip("触发通关香蕉出现的吃蕉目标数")]
        public int targetBananaCount = 20;

        [Tooltip("最大扭曲度上限")]
        public float maxDistortionFactor = 1.5f;

        [Header("特殊香蕉生成设置")]
        [Tooltip("特殊香蕉的生成位置，若为空则在玩家出生点附近或场景原点生成")]
        public Transform glowingBananaSpawnPoint;

        [Tooltip("普通香蕉的预制体（我们将动态用代码为其添加红光光晕做成特殊香蕉）")]
        public GameObject bananaPrefab;

        private int eatenCount = 0;
        private bool hasSpawnedSpecialBanana = false;

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

        private Vector3 templateScale = Vector3.one;

        private void Start()
        {
            // 自动为场景主相机挂载第一视角手臂抓取系统
            if (FirstPersonArmController.Instance == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam.GetComponent<FirstPersonArmController>() == null)
                {
                    mainCam.gameObject.AddComponent<FirstPersonArmController>();
                }
            }

            // 缓存场景里第一个香蕉的原本缩放，避免生成的强光香蕉比正常香蕉大几十倍
            BananaInteractable initialBanana = FindObjectOfType<BananaInteractable>();
            if (initialBanana != null)
            {
                templateScale = initialBanana.transform.lossyScale;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            // 如果是在单关测试模式（没有 SceneTransitionManager），按 P 键可以直接跳过该关进入下一关
            if (Input.GetKeyDown(KeyCode.P) && FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.Log("[Debug] 玩家在单关测试模式下按下了 P 键，正在手动跳过当前关卡...");
                PerformSceneTransition();
            }
#endif
        }

        /// <summary>
        /// 获取当前的网格扭曲度因子 (由 BananaInteractable 调用传递给新生成的香蕉)
        /// </summary>
        public float GetCurrentDistortionFactor()
        {
            if (targetBananaCount <= 0) return 0f;
            float ratio = (float)eatenCount / targetBananaCount;
            return Mathf.Min(ratio * maxDistortionFactor, maxDistortionFactor);
        }

        /// <summary>
        /// 提供给外部查询当前是否已生成特殊红光香蕉
        /// </summary>
        public bool HasSpawnedSpecialBanana()
        {
            return hasSpawnedSpecialBanana;
        }

        /// <summary>
        /// 每次普通香蕉被交互/吃掉时调用
        /// </summary>
        public void OnBananaEaten(BananaInteractable eatenBanana)
        {
            eatenCount++;
            Debug.Log($"[Stage1] 吃掉香蕉。当前计数：{eatenCount}/{targetBananaCount}");

            if (eatenCount >= targetBananaCount && !hasSpawnedSpecialBanana)
            {
                hasSpawnedSpecialBanana = true;
                // 触发转场前奏：生成强光特殊香蕉
                StartCoroutine(TriggerClimaxSequence());
            }

            // 无论是否触发了红光香蕉，都继续进行有丝分裂分裂与计数
            int mitosisSpawnCount = Mathf.Clamp(eatenCount / 4 + 1, 1, 6);
            for (int i = 0; i < mitosisSpawnCount; i++)
            {
                eatenBanana.SpawnNewBanana();
            }
        }

        /// <summary>
        /// 触发特殊通关香蕉出现序列
        /// </summary>
        private IEnumerator TriggerClimaxSequence()
        {
            Debug.Log("<color=yellow>[Stage1] 达到目标吃蕉数，正在生成特殊红光香蕉...</color>");

            yield return new WaitForSeconds(0.2f);

            // 2. 确定特殊香蕉的生成位置
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (glowingBananaSpawnPoint != null)
            {
                spawnPos = glowingBananaSpawnPoint.position;
                spawnRot = glowingBananaSpawnPoint.rotation;
            }
            else
            {
                // 备用位置：玩家主摄像机面前 3.5 米 (比 GameObject.FindWithTag("Player") 更加百分之百可靠)
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    spawnPos = mainCam.transform.position + mainCam.transform.forward * 3.5f;
                }
                else
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null)
                    {
                        spawnPos = player.transform.position + player.transform.forward * 3.5f + Vector3.up * 1f;
                    }
                    else
                    {
                        spawnPos = new Vector3(0, 1.5f, 0);
                    }
                }
            }

            // 3. 实例化特殊香蕉并动态添加红光提示
            if (bananaPrefab != null)
            {
                GameObject glowingGo = Instantiate(bananaPrefab, spawnPos, spawnRot);
                glowingGo.name = "GlowingBanana_Portal";
                
                // 应用和场景一致的缩放大小，防止由于预制体导入缩放比例偏大导致香蕉过大
                glowingGo.transform.localScale = templateScale;

                // 移除自带的扭曲组件，通关香蕉保持完美形态
                BananaDistorter distorter = glowingGo.GetComponentInChildren<BananaDistorter>();
                if (distorter != null) Destroy(distorter);

                // 配置并确保存在交互属性 (以防因为模板组件被 Destroy 而丢失)
                BananaInteractable bi = glowingGo.GetComponent<BananaInteractable>();
                if (bi == null)
                {
                    bi = glowingGo.AddComponent<BananaInteractable>();
                    bi.bananaPrefab = bananaPrefab;
                    bi.spawnAsInteractive = true;
                }
                bi.isGlowingBanana = true;

                // 防御重力下坠：设置刚体为 kinematic，使其静止漂浮在半空中，防止滚落遗失导致玩家点不到
                Rigidbody rb = glowingGo.GetComponent<Rigidbody>();
                if (rb == null) rb = glowingGo.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                // 动态构建红色光源以散发红光光晕
                GameObject lightGo = new GameObject("RedGlowLight");
                lightGo.transform.SetParent(glowingGo.transform, false);
                lightGo.transform.localPosition = Vector3.zero;

                Light pointLight = lightGo.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.color = Color.red;
                pointLight.intensity = 15f;
                pointLight.range = 6f;
                pointLight.shadows = LightShadows.Soft;

                Debug.Log($"[Stage1] 成功在 {spawnPos} 处生成了特殊红色光晕香蕉！");
                EventBus.RaiseAnnouncement("A strange glowing banana appeared in the distance...");
            }
            else
            {
                Debug.LogError("[Stage1] 未指定 bananaPrefab！无法生成通关香蕉。");
                // 应急防御：如果没填预制体，直接切关
                EventBus.RaiseSceneComplete();
            }
        }

        /// <summary>
        /// 当玩家吃掉红色特殊香蕉时触发
        /// </summary>
        public void EatGlowingBanana()
        {
            Debug.Log("<color=green>[Stage1] 玩家成功吃下红色特殊香蕉！触发转场加载阶段 2...</color>");
            EventBus.RaiseAnnouncement("The glowing banana tasted strange... You lost consciousness.");
            
            // 延迟一秒转场以让玩家看到提示
            Invoke("PerformSceneTransition", 1.2f);
        }

        private void PerformSceneTransition()
        {
            EventBus.RaiseSceneComplete();

            // 额外防御：如果在编辑器中独立运行当前场景（没有通过 0_Bootstrap 启动），则没有 SceneTransitionManager。
            // 为了让单关测试顺畅，我们直接在此处通过 SceneManager 载入下一个场景！
#if UNITY_EDITOR
            if (FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning("[Stage1Controller] 未检测到 SceneTransitionManager（可能是单关测试模式）。正在自动载入 2_God 场景进行单关测试过渡。");
                UnityEngine.SceneManagement.SceneManager.LoadScene("2_God");
            }
#endif
        }
    }
}
