using UnityEngine;
using TheLastCompact.Core;

public class CausalLightController : MonoBehaviour
{
    [Header("Connections")]
    public CausalController controller;
    private Light targetLight;

    [Header("Dramatic Flicker Settings")]
    public float baseIntensity = 1000f;
    [Range(0, 1)] public float flickerDepth = 0.8f;
    [Tooltip("核心频率：降低此值会让闪烁变慢")]
    public float speedMultiplier = 25f; // 原来是 50，现在设为 25 实现速度减半

    void Start()
    {
        targetLight = GetComponent<Light>();
        if (targetLight != null) baseIntensity = targetLight.intensity;
    }

    void Update()
    {
        if (controller == null || controller.Model == null || targetLight == null) return;

        float intensity = controller.Model.GlitchIntensity;

        // 只有当崩坏程度超过 10% 时才开始闪烁
        if (intensity > 0.1f)
        {
            // --- 逻辑 1: 噪声采样 (速度减半) ---
            float rawNoise = Mathf.PerlinNoise(Time.time * speedMultiplier, Time.time * 0.2f);

            // --- 逻辑 2: 确定目标亮度 ---
            float flickerFactor = rawNoise > (1.0f - intensity) ? (1.0f - flickerDepth) : 1.0f;
            float microJitter = 1.0f - (Random.value * intensity * 0.15f);
            float targetValue = baseIntensity * flickerFactor * microJitter;

            // --- 逻辑 3: 非对称平滑恢复 (解决起不来的问题) ---
            // 如果是在变亮，速度设为 30f (极快)；如果是在变暗，速度设为 10f (稍慢)
            float lerpSpeed = (targetValue > targetLight.intensity) ? 30f : 10f;
            targetLight.intensity = Mathf.Lerp(targetLight.intensity, targetValue, Time.deltaTime * lerpSpeed);

            // --- 逻辑 4: 极端崩坏下的断电感 ---
            if (intensity > 0.8f && Random.value > 0.99f)
            {
                targetLight.intensity = 0;
            }
        }
        else
        {
            // 平稳期：快速恢复正常亮度
            targetLight.intensity = Mathf.Lerp(targetLight.intensity, baseIntensity, Time.deltaTime * 5f);
        }
    }
}