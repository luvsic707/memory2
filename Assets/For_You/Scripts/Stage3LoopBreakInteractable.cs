using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (莫比乌斯/西西弗斯 3_Rock) 循环解脱与缝合线交互器 (结合方案 2+4)
    /// 哲学概念：加缪《西西弗斯神话》—— 意识到了无限循环的荒谬，选择放弃推石，解开莫比乌斯曲面的缝合线。
    /// 1. 自动在莫比乌斯环扭转处/斜坡山顶渲染发光的莫比乌斯缝合线 (Glowing Mobius Seam)。
    /// 2. 实现 IInteractable 接口，提示 "放弃推动 (踏出循环)"。
    /// 3. 按 Q 交互时：巨石冻结化灰 + 莫比乌斯缝合线拉开撕裂 + 自动触发 Stage 4 转场！
    /// </summary>
    public class Stage3LoopBreakInteractable : MonoBehaviour, IInteractable
    {
        [Header("交互提示")]
        [SerializeField] private string interactHint = "放弃推动 (踏出循环)";

        [Header("缝合线光晕视觉")]
        [Tooltip("莫比乌斯缝合线的发光颜色")]
        public Color seamGlowColor = new Color(0.3f, 0.85f, 1.0f, 0.9f);

        [Tooltip("缝合线光晕环半径")]
        public float seamRadius = 2.5f;

        [Header("转场延迟")]
        [Tooltip("按下 Q 后，巨石定格与缝线拉开的时长（秒）")]
        public float dissolveDuration = 1.8f;

        [Header("音效 (可选)")]
        [Tooltip("解开缝合线/离开循环音效（留空则自动程序化合成）")]
        public AudioClip seamUnzipClip;
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        public string InteractHint => interactHint;

        private bool _isTriggered = false;
        private LineRenderer _seamLine;
        private Material _seamMaterial;
        private AudioSource _audioSource;

        private void Awake()
        {
            EnsureCollider();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
        }

        private void Start()
        {
            SetupSeamVisual();
        }

        private void EnsureCollider()
        {
            if (GetComponent<Collider>() == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = 2.0f;
                sphere.isTrigger = true;
            }
        }

        /// <summary>
        /// 程序化渲染莫比乌斯环缝合线光晕
        /// </summary>
        private void SetupSeamVisual()
        {
            GameObject lineGo = new GameObject("MobiusSeamVisual");
            lineGo.transform.SetParent(transform, false);

            _seamLine = lineGo.AddComponent<LineRenderer>();
            _seamLine.useWorldSpace = false;
            _seamLine.loop = true;
            int segments = 48;
            _seamLine.positionCount = segments;

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _seamMaterial = new Material(shader);
            _seamMaterial.color = seamGlowColor;
            _seamLine.material = _seamMaterial;
            _seamLine.startWidth = 0.25f;
            _seamLine.endWidth = 0.25f;

            // 绘制一个带有扭转感的 3D 缝合线环
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                // 带有莫比乌斯 8 字扭曲立体的光环
                float x = Mathf.Sin(angle) * seamRadius;
                float y = Mathf.Sin(angle * 2f) * 0.4f;
                float z = Mathf.Cos(angle) * seamRadius;
                _seamLine.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private void Update()
        {
            // 缝合线自转与脉冲
            if (_seamLine != null && !_isTriggered)
            {
                float pulse = 0.7f + Mathf.Sin(Time.time * 3f) * 0.3f;
                if (_seamMaterial != null)
                {
                    _seamMaterial.color = new Color(seamGlowColor.r, seamGlowColor.g, seamGlowColor.b, seamGlowColor.a * pulse);
                }
                transform.Rotate(Vector3.up, 15f * Time.deltaTime);
            }
        }

        // ── IInteractable 接口实现 ─────────────────────────────────────────
        public void Interact()
        {
            if (_isTriggered) return;
            _isTriggered = true;

            StartCoroutine(LoopBreakSequence());
        }

        private IEnumerator LoopBreakSequence()
        {
            Debug.Log("<color=cyan>[Stage3] 玩家选择了放弃推石，解开莫比乌斯曲面缝合线！</color>");

            // 1. 广播叙事通告
            EventBus.RaiseAnnouncement("You stopped pushing the rock. The Mobius loop unraveled.");

            // 2. 找到场上的 SisyphusRock 巨石并将其定格（冻结物理，停止滚落/推动）
            SisyphusRock rock = FindObjectOfType<SisyphusRock>();
            if (rock != null)
            {
                Rigidbody rb = rock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                StartCoroutine(DissolveRockRoutine(rock.gameObject));
            }

            // 3. 播放解开缝合线音效
            PlayUnzipSound();

            // 4. 莫比乌斯缝合线剧烈扩大亮起
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 4f;

            Camera mainCam = Camera.main;
            Vector3 camStartPos = mainCam != null ? mainCam.transform.localPosition : Vector3.zero;

            while (elapsed < dissolveDuration)
            {
                float t = elapsed / dissolveDuration;
                float easeIn = t * t;

                transform.localScale = Vector3.Lerp(startScale, targetScale, easeIn);
                if (_seamMaterial != null)
                {
                    _seamMaterial.color = Color.Lerp(seamGlowColor, Color.white, easeIn);
                }

                // 相机产生微弱的解拉链震撼震动
                if (mainCam != null)
                {
                    mainCam.transform.localPosition = camStartPos + (Vector3)Random.insideUnitCircle * (0.04f * t);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (mainCam != null) mainCam.transform.localPosition = camStartPos;

            // 5. 触发 Stage 3 控制器执行转场 -> Stage 4
            if (Stage3Controller.Instance != null)
            {
                Stage3Controller.Instance.TriggerSceneComplete();
            }
            else
            {
                EventBus.RaiseSceneComplete();
            }
        }

        // 巨石平影化散
        private IEnumerator DissolveRockRoutine(GameObject rockGo)
        {
            Vector3 origScale = rockGo.transform.localScale;
            float elapsed = 0f;
            float dur = dissolveDuration * 0.9f;

            while (elapsed < dur)
            {
                float t = elapsed / dur;
                rockGo.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            rockGo.transform.localScale = Vector3.zero;
        }

        // 缝线拉开音效
        private void PlayUnzipSound()
        {
            if (seamUnzipClip != null)
            {
                _audioSource.PlayOneShot(seamUnzipClip, soundVolume);
            }
            else
            {
                // 程序化合成渐强微光声
                _audioSource.PlayOneShot(CreateSynthUnzipClip(), soundVolume * 0.7f);
            }
        }

        private static AudioClip _cachedUnzipClip;
        private AudioClip CreateSynthUnzipClip()
        {
            if (_cachedUnzipClip != null) return _cachedUnzipClip;

            int sampleRate = 44100;
            float duration = 1.2f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / sampleRate;
                float t = time / duration;
                float freq = Mathf.Lerp(150f, 650f, t * t); // 频率从低向高向上扫频（拉链拉开感）
                float env = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * time);
                data[i] = wave * env * 0.35f;
            }

            _cachedUnzipClip = AudioClip.Create("SynthSeamUnzip", samples, 1, sampleRate, false);
            _cachedUnzipClip.SetData(data, 0);
            return _cachedUnzipClip;
        }
    }
}
