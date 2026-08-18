using System;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 全局事件总线，所有解耦的系统通过它进行通讯。
    /// 用法示例：
    ///   EventBus.RaiseSceneComplete(); // 在任意脚本中发送场景完成信号
    ///   EventBus.OnSceneComplete += manager.HandleSceneComplete; // 在监听端注册回调
    ///   EventBus.RaiseAnnouncement("Scene 1 完成，进入下一场景"); // 发送公告信息
    /// </summary>
    public static class EventBus
    {
        // 当场景完成（当前场景的业务已经结束）时触发。
        public static event Action OnSceneComplete;
        // 当需要广播一段文字公告时触发。
        public static event Action<string> OnAnnouncement;

        /// <summary>
        /// 发射场景完成事件。
        /// </summary>
        public static void RaiseSceneComplete()
        {
            try
            {
                OnSceneComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBus] RaiseSceneComplete error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发射文字公告事件。
        /// </summary>
        public static void RaiseAnnouncement(string message)
        {
            try
            {
                OnAnnouncement?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBus] RaiseAnnouncement error: {ex.Message}");
            }
        }
    }
}
