using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互与堆叠生成组件 (重复交互、无奖励)
    /// 玩家按下 Q 键交互时：
    /// 1. 判定是否为特殊的通关香蕉，是则直接触发通关流程。
    /// 2. 否则，增加持久化单例中的 bananaCount++。
    /// 3. 在上方随机位置实例化新香蕉，并从 Stage1Controller 继承当前的网格扭曲因子。
    /// </summary>
    public class BananaInteractable : MonoBehaviour, IInteractable
    {
        [Header("香蕉生成配置")]
        [Tooltip("生成的香蕉预制体（必须包含 Rigidbody 和 Collider 以便物理堆叠）")]
        public GameObject bananaPrefab;

        [Tooltip("生成新香蕉的范围半径（水平偏移量）")]
        public float spawnRadius = 0.4f;

        [Tooltip("生成时向上偏移的高度，促使香蕉自然坠落堆叠")]
        public float spawnHeightOffset = 1.2f;

        [Tooltip("新生成的香蕉是否也是可交互的？（开启后可以疯狂套娃点击）")]
        public bool spawnAsInteractive = true;

        [Header("Stage 1 专属特殊香蕉配置")]
        [Tooltip("该香蕉是否为用于场景切换的特殊红光香蕉")]
        public bool isGlowingBanana = false;

        [Header("音效配置 (Inspector 拖拽)")]
        [Tooltip("吃香蕉/咀嚼音效文件 (拖入你的 .mp3/.wav/.ogg 文件)")]
        public AudioClip eatSoundClip;

        [Tooltip("吃香蕉音效播放音量")]
        [Range(0f, 1f)] public float eatSoundVolume = 0.85f;

        [Header("UI 提示")]
        [SerializeField] private string interactHint = "吃香蕉";

        // 实现 IInteractable 接口的属性
        public string InteractHint => isGlowingBanana ? "吃下强光香蕉" : interactHint;

        private void Awake()
        {
            AutoFitCollider();
        }

        private void AutoFitCollider()
        {
            // 如果自身或子物体已经有任何 Collider，不再添加，确保继承原有碰撞体
            if (GetComponentInChildren<Collider>(true) != null) return;

            BoxCollider box = gameObject.AddComponent<BoxCollider>();

            // 获取所有子物体渲染器的包围盒，计算出总大小，防止 FBX 嵌套导致没有碰撞盒
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                box.size = Vector3.one;
                box.center = Vector3.zero;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // 将世界坐标下的包围盒中心和大小转换为本地坐标系（消除父级缩放影响）
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);

            localSize.x = Mathf.Abs(localSize.x);
            localSize.y = Mathf.Abs(localSize.y);
            localSize.z = Mathf.Abs(localSize.z);

            box.center = localCenter;
            box.size = localSize;
            Debug.Log($"[Banana] 自动适配了 BoxCollider！本地中心: {box.center}，本地大小: {box.size}");
        }

        // 实现 IInteractable 接口的方法
        public void Interact()
        {
            // 自动确保场景里存在第一视角手臂管理器
            if (FirstPersonArmController.Instance == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.gameObject.AddComponent<FirstPersonArmController>();
                }
            }

            // 触发第一视角手部伸出抓取 ➔ 吃蕉动作
            if (FirstPersonArmController.Instance != null)
            {
                FirstPersonArmController.Instance.PlayGrabAndEatMotion(transform.position, () =>
                {
                    ExecuteBananaEatLogic();
                });
            }
            else
            {
                ExecuteBananaEatLogic();
            }
        }

        private void ExecuteBananaEatLogic()
        {
            // 播放吃香蕉音效 (若没有拖入 AudioClip，则播放默认音效)
            PlayEatSoundEffect();

            // 特殊香蕉：直接吃掉通关
            if (isGlowingBanana)
            {
                Debug.Log("[Banana] 玩家吃下了特殊的通关香蕉！");
                if (Stage1Controller.Instance != null)
                {
                    Stage1Controller.Instance.EatGlowingBanana();
                }
                else
                {
                    // 优雅降级：直接触发通关
                    EventBus.RaiseAnnouncement("You ate the glowing banana. Loading next stage...");
                    EventBus.RaiseSceneComplete();
                }
                return;
            }

            // 1. 跨场景行为数据增加
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddBanana();
            }
            else
            {
                Debug.LogWarning("[Banana] 找不到 PlayerBehaviorData 持久化实例！无法进行交互计数。");
            }

            // 2. 通知 Stage1Controller 累积关卡进度并执行有丝分裂分裂
            if (Stage1Controller.Instance != null)
            {
                Stage1Controller.Instance.OnBananaEaten(this);
            }
            else
            {
                // 降级模式：若无 Stage1Controller 则只进行默认的分裂生成
                SpawnNewBanana();
            }
        }

        public void SpawnNewBanana()
        {
            if (bananaPrefab == null)
            {
                Debug.LogError($"[Banana] '{gameObject.name}' 未指定 bananaPrefab！无法生成新香蕉。");
                return;
            }

            // 在当前物体上方加上随机偏移生成
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                spawnHeightOffset,
                Random.Range(-spawnRadius, spawnRadius)
            );
            Vector3 spawnPosition = transform.position + randomOffset;
            Quaternion spawnRotation = Random.rotation;

            GameObject newBanana = Instantiate(bananaPrefab, spawnPosition, spawnRotation);
            newBanana.name = "SpawningBanana_Prop";

            // 继承父香蕉（场景中缩放正确）的世界缩放，并随着扭曲度增加而变大
            float distFactor = Stage1Controller.Instance != null ? Stage1Controller.Instance.GetCurrentDistortionFactor() : 0f;
            newBanana.transform.localScale = transform.lossyScale * (1f + distFactor * 0.35f);

            // 自动注入扭曲变形组件并赋予当前关卡的扭曲因子
            if (Stage1Controller.Instance != null)
            {
                BananaDistorter distorter = newBanana.GetComponent<BananaDistorter>();
                if (distorter == null)
                {
                    distorter = newBanana.AddComponent<BananaDistorter>();
                }
                distorter.distortionFactor = Stage1Controller.Instance.GetCurrentDistortionFactor();

            }

            // 如果新生成的香蕉不需要可交互，剥离该交互组件
            if (!spawnAsInteractive)
            {
                var interactComponent = newBanana.GetComponent<BananaInteractable>();
                if (interactComponent != null)
                {
                    Destroy(interactComponent);
                }
            }

            Debug.Log($"[Banana] 成功在 {spawnPosition} 处生成了一只新物理香蕉，重力下落堆叠。");
        }
        /// <summary>
        /// 播放吃香蕉与咀嚼音效 (自动支持自定义拖拽音效与随机音高变调)
        /// </summary>
        private void PlayEatSoundEffect()
        {
            if (eatSoundClip != null)
            {
                AudioSource.PlayClipAtPoint(eatSoundClip, transform.position, eatSoundVolume);
            }
            else
            {
                // 自动防御备用：在全局 AudioListener/Camera 播放吃香蕉音效
                Camera mainCam = Camera.main;
                Vector3 pos = mainCam != null ? mainCam.transform.position : transform.position;
                
                // 如果在 ArmController 里设置了通用音效，也进行联动
                if (FirstPersonArmController.Instance != null && FirstPersonArmController.Instance.defaultEatSound != null)
                {
                    AudioSource.PlayClipAtPoint(FirstPersonArmController.Instance.defaultEatSound, pos, eatSoundVolume);
                }
            }
        }
    }
}
