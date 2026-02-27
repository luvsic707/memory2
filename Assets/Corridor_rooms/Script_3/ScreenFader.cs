using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace TheLastCompact.Core
{
    /// <summary>
    /// 全屏渐变遮罩工具（单例）
    /// 运行时自动创建 Canvas + 黑色全屏 Image，无需手动配置
    /// 
    /// 用法：ScreenFader.Instance.FadeOutAndIn(onBlack, duration)
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        private Image _fadeImage;
        private Canvas _canvas;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                CreateFadeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 渐黑 → 执行回调(黑屏中) → 等几帧 → 渐入
        /// </summary>
        /// <param name="onBlack">黑屏时执行的操作（如激活门）</param>
        /// <param name="fadeOutTime">渐黑时间</param>
        /// <param name="holdFrames">黑屏期间等待的帧数（让物理引擎消化）</param>
        /// <param name="fadeInTime">渐入时间</param>
        public void FadeOutAndIn(Action onBlack, float fadeOutTime = 0.4f, int holdFrames = 3, float fadeInTime = 0.6f)
        {
            StartCoroutine(DoFadeSequence(onBlack, fadeOutTime, holdFrames, fadeInTime));
        }

        /// <summary>
        /// 当前是否正在过渡中
        /// </summary>
        public bool IsFading { get; private set; }

        // ── 内部实现 ───────────────────────────

        private void CreateFadeUI()
        {
            // 创建 Canvas
            GameObject canvasObj = new GameObject("ScreenFader_Canvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // 最顶层

            canvasObj.AddComponent<CanvasScaler>();

            // 创建全屏黑色 Image
            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            _fadeImage = imgObj.AddComponent<Image>();
            _fadeImage.color = new Color(0, 0, 0, 0); // 初始透明
            _fadeImage.raycastTarget = false;

            // 撑满全屏
            RectTransform rt = _fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private IEnumerator DoFadeSequence(Action onBlack, float fadeOutTime, int holdFrames, float fadeInTime)
        {
            IsFading = true;

            // 1. 渐黑
            yield return StartCoroutine(Fade(0f, 1f, fadeOutTime));

            // 2. 黑屏中执行回调
            onBlack?.Invoke();

            // 3. 等待几帧让物理/渲染引擎消化
            for (int i = 0; i < holdFrames; i++)
                yield return null;

            // 4. 渐入
            yield return StartCoroutine(Fade(1f, 0f, fadeInTime));

            IsFading = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            Color c = _fadeImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                c.a = Mathf.Lerp(from, to, elapsed / duration);
                _fadeImage.color = c;
                yield return null;
            }

            c.a = to;
            _fadeImage.color = c;
        }
    }
}
