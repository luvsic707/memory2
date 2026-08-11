using System.Collections;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 4 (90年代职场异化 4_Modern) 第一视角职场打字手臂控制器
    /// 1. 自动将 Inspector 中挂载的人墙/人物模型（如 Teacher Female Narration 01 / Wolf3D Avatar）
    ///    从默认的 T-Pose（大字型）姿态调整为优雅放在键盘上的第一视角打字姿态！
    /// 2. 每次玩家点击鼠标打字时，触发双手在键盘上的高频敲击按压动画 (Typing Tap Motion)！
    /// </summary>
    public class Stage4OfficeArmController : MonoBehaviour
    {
        public static Stage4OfficeArmController Instance { get; private set; }

        [Header("手动配置 (Inspector 拖拽)")]
        [Tooltip("你在 Unity 场景中放入的人物模型/手臂 GameObject")]
        public GameObject armVisual;

        [Header("手部放置与调整参数")]
        [Tooltip("手臂相对于相机的初始本地偏移坐标 (下移 -0.62m 藏住躯干，手伸向键盘正前方)")]
        public Vector3 armLocalPosition = new Vector3(0f, -0.62f, 0.35f);

        [Tooltip("手臂相对于相机的初始旋转角度")]
        public Vector3 armLocalRotation = Vector3.zero;

        [Tooltip("手臂缩放比例")]
        public Vector3 armLocalScale = Vector3.one;

        [Header("骨骼笔直向前伸姿态参数 (Wolf3D 专属)")]
        [Tooltip("左上臂旋转角度 (向前平伸: -85, 0, -90 或 0, 85, 0)")]
        public Vector3 leftUpperArmRotation = new Vector3(-85f, 0f, -90f);

        [Tooltip("右上臂旋转角度 (向前平伸: -85, 0, 90 或 0, -85, 0)")]
        public Vector3 rightUpperArmRotation = new Vector3(-85f, 0f, 90f);

        [Tooltip("前臂旋转角度 (0, 0, 0 保持笔直向前)")]
        public Vector3 forearmRotation = Vector3.zero;

        [Tooltip("手掌旋转角度 (0, 0, 0 保持水平朝前)")]
        public Vector3 handRotation = Vector3.zero;

        [Header("打字动作参数")]
        [Tooltip("打字敲击时下沉距离 (米)")]
        public float typingTapDepth = 0.018f;

        [Tooltip("打字敲击倾斜角度 (度)")]
        public float typingTapAngle = 6f;

        [Tooltip("打字动作速度")]
        public float typingSpeed = 16f;

        [Tooltip("桌面打字自然呼吸摇晃幅度")]
        public float swayAmount = 0.005f;

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private bool _isTyping = false;
        private Transform _armTransform;
        private Transform _leftArmBone;
        private Transform _rightArmBone;

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
                // 自动在 Main Camera 或 Player 下寻找人物模型节点
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Transform foundAvatar = mainCam.transform.Find("Teacher Female Narration 01 (1)");
                    if (foundAvatar == null) foundAvatar = mainCam.transform.Find("Female Teacher 01");
                    if (foundAvatar == null) foundAvatar = mainCam.transform.GetComponentInChildren<Transform>();

                    if (foundAvatar != null && foundAvatar != mainCam.transform)
                    {
                        armVisual = foundAvatar.gameObject;
                        _armTransform = foundAvatar;
                    }
                }
            }

            if (_armTransform != null)
            {
                // 禁用可能重置 T-Pose 的 Animator，解锁 C# 程序化手部姿态
                Animator anim = _armTransform.GetComponent<Animator>();
                if (anim == null) anim = _armTransform.GetComponentInChildren<Animator>();
                if (anim != null && anim.enabled)
                {
                    anim.enabled = false;
                    Debug.Log($"<color=yellow>[Stage4OfficeArm] 自动禁用了 '{anim.gameObject.name}' 上的 Animator，解锁 T-Pose 程序化打字姿态控制！</color>");
                }

                // 寻找骨骼节点并将双臂向前收拢，直伸笔直放在键盘面前！
                FindAndSetArmBones();

                // 应用玩家配置的打字位置
                ApplyTypingPoseTransform();

                _defaultLocalPos = _armTransform.localPosition;
                _defaultLocalRot = _armTransform.localRotation;
                Debug.Log($"<color=green>[Stage4OfficeArm] 成功锁定 Stage 4 第一视角打字手臂: {_armTransform.name}</color>");
            }
            else
            {
                Debug.LogWarning("[Stage4OfficeArm] 未找到 armVisual 人物模型，请在 Inspector 面板拖入人物模型。");
            }
        }

        /// <summary>
        /// 程序化将人形 T-Pose 手臂转动为笔直向正前方伸向键盘的 FPS 打字姿态
        /// </summary>
        [ContextMenu("Set Straight FPS Arm Pose")]
        public void FindAndSetArmBones()
        {
            if (_armTransform == null) return;

            Transform[] allTransforms = _armTransform.GetComponentsInChildren<Transform>();
            foreach (Transform t in allTransforms)
            {
                string nameLower = t.name.ToLower();
                // 上臂：向前收拢向内倾斜
                if (nameLower.Contains("leftarm") || nameLower.Contains("leftupperarm") || nameLower.Contains("left_arm"))
                {
                    _leftArmBone = t;
                    t.localRotation = Quaternion.Euler(leftUpperArmRotation);
                }
                else if (nameLower.Contains("rightarm") || nameLower.Contains("rightupperarm") || nameLower.Contains("right_arm"))
                {
                    _rightArmBone = t;
                    t.localRotation = Quaternion.Euler(rightUpperArmRotation);
                }
                // 前臂：笔直向前伸向屏幕/键盘正前方
                else if (nameLower.Contains("leftforearm") || nameLower.Contains("left_forearm"))
                {
                    t.localRotation = Quaternion.Euler(forearmRotation);
                }
                else if (nameLower.Contains("rightforearm") || nameLower.Contains("right_forearm"))
                {
                    t.localRotation = Quaternion.Euler(-forearmRotation.x, -forearmRotation.y, forearmRotation.z);
                }
                // 手掌：伏在键盘上方
                else if (nameLower.Contains("lefthand") || nameLower.Contains("left_hand"))
                {
                    t.localRotation = Quaternion.Euler(handRotation);
                }
                else if (nameLower.Contains("righthand") || nameLower.Contains("right_hand"))
                {
                    t.localRotation = Quaternion.Euler(handRotation.x, -handRotation.y, -handRotation.z);
                }
            }
        }

        /// <summary>
        /// 应用 Inspector 参数到手臂 Transform
        /// </summary>
        [ContextMenu("Apply Typing Pose Transform")]
        public void ApplyTypingPoseTransform()
        {
            if (_armTransform != null)
            {
                _armTransform.localPosition = armLocalPosition;
                _armTransform.localRotation = Quaternion.Euler(armLocalRotation);
                _armTransform.localScale = armLocalScale;
                _defaultLocalPos = armLocalPosition;
                _defaultLocalRot = Quaternion.Euler(armLocalRotation);
            }
        }

        private void Update()
        {
            if (_armTransform == null || _isTyping) return;

            // 第一视角桌面自然打字呼吸摇摆
            float time = Time.time;
            float swayX = Mathf.Sin(time * 2f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.5f) * (swayAmount * 1.2f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, 0f), Time.deltaTime * 6f);
        }

        /// <summary>
        /// 由 Stage4Controller 或点击事件调用：触发键盘按压打字敲击动画
        /// </summary>
        [ContextMenu("Test Typing Motion")]
        public void PlayTypingMotion()
        {
            if (_armTransform == null) return;
            StartCoroutine(TypingTapRoutine());
        }

        private IEnumerator TypingTapRoutine()
        {
            _isTyping = true;

            Vector3 startPos = _armTransform.localPosition;
            Quaternion startRot = _armTransform.localRotation;

            // 左右手交替敲击效果
            float sideX = (Random.value < 0.5f ? 1f : -1f) * 0.008f;
            Vector3 tapPos = startPos + new Vector3(sideX, -typingTapDepth, 0.01f);
            Quaternion tapRot = startRot * Quaternion.Euler(typingTapAngle, Random.Range(-3f, 3f), 0f);

            // 1. 快速向下敲击键盘 (Press Key)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * typingSpeed;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armTransform.localPosition = Vector3.Lerp(startPos, tapPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(startRot, tapRot, easeT);
                yield return null;
            }

            // 2. 弹回恢复 (Key Release)
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (typingSpeed * 0.9f);
                float easeT = t * t * (3f - 2f * t);
                _armTransform.localPosition = Vector3.Lerp(tapPos, _defaultLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(tapRot, _defaultLocalRot, easeT);
                yield return null;
            }

            _armTransform.localPosition = _defaultLocalPos;
            _armTransform.localRotation = _defaultLocalRot;
            _isTyping = false;
        }
    }
}
