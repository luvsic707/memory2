using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手部托举与轻微拖拽控制器
    /// 1. 按 Q 交互时，手部向前微伸托住香蕉并轻微拖拽 (Tug)，松手后香蕉模型产生弹性回弹。
    /// 2. 100% 保持在 Inspector 中调好的手部默认位置，不修改任何其他核心逻辑。
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中调好位置的第一视角手部模型 (例如 FlesherApe)")]
        public GameObject armVisual;

        [Tooltip("全局默认吃香蕉/咀嚼音效文件 (.mp3/.wav/.ogg)")]
        public AudioClip defaultEatSound;

        [Header("呼吸摇摆参数")]
        [Tooltip("手部在视角视野中的自然微弱呼吸摇摆幅度")]
        public float swayAmount = 0.005f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private Transform _armTransform;
        private bool _isTugging = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SetupArmReferences();
        }

        private void SetupArmReferences()
        {
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                Transform foundHand = AutoFindHandMeshInHierarchy();
                if (foundHand != null)
                {
                    armVisual = foundHand.gameObject;
                    _armTransform = foundHand;
                }
                else
                {
                    Transform foundHolder = transform.Find("FPS_Arm_Holder");
                    if (foundHolder == null && transform.parent != null)
                    {
                        foundHolder = transform.parent.Find("FPS_Arm_Holder");
                    }
                    if (foundHolder == null)
                    {
                        foundHolder = GameObject.Find("FPS_Arm_Holder")?.transform;
                    }

                    if (foundHolder != null)
                    {
                        armVisual = foundHolder.gameObject;
                        _armTransform = foundHolder;
                    }
                }
            }

            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[FirstPersonArmController] 锁定了你在 Inspector 中调好的原生手部位置: {_defaultLocalPos}</color>");
            }
        }

        private Transform AutoFindHandMeshInHierarchy()
        {
            Camera mainCam = Camera.main;
            Transform rootT = mainCam != null ? mainCam.transform : transform;
            if (rootT.parent != null) rootT = rootT.parent;

            Renderer[] renderers = rootT.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                string nameLower = r.gameObject.name.ToLower();
                if (nameLower.Contains("flesherape") || nameLower.Contains("arm") || nameLower.Contains("hand") || nameLower.Contains("gorilla") || nameLower.Contains("player"))
                {
                    return r.transform;
                }
            }
            return null;
        }

        private void Update()
        {
            if (_armTransform == null || _isTugging) return;

            // 第一视角手部微弱呼吸摇摆
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
            float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ), Time.deltaTime * 5f);
        }

        /// <summary>
        /// 按 Q 键交互：手往前托住香蕉轻微拖拽，松开后香蕉模型回弹
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetBananaWorldPos, System.Action onGrabbedCallback = null)
        {
            if (_isTugging || _armTransform == null)
            {
                onGrabbedCallback?.Invoke();
                return;
            }
            StartCoroutine(TugAndReleaseRoutine(onGrabbedCallback));
        }

        private IEnumerator TugAndReleaseRoutine(System.Action onGrabbedCallback)
        {
            _isTugging = true;

            Vector3 startPos = _defaultLocalPos;
            Quaternion startRot = _defaultLocalRot;

            // 1. 手部向视角前方微伸托住并轻微拖拽 (Tug Forward & Down slightly)
            Vector3 tugOffset = new Vector3(-0.012f, -0.02f, 0.055f);
            Quaternion tugRot = startRot * Quaternion.Euler(5f, -3f, 4f);
            Vector3 targetTugPos = startPos + tugOffset;

            float t = 0f;
            float tugDuration = 0.09f;
            while (t < tugDuration)
            {
                t += Time.deltaTime;
                float easeT = Mathf.Sin((t / tugDuration) * Mathf.PI * 0.5f);
                _armTransform.localPosition = Vector3.Lerp(startPos, targetTugPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(startRot, tugRot, easeT);
                yield return null;
            }

            // 2. 松开手部！瞬间触发回调（咬切截面 + 2-10 崭新香蕉下落 + 计数 + 声音）
            onGrabbedCallback?.Invoke();

            // 3. 手部顺滑归位 (Release & Return)
            t = 0f;
            float returnDuration = 0.12f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float easeT = t * (2f - t);
                _armTransform.localPosition = Vector3.Lerp(targetTugPos, startPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(tugRot, startRot, easeT);
                yield return null;
            }

            _armTransform.localPosition = startPos;
            _armTransform.localRotation = startRot;
            _isTugging = false;
        }
    }
}
