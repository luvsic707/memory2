using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 3 (3_Rock 莫比乌斯西西弗斯推石) 电影级镜头渐进拉远与全景揭示控制器
    /// 1. 玩家进入关卡时为贴身第一视角（看得到苦工双手死死抵住巨石）。
    /// 2. 随着每次点击/按 W 推动巨石前进，相机距离逐渐平滑向后、向上拉远！
    /// 3. 从第一视角 ➔ 俯瞰全景 ➔ 高空远景，最终全景震撼揭示出整个莫比乌斯环形天海全景！
    /// </summary>
    public class MobiusCameraPanController : MonoBehaviour
    {
        public static MobiusCameraPanController Instance { get; private set; }

        [Header("核心引用")]
        [Tooltip("巨石 Transform 引用（若留空，脚本将自动根据名称包含 rock/boulder/sphere 自动寻找）")]
        public Transform boulderTransform;

        [Tooltip("玩家/相机 Transform 引用")]
        public Transform playerTransform;

        [Header("推石与拉远参数")]
        [Tooltip("每次推石巨石滚动的推进距离 (米)")]
        public float boulderPushStep = 1.2f;

        [Tooltip("触发显示莫比乌斯全景所需的推石总次数")]
        public int maxPushesForPanorama = 12;

        [Tooltip("初始镜头拉远后退距离 (米)")]
        public float initialCamDistance = 0.5f;

        [Tooltip("全景视角最大后退拉远距离 (米)")]
        public float maxPanoramaDistance = 45f;

        [Tooltip("全景视角最大上升高度 (米)")]
        public float maxPanoramaHeight = 25f;

        [Tooltip("全景视角下俯倾斜角度 (度)")]
        public float maxPanoramaPitchAngle = 42f;

        [Tooltip("镜头平滑拉远过渡速度")]
        public float cameraSmoothSpeed = 2.5f;

        private Camera _mainCam;
        private Vector3 _initialCamLocalPos;
        private Quaternion _initialCamLocalRot;
        private Vector3 _initialBoulderPos;

        private int _currentPushCount = 0;
        private float _currentProgress = 0f; // 0 (近景) ~ 1 (莫比乌斯全景)
        private Vector3 _targetCamOffset;
        private Quaternion _targetCamRotation;

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

        [Header("莫比乌斯环曲面轨迹推进参数")]
        [Tooltip("莫比乌斯环 3D Transform 引用（若留空将自动寻找 MobiusStrip (3)）")]
        public Transform mobiusTrackTransform;

        [Tooltip("每次推石玩家沿着莫比乌斯环曲面平移推进的步长距离 (米)")]
        public float playerStepDistance = 1.2f;

        [Header("锁定玩家在莫比乌斯轨道 (彻底解决掉落)")]
        [Tooltip("【默认开启】：关闭 WASD 重力自由下坠，将玩家固定锚定在 MobiusStrip (3) 轨迹上，绝对不会下坠掉落！")]
        public bool lockPlayerToTrack = true;

        [Header("自动对齐巨石到玩家视野正前方")]
        [Tooltip("【取消勾选使用你手动摆好的位置】：若取消勾选，脚本将严格保留你在场景里把 player 放在 MobiusBall (3) 前面的手动位置！")]
        public bool autoPositionBoulderInFront = false;

        [Tooltip("巨石放置在玩家正前方的距离 (米)")]
        public float boulderFrontDistance = 2.2f;

        [Tooltip("巨石高度偏置 (米)")]
        public float boulderHeightOffset = 0.85f;

        private Vector3 _initialPlayerPos;
        private Quaternion _initialPlayerRot;

        private void Start()
        {
            EnsureMobiusColliders();
            SetupReferences();
        }

        /// <summary>
        /// 自动为场景中所有的 MobiusStrip 添加 MeshCollider 碰撞体
        /// </summary>
        private void EnsureMobiusColliders()
        {
            MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.gameObject.name.ToLower().Contains("mobius") || mf.gameObject.name.ToLower().Contains("strip"))
                {
                    if (mf.gameObject.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        Debug.Log($"<color=green>[MobiusCam] 自动为 '{mf.gameObject.name}' 添加了 MeshCollider 碰撞体！</color>");
                    }
                }
            }
        }

        private void SetupReferences()
        {
            _mainCam = Camera.main;
            if (_mainCam == null) _mainCam = FindObjectOfType<Camera>();

            if (_mainCam != null)
            {
                _initialCamLocalPos = _mainCam.transform.localPosition;
                _initialCamLocalRot = _mainCam.transform.localRotation;
            }

            if (playerTransform == null && _mainCam != null)
            {
                playerTransform = _mainCam.transform.parent != null ? _mainCam.transform.parent : _mainCam.transform;
            }

            // 自动寻找 MobiusStrip (3) 环轨迹
            if (mobiusTrackTransform == null)
            {
                foreach (GameObject go in FindObjectsOfType<GameObject>())
                {
                    if (go.name.Contains("MobiusStrip (3)") || go.name.Contains("MobiusStrip"))
                    {
                        mobiusTrackTransform = go.transform;
                        break;
                    }
                }
            }

            // 🌟 核心锚定：禁用玩家 CharacterController 的重力自由下坠，将玩家安全锚定在 Mobius 轨道上！
            if (lockPlayerToTrack && playerTransform != null)
            {
                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    Debug.Log($"<color=yellow>[MobiusCam] 自动禁用了 CharacterController 重力下坠，玩家沿莫比乌斯轨道推石向前！</color>");
                }

                _initialPlayerPos = playerTransform.position;
                _initialPlayerRot = playerTransform.rotation;
            }

            // 自动寻找巨石 (优先匹配 MobiusBall (3) / MobiusBall / Ball / Rock)
            if (boulderTransform == null)
            {
                foreach (GameObject go in FindObjectsOfType<GameObject>())
                {
                    string nameLower = go.name.ToLower();
                    if (nameLower.Contains("mobiusball (3)") || nameLower.Contains("mobiusball") || nameLower.Contains("ball") || nameLower.Contains("rock"))
                    {
                        boulderTransform = go.transform;
                        break;
                    }
                }
            }

            // 若开启了 autoPositionBoulderInFront 才会移动石头位置
            if (autoPositionBoulderInFront && boulderTransform != null && playerTransform != null)
            {
                Vector3 frontPos = playerTransform.position + playerTransform.forward * boulderFrontDistance + playerTransform.up * boulderHeightOffset;
                boulderTransform.position = frontPos;
                Debug.Log($"<color=cyan>[MobiusCam] 自动将巨石 '{boulderTransform.name}' 放置在玩家眼前: {frontPos}</color>");
            }

            if (boulderTransform != null)
            {
                _initialBoulderPos = boulderTransform.position;
                Debug.Log($"<color=green>[MobiusCam] 成功锁定巨石 '{boulderTransform.name}'，全景拉远与滚动位移引擎就绪。</color>");
            }

            UpdateCameraTargetOffset();
        }

        private float _lastPushTime = 0f;
        private float _pushCooldown = 0.35f;
        private float _initialFov = 60f;

        private void Update()
        {
            // 支持按 W / 长按 W / 点击鼠标左键 / 长按鼠标推进巨石
            bool isPushInput = Input.GetKeyDown(KeyCode.W) || Input.GetMouseButtonDown(0)
                            || Input.GetKey(KeyCode.W) || Input.GetMouseButton(0);

            if (isPushInput && Time.time - _lastPushTime >= _pushCooldown)
            {
                _lastPushTime = Time.time;
                OnPushBoulder();
            }
        }

        private void LateUpdate()
        {
            // 🌟 关键修复：在 LateUpdate 执行插值，防止视角脚本在每帧覆盖相机位置！
            SmoothUpdateCameraPosition();
        }

        /// <summary>
        /// 每次推石时调用：玩家与巨石沿着莫比乌斯环曲面向前推进，同时镜头向高空拉远
        /// </summary>
        [ContextMenu("Test Push & Zoom Out")]
        public void OnPushBoulder()
        {
            _currentPushCount++;
            _currentProgress = Mathf.Clamp01((float)_currentPushCount / maxPushesForPanorama);

            // 1. 玩家 (Player) 沿着莫比乌斯环朝向前进！
            if (playerTransform != null)
            {
                Vector3 advanceDir = playerTransform.forward;
                playerTransform.position += advanceDir * playerStepDistance;
            }

            // 2. 巨石 (Boulder) 始终保持在玩家正前方滚动推进！
            if (boulderTransform != null && playerTransform != null)
            {
                boulderTransform.position = playerTransform.position + playerTransform.forward * boulderFrontDistance + playerTransform.up * boulderHeightOffset;
                boulderTransform.Rotate(Vector3.right, playerStepDistance * 30f, Space.Self);
            }

            // 3. 触发第一视角双手发力抵住巨石打击感
            if (Stage3PushArmController.Instance != null)
            {
                Stage3PushArmController.Instance.PlayPushMotion();
            }

            // 4. 计算全新的相机拉远全景位姿
            UpdateCameraTargetOffset();

            // 5. 通知行为数据与打破循环机制
            if (PlayerBehaviorData.Instance != null) PlayerBehaviorData.Instance.AddWork();
            if (Stage3InactionBreakController.Instance != null) Stage3InactionBreakController.Instance.OnPush();

            Debug.Log($"<color=cyan>[MobiusCam] 莫比乌斯推石第 {_currentPushCount} 次！玩家与巨石向前推进中... 全景拉远进度: {_currentProgress * 100f:F0}%</color>");
        }

        private void UpdateCameraTargetOffset()
        {
            // 根据进度 calculate 相机后退距离与上升高度
            float currentDist = Mathf.Lerp(initialCamDistance, maxPanoramaDistance, _currentProgress);
            float currentHeight = Mathf.Lerp(0f, maxPanoramaHeight, _currentProgress);
            float currentPitch = Mathf.Lerp(0f, maxPanoramaPitchAngle, _currentProgress);

            // 向后 (Back) 且向上 (Up) 抛物线拉远
            _targetCamOffset = new Vector3(0f, currentHeight, -currentDist);
            _targetCamRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            if (_mainCam != null)
            {
                if (_initialFov == 0f) _initialFov = _mainCam.fieldOfView;
                _mainCam.fieldOfView = Mathf.Lerp(_initialFov, 78f, _currentProgress);
            }
        }

        private void SmoothUpdateCameraPosition()
        {
            if (_mainCam == null) return;

            // 渐进插值相机 Transform (LateUpdate 强制定位)
            Vector3 desiredLocalPos = _initialCamLocalPos + _targetCamOffset;
            Quaternion desiredLocalRot = _initialCamLocalRot * _targetCamRotation;

            _mainCam.transform.localPosition = Vector3.Lerp(_mainCam.transform.localPosition, desiredLocalPos, Time.deltaTime * cameraSmoothSpeed);
            _mainCam.transform.localRotation = Quaternion.Slerp(_mainCam.transform.localRotation, desiredLocalRot, Time.deltaTime * cameraSmoothSpeed);
        }
    }
}
