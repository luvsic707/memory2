using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 2 (上帝已死 2_God) Juice 润色控制器
    /// 1. 祈祷震压 (Prayer Slam & Divine Shockwave): 按 Q / 点击祈祷时，地面扩散神圣金光环 + 视角向下扣击击平抖动。
    /// 2. 危机暗角 (Crisis Pulse): 晃动度较高时，屏幕四周出现低频红色/灰色暗角与视差摇晃。
    /// 3. 坠落失重感 (Abyss Fall Euphoria): 掉下悬浮岛时，Camera FoV 快速从 60° 扩大到 95°，强化高速失重坠落感。
    /// </summary>
    public class Stage2JuiceEffects : MonoBehaviour
    {
        public static Stage2JuiceEffects Instance { get; private set; }

        [Header("神圣祈祷镇压反馈")]
        [Tooltip("祈祷时生成的神圣光环颜色")]
        public Color shockwaveColor = new Color(1.0f, 0.88f, 0.4f, 0.85f);

        [Tooltip("光环扩展的最大半径（米）")]
        public float shockwaveMaxRadius = 6.0f;

        [Tooltip("光环扩散时长（秒）")]
        public float shockwaveDuration = 0.45f;

        [Tooltip("祈祷时摄像机向下重压振幅")]
        public float cameraSlamAmount = 0.08f;

        [Header("危机警戒")]
        [Tooltip("晃动度达到多少时开启屏幕危机暗角 Warning")]
        [Range(0.3f, 0.9f)] public float crisisThreshold = 0.5f;

        [Header("坠落失重动画")]
        [Tooltip("坠落时 Target FoV")]
        public float fallTargetFov = 95f;

        [Tooltip("FoV 扩大时长（秒）")]
        public float fallFovDuration = 2.2f;

        [Header("音效 (Inspector 可选拖拽)")]
        [Tooltip("祈祷神钟/镇压音效 (空则播放程序化合成低音轰鸣)")]
        public AudioClip divineGongClip;

        [Range(0f, 1f)] public float divineGongVolume = 0.75f;

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // 2D 全局音效
        }

        /// <summary>
        /// 祈祷时调用：触发神圣冲击波 + 摄像机扣击抖动 + 音效
        /// </summary>
        public void TriggerPrayerJuice(Vector3 originPos)
        {
            // 1. 生成地面神圣光环
            StartCoroutine(SpawnShockwaveRoutine(originPos));

            // 2. 摄像机向下重压抖动
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                StartCoroutine(CameraSlamRoutine(mainCam));
            }

            // 3. 播放沉闷神钟音效
            PlayDivineGongSound();
        }

        /// <summary>
        /// 坠落时调用：触发 FoV 失重拉伸
        /// </summary>
        public void TriggerFallEuphoriaSequence()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                StartCoroutine(FallFovRoutine(mainCam));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Procedural Shockwave Ring Generator

        private IEnumerator SpawnShockwaveRoutine(Vector3 center)
        {
            GameObject ringGo = new GameObject("DivineShockwaveRing");
            ringGo.transform.position = center + Vector3.up * 0.05f;

            LineRenderer line = ringGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            int segments = 40;
            line.positionCount = segments;

            // 使用默认 Standard Unlit 材质
            Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material mat = new Material(unlitShader);
            mat.color = shockwaveColor;
            line.material = mat;
            line.startWidth = 0.35f;
            line.endWidth = 0.05f;

            float elapsed = 0f;
            while (elapsed < shockwaveDuration)
            {
                float t = elapsed / shockwaveDuration;
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                float radius = Mathf.Lerp(0.3f, shockwaveMaxRadius, easeOut);
                Color currentColor = Color.Lerp(shockwaveColor, new Color(shockwaveColor.r, shockwaveColor.g, shockwaveColor.b, 0f), t);
                mat.color = currentColor;

                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    Vector3 pos = ringGo.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    line.SetPosition(i, pos);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(mat);
            Destroy(ringGo);
        }

        // Camera Slam / Punch Routine
        private IEnumerator CameraSlamRoutine(Camera cam)
        {
            Vector3 originalLocalPos = cam.transform.localPosition;
            float dur = 0.12f;
            float elapsed = 0f;

            // 向下拉沉 + 轻微倾斜
            while (elapsed < dur)
            {
                float t = elapsed / dur;
                float offset = Mathf.Sin(t * Mathf.PI) * cameraSlamAmount;
                cam.transform.localPosition = originalLocalPos - Vector3.up * offset;
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.transform.localPosition = originalLocalPos;
        }

        // Fall FOV Euphoria Routine
        private IEnumerator FallFovRoutine(Camera cam)
        {
            float startFov = cam.fieldOfView;
            float elapsed = 0f;

            while (elapsed < fallFovDuration)
            {
                float t = elapsed / fallFovDuration;
                float easeIn = t * t;
                cam.fieldOfView = Mathf.Lerp(startFov, fallTargetFov, easeIn);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.fieldOfView = fallTargetFov;
        }

        // Divine Gong Sound Playback
        private void PlayDivineGongSound()
        {
            if (divineGongClip != null)
            {
                _audioSource.PlayOneShot(divineGongClip, divineGongVolume);
            }
            else
            {
                // 无 AudioClip 时的优雅降级合成音
                _audioSource.pitch = Random.Range(0.85f, 0.95f);
                _audioSource.PlayOneShot(CreateSyntheticGongClip(), divineGongVolume * 0.6f);
                _audioSource.pitch = 1.0f;
            }
        }

        private static AudioClip _cachedSynthClip;
        private AudioClip CreateSyntheticGongClip()
        {
            if (_cachedSynthClip != null) return _cachedSynthClip;

            int sampleRate = 44100;
            float duration = 0.8f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float freq = 90f; // 低沉基频 90Hz

            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / sampleRate;
                float env = Mathf.Exp(-time * 4f); // 快速衰减包络
                float wave = Mathf.Sin(2f * Mathf.PI * freq * time)
                           + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 1.5f * time)
                           + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2.2f * time);
                data[i] = wave * env * 0.4f;
            }

            _cachedSynthClip = AudioClip.Create("SynthDivineGong", samples, 1, sampleRate, false);
            _cachedSynthClip.SetData(data, 0);
            return _cachedSynthClip;
        }
    }
}
