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

        [Header("祈祷动作参数")]
        [Tooltip("祈祷抬起前伸距离 (推荐 0.18f ~ 0.25f)")]
        public float prayRaiseDistance = 0.20f;

        [Tooltip("祈祷动作提升高度 (推荐 0.12f)")]
        public float prayRaiseHeight = 0.12f;

        [Tooltip("祈祷动作速度")]
        public float praySpeed = 4.2f;

        [Tooltip("祈祷持续保持时间（秒）")]
        public float prayHoldDuration = 0.45f;

        [Tooltip("手臂在视角右下角的自然呼吸摇摆幅度")]
        public float swayAmount = 0.012f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isPraying = false;
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
                else
                {
                    // 若完全没有创建，自动新建占位 Holder
                    GameObject holderGo = new GameObject("Stage2_Arm_Holder");
                    holderGo.transform.SetParent(transform, false);
                    holderGo.transform.localPosition = new Vector3(0f, -0.32f, 0.55f);
                    holderGo.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                    _armTransform = holderGo.transform;
                    armVisual = holderGo;
                }
            }

            if (_armTransform != null)
            {
                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[Stage2PrayerArm] 锁定了你在 Inspector 中配置的 Stage 2 手臂位置: {_defaultLocalPos}，旋转: {_defaultLocalRot.eulerAngles}</color>");
            }
        }

        private void Update()
        {
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
        /// 触发 Stage 2 虔诚祈祷动作
        /// </summary>
        public void PlayPrayerMotion(System.Action onPrayerApexCallback = null)
        {
            if (_isPraying || _armTransform == null) return;
            StartCoroutine(PrayerMotionRoutine(onPrayerApexCallback));
        }

        private IEnumerator PrayerMotionRoutine(System.Action onPrayerApexCallback)
        {
            _isPraying = true;

            Vector3 startLocalPos = _armTransform.localPosition;
            Quaternion startLocalRot = _armTransform.localRotation;

            // 祈祷高举目标位姿（向中央合十抬起）
            Vector3 prayLocalPos = startLocalPos + new Vector3(0f, prayRaiseHeight, prayRaiseDistance);
            Quaternion prayLocalRot = startLocalRot * Quaternion.Euler(-22f, 0f, 0f);

            // 1. 双手平缓高举求告 (Raise Hands)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * praySpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armTransform.localPosition = Vector3.Lerp(startLocalPos, prayLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(startLocalRot, prayLocalRot, easeT);
                yield return null;
            }

            // 达到祈祷最高点，触发闪光/音效等游戏逻辑
            onPrayerApexCallback?.Invoke();

            // 2. 祈祷凝滞微颤 (Hold & Sacred Tremor)
            float holdTimer = 0f;
            while (holdTimer < prayHoldDuration)
            {
                holdTimer += Time.deltaTime;
                Vector3 tremor = Random.insideUnitSphere * 0.003f;
                _armTransform.localPosition = prayLocalPos + tremor;
                yield return null;
            }

            // 3. 平滑归位 (Return to Idle)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (praySpeed * 0.85f);
                float easeT = t * t * (3f - 2f * t);
                _armTransform.localPosition = Vector3.Lerp(prayLocalPos, startLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(prayLocalRot, startLocalRot, easeT);
                yield return null;
            }

            _armTransform.localPosition = _defaultLocalPos;
            _armTransform.localRotation = _defaultLocalRot;
            _isPraying = false;
        }
    }
}
