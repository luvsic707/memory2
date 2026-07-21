using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 点击驱动的"太阳弧线运动"控制器 (Stage 3)
    ///
    /// 每次玩家点击鼠标，场景内的 Directional Light 就沿弧线向前推移一步，
    /// 仿佛玩家每一次努力都在驱动着日月轮转，但绕完一整圈后太阳又会回到原处——
    /// 时间在玩家的劳作中流转，却永远回到起点，呼应西西弗斯的荒谬。
    /// </summary>
    public class SunArcController : MonoBehaviour
    {
        [Header("目标光源")]
        [Tooltip("场景中要旋转的平行光（Directional Light），若为空自动查找场景中第一个 Directional Light")]
        public Light sunLight;

        [Header("弧线运动参数")]
        [Tooltip("太阳弧线旋转的轴向 (日出到日落方向)。默认绕 Z 轴：光从东方升起、西方落下")]
        public Vector3 orbitAxis = Vector3.right;

        [Tooltip("每次点击太阳前进的角度（度）。推荐 5~15 度，越小越精细")]
        [Range(1f, 45f)]
        public float degreesPerClick = 8f;

        [Tooltip("太阳平滑移动的角速度 (度/秒)，控制每次点击后的推移速度")]
        public float rotationSpeed = 45f;

        [Header("光照颜色过渡")]
        [Tooltip("在太阳弧线旋转的过程中，同步过渡光照颜色（仿黎明-正午-黄昏-夜晚）")]
        public bool animateColor = true;

        [Tooltip("太阳颜色曲线，根据旋转进度 (0~1 = 从初始角度转一圈) 采样颜色")]
        public Gradient sunColorGradient;

        // 旋转状态追踪
        private float _currentAngle = 0f;
        private float _targetAngle = 0f;
        private Coroutine _rotateCoroutine;

        private void Awake()
        {
            // 自动查找场景中的平行光
            if (sunLight == null)
            {
                Light[] allLights = FindObjectsOfType<Light>();
                foreach (var l in allLights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            // 设置默认的黎明~黄昏颜色渐变
            if (sunColorGradient == null || sunColorGradient.colorKeys.Length == 0)
            {
                sunColorGradient = CreateDefaultSunGradient();
            }
        }

        private void Update()
        {
            if (sunLight == null) return;

            // 监听左键点击：每次点击驱动太阳推进一步
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q))
            {
                _targetAngle += degreesPerClick;

                // 重启平滑旋转协程
                if (_rotateCoroutine != null) StopCoroutine(_rotateCoroutine);
                _rotateCoroutine = StartCoroutine(RotateSunSmooth());
            }
        }

        private IEnumerator RotateSunSmooth()
        {
            while (Mathf.Abs(_currentAngle - _targetAngle) > 0.05f)
            {
                float step = rotationSpeed * Time.deltaTime;
                _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, step);

                // 应用旋转：绕指定轴旋转光源的方向
                sunLight.transform.rotation = Quaternion.AngleAxis(_currentAngle, orbitAxis)
                    * Quaternion.Euler(50f, -30f, 0f); // 初始仰角 50 度，方位 -30 度

                // 同步更新光照颜色（按 0~360 一圈的归一化进度采样渐变）
                if (animateColor)
                {
                    float progress = (_currentAngle % 360f + 360f) % 360f / 360f;
                    sunLight.color = sunColorGradient.Evaluate(progress);

                    // 太阳在"下半圆"时（180~360 度）逐渐变暗，模拟夜晚
                    float intensity = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI));
                    sunLight.intensity = Mathf.Lerp(0.1f, 1.8f, intensity);
                }

                yield return null;
            }
        }

        /// <summary>
        /// 创建默认的日出日落色温渐变：黎明橘红 → 正午白 → 黄昏深橙 → 午夜蓝黑
        /// </summary>
        private Gradient CreateDefaultSunGradient()
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.4f, 0.1f), 0.0f),  // 日出：橘红
                    new GradientColorKey(new Color(1.0f, 0.95f, 0.85f), 0.25f), // 上午：暖白
                    new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.5f),   // 正午：纯白
                    new GradientColorKey(new Color(1.0f, 0.7f, 0.3f), 0.75f),  // 黄昏：金橙
                    new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), 1.0f),   // 夜晚：深蓝
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                }
            );
            return g;
        }
    }
}
