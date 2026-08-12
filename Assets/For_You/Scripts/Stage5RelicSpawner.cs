using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 高级 3D 遗迹/算法物化生成器 (Stage 5 3D Relic & Materialization Spawner)
    /// 
    /// 巧妙设计理念：
    /// 1. 【2D ➔ 3D 物化浮凸】：当墙面视频切屏时，前关的 3D 核心遗迹（香蕉/圣手/莫比乌斯巨石/90年代显示器）
    ///    会从 2D 墙面中优雅“脱胎/浮凸出来”，化为全息 3D 实体悬浮在走廊空间中！
    /// 2. 【失重浮沉与磁力凝视】：3D 遗迹在走廊两旁如太空遗迹般缓速自转与微呼吸。
    ///    当玩家准心注视遗迹时，遗迹产生磁力吸引向玩家倾斜，注视满 0.4 秒后化为量子星粉粒子解构消散！
    /// 3. 【精准行为驱动】：严格依据 PlayerBehaviorData 的前关数值决定出现的遗迹类型。
    /// </summary>
    public class Stage5RelicSpawner : MonoBehaviour
    {
        public static Stage5RelicSpawner Instance { get; private set; }

        [Header("3D 遗迹生成参数")]
        [Tooltip("走廊两旁最大允许同时悬浮的 3D 遗迹数量")]
        public int maxActiveRelics = 4;

        [Tooltip("遗迹生成间隔（秒）")]
        public float spawnInterval = 4.0f;

        [Tooltip("遗迹悬浮在走廊四周的半径范围")]
        public float floatRadius = 2.2f;

        [Header("遗迹材质与光泽")]
        [Tooltip("遗迹全息高光发光 Color")]
        public Color holographicColor = new Color(0.2f, 0.9f, 1.0f, 0.85f);

        private List<GameObject> _activeRelics = new List<GameObject>();
        private float _spawnTimer = 0f;
        private Transform _playerTransform;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (_playerTransform != null) return;
            if (Camera.main != null) _playerTransform = Camera.main.transform;
        }

        private void Update()
        {
            FindPlayer();
            if (_playerTransform == null) return;

            float progress = Stage5Controller.Instance != null ? Stage5Controller.Instance.phaseProgress : 0f;
            if (progress < 0.20f) return; // Phase 1 前期保持走廊清爽

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval && _activeRelics.Count < maxActiveRelics)
            {
                _spawnTimer = 0f;
                SpawnMaterializedRelic(progress);
            }

            // 清理空或摧毁的遗迹
            _activeRelics.RemoveAll(item => item == null);
        }

        /// <summary>
        /// 从墙面物化浮凸出 3D 遗迹
        /// </summary>
        private void SpawnMaterializedRelic(float phaseProgress)
        {
            string theme = GetDominantTheme();

            GameObject relicGo = new GameObject($"Relic_{theme}_{Time.time:F0}");
            relicGo.transform.SetParent(transform, false);

            // 在玩家前方 3m ~ 8m，左右两侧墙面附近生成
            float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
            float zOffset = Random.Range(3.5f, 9.0f);
            float yOffset = Random.Range(-0.5f, 0.8f);

            Vector3 spawnPos = _playerTransform.position + _playerTransform.forward * zOffset + _playerTransform.right * (floatRadius * sideSign) + _playerTransform.up * yOffset;
            relicGo.transform.position = spawnPos;

            // 构建 3D 网格模型
            MeshFilter mf = relicGo.AddComponent<MeshFilter>();
            MeshRenderer mr = relicGo.AddComponent<MeshRenderer>();
            SphereCollider col = relicGo.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            BuildRelicMeshAndMaterial(theme, mf, mr);

            // 挂载失重浮沉与凝视响应组件
            RelicFloatingBehavior behavior = relicGo.AddComponent<RelicFloatingBehavior>();
            behavior.relicType = theme;

            relicGo.transform.localScale = Vector3.zero; // 初始 Scale = 0，动画浮凸变大
            StartCoroutine(AnimateEmergence(relicGo.transform));

            _activeRelics.Add(relicGo);
            Debug.Log($"<color=cyan>[Stage 5 3D遗迹] 墙面物化浮凸出前关遗迹 ({theme})！位置: {spawnPos}</color>");
        }

        private IEnumerator AnimateEmergence(Transform t)
        {
            float elapsed = 0f;
            float duration = 1.2f;
            Vector3 targetScale = Vector3.one * Random.Range(0.45f, 0.65f);

            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                float ratio = elapsed / duration;
                float easeRatio = Mathf.Sin(ratio * Mathf.PI * 0.5f);
                t.localScale = Vector3.Lerp(Vector3.zero, targetScale, easeRatio);
                yield return null;
            }
        }

        private void BuildRelicMeshAndMaterial(string theme, MeshFilter mf, MeshRenderer mr)
        {
            GameObject tempPrimitive = null;
            Color themeGlowColor = Color.white;

            switch (theme)
            {
                case "banana":
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    themeGlowColor = new Color(1.0f, 0.85f, 0.2f, 0.9f); // 金黄全息
                    break;
                case "prayer":
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    themeGlowColor = new Color(0.3f, 0.8f, 1.0f, 0.9f); // 圣光天蓝
                    break;
                case "push":
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    themeGlowColor = new Color(0.9f, 0.5f, 0.2f, 0.9f); // 莫比乌斯熔岩橙
                    break;
                case "work":
                default:
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    themeGlowColor = new Color(0.2f, 1.0f, 0.6f, 0.9f); // 90年代荧光绿
                    break;
            }

            if (tempPrimitive != null)
            {
                MeshFilter tempMf = tempPrimitive.GetComponent<MeshFilter>();
                if (tempMf != null) mf.sharedMesh = tempMf.sharedMesh;
                Destroy(tempPrimitive);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", themeGlowColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", themeGlowColor);

            mr.material = mat;
        }

        private string GetDominantTheme()
        {
            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes  = PlayerBehaviorData.Instance.pushCount;
                works   = PlayerBehaviorData.Instance.workCount;
            }
            if (bananas + prayers + pushes + works == 0) works = 35;

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
            if (max == bananas) return "banana";
            if (max == prayers) return "prayer";
            if (max == pushes)  return "push";
            return "work";
        }
    }

    /// <summary>
    /// 3D 遗迹的失重自转与注视磁吸解构行为
    /// </summary>
    public class RelicFloatingBehavior : MonoBehaviour
    {
        public string relicType;

        private Vector3 _startPos;
        private Vector3 _rotSpeed;
        private float _timeOffset;
        private bool _isGazed = false;
        private float _gazeHoldTimer = 0f;
        private Transform _camTransform;

        private void Start()
        {
            _startPos = transform.position;
            _rotSpeed = new Vector3(Random.Range(10f, 25f), Random.Range(15f, 35f), Random.Range(8f, 20f));
            _timeOffset = Random.Range(0f, 10f);

            if (Camera.main != null) _camTransform = Camera.main.transform;
        }

        private void Update()
        {
            float t = Time.time + _timeOffset;

            // 1. 失重呼吸自转
            transform.Rotate(_rotSpeed * Time.deltaTime, Space.Self);
            Vector3 floatOffset = new Vector3(Mathf.Sin(t * 1.2f) * 0.1f, Mathf.Cos(t * 1.5f) * 0.12f, Mathf.Sin(t * 0.9f) * 0.08f);

            // 2. 凝视磁吸响应
            CheckGaze();

            if (_isGazed && _camTransform != null)
            {
                _gazeHoldTimer += Time.deltaTime;

                // 向相机轻微磁吸靠近
                Vector3 desiredPos = _startPos + floatOffset + (_camTransform.position - _startPos).normalized * 0.25f;
                transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 3.0f);

                // 注视满 0.45 秒解构破裂
                if (_gazeHoldTimer >= 0.45f)
                {
                    DissolveAndExplode();
                }
            }
            else
            {
                _gazeHoldTimer = Mathf.Max(0f, _gazeHoldTimer - Time.deltaTime * 2f);
                transform.position = Vector3.Lerp(transform.position, _startPos + floatOffset, Time.deltaTime * 2.0f);
            }
        }

        private void CheckGaze()
        {
            if (_camTransform == null) return;

            Ray ray = new Ray(_camTransform.position, _camTransform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 50f))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    _isGazed = true;
                    return;
                }
            }

            _isGazed = false;
        }

        private void DissolveAndExplode()
        {
            Debug.Log($"<color=green>[3D遗迹解构] 遗迹 '{name}' ({relicType}) 被玩家注视解构，增加算法偏好！</color>");

            if (Stage5Controller.Instance != null)
            {
                Stage5Controller.Instance.phaseProgress += 0.035f;
                Stage5Controller.Instance.PlayGazeFeedbackSound();
            }

            Destroy(gameObject);
        }
    }
}
