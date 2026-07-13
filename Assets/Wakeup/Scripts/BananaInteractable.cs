using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互与堆叠生成组件 (重复交互、无奖励)
    /// 玩家按下 Q 键交互时：
    /// 1. 悄悄让持久化单例中的 bananaCount++
    /// 2. 在上方随机位置实例化一个新香蕉（带物理碰撞和重力，越堆越多）
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

        [Header("UI 提示")]
        [SerializeField] private string interactHint = "吃香蕉";

        // 实现 IInteractable 接口的属性
        public string InteractHint => interactHint;

        private void Awake()
        {
            // 防御编程：确保香蕉本身有一个 Collider 才能被射检测到
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
                Debug.LogWarning($"[Banana] '{gameObject.name}' 上没有 Collider！已自动添加 BoxCollider，确保 Q 键射线检测可用。");
            }
        }

        // 实现 IInteractable 接口的方法
        public void Interact()
        {
            // 1. 跨场景行为数据增加
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddBanana();
            }
            else
            {
                Debug.LogWarning("[Banana] 找不到 PlayerBehaviorData 持久化实例！无法进行交互计数。");
            }

            // 2. 视觉表现：物理生成新香蕉
            SpawnNewBanana();
        }

        private void SpawnNewBanana()
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
    }
}
