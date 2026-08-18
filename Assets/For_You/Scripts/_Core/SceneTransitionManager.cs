using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 场景切换管理器（For You 支线）。
    /// 通过 EventBus 收到场景完成信号后，依据 <see cref="SceneSequenceConfig"/> 中配置的顺序
    /// 自动加载下一场景，实现完全 data‑driven、event‑driven、解耦的传送机制。
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("Data‑driven 配置")]
        [Tooltip("为 'For You' 支线配置的有序场景列表（需与 Build Settings 中的场景名称保持一致）")]
        public SceneSequenceConfig sequenceConfig;

        [Header("调试选项")]
        [Tooltip("是否启用按下 P 键切换至下一关的调试快捷键")]
        public bool enableDebugShortcut = true;

        private int currentIndex = -1; // Awake 时设为 0 并加载首场景

        private void Update()
        {
            if (enableDebugShortcut && Input.GetKeyDown(KeyCode.P))
            {
                EventBus.RaiseAnnouncement("[Debug] 玩家按下了 P 键，正在手动跳过当前关卡...");
                EventBus.RaiseSceneComplete();
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            EventBus.OnSceneComplete += HandleSceneComplete;
            EventBus.OnAnnouncement += HandleAnnouncement;

            if (sequenceConfig == null || sequenceConfig.sceneNames.Count == 0)
            {
                Debug.LogError("[SceneTransitionManager] 未配置 SceneSequenceConfig 或场景列表为空。无法进行自动场景切换。");
                return;
            }

            LoadNextScene();
        }

        private void OnDestroy()
        {
            EventBus.OnSceneComplete -= HandleSceneComplete;
            EventBus.OnAnnouncement -= HandleAnnouncement;
        }

        private void HandleSceneComplete()
        {
            Debug.Log($"[SceneTransitionManager] 收到场景完成信号，当前索引 {currentIndex}，准备加载下一个场景。");
            LoadNextScene();
        }

        private void HandleAnnouncement(string message)
        {
            // 只做日志，UI 订阅者可自行展示。
            Debug.Log($"[Announcement] {message}");
        }

        private void LoadNextScene()
        {
            if (sequenceConfig == null || sequenceConfig.sceneNames.Count == 0)
            {
                Debug.LogWarning("[SceneTransitionManager] 场景序列未配置，跳过加载。");
                return;
            }

            currentIndex = (currentIndex + 1) % sequenceConfig.sceneNames.Count;
            string nextSceneName = sequenceConfig.sceneNames[currentIndex];

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogError($"[SceneTransitionManager] 第 {currentIndex} 位场景名称为空，加载被中止。");
                return;
            }

            Debug.Log($"[SceneTransitionManager] 正在加载场景 '{nextSceneName}' (index {currentIndex})");
            SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        }
    }
}
