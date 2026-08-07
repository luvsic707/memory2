using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉交互 Juice 效果（完全非破坏性，不修改核心逻辑）
    /// 在香蕉原地播放 Squash & Stretch + 旋转弹开 + 镜头微震
    /// 由 BananaInteractable 在执行逻辑前调用，动画结束后回调继续执行逻辑
    /// </summary>
    public class BananaJuice : MonoBehaviour
    {
        [Header("Squash & Stretch")]
        [Tooltip("被点击时的 Y 轴压缩幅度（0=不压，0.3=压30%）")]
        [Range(0f, 0.5f)] public float squashY = 0.28f;

        [Tooltip("Squash 动画持续时间（秒）")]
        public float squashDuration = 0.07f;

        [Tooltip("弹回时的 Overshoot 弹性倍率（1=不超，1.15=轻微超出）")]
        public float overshoot = 1.12f;

        [Header("旋转 Wobble")]
        [Tooltip("被点击时的随机 Z 轴旋转角度（±度数）")]
        public float wobbleAngle = 18f;

        [Tooltip("旋转回正的时间（秒）")]
        public float wobbleDuration = 0.18f;

        [Header("摄像机震屏")]
        [Tooltip("震屏幅度")]
        public float shakeIntensity = 0.025f;

        [Tooltip("震屏持续时间（秒）")]
        public float shakeDuration = 0.09f;

        [Header("弹出位移（模拟被弹了一下）")]
        [Tooltip("点击时在随机方向弹出的位移量")]
        public float punchDistance = 0.04f;

        [Tooltip("弹出后归位的时间（秒）")]
        public float punchDuration = 0.12f;

        // ── 内部状态 ──────────────────────────────────────────────────────
        private Vector3 _originalLocalScale;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalPos;
        private bool _isAnimating = false;

        private void Awake()
        {
            _originalLocalScale = transform.localScale;
            _originalLocalRotation = transform.localRotation;
            _originalLocalPos = transform.localPosition;
        }

        /// <summary>
        /// 播放一次完整的 Juice 动画，动画完成后调用 onComplete
        /// 如果已在播放则排队等待（直接执行 onComplete，不阻塞逻辑）
        /// </summary>
        public void PlayJuice(System.Action onComplete = null)
        {
            if (_isAnimating)
            {
                // 如果已在动画中，直接触发逻辑，不重复播放动画
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(JuiceCoroutine(onComplete));
        }

        // ─────────────────────────────────────────────────────────────────
        private IEnumerator JuiceCoroutine(System.Action onComplete)
        {
            _isAnimating = true;

            // 缓存当前状态（支持 BananaDistorter 已扭曲的情况）
            Vector3 currentScale = transform.localScale;
            Quaternion currentRot = transform.localRotation;
            Vector3 currentPos = transform.localPosition;

            // ── 1. Squash（压扁，X/Z 扩张补偿体积）──────────────────────
            Vector3 squashedScale = new Vector3(
                currentScale.x * (1f + squashY * 0.6f),
                currentScale.y * (1f - squashY),
                currentScale.z * (1f + squashY * 0.6f)
            );

            // 随机 Z 轴 wobble 方向
            float wobbleDir = Random.value > 0.5f ? 1f : -1f;
            Quaternion wobbledRot = currentRot * Quaternion.Euler(0, 0, wobbleAngle * wobbleDir);

            // 随机弹出位移方向（水平面）
            Vector2 punch2D = Random.insideUnitCircle.normalized * punchDistance;
            Vector3 punchedPos = currentPos + new Vector3(punch2D.x, 0, punch2D.y);

            // 压扁阶段
            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                float t = elapsed / squashDuration;
                transform.localScale = Vector3.Lerp(currentScale, squashedScale, t);
                transform.localRotation = Quaternion.Slerp(currentRot, wobbledRot, t);
                transform.localPosition = Vector3.Lerp(currentPos, punchedPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── 2. Overshoot 弹回（先超出再稳定）────────────────────────
            Vector3 overshootScale = new Vector3(
                currentScale.x * (1f - squashY * 0.25f * (overshoot - 1f)),
                currentScale.y * overshoot,
                currentScale.z * (1f - squashY * 0.25f * (overshoot - 1f))
            );

            elapsed = 0f;
            float overshootDur = squashDuration * 0.8f;
            while (elapsed < overshootDur)
            {
                float t = elapsed / overshootDur;
                transform.localScale = Vector3.Lerp(squashedScale, overshootScale, t);
                transform.localRotation = Quaternion.Slerp(wobbledRot, currentRot, t);
                transform.localPosition = Vector3.Lerp(punchedPos, currentPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── 3. 触发逻辑回调（香蕉吃掉逻辑在这里执行）──────────────
            onComplete?.Invoke();

            // ── 4. 平滑回正到原始缩放（在逻辑执行后继续恢复）───────────
            elapsed = 0f;
            Vector3 fromScale = transform.localScale;
            Quaternion fromRot = transform.localRotation;
            while (elapsed < wobbleDuration)
            {
                float t = elapsed / wobbleDuration;
                float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                transform.localScale = Vector3.Lerp(fromScale, currentScale, ease);
                transform.localRotation = Quaternion.Slerp(fromRot, currentRot, ease);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localScale = currentScale;
            transform.localRotation = currentRot;
            transform.localPosition = currentPos;

            _isAnimating = false;
        }

        /// <summary>
        /// 触发摄像机震屏（由外部调用或内部使用）
        /// </summary>
        public static IEnumerator ShakeMainCamera(float intensity, float duration)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Transform camTransform = cam.transform;
            Vector3 originalPos = camTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float strength = Mathf.Lerp(intensity, 0f, elapsed / duration);
                camTransform.localPosition = originalPos + (Vector3)Random.insideUnitCircle * strength;
                elapsed += Time.deltaTime;
                yield return null;
            }
            camTransform.localPosition = originalPos;
        }
    }
}
