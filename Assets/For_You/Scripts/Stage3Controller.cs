using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第 3 阶段（3_Rock / 莫比乌斯西西弗斯）总控制器
    /// 目前测试阶段：按 P 键直接触发转场到第 4 阶段 (4_Modern)。
    /// 后续可在 TriggerSceneComplete() 前插入开门动画、特殊叙事演出等完整触发逻辑。
    /// </summary>
    public class Stage3Controller : MonoBehaviour
    {
        public static Stage3Controller Instance { get; private set; }

        [Header("转场参数")]
        [Tooltip("转场前的等待时间（秒），用于播放声效/淡出动画")]
        public float transitionDelay = 1.5f;

        [Tooltip("下一关场景名称，在没有 SceneTransitionManager 的单关测试模式中使用")]
        public string nextSceneName = "4_Modern";

        private bool _isTransitioning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // 调试快捷键：按 P 键直接触发转场（测试阶段用）
            if (Input.GetKeyDown(KeyCode.P) && !_isTransitioning)
            {
                Debug.Log("[Stage3] 按下 P 键，触发转场跳过至下一关...");
                StartCoroutine(TransitionSequence());
            }
        }

        /// <summary>
        /// 公共接口：供未来的大门交互、叙事脚本等调用以触发转场
        /// </summary>
        public void TriggerSceneComplete()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionSequence());
        }

        private IEnumerator TransitionSequence()
        {
            _isTransitioning = true;

            // 通告（可选）
            EventBus.RaiseAnnouncement("The loop has no end. But you can still walk away.");

            yield return new WaitForSeconds(transitionDelay);

            PerformSceneTransition();
        }

        private void PerformSceneTransition()
        {
            // 通知全局系统（有 SceneTransitionManager 时走正常流程）
            EventBus.RaiseSceneComplete();

            // 编辑器单关测试兜底：没有 Bootstrap 时直接用 SceneManager 加载
#if UNITY_EDITOR
            if (FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning($"[Stage3Controller] 未检测到 SceneTransitionManager，单关测试模式：直接加载 {nextSceneName}");
                SceneManager.LoadScene(nextSceneName);
            }
#endif
        }
    }
}
