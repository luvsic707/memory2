using System.Collections;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 4 (现代异化 4_Modern) 坍塌走出去切关控制器
    /// 核心机制：
    /// 1. 玩家不断打字/工作点击（OnClick）。
    /// 2. 办公室坍塌达到指定次数（collapseThresholdClicks，默认 12 次）时，工位崩溃破裂，开启出口！
    /// 3. 屏幕提示 "工位坍塌，请走出废墟"。
    /// 4. 玩家走或步出工位边界（或按 WASD / 点击出口）➔ 自动触发切关载入 Stage 5 (5_Contemporary)！
    /// </summary>
    public class Stage4WalkOutExitController : MonoBehaviour
    {
        public static Stage4WalkOutExitController Instance { get; private set; }

        [Header("坍塌门槛")]
        [Tooltip("工作点击达到多少次后，判定场景坍塌完成并开启走出通道")]
        public int collapseThresholdClicks = 12;

        [Header("走出与传送边界")]
        [Tooltip("未坍塌前，超出此距离（米）会被强行传送回工位原点")]
        public float boundaryTeleportDistance = 2.8f;

        [Tooltip("坍塌后，超出此距离（米）判定为成功走出去切关")]
        public float exitDistanceThreshold = 3.5f;

        [Header("屏幕提示")]
        [TextArea]
        public string exitPromptText = "💡 办公室已坍塌，走出工位/废墟进入下一阶段 (WASD 或 按 E 迈出)";

        private int _currentClicks = 0;
        private bool _isExitOpen = false;
        private bool _hasExited = false;
        private Vector3 _initialPlayerPos;
        private Quaternion _initialPlayerRot;
        private GameObject _playerGo;
        private GameObject _boundaryTriggersGo;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void Start()
        {
            FindPlayerReference();
            SetupBoundaryColliders();
        }

        private void FindPlayerReference()
        {
            _playerGo = GameObject.FindWithTag("Player");
            if (_playerGo == null)
            {
                var p = FindObjectOfType<UniversalPlayer>();
                if (p != null) _playerGo = p.gameObject;
            }
            if (_playerGo == null)
            {
                Camera cam = Camera.main;
                if (cam != null) _playerGo = cam.gameObject;
            }

            if (_playerGo != null)
            {
                _initialPlayerPos = _playerGo.transform.position;
                _initialPlayerRot = _playerGo.transform.rotation;
            }
        }

        /// <summary>
        /// 沿 Office (87) 四周自动装配 4 个 Trigger BoxCollider，形成封闭空间防越界
        /// </summary>
        private void SetupBoundaryColliders()
        {
            _boundaryTriggersGo = new GameObject("Office_Boundary_Colliders");
            _boundaryTriggersGo.transform.position = _initialPlayerPos;

            float size = boundaryTeleportDistance * 2f;
            float thickness = 0.5f;
            float height = 5f;

            // 东、西、南、北 4 个 Trigger 碰撞体
            CreateWallTrigger(_boundaryTriggersGo.transform, new Vector3(0f, 0f, size * 0.5f), new Vector3(size, height, thickness));  // 北
            CreateWallTrigger(_boundaryTriggersGo.transform, new Vector3(0f, 0f, -size * 0.5f), new Vector3(size, height, thickness)); // 南
            CreateWallTrigger(_boundaryTriggersGo.transform, new Vector3(size * 0.5f, 0f, 0f), new Vector3(thickness, height, size));  // 东
            CreateWallTrigger(_boundaryTriggersGo.transform, new Vector3(-size * 0.5f, 0f, 0f), new Vector3(thickness, height, size)); // 西
        }

        private void CreateWallTrigger(Transform parent, Vector3 localPos, Vector3 boxSize)
        {
            GameObject wall = new GameObject("BoundaryWallTrigger");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;

            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = boxSize;

            var receiver = wall.AddComponent<BoundaryTriggerReceiver>();
            receiver.controller = this;
        }

        /// <summary>
        /// 由 Trigger 碰撞体或 Update 判定触发：将试图逃离的玩家传送回工位
        /// </summary>
        public void OnPlayerTouchBoundary()
        {
            if (_isExitOpen || _hasExited || _playerGo == null) return;

            TeleportPlayerToOrigin();
        }

        private void TeleportPlayerToOrigin()
        {
            Debug.Log("<color=orange>[Stage4] 未达到坍塌门槛！玩家试图走出去，强行传送回工位原点。</color>");

            CharacterController cc = _playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _playerGo.transform.position = _initialPlayerPos;
            _playerGo.transform.rotation = _initialPlayerRot;

            if (cc != null) cc.enabled = true;

            // 3D 屏幕显示强行归位提示
            MonitorTextController monitor = FindObjectOfType<MonitorTextController>();
            if (monitor != null)
            {
                monitor.SetCustomMessage("SYS: BOUNDARY VIOLATION.\nReturned to workstation.");
            }
        }

        /// <summary>
        /// 由 Stage4Controller 在每次玩家点击/打字工作时调用
        /// </summary>
        public void OnWorkClick()
        {
            if (_isExitOpen || _hasExited) return;

            _currentClicks++;
            if (_currentClicks >= collapseThresholdClicks)
            {
                OpenExitWay();
            }
        }

        private void OpenExitWay()
        {
            _isExitOpen = true;
            Debug.Log($"<color=yellow>[Stage4] 办公室坍塌次数达到 {collapseThresholdClicks} 次！开启走出通道。</color>");

            // 销毁四周的防逃离传送碰撞体
            if (_boundaryTriggersGo != null)
            {
                Destroy(_boundaryTriggersGo);
            }

            // 1. 广播剧情字幕通告
            EventBus.RaiseAnnouncement("The office has collapsed. Walk out of the ruins.");

            // 2. 将引导文字直接写入场景 3D 绿色终端显示屏
            MonitorTextController monitor = FindObjectOfType<MonitorTextController>();
            if (monitor != null)
            {
                monitor.SetCustomMessage("SYS: DISSOLVED.\n\n...office is a skin.\nLeak to next layer ->");
            }
        }

        private void Update()
        {
            if (_hasExited || _playerGo == null) return;

            float dist = Vector3.Distance(_playerGo.transform.position, _initialPlayerPos);

            if (!_isExitOpen)
            {
                // 未坍塌：一旦玩家移动超过传送边界，立刻强行传送回原点
                if (dist >= boundaryTeleportDistance)
                {
                    TeleportPlayerToOrigin();
                }
            }
            else
            {
                // 坍塌完成：走出边界切关进入 Stage 5
                if (dist >= exitDistanceThreshold)
                {
                    Debug.Log($"[Stage4] 检测到玩家移出工位 {dist:F1} 米！触发走出切关。");
                    TriggerWalkOutComplete();
                    return;
                }
            }

            // 2. 备用快捷按键：在坍塌开启后，按 E 键或 W 键向前迈出走出去
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("[Stage4] 玩家按键向外迈出！触发走出切关。");
                TriggerWalkOutComplete();
            }
        }

        private void TriggerWalkOutComplete()
        {
            if (_hasExited) return;
            _hasExited = true;

            Debug.Log("<color=green>[Stage4] 玩家成功走出坍塌办公室！触发转场加载 Stage 5...</color>");

            EventBus.RaiseAnnouncement("You stepped out of the workplace into the algorithm stream.");

            // 执行 Stage4Controller 转场
            if (Stage4Controller.Instance != null)
            {
                Stage4Controller.Instance.TriggerSceneComplete();
            }
            else
            {
                EventBus.RaiseSceneComplete();
            }
        }

        // 所有引导与通报均全额映射至场景内 3D 绿色终端屏幕，保持 2D HUD 100% 干净
    }

    /// <summary>
    /// 挂载在四周 Trigger BoxCollider 上的边界越界检测组件
    /// </summary>
    public class BoundaryTriggerReceiver : MonoBehaviour
    {
        public Stage4WalkOutExitController controller;

        private void OnTriggerEnter(Collider other)
        {
            if (controller == null) return;
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || (Camera.main != null && (other.gameObject == Camera.main.gameObject || other.transform.IsChildOf(Camera.main.transform))))
            {
                controller.OnPlayerTouchBoundary();
            }
        }
    }
}
