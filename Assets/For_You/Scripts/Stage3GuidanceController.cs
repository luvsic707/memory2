using System.Collections;
using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (莫比乌斯西西弗斯 3_Rock) 剧情通告与动作引导控制器
    /// 流程：
    /// 1. 玩家正常按 Q 推动巨石/滚球。
    /// 2. 推动次数达到 requiredPushCount（默认 10 次）后，出现哲理剧情字幕与动作引导：
    ///    "你一次次推石，循环永无止境... 按 [E] 键停止推动，打破循环。"
    /// 3. 玩家按下 E 键 ➔ 巨石定格消散，转场进入 Stage 4 (4_Modern) 办公室！
    /// </summary>
    public class Stage3GuidanceController : MonoBehaviour
    {
        public static Stage3GuidanceController Instance { get; private set; }

        [Header("触发门槛")]
        [Tooltip("玩家按 Q 推动多少次后，出现剧情字幕与 E 键解脱引导")]
        public int requiredPushCount = 10;

        [Header("剧情字幕与提示文本")]
        [TextArea]
        public string announcementText = "You pushed the boulder again and again. The loop has no end.";

        [TextArea]
        public string actionPromptText = "💡 按 [E] 键停止推动，打破循环";

        [Header("转场延迟")]
        public float exitDelay = 1.5f;

        private int _currentPushes = 0;
        private bool _hasGuided = false;
        private bool _hasExited = false;
        private GUIStyle _promptStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void Update()
        {
            if (_hasExited) return;

            // 1. 监测按 Q 或点击推球
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(0))
            {
                _currentPushes++;
                CheckPushThreshold();
            }

            // 2. 一旦剧情引导开启，监测 E 键解脱
            if (_hasGuided && Input.GetKeyDown(KeyCode.E))
            {
                TriggerBreakLoop();
            }
        }

        private void CheckPushThreshold()
        {
            if (!_hasGuided && _currentPushes >= requiredPushCount)
            {
                _hasGuided = true;
                Debug.Log($"[Stage3Guidance] 推动达到 {requiredPushCount} 次！显示剧情字幕与 E 键动作引导。");
                
                // 广播剧情字幕通告
                EventBus.RaiseAnnouncement(announcementText);
            }
        }

        private void TriggerBreakLoop()
        {
            if (_hasExited) return;
            _hasExited = true;

            Debug.Log("<color=cyan>[Stage3Guidance] 玩家按下了 E 键，停止推动，打破莫比乌斯循环！</color>");

            // 1. 广播解脱字幕
            EventBus.RaiseAnnouncement("You stopped pushing. The infinite loop unraveled.");

            // 2. 定格并消散场上的滚球/巨石
            BallOnMobius ball = FindObjectOfType<BallOnMobius>();
            if (ball != null)
            {
                ball.enabled = false;
                StartCoroutine(DissolveObjectRoutine(ball.gameObject));
            }

            SisyphusRock rock = FindObjectOfType<SisyphusRock>();
            if (rock != null)
            {
                Rigidbody rb = rock.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                StartCoroutine(DissolveObjectRoutine(rock.gameObject));
            }

            // 3. 延迟后触发切关
            StartCoroutine(PerformTransitionRoutine());
        }

        private IEnumerator DissolveObjectRoutine(GameObject go)
        {
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;
            while (elapsed < exitDelay * 0.8f)
            {
                float t = elapsed / (exitDelay * 0.8f);
                go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            go.transform.localScale = Vector3.zero;
        }

        private IEnumerator PerformTransitionRoutine()
        {
            yield return new WaitForSeconds(exitDelay);

            if (Stage3Controller.Instance != null)
            {
                Stage3Controller.Instance.TriggerSceneComplete();
            }
            else
            {
                EventBus.RaiseSceneComplete();
            }
        }

        // 渲染干净居中的动作引导 Prompt
        private void OnGUI()
        {
            if (!_hasGuided || _hasExited) return;

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle();
                _promptStyle.fontSize = 22;
                _promptStyle.fontStyle = FontStyle.Bold;
                _promptStyle.normal.textColor = new Color(0.4f, 0.92f, 1.0f, 0.95f);
                _promptStyle.alignment = TextAnchor.MiddleCenter;
            }

            float width = 450f;
            float height = 45f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 90f;

            GUI.Box(new Rect(x - 15, y - 5, width + 30, height + 10), "");
            GUI.Label(new Rect(x, y, width, height), actionPromptText, _promptStyle);
        }
    }
}
