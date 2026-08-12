using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 超级显眼 3D 遗迹/算法物化生成器 (High-Visibility 3D Relic Spawner)
    /// 解决问题：之前因 0.2 进度门槛与位置偏出墙体导致看不到，现已彻底优化为开局即刻高亮悬浮在视野中央！
    /// </summary>
    public class Stage5RelicSpawner : MonoBehaviour
    {
        public static Stage5RelicSpawner Instance { get; private set; }

        [Header("3D 遗迹生成参数")]
        [Tooltip("走廊内同时悬浮的 3D 遗迹最大数量")]
        public int maxActiveRelics = 4;

        [Tooltip("遗迹生成间隔（秒）")]
        public float spawnInterval = 3.0f;

        [Tooltip("遗迹在走廊内部左右分布半径（必须在走廊内部 x = -0.8 ~ 0.8 以内）")]
        public float corridorXRadius = 0.75f;

        [Header("遗迹尺寸与高光")]
        [Tooltip("遗迹基础缩放比例（必须足够宏大显眼）")]
        public float baseRelicScale = 1.4f;

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
            // 🌟 开局立即强行生成 3 个，确保一进场景 100% 能看清走廊里的 3D 遗迹！
            StartCoroutine(InitialSpawnSequence());
        }

        private IEnumerator InitialSpawnSequence()
        {
            yield return new WaitForSeconds(0.5f);
            FindPlayer();
            if (_playerTransform == null) yield break;

            for (int i = 0; i < maxActiveRelics; i++)
            {
                SpawnMaterializedRelic(0.5f, i);
                yield return new WaitForSeconds(0.3f);
            }
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

            // 按 T 键调试强行生成 3D 遗迹
            if (Input.GetKeyDown(KeyCode.T))
            {
                SpawnMaterializedRelic(0.8f, 0);
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval && _activeRelics.Count < maxActiveRelics)
            {
                _spawnTimer = 0f;
                SpawnMaterializedRelic(0.5f, _activeRelics.Count);
            }

            _activeRelics.RemoveAll(item => item == null);
        }

        [ContextMenu("Force Spawn 3D Relic Right Now")]
        public void ForceSpawnRelic()
        {
            FindPlayer();
            SpawnMaterializedRelic(0.9f, 0);
        }

        /// <summary>
        /// 从墙面物化浮凸出 3D 遗迹（直接放在玩家视野正前方）
        /// </summary>
        private void SpawnMaterializedRelic(float phaseProgress, int index)
        {
            if (_playerTransform == null) return;

            string theme = GetDominantTheme();

            GameObject relicGo = new GameObject($"Relic_3D_{theme}_{Time.time:F0}");
            relicGo.transform.SetParent(transform, false);

            // 精准计算放置在走廊内部视野正前方（x: -0.75 ~ +0.75, y: -0.3 ~ +0.6, z: 2.2 ~ 6.0）
            float sideSign = (index % 2 == 0) ? 1.0f : -1.0f;
            float zOffset = 2.5f + (index * 1.5f);
            float yOffset = Random.Range(-0.3f, 0.5f);
            float xOffset = (corridorXRadius * sideSign) * Random.Range(0.6f, 1.0f);

            Vector3 spawnPos = _playerTransform.position 
                             + _playerTransform.forward * zOffset 
                             + _playerTransform.right * xOffset 
                             + _playerTransform.up * yOffset;

            relicGo.transform.position = spawnPos;

            // 构建 3D 实体模型
            MeshFilter mf = relicGo.AddComponent<MeshFilter>();
            MeshRenderer mr = relicGo.AddComponent<MeshRenderer>();
            SphereCollider col = relicGo.AddComponent<SphereCollider>();
            col.radius = 1.0f;

            BuildRelicMeshAndMaterial(theme, mf, mr);

            // 挂载失重浮沉与凝视响应组件
            RelicFloatingBehavior behavior = relicGo.AddComponent<RelicFloatingBehavior>();
            behavior.relicType = theme;

            relicGo.transform.localScale = Vector3.zero;
            StartCoroutine(AnimateEmergence(relicGo.transform));

            _activeRelics.Add(relicGo);
            Debug.Log($"<color=cyan>🌟 [Stage 5 3D遗迹生成] 显眼 3D 前关遗迹 ({theme}) 已成功刷新在玩家眼前！位置: {spawnPos}</color>");
        }

        private IEnumerator AnimateEmergence(Transform t)
        {
            float elapsed = 0f;
            float duration = 0.8f;
            Vector3 targetScale = Vector3.one * baseRelicScale;

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
                    themeGlowColor = new Color(1.0f, 0.85f, 0.1f, 1.0f); // 鲜亮金黄
                    break;
                case "prayer":
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    themeGlowColor = new Color(0.2f, 0.8f, 1.0f, 1.0f); // 圣洁天蓝
                    break;
                case "push":
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    themeGlowColor = new Color(1.0f, 0.45f, 0.1f, 1.0f); // 莫比乌斯熔岩橙
                    break;
                case "work":
                default:
                    tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    themeGlowColor = new Color(0.2f, 1.0f, 0.5f, 1.0f); // 90年代荧光绿
                    break;
            }

            if (tempPrimitive != null)
            {
                MeshFilter tempMf = tempPrimitive.GetComponent<MeshFilter>();
                if (tempMf != null) mf.sharedMesh = tempMf.sharedMesh;
                Destroy(tempPrimitive);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", themeGlowColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", themeGlowColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", themeGlowColor * 1.5f);
            }

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
            _rotSpeed = new Vector3(Random.Range(20f, 45f), Random.Range(30f, 60f), Random.Range(15f, 40f));
            _timeOffset = Random.Range(0f, 10f);

            if (Camera.main != null) _camTransform = Camera.main.transform;
        }

        private void Update()
        {
            float t = Time.time + _timeOffset;

            // 1. 显眼的失重浮沉与快速自转
            transform.Rotate(_rotSpeed * Time.deltaTime, Space.Self);
            Vector3 floatOffset = new Vector3(Mathf.Sin(t * 1.8f) * 0.18f, Mathf.Cos(t * 2.0f) * 0.22f, Mathf.Sin(t * 1.5f) * 0.15f);

            // 2. 凝视磁吸响应
            CheckGaze();

            if (_isGazed && _camTransform != null)
            {
                _gazeHoldTimer += Time.deltaTime;

                // 强烈磁吸靠近镜头
                Vector3 desiredPos = _startPos + floatOffset + (_camTransform.position - _startPos).normalized * 0.6f;
                transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 5.0f);

                // 注视满 0.35 秒解构破裂
                if (_gazeHoldTimer >= 0.35f)
                {
                    DissolveAndExplode();
                }
            }
            else
            {
                _gazeHoldTimer = Mathf.Max(0f, _gazeHoldTimer - Time.deltaTime * 2f);
                transform.position = Vector3.Lerp(transform.position, _startPos + floatOffset, Time.deltaTime * 3.0f);
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
            Debug.Log($"<color=green>💥 [3D遗迹解构] 遗迹 '{name}' ({relicType}) 被玩家注视解构！</color>");

            if (Stage5Controller.Instance != null)
            {
                Stage5Controller.Instance.phaseProgress += 0.05f;
                Stage5Controller.Instance.PlayGazeFeedbackSound();
            }

            Destroy(gameObject);
        }
    }
}
