using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (莫比乌斯/西西弗斯 3_Rock) 循环解脱与缝合线交互器 (结合方案 2+4)
    /// 哲学概念：加缪《西西弗斯神话》—— 意识到了无限循环的荒谬，选择放弃推石，解开莫比乌斯曲面的缝合线。
    /// 交互设计：
    /// 1. 按 Q 键继续进行无限西西弗斯推球；
    /// 2. 随时按 E 键 或 用鼠标直接点击场景中的发光缝合环 ➔ 触发 "放弃推动 (踏出循环)" 动画！
    /// 3. 巨石定格化灰 + 莫比乌斯缝合线剧烈撕裂 + 转场进入 Stage 4 (4_Modern) 办公室！
    /// </summary>
    public class Stage3LoopBreakInteractable : MonoBehaviour, IInteractable
    {
        [Header("交互提示")]
        [SerializeField] private string interactHint = "按 E 键 / 点击缝合线：放弃推动 (踏出循环)";

        [Header("缝合线光晕视觉")]
        [Tooltip("莫比乌斯缝合线的发光颜色")]
        public Color seamGlowColor = new Color(0.3f, 0.85f, 1.0f, 0.9f);

        [Tooltip("缝合线光晕环半径")]
        public float seamRadius = 2.5f;

        [Header("转场延迟")]
        [Tooltip("按下 E 键或点击后，巨石定格与缝线拉开的时长（秒）")]
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
        private GUIStyle _guiStyle;

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
                sphere.radius = seamRadius * 1.5f;
            }
        }

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
            _seamLine.startWidth = 0.3f;
            _seamLine.endWidth = 0.3f;

            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Sin(angle) * seamRadius;
                float y = Mathf.Sin(angle * 2f) * 0.4f;
                float z = Mathf.Cos(angle) * seamRadius;
                _seamLine.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private void Update()
        {
            if (_isTriggered) return;

            // 1. 缝合线自转与脉冲
            if (_seamLine != null)
            {
                float pulse = 0.7f + Mathf.Sin(Time.time * 3.5f) * 0.3f;
                if (_seamMaterial != null)
                {
                    _seamMaterial.color = new Color(seamGlowColor.r, seamGlowColor.g, seamGlowColor.b, seamGlowColor.a * pulse);
                }
                transform.Rotate(Vector3.up, 18f * Time.deltaTime);
            }

            // 2. 键盘快捷键监听：按 E 键直接触发放弃推动并踏出循环
            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("[Stage3] 玩家按下了 E 键：触发放弃推动，踏出循环！");
                Interact();
                return;
            }

            // 3. 鼠标直接点击 3D 空间中的发光缝合环判定
            if (Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform))
                        {
                            Debug.Log("[Stage3] 玩家用鼠标直接点击了莫比乌斯缝合线！");
                            Interact();
                        }
                    }
                }
            }
        }

        // 在屏幕上渲染清晰的解脱操作 UI 提示
        private void OnGUI()
        {
            if (_isTriggered) return;

            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle();
                _guiStyle.fontSize = 20;
                _guiStyle.fontStyle = FontStyle.Bold;
                _guiStyle.normal.textColor = new Color(0.4f, 0.9f, 1.0f, 0.95f);
                _guiStyle.alignment = TextAnchor.MiddleCenter;
            }

            // 在屏幕下方中央渲染操作提示
            float width = 450f;
            float height = 40f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 80f;

            GUI.Box(new Rect(x - 10, y - 5, width + 20, height + 10), "");
            GUI.Label(new Rect(x, y, width, height), "💡 按 【E】 键放弃推动，解开莫比乌斯循环 (进入下一阶段)", _guiStyle);
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

            // 2. 找到场上的 SisyphusRock / BallOnMobius 并将其定格（冻结运动）
            SisyphusRock rock = FindObjectOfType<SisyphusRock>();
            if (rock != null)
            {
                Rigidbody rb = rock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                StartCoroutine(DissolveRockRoutine(rock.gameObject));
            }

            BallOnMobius ball = FindObjectOfType<BallOnMobius>();
            if (ball != null)
            {
                ball.enabled = false;
                StartCoroutine(DissolveRockRoutine(ball.gameObject));
            }

            // 3. 播放解开缝合线音效
            PlayUnzipSound();

            // 4. 莫比乌斯缝合线剧烈扩大亮起
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 5f;

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

                if (mainCam != null)
                {
                    mainCam.transform.localPosition = camStartPos + (Vector3)Random.insideUnitCircle * (0.05f * t);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (mainCam != null) mainCam.transform.localPosition = camStartPos;

            // 5. 触发 Stage 3 控制器执行转场 -> Stage 4 (4_Modern)
            if (Stage3Controller.Instance != null)
            {
                Stage3Controller.Instance.TriggerSceneComplete();
            }
            else
            {
                EventBus.RaiseSceneComplete();
            }
        }

        private IEnumerator DissolveRockRoutine(GameObject go)
        {
            Vector3 origScale = go.transform.localScale;
            float elapsed = 0f;
            float dur = dissolveDuration * 0.9f;

            while (elapsed < dur)
            {
                float t = elapsed / dur;
                go.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            go.transform.localScale = Vector3.zero;
        }

        private void PlayUnzipSound()
        {
            if (seamUnzipClip != null)
            {
                _audioSource.PlayOneShot(seamUnzipClip, soundVolume);
            }
            else
            {
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
                float freq = Mathf.Lerp(150f, 650f, t * t);
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
