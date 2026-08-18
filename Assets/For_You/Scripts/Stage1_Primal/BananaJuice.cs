using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互 Juice 4阶段连贯动画：
    /// 1. 手部前伸接触香蕉；
    /// 2. 接触后手与香蕉同步往回拖拽；
    /// 3. 手松开平滑复位；
    /// 4. 手复位后香蕉高弹性弹回原位，并触发咬切与生成。
    /// </summary>
    public class BananaJuice : MonoBehaviour
    {
        [Header("Squash & Stretch")]
        [Tooltip("被拖拽拉伸时的 Y 轴压缩幅度")]
        [Range(0f, 0.5f)] public float squashY = 0.22f;

        [Tooltip("拖拽阶段持续时间（秒）")]
        public float squashDuration = 0.10f;

        [Tooltip("松手弹回时的 Overshoot 弹性倍率")]
        public float overshoot = 1.15f;

        [Tooltip("点击旋转偏摆角度")]
        public float wobbleAngle = 18f;

        [Header("摄像机震屏")]
        [Tooltip("震屏幅度")]
        public float shakeIntensity = 0.02f;

        [Tooltip("震屏持续时间（秒）")]
        public float shakeDuration = 0.08f;

        [Header("拖拽位移")]
        [Tooltip("被手部往回拖拽时的拉扯位移量")]
        public float punchDistance = 0.06f;

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

            // ── Phase 1: 等待手部前伸与香蕉相接触 (0.10s) ─────────────────
            yield return new WaitForSeconds(0.10f);

            // ── Phase 2: 接触到香蕉后往回拖拽一下 (Drag Backwards 0.10s) ───
            Vector3 draggedScale = new Vector3(
                currentScale.x * (1f + squashY * 0.5f),
                currentScale.y * (1f - squashY * 0.8f),
                currentScale.z * (1f + squashY * 0.5f)
            );

            Camera mainCam = Camera.main;
            Vector3 dragDir = mainCam != null ? -mainCam.transform.forward : -transform.forward;
            Vector3 draggedPos = currentPos + dragDir * punchDistance - transform.up * (punchDistance * 0.5f);
            Quaternion draggedRot = currentRot * Quaternion.Euler(10f, -6f, 8f);

            float elapsed = 0f;
            float dragDuration = 0.10f;
            while (elapsed < dragDuration)
            {
                float t = elapsed / dragDuration;
                transform.localScale = Vector3.Lerp(currentScale, draggedScale, t);
                transform.localRotation = Quaternion.Slerp(currentRot, draggedRot, t);
                transform.localPosition = Vector3.Lerp(currentPos, draggedPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── Phase 3: 等待手松开并平滑复位 (0.12s) ───────────────────────
            yield return new WaitForSeconds(0.12f);

            // ── Phase 4: 手复位后，香蕉高弹性弹回原位 (Elastic Spring-Back Bounce) ─
            elapsed = 0f;
            float springDuration = 0.20f;
            while (elapsed < springDuration)
            {
                float t = elapsed / springDuration;
                // 高弹性阻尼弹簧曲线
                float bounceFactor = Mathf.Sin(t * Mathf.PI * 3.5f) * Mathf.Exp(-t * 5.5f) * 0.2f;

                transform.localScale = currentScale * (1f + bounceFactor);
                transform.localPosition = Vector3.Lerp(draggedPos, currentPos, t * (2f - t));
                transform.localRotation = Quaternion.Slerp(draggedRot, currentRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = currentScale;
            transform.localRotation = currentRot;
            transform.localPosition = currentPos;

            // 香蕉弹回原位瞬间，触发咬断切口 + 露出乳白果肉 + 生成 2-10 个崭新香蕉 + 计数 + 声音
            onComplete?.Invoke();

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
