using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第 4 阶段（4_Modern / 现代异化）总控制器。
    /// 协调鼠标点击，驱动电脑屏幕文字序列和办公室坍塌效果。
    /// 按 P 键可调试跳关到下一场景。
    /// </summary>
    public class Stage4Controller : MonoBehaviour
    {
        public static Stage4Controller Instance { get; private set; }

        [Header("子系统引用（若为空则自动查找）")]
        public MonitorTextController monitorText;
        public OfficeCollapseController officeCollapse;

        [Header("环境初始化")]
        [Tooltip("启动时自动将环境光设为黑色，让 Spotlight 效果更突出")]
        public bool darkenAmbientOnStart = true;

        [Header("转场参数")]
        public float transitionDelay = 2f;
        public string nextSceneName = "5_Contemporary";

        private bool _isTransitioning = false;
        private int _totalClicks = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 自动绑定子系统
            if (monitorText == null) monitorText = FindObjectOfType<MonitorTextController>();
            if (officeCollapse == null) officeCollapse = FindObjectOfType<OfficeCollapseController>();

            // 压暗环境光，让场景聚焦在 Spotlight 上
            if (darkenAmbientOnStart)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                Debug.Log("[Stage4] 环境光已设为黑色，舞台准备就绪。");
            }

            Debug.Log("[Stage4] 关卡初始化完成。点击鼠标左键开始工作...");
        }

        private void Update()
        {
            // 鼠标左键点击交互
            if (Input.GetMouseButtonDown(0))
            {
                OnPlayerClick();
            }

            // P 键调试跳关
            if (Input.GetKeyDown(KeyCode.P) && !_isTransitioning)
            {
                StartCoroutine(TransitionSequence());
            }
        }

        private void OnPlayerClick()
        {
            _totalClicks++;

            // 驱动电脑屏幕文字更新
            monitorText?.OnClick();

            // 驱动所有 Office 向内坍塌
            officeCollapse?.OnClick();

            // 记录行为数据
            if (PlayerBehaviorData.Instance != null)
                PlayerBehaviorData.Instance.AddWork();

            Debug.Log($"[Stage4] 点击次数: {_totalClicks}");
        }

        public void TriggerSceneComplete()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionSequence());
        }

        private IEnumerator TransitionSequence()
        {
            _isTransitioning = true;
            EventBus.RaiseAnnouncement("The walls are closing in. But the work goes on.");
            yield return new WaitForSeconds(transitionDelay);
            PerformSceneTransition();
        }

        private void PerformSceneTransition()
        {
            EventBus.RaiseSceneComplete();
#if UNITY_EDITOR
            if (FindObjectOfType<SceneTransitionManager>() == null)
            {
                Debug.LogWarning($"[Stage4Controller] 单关测试模式：直接加载 {nextSceneName}");
                SceneManager.LoadScene(nextSceneName);
            }
#endif
        }
    }
}
