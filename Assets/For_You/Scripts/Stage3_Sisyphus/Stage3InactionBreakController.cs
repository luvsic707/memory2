using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (莫比乌斯/西西弗斯 3_Rock) 放弃交互打破循环控制器 (Negative Action Mechanic)
    /// 核心哲学：加缪《西西弗斯神话》—— 机械推石是无尽荒谬，唯有停止双手、放弃交互，莫比乌斯循环才被真正打破。
    /// 流程：
    /// 1. 玩家进入 Stage 3，正常按 Q 或点击鼠标推球（提示暗示需要一直交互）。
    /// 2. 玩家至少推球 minPushesToUnlock 次（默认 5 次）确立了参与循环。
    /// 3. 当玩家【停止任何交互】持续 idleTimeThreshold 秒（默认 3.5 秒）时：
    ///    系统判定玩家“意识到了荒谬并放弃了交互” ➔ 滚球静止消散 ➔ 触发转场进入 Stage 4 (4_Modern)！
    /// </summary>
    public class Stage3InactionBreakController : MonoBehaviour
    {
        public static Stage3InactionBreakController Instance { get; private set; }

        [Header("机制参数")]
        [Tooltip("玩家至少需要推球/交互多少次，才开启'放弃交互检测'")]
        public int minPushesToUnlock = 5;

        [Tooltip("停止任何交互（不按 Q，不点鼠标）持续多少秒后，判定为放弃交互并打破循环")]
        public float idleTimeThreshold = 3.5f;

        [Header("转场视觉")]
        [Tooltip("从停止交互到触发切关的时长（秒）")]
        public float dissolveDuration = 1.6f;

        // 内部状态
        private int _pushCount = 0;
        private float _idleTimer = 0f;
        private bool _canDetectInaction = false;
        private bool _isLoopBroken = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        /// <summary>
        /// 外部脚本（如 MobiusCameraPanController）通知推石交互
        /// </summary>
        public void OnPush()
        {
            _idleTimer = 0f;
            _pushCount++;
            if (!_canDetectInaction && _pushCount >= minPushesToUnlock)
            {
                _canDetectInaction = true;
                Debug.Log($"[Stage3] 玩家推球达到 {_pushCount} 次，'放弃交互打破循环' 机制已激活。");
            }
        }

        private void Update()
        {
            if (_isLoopBroken) return;

            // 1. 监测玩家是否有交互动作（按 Q / 键盘任意键 / 鼠标任意键）
            bool playerInteracted = Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(0)
                                 || Input.GetKey(KeyCode.Q) || Input.GetMouseButton(0);

            if (playerInteracted)
            {
                // 玩家正在交互：重置无动作计时器
                _idleTimer = 0f;

                if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(0))
                {
                    OnPush();
                }
            }
            else if (_canDetectInaction)
            {
                // 2. 玩家处于完全无动作状态，累加计时器
                _idleTimer += Time.deltaTime;

                // 达到静置时间门槛，触发打破循环！
                if (_idleTimer >= idleTimeThreshold)
                {
                    TriggerInactionBreak();
                }
            }
        }

        private void TriggerInactionBreak()
        {
            if (_isLoopBroken) return;
            _isLoopBroken = true;

            Debug.Log("<color=cyan>[Stage3] 玩家放弃了交互！系统判定莫比乌斯循环被打破，切入 Stage 4...</color>");

            // 1. 广播哲学解脱字幕
            EventBus.RaiseAnnouncement("You stopped pushing the rock. The endless loop has broken.");

            // 2. 静止并渐隐场上的滚球与巨石
            BallOnMobius ball = FindObjectOfType<BallOnMobius>();
            if (ball != null)
            {
                ball.enabled = false;
                StartCoroutine(DissolveRoutine(ball.gameObject));
            }

            SisyphusRock rock = FindObjectOfType<SisyphusRock>();
            if (rock != null)
            {
                Rigidbody rb = rock.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                StartCoroutine(DissolveRoutine(rock.gameObject));
            }

            // 3. 执行转场进入 Stage 4 (4_Modern)
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator DissolveRoutine(GameObject go)
        {
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;
            while (elapsed < dissolveDuration * 0.8f)
            {
                float t = elapsed / (dissolveDuration * 0.8f);
                go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            go.transform.localScale = Vector3.zero;
        }

        private IEnumerator TransitionRoutine()
        {
            yield return new WaitForSeconds(dissolveDuration);

            if (Stage3Controller.Instance != null)
            {
                Stage3Controller.Instance.TriggerSceneComplete();
            }
            else
            {
                EventBus.RaiseSceneComplete();
            }
        }
    }
}
