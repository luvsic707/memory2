using System.Collections;
using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 大猩猩开场低吼与字幕翻译控制器 (解耦事件驱动版)
    /// 配合“其实早在公元前18000年...”迷因设计的沉浸式音效与翻译字幕展现。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ApePrologueController : MonoBehaviour
    {
        [Header("事件配置")]
        [Tooltip("触发该效果的广播事件 ID")]
        public string triggerEventId = "StartApePrologue";

        [Tooltip("播放完毕后向系统广播的事件 ID")]
        public string completeEventId = "ApePrologueComplete";

        [Header("声音设置")]
        [Tooltip("猩猩低吼/嚎叫音效（如果不填，则自动使用 AudioSource 上的音频）")]
        public AudioClip growlAudio;

        [Header("字幕设置")]
        [Tooltip("翻译字幕文本")]
        [TextArea(3, 5)]
        public string translationText = "其实早在公元前18,000年，我们就认识了\n我们还一起摘不拿拿，只是你忘了";

        [Tooltip("每字显示的打字机速度（秒/字），如果为 0 则直接显示整行")]
        public float typewriterSpeed = 0.08f;

        [Tooltip("字幕显示的总持续时间（秒）")]
        public float displayDuration = 6.0f;

        [Header("UI 引用 (可选)")]
        [Tooltip("绑定已有的 TextMeshProUGUI，如果不填则在运行时自动生成电影级字幕 UI")]
        public TextMeshProUGUI subtitleText;

        private AudioSource _audioSource;
        private GameObject _generatedCanvasObj;
        private bool _isPlaying = false;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
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
            if (eventId == triggerEventId)
            {
                StartPrologueEffect();
            }
        }

        public void StartPrologueEffect()
        {
            if (_isPlaying) return;
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            _isPlaying = true;
            Debug.Log("[ApePrologue] 开始播放低吼声与翻译字幕...");

            // 1. 自动生成电影级字幕 UI (如果用户没有手动绑定)
            EnsureSubtitleUI();

            if (subtitleText != null)
            {
                subtitleText.text = "";
                subtitleText.gameObject.SetActive(true);
            }

            // 2. 播放猩猩低吼音效
            if (growlAudio != null)
            {
                _audioSource.PlayOneShot(growlAudio);
            }
            else if (_audioSource.clip != null)
            {
                _audioSource.Play();
            }
            else
            {
                Debug.LogWarning("[ApePrologue] 没有分配 growlAudio 并且 AudioSource 上没有 AudioClip！将不播放声音。");
            }

            // 3. 打字机式展示翻译字幕
            if (subtitleText != null)
            {
                if (typewriterSpeed > 0f)
                {
                    string currentText = "";
                    for (int i = 0; i < translationText.Length; i++)
                    {
                        currentText += translationText[i];
                        subtitleText.text = currentText;
                        yield return new WaitForSeconds(typewriterSpeed);
                    }
                }
                else
                {
                    subtitleText.text = translationText;
                }
            }

            // 4. 等待指定的展示时长 (扣除打字机显示所用掉的时间，保证总展示时长一致)
            float elapsed = typewriterSpeed > 0f ? (translationText.Length * typewriterSpeed) : 0f;
            float waitRemaining = Mathf.Max(0.5f, displayDuration - elapsed);
            yield return new WaitForSeconds(waitRemaining);

            // 5. 渐隐字幕
            if (subtitleText != null)
            {
                float fadeDuration = 0.8f;
                float fadeElapsed = 0f;
                Color originalColor = subtitleText.color;

                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
                    subtitleText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    yield return null;
                }

                subtitleText.text = "";
                subtitleText.color = originalColor; // 恢复原始颜色
                subtitleText.gameObject.SetActive(false);
            }

            // 6. 销毁生成的 UI 物体，释放资源
            if (_generatedCanvasObj != null)
            {
                Destroy(_generatedCanvasObj);
            }

            Debug.Log("[ApePrologue] 低吼与字幕播放完成！向全局事件总线广播: " + completeEventId);
            _isPlaying = false;

            // 广播完成事件，用于通知剧情管理器（Manager）继续后面的“起床/获得控制权”流程
            NarrationAnnouncer.TriggerSceneEvent(completeEventId);
        }

        private void EnsureSubtitleUI()
        {
            if (subtitleText == null)
            {
                _generatedCanvasObj = new GameObject("ApeSubtitleCanvas");
                Canvas canvas = _generatedCanvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // 确保在最上层展示

                UnityEngine.UI.CanvasScaler scaler = _generatedCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                GameObject textObj = new GameObject("SubtitleText");
                textObj.transform.SetParent(_generatedCanvasObj.transform, false);

                subtitleText = textObj.AddComponent<TextMeshProUGUI>();
                
                // 电影/迷因字幕风格：白色文字，居中偏下，带有黑边阴影
                subtitleText.alignment = TextAlignmentOptions.Center;
                subtitleText.fontSize = 45f;
                subtitleText.color = Color.white;
                
                // 开启轮廓黑边以确保任何背景下都清晰可辨
                subtitleText.outlineWidth = 0.22f;
                subtitleText.outlineColor = Color.black;

                // 底部定位居中，留出 150 像素边距
                RectTransform rect = textObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.1f, 0.05f);
                rect.anchorMax = new Vector2(0.9f, 0.2f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                Debug.Log("[ApePrologue] 运行时成功自动创建了电影级字幕 UI 渲染器。");
            }
        }
    }
}
