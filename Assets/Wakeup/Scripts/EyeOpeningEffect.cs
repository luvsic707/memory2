using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 睁眼效果控制脚本 (解耦插拔式设计)
    /// 实现黑屏渐亮、重度模糊到清晰、以及眼皮开合眨眼动画。
    /// 具有运行时自动补齐 UI 和 Volume 的防御设计。
    /// </summary>
    public class EyeOpeningEffect : MonoBehaviour
    {
        [Header("时间与曲线设置")]
        [Tooltip("睁眼过程的总持续时间（秒）")]
        public float duration = 5.0f;

        [Tooltip("眼皮开合动画曲线 (0=闭眼, 1=完全睁开)")]
        public AnimationCurve eyelidCurve;

        [Tooltip("画面清晰与亮度曲线 (0=全黑重度模糊, 1=完全亮起清晰)")]
        public AnimationCurve visualClearCurve;

        [Header("引用设置 (选填，不填则运行时自动生成/寻找)")]
        public Volume postProcessVolume;
        public CanvasGroup blackOverlay;
        public RectTransform upperEyelid;
        public RectTransform lowerEyelid;

        // 动效完成时的回调
        private System.Action _onCompleteCallback;
        private bool _isPlaying = false;

        // 景深后处理的原始状态备份
        private bool _hadDof = false;
        private bool _originalDofActive = false;
        private float _originalDofFocusDistance = 10f;
        private DepthOfField _dofComponent;

        // 自动生成的 UI Canvas
        private GameObject _generatedCanvasObj;

        private void Awake()
        {
            // 防御机制：如果曲线为空，初始化默认的双闪/眨眼曲线
            InitializeDefaultCurves();

            // 防御机制：自动补齐 UI 元素
            EnsureUIElements();

            // 防御机制：寻找场景中的 Volume
            EnsureVolume();
        }

        private void OnEnable()
        {
            NarrationAnnouncer.OnSceneEventTriggered += HandleSceneEvent;
        }

        private void OnDisable()
        {
            NarrationAnnouncer.OnSceneEventTriggered -= HandleSceneEvent;
        }

        private void HandleSceneEvent(string eventId)
        {
            if (eventId == "StartEyeOpening")
            {
                PlayEffect(() =>
                {
                    Debug.Log("[EyeOpeningEffect] 睁眼动效播放完毕，向全局事件总线广播: EyeOpeningComplete");
                    NarrationAnnouncer.TriggerSceneEvent("EyeOpeningComplete");
                });
            }
        }

        private void InitializeDefaultCurves()
        {
            if (eyelidCurve == null || eyelidCurve.keys.Length == 0)
            {
                eyelidCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),       // 闭眼
                    new Keyframe(0.15f, 0.3f),  // 眯眼微开 1
                    new Keyframe(0.25f, 0f),    // 闭上
                    new Keyframe(0.45f, 0.6f),  // 眯眼微开 2
                    new Keyframe(0.6f, 0.1f),   // 快闭上
                    new Keyframe(0.85f, 1.0f),  // 彻底睁开
                    new Keyframe(1.0f, 1.0f)
                );
            }

            if (visualClearCurve == null || visualClearCurve.keys.Length == 0)
            {
                visualClearCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),       // 全黑模糊
                    new Keyframe(0.15f, 0.15f), // 微亮微糊 1
                    new Keyframe(0.25f, 0.02f), // 变暗
                    new Keyframe(0.45f, 0.45f), // 微亮微糊 2
                    new Keyframe(0.6f, 0.08f),  // 变暗
                    new Keyframe(0.85f, 0.85f), // 基本看清
                    new Keyframe(1.0f, 1.0f)    // 完全看清
                );
            }
        }

        private void EnsureUIElements()
        {
            // 如果三个核心 UI 中有任何一个未指定，就自动生成一整套 UI
            if (blackOverlay == null || upperEyelid == null || lowerEyelid == null)
            {
                _generatedCanvasObj = new GameObject("EyeOpeningCanvas");
                Canvas canvas = _generatedCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999; // 保证在最上层覆盖所有 UI

                UnityEngine.UI.CanvasScaler scaler = _generatedCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                _generatedCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // 1. 全屏黑色背景
                GameObject overlayObj = new GameObject("BlackOverlay");
                overlayObj.transform.SetParent(_generatedCanvasObj.transform, false);
                var overlayImage = overlayObj.AddComponent<UnityEngine.UI.Image>();
                overlayImage.color = Color.black;

                var overlayRect = overlayObj.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.sizeDelta = Vector2.zero;

                blackOverlay = overlayObj.AddComponent<CanvasGroup>();
                blackOverlay.alpha = 1f;

                // 2. 上眼皮
                GameObject upperObj = new GameObject("UpperEyelid");
                upperObj.transform.SetParent(_generatedCanvasObj.transform, false);
                var upperImage = upperObj.AddComponent<UnityEngine.UI.Image>();
                upperImage.color = Color.black;

                upperEyelid = upperObj.GetComponent<RectTransform>();
                upperEyelid.anchorMin = new Vector2(0, 1);
                upperEyelid.anchorMax = new Vector2(1, 1);
                upperEyelid.pivot = new Vector2(0.5f, 1f);
                upperEyelid.sizeDelta = new Vector2(0, 600); // 覆盖半屏多一点（重叠）
                upperEyelid.anchoredPosition = Vector2.zero;

                // 3. 下眼皮
                GameObject lowerObj = new GameObject("LowerEyelid");
                lowerObj.transform.SetParent(_generatedCanvasObj.transform, false);
                var lowerImage = lowerObj.AddComponent<UnityEngine.UI.Image>();
                lowerImage.color = Color.black;

                lowerEyelid = lowerObj.GetComponent<RectTransform>();
                lowerEyelid.anchorMin = new Vector2(0, 0);
                lowerEyelid.anchorMax = new Vector2(1, 0);
                lowerEyelid.pivot = new Vector2(0.5f, 0f);
                lowerEyelid.sizeDelta = new Vector2(0, 600);
                lowerEyelid.anchoredPosition = Vector2.zero;

                Debug.Log("[EyeOpeningEffect] 运行时成功自动生成了眨眼 UI 遮罩组件。");
            }
        }

        private void EnsureVolume()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = FindAnyObjectByType<Volume>();
            }

            if (postProcessVolume != null)
            {
                if (postProcessVolume.profile.TryGet<DepthOfField>(out _dofComponent))
                {
                    _hadDof = true;
                    _originalDofActive = _dofComponent.active;
                    _originalDofFocusDistance = _dofComponent.focusDistance.value;
                    Debug.Log($"[EyeOpeningEffect] 自动绑定了 Volume: {postProcessVolume.name}，并检测到了 DepthOfField 配置。");
                }
                else
                {
                    Debug.LogWarning($"[EyeOpeningEffect] 找到了 Volume: {postProcessVolume.name}，但其 Profile 中没有启用 Depth of Field！模糊效果将无法展示。");
                }
            }
            else
            {
                Debug.LogWarning("[EyeOpeningEffect] 场景中未找到任何 Post-process Volume！模糊效果将无法展示。");
            }
        }

        /// <summary>
        /// 外部接口：开始播放睁眼效果
        /// </summary>
        /// <param name="onComplete">动画完成后的回调</param>
        public void PlayEffect(System.Action onComplete)
        {
            if (_isPlaying) return;
            _onCompleteCallback = onComplete;
            StartCoroutine(AnimateEyeOpening());
        }

        private IEnumerator AnimateEyeOpening()
        {
            _isPlaying = true;
            Debug.Log("[EyeOpeningEffect] 睁眼动画开始播放...");

            // 确保开局时绝对全黑和闭眼状态
            SetEyelidState(0f);
            SetVisualClearState(0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);

                // 评估当前时间点的动画曲线数值
                float eyelidOpenRatio = eyelidCurve.Evaluate(p);
                float visualClearRatio = visualClearCurve.Evaluate(p);

                // 更新眼皮和模糊亮度状态
                SetEyelidState(eyelidOpenRatio);
                SetVisualClearState(visualClearRatio);

                yield return null;
            }

            // 确保动画结束时状态完全恢复
            SetEyelidState(1f);
            SetVisualClearState(1f);

            Debug.Log("[EyeOpeningEffect] 睁眼动画播放完成！");
            _isPlaying = false;

            // 还原景深后处理的原始配置（不破坏后续游戏内的后处理表现）
            RestoreOriginalDofSettings();

            // 彻底销毁生成的 UI 物体，释放资源
            if (_generatedCanvasObj != null)
            {
                Destroy(_generatedCanvasObj);
            }

            // 触发完成回调
            _onCompleteCallback?.Invoke();
        }

        private void SetEyelidState(float openRatio)
        {
            if (upperEyelid != null && lowerEyelid != null)
            {
                float upperHeight = upperEyelid.sizeDelta.y;
                float lowerHeight = lowerEyelid.sizeDelta.y;

                // openRatio = 0 表示全闭 (偏移量为 0)
                // openRatio = 1 表示完全睁开 (眼皮挪出屏幕外)
                // 加 50 像素确保完全移出，不留黑边
                upperEyelid.anchoredPosition = new Vector2(0, openRatio * (upperHeight + 50f));
                lowerEyelid.anchoredPosition = new Vector2(0, -openRatio * (lowerHeight + 50f));
            }
        }

        private void SetVisualClearState(float clearRatio)
        {
            // 1. 全屏黑色遮罩透明度 (clearRatio = 1 时 alpha = 0)
            if (blackOverlay != null)
            {
                blackOverlay.alpha = 1f - clearRatio;
            }

            // 2. 调节景深焦距 (clearRatio = 0 时极度模糊 focus = 0.05m，clearRatio = 1 时清晰 focus = 10m)
            if (_hadDof && _dofComponent != null)
            {
                _dofComponent.active = true;
                // 利用对数或平滑插值，让对焦过程有大范围模糊迅速拉清的感觉
                float focusDistance = Mathf.Lerp(0.05f, 10.0f, clearRatio);
                _dofComponent.focusDistance.Override(focusDistance);
            }
        }

        private void RestoreOriginalDofSettings()
        {
            if (_hadDof && _dofComponent != null)
            {
                _dofComponent.active = _originalDofActive;
                _dofComponent.focusDistance.Override(_originalDofFocusDistance);
            }
        }
    }
}
