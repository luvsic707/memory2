using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 2 (神性/祈祷 2_God) 第一视角手臂控制器
    /// 继承并延续 Stage 1 手臂架构设计：
    /// 1. 允许在 Inspector 的 armVisual 中自由拖入准备好的 Stage 2 手臂模型/Prefab。
    /// 2. 保持自然的第一视角呼吸摇摆 (Idle Sway) 与视角延迟跟动 (Look Lag)。
    /// 3. 当玩家按 Q 键或进行祈祷交互时，触发虔诚双手合十/求告高举动画 (Prayer Motion)！
    /// </summary>
    public class Stage2PrayerArmController : MonoBehaviour
    {
        public static Stage2PrayerArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中调好位置的 Stage 2 祈祷手臂 GameObject/Prefab")]
        public GameObject armVisual;

        [Header("弯腰跪拜动作参数")]
        [Tooltip("弯腰跪拜下沉深度 (米)")]
        public float bowDropDepth = 0.38f;

        [Tooltip("弯腰前倾前伸距离 (米)")]
        public float bowForwardReach = 0.28f;

        [Tooltip("弯腰前倾俯仰角度 (度)")]
        public float bowTiltAngle = 40f;

        [Tooltip("跪拜时头部/相机下俯倾斜角度 (度)")]
        public float cameraBowAngle = 10f;

        [Tooltip("跪拜动作速度")]
        public float praySpeed = 3.2f;

        [Tooltip("伏地祈祷凝滞保持时间（秒）")]
        public float prayHoldDuration = 0.45f;

        [Tooltip("手臂在视角右下角的自然呼吸摇摆幅度")]
        public float swayAmount = 0.012f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isPraying = false;
        private Transform _armTransform;
        private Camera _playerCam;
        private Quaternion _defaultCamLocalRot;

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
            _playerCam = Camera.main;
            if (_playerCam == null) _playerCam = GetComponentInChildren<Camera>();
            if (_playerCam != null) _defaultCamLocalRot = _playerCam.transform.localRotation;

            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 智能自动寻找 Main Camera 或 Player 下建好的 Stage2_Arm_Holder 节点
                Transform foundHolder = transform.Find("Stage2_Arm_Holder");
                if (foundHolder == null && transform.parent != null)
                {
                    foundHolder = transform.parent.Find("Stage2_Arm_Holder");
                }
                if (foundHolder == null)
                {
                    foundHolder = GameObject.Find("Stage2_Arm_Holder")?.transform;
                }

                if (foundHolder != null)
                {
                    armVisual = foundHolder.gameObject;
                    _armTransform = foundHolder;
                }
            }

            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[Stage2PrayerArm] 锁定手臂坐标: {_defaultLocalPos}，旋转: {_defaultLocalRot.eulerAngles}</color>");
            }
            else
            {
                Debug.LogWarning("[Stage2PrayerArm] 尚未指定 armVisual，请拖入双手模型。");
            }
        }

        private void Update()
        {
            // 按 Q 键随时触发弯腰跪拜双手祈祷动作
            if (Input.GetKeyDown(KeyCode.Q) && !_isPraying)
            {
                Debug.Log("<color=cyan>[Stage2PrayerArm] 收到 Q 键输入！播放弯腰跪拜祈祷动画。</color>");
                PlayPrayerMotion();
            }

            if (_armTransform == null || _isPraying) return;

            // 第一视角手部自然呼吸与视角摆动
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.5f) * swayAmount;
            float swayY = Mathf.Cos(time * 1.8f) * (swayAmount * 1.2f);
            float swayZ = Mathf.Sin(time * 1.2f) * (swayAmount * 0.8f);

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.012f, -mouseY * 0.012f, 0f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ) + lagOffset, Time.deltaTime * 5f);
        }

        /// <summary>
        /// 触发 Stage 2 弯腰/跪拜虔诚祈祷动作
        /// </summary>
        [ContextMenu("Test Prayer Motion")]
        public void PlayPrayerMotion(System.Action onPrayerApexCallback = null)
        {
            if (_isPraying) return;
            if (_armTransform == null)
            {
                SetupArmReferences();
            }
            if (_armTransform == null) return;

            StartCoroutine(BowingKneelMotionRoutine(onPrayerApexCallback));
        }

        /// <summary>
        /// 弯腰/跪拜抛物线弧线动作 (Bowing & Kneeling Arc Motion)
        /// </summary>
        private IEnumerator BowingKneelMotionRoutine(System.Action onPrayerApexCallback)
        {
            _isPraying = true;

            Vector3 startLocalPos = _defaultLocalPos;
            Quaternion startLocalRot = _defaultLocalRot;

            if (_playerCam != null) _defaultCamLocalRot = _playerCam.transform.localRotation;

            // 1. 弯腰跪伏下沉抛物线 (Bow & Kneel Arc Downward)
            // 手臂向下方、前伸，角度随上身前倾俯仰
            Vector3 bowApexPos = startLocalPos + new Vector3(0f, -bowDropDepth, bowForwardReach);
            Quaternion bowApexRot = startLocalRot * Quaternion.Euler(bowTiltAngle, 0f, 0f);
            Quaternion camBowRot = (_playerCam != null) ? _defaultCamLocalRot * Quaternion.Euler(cameraBowAngle, 0f, 0f) : Quaternion.identity;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * praySpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f); // 弧线缓动

                // 注入贝塞尔弧线偏置，让双手划过一条优美的跪拜抛物线
                Vector3 arcOffset = new Vector3(0f, Mathf.Sin(easeT * Mathf.PI) * 0.08f, 0f);

                _armTransform.localPosition = Vector3.Lerp(startLocalPos, bowApexPos, easeT) + arcOffset;
                _armTransform.localRotation = Quaternion.Slerp(startLocalRot, bowApexRot, easeT);

                if (_playerCam != null)
                {
                    _playerCam.transform.localRotation = Quaternion.Slerp(_defaultCamLocalRot, camBowRot, easeT);
                }

                yield return null;
            }

            // 达到伏地祈祷顶点，触发光效、神圣平息与数据记录
            onPrayerApexCallback?.Invoke();
            if (Stage2JuiceEffects.Instance != null)
            {
                Stage2JuiceEffects.Instance.TriggerPrayerJuice(_armTransform.position);
            }

            // 2. 伏地凝滞微颤 (Hold at Kneeling Apex)
            float holdTimer = 0f;
            while (holdTimer < prayHoldDuration)
            {
                holdTimer += Time.deltaTime;
                Vector3 tremor = Random.insideUnitSphere * 0.005f;
                _armTransform.localPosition = bowApexPos + tremor;
                yield return null;
            }

            // 3. 身躯起身恢复弧线 (Rise Back Arc)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (praySpeed * 0.9f);
                float easeT = t * t * (3f - 2f * t);

                Vector3 riseArc = new Vector3(0f, Mathf.Sin(easeT * Mathf.PI) * 0.05f, 0f);

                _armTransform.localPosition = Vector3.Lerp(bowApexPos, startLocalPos, easeT) + riseArc;
                _armTransform.localRotation = Quaternion.Slerp(bowApexRot, startLocalRot, easeT);

                if (_playerCam != null)
                {
                    _playerCam.transform.localRotation = Quaternion.Slerp(camBowRot, _defaultCamLocalRot, easeT);
                }

                yield return null;
            }

            _armTransform.localPosition = _defaultLocalPos;
            _armTransform.localRotation = _defaultLocalRot;
            if (_playerCam != null) _playerCam.transform.localRotation = _defaultCamLocalRot;
            _isPraying = false;
        }
    }
}
    }
}
