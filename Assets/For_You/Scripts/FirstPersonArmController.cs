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

        [Header("抓取动画参数")]
        [Tooltip("手臂伸出抓取的目标前伸距离")]
        public float reachDistance = 0.85f;

        [Tooltip("手臂伸出与收回的动画速度")]
        public float grabSpeed = 8.5f;

        [Tooltip("手臂在视角右下角的自然呼吸摇摆幅度")]
        public float swayAmount = 0.015f;

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
            // 优先使用玩家在 Inspector 里手动拖入的 armVisual
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 如果场景里有同名的 "FPS_Arm_Holder" 或子物体，自动寻找
                Transform foundHolder = transform.Find("FPS_Arm_Holder");
                if (foundHolder != null)
                {
                    _armTransform = foundHolder;
                }
                else
                {
                    // 若玩家完全没有手动配置，创建一个简单的占位容器供测试
                    GameObject holderGo = new GameObject("FPS_Arm_Holder");
                    holderGo.transform.SetParent(transform, false);
                    holderGo.transform.localPosition = new Vector3(0.35f, -0.35f, 0.6f);
                    holderGo.transform.localRotation = Quaternion.Euler(20f, -25f, 10f);
                    _armTransform = holderGo.transform;
                }
            }

            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
            }
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
            Quaternion grabLocalRot = Quaternion.LookRotation(reachDir) * Quaternion.Euler(30f, -10f, 15f);

            // 1. 手臂伸出向目标 (Reach Out)
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

            // 2. 手臂快速拉回嘴边 (Pull to Mouth)
            Vector3 mouthLocalPos = _defaultLocalPos + new Vector3(-0.25f, 0.15f, -0.2f);
            Quaternion mouthLocalRot = Quaternion.Euler(45f, -10f, 30f);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (grabSpeed * 0.9f);
                float easeT = t * t * (3f - 2f * t);
                _armTransform.localPosition = Vector3.Lerp(grabLocalPos, mouthLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(grabLocalRot, mouthLocalRot, easeT);
                yield return null;
            }

            // 3. 轻微吞咽抖动
            float eatShake = 0f;
            while (eatShake < 0.16f)
            {
                eatShake += Time.deltaTime;
                _armTransform.localPosition = mouthLocalPos + Random.insideUnitSphere * 0.012f;
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
