using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 第一视角手部触碰、拖拽与复位控制器
    /// 1. 手部向前伸出精准接触香蕉模型 3D 位置 (Reach Forward & Contact via InverseTransformPoint)；
    /// 2. 接触香蕉后手部向玩家方向 (startLocalPos) 相对回拖 40% 距离；
    /// 3. 手部松开并平滑复位 (Release & Return)；
    /// 4. 触发香蕉回弹与切面/生成逻辑。
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
        private bool _isGrabbing = false;

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
            if (_armTransform == null || _isGrabbing) return;

            // 第一视角手部微弱呼吸摇摆
            float time = Time.time;
            float swayX = Mathf.Sin(time * 1.8f) * swayAmount;
            float swayY = Mathf.Cos(time * 2.2f) * (swayAmount * 1.2f);
            float swayZ = Mathf.Sin(time * 1.5f) * (swayAmount * 0.8f);

            _armTransform.localPosition = Vector3.Lerp(_armTransform.localPosition, _defaultLocalPos + new Vector3(swayX, swayY, swayZ), Time.deltaTime * 5f);
        }

        /// <summary>
        /// 驱动手部产生 4 阶段精细抓拉动作：
        /// 1. 手前伸精准接触香蕉 3D 转换落点；
        /// 2. 接触后手往 Player 方向相对回拉 40%；
        /// 3. 手松开并平滑复位；
        /// 4. 触发香蕉回弹与切面/生成逻辑。
        /// </summary>
        public void PlayGrabAndEatMotion(Vector3 targetBananaWorldPos, System.Action onCompleteCallback = null)
        {
            if (_isGrabbing || _armTransform == null)
            {
                onCompleteCallback?.Invoke();
                return;
            }
            StartCoroutine(ReachContactDragRoutine(targetBananaWorldPos, onCompleteCallback));
        }

        private IEnumerator ReachContactDragRoutine(Vector3 targetBananaWorldPos, System.Action onCompleteCallback)
        {
            _isGrabbing = true;

            Vector3 startLocalPos = _defaultLocalPos;
            Quaternion startLocalRot = _defaultLocalRot;

            // 关键修复：把香蕉的世界坐标转换成本地坐标，作为真正的接触点
            Transform refParent = _armTransform.parent;
            Vector3 contactLocalPos = refParent != null
                ? refParent.InverseTransformPoint(targetBananaWorldPos)
                : targetBananaWorldPos;

            // 在此基础上叠加一个极小的手型微调抓握姿态 offset
            Vector3 graspOffset = new Vector3(-0.02f, -0.01f, 0.0f);
            contactLocalPos += graspOffset;

            Quaternion contactLocalRot = startLocalRot * Quaternion.Euler(12f, -8f, 10f);

            float elapsed = 0f;
            float reachDuration = 0.10f;
            while (elapsed < reachDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / reachDuration;
                float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                _armTransform.localPosition = Vector3.Lerp(startLocalPos, contactLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(startLocalRot, contactLocalRot, easeT);
                yield return null;
            }

            // ── Phase 2: 抓住后往回拖拽，方向朝 player（即朝 startLocalPos 靠近 40%）──
            Vector3 dragBackLocalPos = Vector3.Lerp(contactLocalPos, startLocalPos, 0.4f);
            Quaternion dragBackLocalRot = startLocalRot * Quaternion.Euler(18f, -12f, 15f);

            elapsed = 0f;
            float dragDuration = 0.10f;
            while (elapsed < dragDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dragDuration;
                float easeT = t * t * (3f - 2f * t);
                _armTransform.localPosition = Vector3.Lerp(contactLocalPos, dragBackLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(contactLocalRot, dragBackLocalRot, easeT);
                yield return null;
            }

            // ── Phase 3: 手松开并平滑复位 ──────
            elapsed = 0f;
            float returnDuration = 0.12f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                _armTransform.localPosition = Vector3.Lerp(dragBackLocalPos, startLocalPos, easeT);
                _armTransform.localRotation = Quaternion.Slerp(dragBackLocalRot, startLocalRot, easeT);
                yield return null;
            }

            _armTransform.localPosition = startLocalPos;
            _armTransform.localRotation = startLocalRot;
            _isGrabbing = false;

            onCompleteCallback?.Invoke();
        }
    }
}
