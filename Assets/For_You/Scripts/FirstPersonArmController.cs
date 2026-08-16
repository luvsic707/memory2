using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手部模型 (FlesherApe) 抓取与啃咬动作控制器
    /// 1. 100% 兼容 Animator 状态机：支持 Trigger 触发 "Grab" / "Eat" 抓取动画剪辑。
    /// 2. 在 LateUpdate() 中执行手臂前伸与手指勾抓，彻底防止 Animator 逐帧覆盖 transform 位置与角度。
    /// 3. 当玩家按下 Q 键吃香蕉时，触发手部抓取与咬切逻辑。
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中调好位置的第一视角手部模型 (例如 FlesherApe)")]
        public GameObject armVisual;

        [Tooltip("全局默认吃香蕉/咀嚼音效文件 (.mp3/.wav/.ogg)")]
        public AudioClip defaultEatSound;

        [Header("抓取动画参数")]
        [Tooltip("手部伸出抓取的目标前伸距离 (针对 FlesherApe 优化至 0.28m)")]
        public float reachDistance = 0.28f;

        [Tooltip("手部伸出与收回的动画速度 (调缓至 5.2f 顺滑逼真)")]
        public float grabSpeed = 5.2f;

        [Tooltip("手部在视角视野中的自然呼吸摇摆幅度")]
        public float swayAmount = 0.008f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isGrabbing = false;
        private Transform _armTransform;

        private Animator _apeAnimator;
        private Transform[] _fingerBones;
        private Quaternion[] _fingerDefaultRotations;

        private Vector3 _currentGrabLocalPos;
        private Quaternion _currentGrabLocalRot;
        private float _currentCurlFactor = 0f;

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
            // 1. 优先使用玩家在 Inspector 里手动拖入的 FlesherApe 模型
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 2. 智能自动寻找场景/Player下已建好的 3D 手部模型 (FlesherApe / Hand / Arm / Gorilla)
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
                    else
                    {
                        GameObject holderGo = new GameObject("FPS_Arm_Holder");
                        holderGo.transform.SetParent(transform, false);
                        holderGo.transform.localPosition = new Vector3(0.35f, -0.35f, 0.6f);
                        holderGo.transform.localRotation = Quaternion.Euler(20f, -25f, 10f);
                        _armTransform = holderGo.transform;
                        armVisual = holderGo;
                    }
                }
            }

            // 🌟 100% 严格锁定 Inspector 中针对 FlesherApe 调好的初始位置与角度
            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                _apeAnimator = _armTransform.GetComponentInChildren<Animator>();
                if (_apeAnimator == null) _apeAnimator = _armTransform.GetComponentInParent<Animator>();

                CacheFingerBones();

                Debug.Log($"<color=green>[FirstPersonArmController] 成功锁定 FlesherApe 手部模型！初始位置: {_defaultLocalPos}，初始旋转: {_defaultLocalRot.eulerAngles}</color>");
            }
        }

        private void CacheFingerBones()
        {
            if (_armTransform == null) return;

            var allTransforms = _armTransform.GetComponentsInChildren<Transform>(true);
            var boneList = new List<Transform>();
            var rotList = new List<Quaternion>();

            foreach (var t in allTransforms)
            {
                string nameLower = t.name.ToLower();
                if (nameLower.Contains("finger") || nameLower.Contains("thumb") || 
                    nameLower.Contains("index") || nameLower.Contains("middle") || 
                    nameLower.Contains("ring") || nameLower.Contains("pinky") || 
                    nameLower.Contains("digit"))
                {
                    boneList.Add(t);
                    rotList.Add(t.localRotation);
                }
            }

            _fingerBones = boneList.ToArray();
            _fingerDefaultRotations = rotList.ToArray();
        }

        private void SetFingerCurl(float curlFactor)
        {
            if (_fingerBones == null || _fingerBones.Length == 0) return;

            for (int i = 0; i < _fingerBones.Length; i++)
            {
                if (_fingerBones[i] == null) continue;
                Quaternion targetRot = _fingerDefaultRotations[i] * Quaternion.Euler(curlFactor * 55f, 0f, 0f);
                _fingerBones[i].localRotation = Quaternion.Slerp(_fingerDefaultRotations[i], targetRot, curlFactor);
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
                    Debug.Log($"<color=cyan>[FirstPersonArmController] 自动定位匹配到手部模型: {r.gameObject.name}</color>");
                    return r.transform;
                }
            }
            return null;
        }

        private void LateUpdate()
        {
            if (_armTransform == null) return;

            if (!_isGrabbing)
            {
                // 在 LateUpdate 中更新呼吸摇摆，防止被 Animator 逐帧重置覆盖
                float time = Time.time;
                float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
                float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
                float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                Vector3 lagOffset = new Vector3(-mouseX * 0.012f, -mouseY * 0.012f, 0f);

                _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 6f);
            }
            else
            {
                // 抓取过程中在 LateUpdate 强制应用前伸与抱捏，防止被 Animator 覆盖！
                if (_currentGrabLocalPos != Vector3.zero)
                {
                    _armTransform.localPosition = _currentGrabLocalPos;
                    _armTransform.localRotation = _currentGrabLocalRot;
                }
                SetFingerCurl(_currentCurlFactor);
            }
        }

        /// <summary>
        /// 驱动 FlesherApe 产生伸手抓取香蕉 ➔ 抱捏抓握 ➔ 送嘴咀嚼 ➔ 恢复初始姿态
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetWorldPos, System.Action onGrabbedCallback = null)
        {
            if (_isGrabbing || _armTransform == null) return;
            StartCoroutine(GrabAndEatRoutine(targetWorldPos, onGrabbedCallback));
        }

        private IEnumerator GrabAndEatRoutine(Vector3 targetWorldPos, System.Action onGrabbedCallback)
        {
            _isGrabbing = true;

            // 1. 若 FlesherApe 挂有 Animator，自动发送 Animator Trigger
            if (_apeAnimator != null)
            {
                _apeAnimator.SetTrigger("Grab");
                _apeAnimator.SetTrigger("Eat");
                try { _apeAnimator.Play("Grab", 0, 0f); } catch { }
            }

            Vector3 startLocalPos = _defaultLocalPos;
            Quaternion startLocalRot = _defaultLocalRot;

            Transform parentT = _armTransform.parent != null ? _armTransform.parent : transform;
            Vector3 targetLocalPos = parentT.InverseTransformPoint(targetWorldPos);
            Vector3 reachDir = (targetLocalPos - startLocalPos).normalized;
            if (reachDir == Vector3.zero) reachDir = Vector3.forward;

            Vector3 grabLocalPos = startLocalPos + reachDir * reachDistance;
            Quaternion grabLocalRot = startLocalRot * Quaternion.Euler(22f, -12f, 15f);

            // 2. FlesherApe 手部优雅伸出向香蕉目标 + 手指抓握收紧 (Reach Out & Grab)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * grabSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _currentGrabLocalPos = Vector3.Lerp(startLocalPos, grabLocalPos, easeT);
                _currentGrabLocalRot = Quaternion.Slerp(startLocalRot, grabLocalRot, easeT);
                _currentCurlFactor = easeT;
                yield return null;
            }

            // 抓到香蕉瞬间的回调（吃蕉咬切 + 生成新香蕉 + 播放声音）
            onGrabbedCallback?.Invoke();

            // 3. FlesherApe 手部将香蕉送回嘴边 (Pull to Mouth)
            Vector3 mouthLocalPos = _defaultLocalPos + new Vector3(-0.06f, 0.08f, -0.04f);
            Quaternion mouthLocalRot = startLocalRot * Quaternion.Euler(18f, -8f, 10f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.85f);
                float easeT = t * t * (3f - 2f * t);
                _currentGrabLocalPos = Vector3.Lerp(grabLocalPos, mouthLocalPos, easeT);
                _currentGrabLocalRot = Quaternion.Slerp(grabLocalRot, mouthLocalRot, easeT);
                yield return null;
            }

            // 4. 咀嚼轻微咽下抖动
            float eatShake = 0f;
            while (eatShake < 0.12f)
            {
                eatShake += Time.deltaTime;
                _currentGrabLocalPos = mouthLocalPos + Random.insideUnitSphere * 0.005f;
                yield return null;
            }

            // 5. 手指松开，平滑恢复至原本 Inspector 调好的姿态
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                _currentGrabLocalPos = Vector3.Lerp(mouthLocalPos, _defaultLocalPos, t);
                _currentGrabLocalRot = Quaternion.Slerp(mouthLocalRot, _defaultLocalRot, t);
                _currentCurlFactor = 1f - t;
                yield return null;
            }

            _currentGrabLocalPos = Vector3.zero;
            _currentCurlFactor = 0f;
            _isGrabbing = false;
        }
    }
}
