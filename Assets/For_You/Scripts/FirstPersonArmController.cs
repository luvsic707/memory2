using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手部模型控制器 (完全撤回抓取程序化动画位移，恢复最稳定顺畅的场景原生手部姿态)
    /// 1. 彻底撤回所有手部前伸与旋转抓取位移，绝无任何模型错位、翻转或漂移 Bug。
    /// 2. 100% 保持你在 Inspector 中调好的 FlesherApe 原生手部位置。
    /// 3. 当玩家按下 Q 键时，瞬间顺畅触发咬切、堆叠 2-10 个崭新香蕉与音效计数。
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
            if (_armTransform == null) return;

            // 第一视角手部微弱呼吸摇摆
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
            float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ), Time.deltaTime * 5f);
        }

        /// <summary>
        /// 触发交互吃香蕉逻辑 (已撤回抓取程序化位移，直接瞬间顺畅触发逻辑)
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetBananaWorldPos, System.Action onGrabbedCallback = null)
        {
            // 秒级直接触发咬切截面、生成 2-10 个崭新完整香蕉与咀嚼音效
            onGrabbedCallback?.Invoke();
        }
    }
}
