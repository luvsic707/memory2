using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互 Juice 弹性回弹效果
    /// 当玩家按 Q 键交互时：
    /// 1. 手部向前微伸托住香蕉并轻微下拉拖拽 (Tug Drag)；
    /// 2. 手部松开瞬间，香蕉模型产生高弹性橡皮回弹震荡 (Elastic Spring Back)；
    /// 3. 回弹高潮瞬间触发咬断切口、露出乳白果肉、生成 2-10 个崭新香蕉与音效计数。
    /// </summary>
    public class BananaJuice : MonoBehaviour
    {
        [Header("Squash & Stretch")]
        [Tooltip("被拖拽拉伸时的 Y 轴压缩幅度")]
        [Range(0f, 0.5f)] public float squashY = 0.22f;

        [Tooltip("拖拽阶段持续时间（秒）")]
        public float squashDuration = 0.08f;

        [Tooltip("松手弹回时的 Overshoot 弹性倍率")]
        public float overshoot = 1.15f;

        [Header("摄像机震屏")]
        [Tooltip("震屏幅度")]
        public float shakeIntensity = 0.02f;

        [Tooltip("震屏持续时间（秒）")]
        public float shakeDuration = 0.08f;

        [Header("轻微拖拽位移")]
        [Tooltip("拖拽时向前方与下方的拉扯位移量")]
        public float punchDistance = 0.035f;

        private bool _isAnimating = false;

        public void PlayJuice(System.Action onComplete = null)
        {
            if (_isAnimating)
            {
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(JuiceCoroutine(onComplete));
        }

        private IEnumerator JuiceCoroutine(System.Action onComplete)
        {
            _isAnimating = true;

            Vector3 currentScale = transform.localScale;
            Quaternion currentRot = transform.localRotation;
            Vector3 currentPos = transform.localPosition;

            // ── 1. 手往前托住轻拉香蕉模型 (Tug Drag) ──────────────────────
            Vector3 tuggedScale = new Vector3(
                currentScale.x * (1f + squashY * 0.4f),
                currentScale.y * (1f - squashY * 0.7f),
                currentScale.z * (1f + squashY * 0.4f)
            );

            Vector3 tuggedPos = currentPos + transform.forward * punchDistance - transform.up * (punchDistance * 0.6f);
            Quaternion tuggedRot = currentRot * Quaternion.Euler(6f, -4f, 5f);

            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                float t = elapsed / squashDuration;
                transform.localScale = Vector3.Lerp(currentScale, tuggedScale, t);
                transform.localRotation = Quaternion.Slerp(currentRot, tuggedRot, t);
                transform.localPosition = Vector3.Lerp(currentPos, tuggedPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── 2. 松手！触发吃蕉咬切 + 生成 2-10 崭新香蕉 + 计数 + 音效 ────
            onComplete?.Invoke();

            // ── 3. 模型高弹性橡皮回弹震荡 (Elastic Spring-Back Bounce) ──────
            elapsed = 0f;
            float springDuration = 0.22f;
            while (elapsed < springDuration)
            {
                float t = elapsed / springDuration;
                // 阻尼弹簧回弹曲线 (Damped Elastic Oscillator)
                float bounceFactor = Mathf.Sin(t * Mathf.PI * 3.2f) * Mathf.Exp(-t * 6f) * 0.18f;

                transform.localScale = currentScale * (1f + bounceFactor);
                transform.localPosition = Vector3.Lerp(tuggedPos, currentPos, t * (2f - t));
                transform.localRotation = Quaternion.Slerp(tuggedRot, currentRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = currentScale;
            transform.localRotation = currentRot;
            transform.localPosition = currentPos;

            _isAnimating = false;
        }

        public static IEnumerator ShakeMainCamera(float intensity, float duration)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) yield break;

            Vector3 origPos = mainCam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                Vector3 randomOffset = Random.insideUnitSphere * intensity;
                mainCam.transform.localPosition = origPos + randomOffset;
                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCam.transform.localPosition = origPos;
        }
    }
}
