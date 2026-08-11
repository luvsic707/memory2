using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (西西弗斯推石/荒诞劳作 3_Rock) 第一视角推石手臂控制器
    /// 继承延续 Stage 1 / Stage 2 / Stage 4 的统一手臂架构设计：
    /// 1. 允许在 Inspector 的 armVisual 中自由拖入准备好的 Stage 3 苦工推石手臂模型/Prefab。
    /// 2. 支持【手动编辑模式】(overrideBonesViaScript = false)，绝不锁定你手动调好的关节角度。
    /// 3. 当玩家推动巨石/按键推进时，触发双手死死抵住巨石发力前推的肌肉颤抖推石动画 (Push Motion)！
    /// </summary>
    public class Stage3PushArmController : MonoBehaviour
    {
        public static Stage3PushArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中调好位置的 Stage 3 推石手臂 GameObject/Prefab")]
        public GameObject armVisual;

        [Header("手动姿态模式")]
        [Tooltip("【默认取消勾选】：取消勾选时，脚本不会强行覆盖你的骨骼！你可以直接在 Unity Scene 视图中旋转手臂关节点！")]
        public bool overrideBonesViaScript = false;

        [Header("镜像左手配置")]
        [Tooltip("拖入要作为左手的那个 3D 模型，脚本在启动时会自动将其 Scale.X 设为 -1，将其完美镜像转换为左手！")]
        public Transform leftHandToMirror;

        [Header("推石发力动作参数")]
        [Tooltip("推石前压距离 (米)")]
        public float pushDistance = 0.26f;

        [Tooltip("推石抬高距离 (米)")]
        public float pushHeight = 0.08f;

        [Tooltip("推石前倾角度 (度)")]
        public float pushTiltAngle = -18f;

        [Tooltip("推石动作推进速度")]
        public float pushSpeed = 4.0f;

        [Tooltip("推石肌肉发力震颤强度 (米)")]
        public float pushTremorIntensity = 0.005f;

        [Tooltip("发力顶点凝滞保持时间（秒）")]
        public float pushHoldDuration = 0.35f;

        [Tooltip("手臂自然发力呼吸摇晃幅度")]
        public float swayAmount = 0.010f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isPushing = false;
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
            // 自动镜像左手模型
            if (leftHandToMirror != null)
            {
                Vector3 curScale = leftHandToMirror.localScale;
                if (curScale.x > 0)
                {
                    leftHandToMirror.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
                    Debug.Log($"<color=cyan>[Stage3PushArm] 自动镜像翻转了 '{leftHandToMirror.name}' (Scale.X = -1)，成功转换为左手！</color>");
                }
            }
            if (armVisual != null)
            {
                _armTransform = armVisual.transform;
            }
            else
            {
                // 智能自动寻找 Main Camera 或 Player 下建好的 Stage3_Arm_Holder 节点
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Transform foundHolder = mainCam.transform.Find("Stage3_Arm_Holder");
                    if (foundHolder == null) foundHolder = mainCam.transform.Find("FPS_Arm_Holder");
                    if (foundHolder == null) foundHolder = mainCam.transform.GetComponentInChildren<Transform>();

                    if (foundHolder != null && foundHolder != mainCam.transform)
                    {
                        armVisual = foundHolder.gameObject;
                        _armTransform = foundHolder;
                    }
                }
            }

            if (_armTransform != null)
            {
                // 禁用可能强制重置姿态的 Animator
                Animator anim = _armTransform.GetComponent<Animator>();
                if (anim == null) anim = _armTransform.GetComponentInChildren<Animator>();
                if (anim != null && anim.enabled)
                {
                    anim.enabled = false;
                    Debug.Log($"<color=yellow>[Stage3PushArm] 自动禁用了 '{anim.gameObject.name}' 上的 Animator，完全解锁手动/脚本姿态控制！</color>");
                }

                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[Stage3PushArm] 成功锁定 Stage 3 第一视角推石手臂: {_armTransform.name}</color>");
            }
            else
            {
                Debug.LogWarning("[Stage3PushArm] 尚未指定 armVisual，请在 Inspector 中拖入 Stage 3 手臂模型。");
            }
        }

        private void Update()
        {
            // 按 W 键 / 按下前进推石时自动触发手臂前推动作
            if ((Input.GetKeyDown(KeyCode.W) || Input.GetMouseButtonDown(0)) && !_isPushing)
            {
                PlayPushMotion();
            }

            if (_armTransform == null || _isPushing) return;

            // 第一视角推石手臂自然发力呼吸与视角跟摆
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.6f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.0f) * (swayAmount * 1.2f);

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            Vector3 lagOffset = new Vector3(-mouseX * 0.010f, -mouseY * 0.010f, 0f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, 0f) + lagOffset, Time.deltaTime * 5f);
        }

        /// <summary>
        /// 触发 Stage 3 双手死死抵住巨石发力前推动作
        /// </summary>
        [ContextMenu("Test Push Motion")]
        public void PlayPushMotion(System.Action onPushApexCallback = null)
        {
            if (_isPushing) return;
            if (_armTransform == null) SetupArmReferences();
            if (_armTransform == null) return;

            StartCoroutine(PushMotionRoutine(onPushApexCallback));
        }

        private IEnumerator PushMotionRoutine(System.Action onPushApexCallback)
        {
            _isPushing = true;

            Vector3 startLocalPos = _defaultLocalPos;
            Quaternion startLocalRot = _defaultLocalRot;

            // 1. 双手死死抵住巨石发力前压 (Push Forward Against Boulder)
            Vector3 pushApexPos = startLocalPos + new Vector3(0f, pushHeight, pushDistance);
            Quaternion pushApexRot = startLocalRot * Quaternion.Euler(pushTiltAngle, 0f, 0f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * pushSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f); // 弧线发力

                Vector3 arcOffset = new Vector3(0f, Mathf.Sin(easeT * Mathf.PI) * 0.03f, 0f);

                _armTransform.localPosition = Vector3.Lerp(startLocalPos, pushApexPos, easeT) + arcOffset;
                _armTransform.localRotation = Quaternion.Slerp(startLocalRot, pushApexRot, easeT);
                yield return null;
            }

            // 发力顶点回调
            onPushApexCallback?.Invoke();

            // 2. 发力极限肌肉震颤 (Muscle Strain Tremor)
            float holdTimer = 0f;
            while (holdTimer < pushHoldDuration)
            {
                holdTimer += Time.deltaTime;
                Vector3 tremor = Random.insideUnitSphere * pushTremorIntensity;
                _armTransform.localPosition = pushApexPos + tremor;
                yield return null;
            }

            // 3. 力量归位 (Release & Recoil Back)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (pushSpeed * 0.85f);
                float easeT = t * t * (3f - 2f * t);

                _armTransform.localPosition = Vector3.Lerp(pushApexPos, startLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(pushApexRot, startLocalRot, easeT);
                yield return null;
            }

            _armTransform.localPosition = _defaultLocalPos;
            _armTransform.localRotation = _defaultLocalRot;
            _isPushing = false;
        }
    }
}
