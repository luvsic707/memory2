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

        [Header("走出距离判定")]
        [Tooltip("玩家离开工位初始中心点多少米后，判定为成功走出去")]
        public float exitDistanceThreshold = 3.5f;

        [Header("屏幕提示")]
        [TextArea]
        public string exitPromptText = "💡 办公室已坍塌，走出工位/废墟进入下一阶段 (WASD 或 按 E 迈出)";

        private int _currentClicks = 0;
        private bool _isExitOpen = false;
        private bool _hasExited = false;
        private Vector3 _initialPlayerPos;
        private GameObject _playerGo;
        private GUIStyle _guiStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        private void Start()
        {
            FindPlayerReference();
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

            // 1. 广播剧情字幕通告
            EventBus.RaiseAnnouncement("The office has collapsed. Walk out of the ruins.");

            // 2. 将引导文字直接写入场景 3D 绿色终端显示屏，不触发任何 2D UI
            MonitorTextController monitor = FindObjectOfType<MonitorTextController>();
            if (monitor != null)
            {
                monitor.SetCustomMessage("[CRITICAL FAILURE]:\nWorkplace Has Collapsed.\n\n[GUIDANCE]:\nSTOP WORKING.\nWalk out of the ruins to enter Stage 5 ->");
            }

            // 3. 更新玩家初始位置参照
            if (_playerGo != null)
            {
                _initialPlayerPos = _playerGo.transform.position;
            }
        }

        private void Update()
        {
            if (!_isExitOpen || _hasExited) return;

            // 1. 监测玩家移动超出工位边界（走出判定）
            if (_playerGo != null)
            {
                float dist = Vector3.Distance(_playerGo.transform.position, _initialPlayerPos);
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
}
