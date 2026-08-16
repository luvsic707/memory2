using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手部模型 (FlesherApe) 抓取与啃咬动作控制器
    /// 1. 自动追踪视角前方的香蕉，通过 3D 世界坐标驱动 FlesherApe 手部前伸抓取 (Reach Out) ➔ 抓握 (Snatch) ➔ 拿回嘴边 (Pull to Mouth) ➔ 平滑归位。
    /// 2. 彻底解决 FBX 本地缩放/坐标轴不匹配导致的抓取无效果问题。
    /// 3. 支持 Amature 骨骼手指关节弯曲与自动复位。
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
        [Tooltip("手部前伸抓取的前方目标距离 (米)")]
        public float reachDistance = 0.55f;

        [Tooltip("手部伸出与收回的动画速度 (调缓至 4.5f 顺滑自然)")]
        public float grabSpeed = 4.5f;

        [Tooltip("手部在视角视野中的自然呼吸摇摆幅度")]
        public float swayAmount = 0.006f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isGrabbing = false;
        private Transform _armTransform;

        private Transform[] _fingerBones;
        private Quaternion[] _fingerDefaultRotations;

        private Vector3 _grabTargetWorldPos;
        private Quaternion _grabTargetWorldRot;
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
            // 1. 优先使用 Inspector 里配置的 FlesherApe 模型或其父级
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 2. 自动在 Main Camera / Player 下寻找 FlesherApe 或 FPS_Arm_Holder
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

            // 🌟 严格记录你在 Inspector 中调好的 FlesherApe 默认本地位置与旋转
            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;

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
                    nameLower.Contains("digit") || nameLower.Contains("hand"))
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
                Quaternion targetRot = _fingerDefaultRotations[i] * Quaternion.Euler(curlFactor * 40f, 0f, 0f);
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
                // 第一视角呼吸摇摆
                float time = Time.time;
                float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
                float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
                float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                Vector3 lagOffset = new Vector3(-mouseX * 0.008f, -mouseY * 0.008f, 0f);

                _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 5f);
            }
            else
            {
                // 在 LateUpdate 中根据 3D 世界坐标强行驱动 FlesherApe 手部伸手抓取
                if (_grabTargetWorldPos != Vector3.zero)
                {
                    _armTransform.position = Vector3.Lerp(_armTransform.position, _grabTargetWorldPos, Time.deltaTime * (grabSpeed * 2.5f));
                    _armTransform.rotation = Quaternion.Slerp(_armTransform.rotation, _grabTargetWorldRot, Time.deltaTime * (grabSpeed * 2.5f));
                }
                SetFingerCurl(_currentCurlFactor);
            }
        }

        /// <summary>
        /// 触发 FlesherApe 手部 3D 世界坐标抓取与吃蕉动作
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetBananaWorldPos, System.Action onGrabbedCallback = null)
        {
            if (_isGrabbing || _armTransform == null) return;
            StartCoroutine(GrabAndEatRoutine(targetBananaWorldPos, onGrabbedCallback));
        }

        private IEnumerator GrabAndEatRoutine(Vector3 targetBananaWorldPos, System.Action onGrabbedCallback)
        {
            _isGrabbing = true;

            Camera mainCam = Camera.main;
            Transform camT = mainCam != null ? mainCam.transform : transform;

            Vector3 startWorldPos = _armTransform.position;
            Quaternion startWorldRot = _armTransform.rotation;

            // 1. 计算手部抓取的前伸世界坐标（直接伸向相机前方/香蕉方向）
            Vector3 grabTargetPos = camT.position + camT.forward * reachDistance + camT.right * 0.1f - camT.up * 0.12f;
            Quaternion grabTargetRot = Quaternion.LookRotation(camT.forward, camT.up) * Quaternion.Euler(35f, -15f, 20f);

            _grabTargetWorldPos = grabTargetPos;
            _grabTargetWorldRot = grabTargetRot;

            // 手臂前伸并屈指抱捏抓取
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * grabSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _grabTargetWorldPos = Vector3.Lerp(startWorldPos, grabTargetPos, easeT);
                _grabTargetWorldRot = Quaternion.Slerp(startWorldRot, grabTargetRot, easeT);
                _currentCurlFactor = easeT;
                yield return null;
            }

            // 抓到香蕉瞬间的回调（切削咬口 + 堆叠 2-10 个新香蕉 + 声音）
            onGrabbedCallback?.Invoke();

            // 2. 将手部拉回嘴边 (Pull to Mouth)
            Vector3 mouthTargetPos = camT.position + camT.forward * 0.3f - camT.up * 0.16f;
            Quaternion mouthTargetRot = Quaternion.LookRotation(camT.forward, camT.up) * Quaternion.Euler(20f, -10f, 10f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.85f);
                float easeT = t * t * (3f - 2f * t);
                _grabTargetWorldPos = Vector3.Lerp(grabTargetPos, mouthTargetPos, easeT);
                _grabTargetWorldRot = Quaternion.Slerp(grabTargetRot, mouthTargetRot, easeT);
                yield return null;
            }

            // 3. 咀嚼抖动
            float eatShake = 0f;
            while (eatShake < 0.12f)
            {
                eatShake += Time.deltaTime;
                _grabTargetWorldPos = mouthTargetPos + Random.insideUnitSphere * 0.005f;
                yield return null;
            }

            // 4. 手指松开，手臂平滑归位至默认姿态
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                Vector3 defaultWorldPos = _armTransform.parent != null ? _armTransform.parent.TransformPoint(_defaultLocalPos) : _defaultLocalPos;
                Quaternion defaultWorldRot = _armTransform.parent != null ? _armTransform.parent.rotation * _defaultLocalRot : _defaultLocalRot;

                _grabTargetWorldPos = Vector3.Lerp(mouthTargetPos, defaultWorldPos, t);
                _grabTargetWorldRot = Quaternion.Slerp(mouthTargetRot, defaultWorldRot, t);
                _currentCurlFactor = 1f - t;
                yield return null;
            }

            _grabTargetWorldPos = Vector3.zero;
            _currentCurlFactor = 0f;
            _isGrabbing = false;
        }
    }
}
