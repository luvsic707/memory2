using UnityEngine;
using System.Collections;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 高性能门显隐控制器 v2
    /// 
    /// 策略：
    ///   - Renderer：一帧内全部切换（轻量操作）
    ///   - Collider：分帧逐个开启，避免物理引擎单帧烘焙碰撞网格的 CPU 峰值
    /// 
    /// 使用方式：
    ///   1. 挂载到门的根 GameObject 上
    ///   2. startHidden = true（默认隐藏）
    ///   3. MemoryItem 的 OnCollected 事件中调用 FastDoorController.Show()
    /// </summary>
    public class FastDoorController : MonoBehaviour
    {
        [Header("初始状态")]
        [Tooltip("勾选后，Awake 时自动隐藏")]
        public bool startHidden = true;

        [Header("碰撞体分帧设置")]
        [Tooltip("每帧开启几个碰撞体（值越小越平滑，但完成越慢）")]
        [Range(1, 10)]
        public int collidersPerFrame = 2;

        // 缓存
        private Renderer[] _renderers;
        private Collider[]  _colliders;
        private bool _isVisible;
        private Coroutine _activeCoroutine;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders  = GetComponentsInChildren<Collider>(true);

            Debug.Log($"[FastDoor] 缓存完成 | Renderers: {_renderers.Length}, Colliders: {_colliders.Length}");

            if (startHidden)
            {
                SetRenderersEnabled(false);
                SetCollidersEnabled(false);
                _isVisible = false;
            }
            else
            {
                _isVisible = true;
            }
        }

        /// <summary>
        /// 显示门 —— 渲染器即时开启，碰撞体分帧开启
        /// 可直接拖入 UnityEvent
        /// </summary>
        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            // 渲染器：立即全部开启（无性能问题）
            SetRenderersEnabled(true);

            // 碰撞体：分帧逐步开启
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(EnableCollidersGradually());

            Debug.Log("[FastDoor] 门开始显示");
        }

        /// <summary>
        /// 隐藏门 —— 全部立即关闭（关闭操作开销极小）
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }

            SetRenderersEnabled(false);
            SetCollidersEnabled(false);
            Debug.Log("[FastDoor] 门已隐藏");
        }

        public void Toggle()
        {
            if (_isVisible) Hide(); else Show();
        }

        public bool IsVisible => _isVisible;

        // ── 内部方法 ───────────────────────────

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].enabled = enabled;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _colliders.Length; i++)
                _colliders[i].enabled = enabled;
        }

        /// <summary>
        /// 每帧只开启 collidersPerFrame 个碰撞体，把物理开销分散到多帧
        /// </summary>
        private IEnumerator EnableCollidersGradually()
        {
            int count = 0;
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = true;
                count++;

                if (count >= collidersPerFrame)
                {
                    count = 0;
                    yield return null; // 等待下一帧
                }
            }

            _activeCoroutine = null;
            Debug.Log($"[FastDoor] 全部碰撞体开启完毕 ({_colliders.Length} 个)");
        }
    }
}