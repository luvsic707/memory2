using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 香蕉吃掉 Juice 效果控制器
    /// 挂在香蕉 GameObject 上，由 BananaInteractable 调用 PlayEatSequence()
    /// 流程：飞向镜头 → Squash 弹性 → 逐步出现缺口（分段缩小一端）→ 整体消失
    /// </summary>
    public class BananaEatEffect : MonoBehaviour
    {
        [Header("飞向镜头设置")]
        [Tooltip("香蕉飞向镜头的时长（秒）")]
        public float flyDuration = 0.25f;

        [Tooltip("飞向镜头的停止距离（离摄像机多近停下）")]
        public float stopDistanceFromCamera = 1.0f;

        [Tooltip("飞向镜头时的缩放倍率（接近时显得变大）")]
        public float flyScaleMultiplier = 1.4f;

        [Header("咬合 & 缺口设置")]
        [Tooltip("分几口吃掉（每口触发一次缺口+抖动）")]
        [Range(1, 5)] public int biteCount = 2;

        [Tooltip("每次咬合之间的间隔（秒）")]
        public float biteInterval = 0.12f;

        [Tooltip("每次咬合时的 Squash 压缩幅度")]
        public float squashAmount = 0.22f;

        [Header("消失设置")]
        [Tooltip("最后消失的时长（秒）")]
        public float dissolveDuration = 0.18f;

        [Header("震屏设置")]
        [Tooltip("每次咬合时震屏的强度")]
        public float shakeIntensity = 0.04f;

        [Tooltip("震屏持续时间（秒）")]
        public float shakeDuration = 0.1f;

        [Header("粒子溅射（可选）")]
        [Tooltip("吃掉时播放的粒子效果（拖入 Prefab，若为空则跳过）")]
        public GameObject splashParticlePrefab;

        private Vector3 _originalScale;
        private bool _isPlaying = false;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// 由 BananaInteractable 调用：开始整套吃蕉动画流程，完成后调用 onComplete
        /// </summary>
        public void PlayEatSequence(System.Action onComplete)
        {
            if (_isPlaying) return;
            _isPlaying = true;
            StartCoroutine(EatSequenceCoroutine(onComplete));
        }

        private IEnumerator EatSequenceCoroutine(System.Action onComplete)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                onComplete?.Invoke();
                _isPlaying = false;
                yield break;
            }

            // ── 1. 飞向镜头 ──────────────────────────────────────────────
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            // 目标位置：镜头前方，稍偏右下（模拟拿到嘴边）
            Vector3 targetPos = cam.transform.position
                                + cam.transform.forward * stopDistanceFromCamera
                                + cam.transform.right * 0.25f
                                - cam.transform.up * 0.15f;
            Quaternion targetRot = Quaternion.LookRotation(cam.transform.forward, cam.transform.up)
                                   * Quaternion.Euler(20f, -30f, 15f);
            Vector3 targetScale = startScale * flyScaleMultiplier;

            // 解除 Rigidbody 物理（防止飞行途中受重力影响）
            Rigidbody rb = GetComponent<Rigidbody>();
            bool wasKinematic = false;
            if (rb != null) { wasKinematic = rb.isKinematic; rb.isKinematic = true; }

            float elapsed = 0f;
            while (elapsed < flyDuration)
            {
                float t = elapsed / flyDuration;
                float smooth = t * t * (3f - 2f * t); // smoothstep

                transform.position = Vector3.Lerp(startPos, targetPos, smooth);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
                transform.localScale = Vector3.Lerp(startScale, targetScale, smooth);

                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = targetPos;
            transform.rotation = targetRot;
            transform.localScale = targetScale;

            // ── 2. 逐步咬合（Squash + 缺口模拟）────────────────────────
            float currentYScale = targetScale.y;
            float biteYReduction = targetScale.y / (biteCount + 1);

            for (int i = 0; i < biteCount; i++)
            {
                // Squash 压扁弹起
                yield return StartCoroutine(SquashAnimation(targetScale, squashAmount));

                // 粒子喷溅
                if (splashParticlePrefab != null)
                    Instantiate(splashParticlePrefab, transform.position, Quaternion.identity);

                // 震屏
                StartCoroutine(ShakeCamera(cam, shakeIntensity, shakeDuration));

                // 缺口：Y 轴持续缩小，X/Z 轻微扩张（挤压变形感）
                currentYScale -= biteYReduction;
                Vector3 afterBiteScale = new Vector3(
                    targetScale.x * (1f + 0.08f * (i + 1)),
                    Mathf.Max(currentYScale, targetScale.y * 0.15f),
                    targetScale.z * (1f + 0.08f * (i + 1))
                );
                yield return StartCoroutine(LerpScale(transform.localScale, afterBiteScale, 0.08f));
                yield return new WaitForSeconds(biteInterval);
            }

            // ── 3. 最终消失（整体缩小至零）──────────────────────────────
            Vector3 finalScale = transform.localScale;
            float t2 = 0f;
            while (t2 < dissolveDuration)
            {
                float progress = t2 / dissolveDuration;
                float easedOut = 1f - (progress * progress);
                transform.localScale = finalScale * easedOut;
                t2 += Time.deltaTime;
                yield return null;
            }
            transform.localScale = Vector3.zero;

            // ── 4. 执行逻辑回调（生成新香蕉、计数等）────────────────────
            onComplete?.Invoke();
            if (rb != null) rb.isKinematic = wasKinematic;
            yield return null;
            Destroy(gameObject);
        }

        private IEnumerator SquashAnimation(Vector3 baseScale, float squash)
        {
            float halfDur = 0.06f;
            Vector3 squashedScale = new Vector3(
                baseScale.x * (1f + squash),
                baseScale.y * (1f - squash * 0.6f),
                baseScale.z * (1f + squash)
            );

            float elapsed = 0f;
            Vector3 from = transform.localScale;
            while (elapsed < halfDur)
            {
                transform.localScale = Vector3.Lerp(from, squashedScale, elapsed / halfDur);
                elapsed += Time.deltaTime;
                yield return null;
            }
            elapsed = 0f;
            from = squashedScale;
            while (elapsed < halfDur)
            {
                transform.localScale = Vector3.Lerp(from, baseScale, elapsed / halfDur);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                transform.localScale = Vector3.Lerp(from, to, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localScale = to;
        }

        private IEnumerator ShakeCamera(Camera cam, float intensity, float duration)
        {
            if (cam == null) yield break;
            Vector3 originalPos = cam.transform.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float strength = Mathf.Lerp(intensity, 0f, elapsed / duration);
                cam.transform.localPosition = originalPos + (Vector3)Random.insideUnitCircle * strength;
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.transform.localPosition = originalPos;
        }
    }
}
