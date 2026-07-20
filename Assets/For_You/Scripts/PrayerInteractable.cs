using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 神像交互组件 (阶段 2: 神殿/神)
    /// 玩家按下 Q 键跪拜/祈祷时：
    /// 1. 悄悄让持久化单例中的 prayerCount++
    /// 2. 神像无任何回应（冷漠无回应的交互）
    /// </summary>
    public class PrayerInteractable : MonoBehaviour, IInteractable
    {
        [Header("UI 提示")]
        [SerializeField] private string interactHint = "祈祷";

        // 实现 IInteractable 接口的属性
        public string InteractHint => interactHint;

        private void Awake()
        {
            // 防御编程：确保神像本身有一个 Collider 才能被射线检测到
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
                Debug.LogWarning($"[Prayer] '{gameObject.name}' 上没有 Collider！已自动添加 BoxCollider，确保 Q 键射线检测可用。");
            }
        }

        // 实现 IInteractable 接口的方法
        public void Interact()
        {
            // 1. 跨场景行为数据增加
            if (PlayerBehaviorData.Instance != null)
            {
                PlayerBehaviorData.Instance.AddPrayer();
            }
            else
            {
                Debug.LogWarning("[Prayer] 找不到 PlayerBehaviorData 持久化实例！无法进行祈祷计数。");
            }

            // 2. 没有任何物理、音效或动画回应 (静默完成)
            Debug.Log("[Prayer] 玩家进行了一次祈祷，但神像毫无反应...");
        }
    }
}
