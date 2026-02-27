using UnityEngine;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 统一光标管理服务
    /// 所有脚本禁止直接调用 Cursor.lockState / Cursor.visible
    /// 必须通过此服务统一管理，确保光标状态一致
    /// </summary>
    public static class CursorService
    {
        /// <summary>锁定光标并隐藏 (游戏中 FPS 模式)</summary>
        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>解锁光标并显示 (菜单/对话/阅读模式)</summary>
        public static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>查询光标是否已锁定</summary>
        public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;
    }
}
