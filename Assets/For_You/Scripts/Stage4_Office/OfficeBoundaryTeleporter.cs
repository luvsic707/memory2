using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 挂载在四周的 Trigger 碰撞体上。
    /// 未达到坍塌门槛前，玩家触发此 Trigger 会被强行传送回指定的 spawnPoint 原点。
    /// 在 Unity Editor Scene 视图中带有醒目的 Gizmos 边界可视化！
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class OfficeBoundaryTeleporter : MonoBehaviour
    {
        [Header("传送目标位置")]
        [Tooltip("纯世界三维坐标 (X, Y, Z)，玩家越界后将被传送至该三维坐标")]
        public Vector3 spawnPosition = Vector3.zero;

        [Tooltip("可选 Transform 引用（若拖入则优先使用该物体的坐标，留空则严格按上面的 Vector3 纯坐标传送）")]
        public Transform spawnTransform;

        [Header("解封条件")]
        [Tooltip("坍塌点击门槛。工作点击达到此次数后，防越界传送自动失效")]
        public int collapseThresholdClicks = 12;

        [Header("音效反馈")]
        [Tooltip("传送时的音效（可选）")]
        public AudioClip teleportSfx;

        [Header("Editor 可视化颜色")]
        public Color activeGizmoColor = new Color(0f, 0.9f, 1f, 0.4f);
        public Color disabledGizmoColor = new Color(1f, 0.2f, 0.2f, 0.15f);

        private void Start()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
            }
        }

        public Vector3 GetTargetPosition()
        {
            if (spawnTransform != null) return spawnTransform.position;
            return spawnPosition;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (this == null || !enabled || !gameObject.activeInHierarchy) return;

            // 如果工作点击次数已达到坍塌门槛，防逃离传送自动失效
            if (Application.isPlaying && PlayerBehaviorData.Instance != null && PlayerBehaviorData.Instance.workCount >= collapseThresholdClicks)
            {
                return;
            }

            // 判定触发对象是否为玩家或主相机
            GameObject targetPlayer = null;
            if (other.CompareTag("Player"))
            {
                targetPlayer = other.gameObject;
            }
            else if (other.GetComponent<CharacterController>() != null)
            {
                targetPlayer = other.gameObject;
            }
            else if (Camera.main != null && (other.gameObject == Camera.main.gameObject || other.transform.IsChildOf(Camera.main.transform)))
            {
                targetPlayer = Camera.main.gameObject;
            }

            if (targetPlayer != null)
            {
                TeleportPlayer(targetPlayer);
            }
        }

        public void TeleportPlayer(GameObject player)
        {
            if (this == null || player == null) return;

            Vector3 targetPos = GetTargetPosition();
            Quaternion targetRot = (spawnTransform != null) ? spawnTransform.rotation : Quaternion.identity;

            // 如果玩家已经在原点/座椅附近，无需重复传送
            if (Vector3.Distance(player.transform.position, targetPos) < 0.8f) return;

            // 禁用 CharacterController 避免物理阻塞
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = targetPos;
            if (spawnTransform != null) player.transform.rotation = targetRot;

            if (cc != null) cc.enabled = true;

            // 3D 绿色屏幕警告
            MonitorTextController monitor = FindObjectOfType<MonitorTextController>();
            if (monitor != null)
            {
                monitor.SetCustomMessage("SYS: BOUNDARY VIOLATION.\nReturned to workstation.");
            }

            // 播放音效
            if (teleportSfx != null)
            {
                AudioSource.PlayClipAtPoint(teleportSfx, targetPos);
            }

            Debug.Log($"<color=cyan>[OfficeBoundaryTeleporter] 玩家试图越界！已成功传送归位至 {targetPos}</color>");
        }

        private void OnDrawGizmos()
        {
            if (this == null) return;

            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            bool isUnlocked = false;
            if (Application.isPlaying && PlayerBehaviorData.Instance != null)
            {
                isUnlocked = (PlayerBehaviorData.Instance.workCount >= collapseThresholdClicks);
            }

            Gizmos.color = isUnlocked ? disabledGizmoColor : activeGizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
