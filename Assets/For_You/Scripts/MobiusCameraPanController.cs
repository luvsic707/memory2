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

        [Tooltip("每次推石玩家沿着莫比乌斯环曲面平移推进的步长距离 (米，0.38m 营造沉重的拟真推石速度)")]
        public float playerStepDistance = 0.38f;

        [Header("玩家双手与 MobiusBall (3) 紧密贴合参数")]
        [Tooltip("巨石紧贴在玩家手心正前方的相对距离 (米，0.95m 确保双手 100% 物理死死贴在球面上)")]
        public float boulderPairFrontDistance = 0.95f;

        [Tooltip("巨石相对于手心的高度偏置 (米)")]
        public float boulderPairHeightOffset = 0.10f;

        [Header("锁定玩家在莫比乌斯轨道 (彻底解决掉落)")]
        [Tooltip("【默认开启】：关闭 WASD 重力自由下坠，将玩家固定锚定在 MobiusStrip (3) 轨迹上，绝对不会下坠掉落！")]
        public bool lockPlayerToTrack = true;

        private Vector3 _initialPlayerPos;
        private Quaternion _initialPlayerRot;
        private Vector3 _trackCenter;
        private float _radius = 8f;
        private float _currentAngle = 0f;

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

            // 🌟 精准匹配根节点下的 MobiusBall (3) 或 MobiusBall，防止匹配错天空背景中的其他微型时钟球体
            if (boulderTransform == null)
            {
                GameObject exactBall = GameObject.Find("MobiusBall (3)");
                if (exactBall == null) exactBall = GameObject.Find("MobiusBall");
                if (exactBall != null) boulderTransform = exactBall.transform;
            }

            if (boulderTransform == null)
            {
                foreach (GameObject go in FindObjectsOfType<GameObject>())
                {
                    string nameLower = go.name.ToLower();
                    if (nameLower.Contains("mobiusball (3)") || nameLower.Contains("mobiusball"))
                    {
                        boulderTransform = go.transform;
                        break;
                    }
                }
            }

            // 🌟 1. 组合体强力对齐：开局自动把巨石对齐紧贴在玩家双手的正前方！(彻底解决脱节)
            SnapBoulderToHandsFront();

            // 🌟 2. 莫比乌斯环圆弧切线轨道极坐标系统
            if (mobiusTrackTransform != null && playerTransform != null)
            {
                _trackCenter = mobiusTrackTransform.position;
                Vector3 radial = playerTransform.position - _trackCenter;
                float r = new Vector2(radial.x, radial.z).magnitude;
                if (r > 0.5f) _radius = r;
                _currentAngle = Mathf.Atan2(radial.z, radial.x);
                Debug.Log($"<color=cyan>[MobiusCam] 莫比乌斯环切线轨道就绪: Center={_trackCenter}, Radius={_radius:F2}m, InitialAngle={_currentAngle * Mathf.Rad2Deg:F1}°</color>");
            }

            // 🌟 3. 核心锚定：禁用 UniversalPlayer 自由行走脚本，防止重力下坠并规避 CharacterController.Move 报错！
            if (lockPlayerToTrack && playerTransform != null)
            {
                MonoBehaviour universalPlayerScript = playerTransform.GetComponent("UniversalPlayer") as MonoBehaviour;
                if (universalPlayerScript == null) universalPlayerScript = playerTransform.GetComponent("CorridorPlayer") as MonoBehaviour;

                if (universalPlayerScript != null)
                {
                    universalPlayerScript.enabled = false;
                    Debug.Log($"<color=yellow>[MobiusCam] 成功禁用了 UniversalPlayer 自由行走脚本，接管推石与莫比乌斯轨位移！</color>");
                }

                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = true; // 保持 CharacterController 开启，避免报警
                }

                _initialPlayerPos = playerTransform.position;
                _initialPlayerRot = playerTransform.rotation;
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

            // 🌟 1. 沿莫比乌斯环弧形切线推进 (Circle Orbit Tangent Advance)
            if (mobiusTrackTransform != null && playerTransform != null)
            {
                float deltaAngle = (playerStepDistance / Mathf.Max(_radius, 1f));
                _currentAngle += deltaAngle;

                Vector3 newPlayerPos = _trackCenter + new Vector3(Mathf.Cos(_currentAngle) * _radius, playerTransform.position.y - _trackCenter.y, Mathf.Sin(_currentAngle) * _radius);
                Vector3 tangentDir = new Vector3(-Mathf.Sin(_currentAngle), 0f, Mathf.Cos(_currentAngle)).normalized;

                playerTransform.position = newPlayerPos;
                if (tangentDir != Vector3.zero) playerTransform.rotation = Quaternion.LookRotation(tangentDir, Vector3.up);
            }
            else if (playerTransform != null)
            {
                playerTransform.position += playerTransform.forward * playerStepDistance;
            }

            // 🌟 2. 巨石 (Boulder) 与玩家【100% 组合绑定】，死死锁定在玩家双手正前方滚动！
            if (boulderTransform != null && playerTransform != null)
            {
                boulderTransform.position = playerTransform.position + playerTransform.forward * boulderPairFrontDistance + playerTransform.up * boulderPairHeightOffset;
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

        /// <summary>
        /// 强制将选中的 MobiusBall 瞬移对齐紧贴在玩家双手的正前方
        /// </summary>
        [ContextMenu("Snap Boulder To Hands Front Right Now")]
        public void SnapBoulderToHandsFront()
        {
            if (playerTransform == null || boulderTransform == null) SetupReferences();
            if (boulderTransform != null && playerTransform != null)
            {
                boulderTransform.position = playerTransform.position + playerTransform.forward * boulderPairFrontDistance + playerTransform.up * boulderPairHeightOffset;
                Debug.Log($"<color=green>[MobiusCam] 已将巨石 '{boulderTransform.name}' 精准对齐至玩家双手正前方！</color>");
            }
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
