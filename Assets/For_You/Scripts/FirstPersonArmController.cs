using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手臂与抓取控制器 (支持 Unity 编辑器手动配置拖拽)
    /// 1. 如果你在 Main Camera 下手动创建并配置好了手臂 Prefab/GameObject，
    ///    把手臂 GameObject 拖入 Inspector 的 armVisual 槽位即可！
    /// 2. 当玩家交互香蕉时，手臂会自动向香蕉方向伸出抓取 (Reach Forward) ➔ 抓握 (Grab) ➔ 拿回嘴边 (Pull to Mouth)。
    /// </summary>
    public class FirstPersonArmController : MonoBehaviour
    {
        public static FirstPersonArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中调好位置的第一视角手臂 GameObject/Prefab")]
        public GameObject armVisual;

        [Tooltip("全局默认吃香蕉/咀嚼音效文件 (.mp3/.wav/.ogg)")]
        public AudioClip defaultEatSound;

        [Header("抓取动画参数")]
        [Tooltip("手臂伸出抓取的目标前伸距离 (调小至 0.22 保持优雅在视野内)")]
        public float reachDistance = 0.22f;

        [Tooltip("手臂伸出与收回的动画速度 (调缓至 4.8f 顺滑不刺眼)")]
        public float grabSpeed = 4.8f;

        [Tooltip("手臂在视角右下角的自然呼吸摇摆幅度")]
        public float swayAmount = 0.012f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isGrabbing = false;
        private Transform _armTransform;

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
            // 1. 优先使用玩家在 Inspector 里手动拖入的 armVisual
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 2. 智能自动寻找场景/Player下已建好的 3D 手部模型 (Hand/Arm/Gorilla)
                Transform foundHand = AutoFindHandMeshInHierarchy();
                if (foundHand != null)
                {
                    armVisual = foundHand.gameObject;
                    _armTransform = foundHand;
                }
                else
                {
                    // 3. 寻找 Main Camera 或 Player 下建好的 FPS_Arm_Holder 节点
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
                        // 若完全没有创建，新建占位 Holder
                        GameObject holderGo = new GameObject("FPS_Arm_Holder");
                        holderGo.transform.SetParent(transform, false);
                        holderGo.transform.localPosition = new Vector3(0.35f, -0.35f, 0.6f);
                        holderGo.transform.localRotation = Quaternion.Euler(20f, -25f, 10f);
                        _armTransform = holderGo.transform;
                        armVisual = holderGo;
                    }
                }
            }

            // 🌟 100% 严格记住玩家在 Unity Inspector 里调好的第一视角绝对位置与角度！
            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[FirstPersonArm] 锁定了你在 Inspector 中调好的手臂位置: {_defaultLocalPos}，旋转: {_defaultLocalRot.eulerAngles}</color>");
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
                if (nameLower.Contains("arm") || nameLower.Contains("hand") || nameLower.Contains("gorilla") || nameLower.Contains("player"))
                {
                    Debug.Log($"<color=cyan>[FirstPersonArmController] 自动定位匹配到玩家第一视角手部模型: {r.gameObject.name}</color>");
                    return r.transform;
                }
            }
            return null;
        }

        private void Update()
        {
            if (_armTransform == null || _isGrabbing) return;

            // 第一视角手部自然呼吸与视角摆动 (Idle Sway & Look Lag)
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
            float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.012f, -mouseY * 0.012f, 0f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 6f);
        }

        /// <summary>
        /// 触发手臂抓取与吃蕉动作
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetWorldPos, System.Action onGrabbedCallback = null)
        {
            if (_isGrabbing || _armTransform == null) return;
            StartCoroutine(GrabAndEatRoutine(targetWorldPos, onGrabbedCallback));
        }

        private IEnumerator GrabAndEatRoutine(Vector3 targetWorldPos, System.Action onGrabbedCallback)
        {
            _isGrabbing = true;

            Vector3 startLocalPos = _armTransform.localPosition;
            Quaternion startLocalRot = _armTransform.localRotation;

            Transform parentT = _armTransform.parent != null ? _armTransform.parent : transform;
            Vector3 targetLocalPos = parentT.InverseTransformPoint(targetWorldPos);
            Vector3 reachDir = (targetLocalPos - startLocalPos).normalized;
            if (reachDir == Vector3.zero) reachDir = Vector3.forward;

            Vector3 grabLocalPos = startLocalPos + reachDir * reachDistance;
            Quaternion grabLocalRot = startLocalRot * Quaternion.Euler(12f, -5f, 8f);

            // 1. 手臂优雅伸出向目标 (Reach Out)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * grabSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armTransform.localPosition = Vector3.Lerp(startLocalPos, grabLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(startLocalRot, grabLocalRot, easeT);
                yield return null;
            }

            // 抓到物体后的逻辑回调
            onGrabbedCallback?.Invoke();

            // 2. 手臂平缓拉回嘴边 (Pull to Mouth)
            Vector3 mouthLocalPos = _defaultLocalPos + new Vector3(-0.08f, 0.06f, -0.05f);
            Quaternion mouthLocalRot = startLocalRot * Quaternion.Euler(18f, -8f, 12f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.85f);
                float easeT = t * t * (3f - 2f * t);
                _armTransform.localPosition = Vector3.Lerp(grabLocalPos, mouthLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(grabLocalRot, mouthLocalRot, easeT);
                yield return null;
            }

            // 3. 轻微吞咽抖动
            float eatShake = 0f;
            while (eatShake < 0.12f)
            {
                eatShake += Time.deltaTime;
                _armTransform.localPosition = mouthLocalPos + Random.insideUnitSphere * 0.006f;
                yield return null;
            }

            // 4. 平滑恢复初始位置
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                _armTransform.localPosition = Vector3.Lerp(mouthLocalPos, _defaultLocalPos, t);
                _armTransform.localRotation = Quaternion.Slerp(mouthLocalRot, _defaultLocalRot, t);
                yield return null;
            }

            _isGrabbing = false;
        }
    }
}
