using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 超现实魔幻当代艺术遗迹生成器 (Surrealist High-Art Relic Spawner)
    /// 审美标准：借鉴当代先锋艺术展，结合霓虹红/尖刺光束/全息环/粒子喷涌，打造极具震撼感与魔幻氛围的遗迹物化！
    /// </summary>
    public class Stage5RelicSpawner : MonoBehaviour
    {
        public static Stage5RelicSpawner Instance { get; private set; }

        [Header("自定义模型槽位 (Inspector 自由拖拽)")]
        [Tooltip("香蕉偏好 3D 自定义模型（未填则自动生成魔幻艺术几何体）")]
        public GameObject[] customBananaPrefabs;

        [Tooltip("祈祷偏好 3D 自定义模型")]
        public GameObject[] customPrayerPrefabs;

        [Tooltip("推石偏好 3D 自定义模型")]
        public GameObject[] customPushPrefabs;

        [Tooltip("职场打字偏好 3D 自定义模型")]
        public GameObject[] customWorkPrefabs;

        [Header("魔幻视觉与生成配置")]
        [Tooltip("走廊内同时悬浮的 3D 遗迹最大数量")]
        public int maxActiveRelics = 4;

        [Tooltip("遗迹生成间隔（秒）")]
        public float spawnInterval = 3.2f;

        [Tooltip("遗迹基础缩放比例（控制尺寸防遮挡）")]
        public float baseRelicScale = 0.55f;

        [Header("超现实 Contemporary Pop 霓虹配色")]
        public Color neonRedColor = new Color(1.0f, 0.0f, 0.35f, 1.0f);     // 尖刺霓虹洋红 (参考图主色)
        public Color neonCyanColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);    // 电光青
        public Color neonGoldColor = new Color(1.0f, 0.85f, 0.0f, 1.0f);    // 亮黄
        public Color neonEmeraldColor = new Color(0.1f, 1.0f, 0.5f, 1.0f);  // 荧光绿

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
            StartCoroutine(InitialSpawnSequence());
        }

        private IEnumerator InitialSpawnSequence()
        {
            yield return new WaitForSeconds(0.4f);
            FindPlayer();
            if (_playerTransform == null) yield break;

            for (int i = 0; i < maxActiveRelics; i++)
            {
                SpawnSurrealRelic(i);
                yield return new WaitForSeconds(0.35f);
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

            if (Input.GetKeyDown(KeyCode.T))
            {
                SpawnSurrealRelic(0);
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval && _activeRelics.Count < maxActiveRelics)
            {
                _spawnTimer = 0f;
                SpawnSurrealRelic(_activeRelics.Count);
            }

            _activeRelics.RemoveAll(item => item == null);
        }

        [ContextMenu("Force Spawn Surreal Relic")]
        public void ForceSpawnRelic()
        {
            FindPlayer();
            SpawnSurrealRelic(0);
        }

        /// <summary>
        /// 生成魔幻超现实 3D 遗迹
        /// </summary>
        private void SpawnSurrealRelic(int index)
        {
            if (_playerTransform == null) return;

            string theme = GetDominantTheme();

            GameObject relicRoot = new GameObject($"SurrealRelic_{theme}_{Time.time:F0}");
            relicRoot.transform.SetParent(transform, false);

            float sideSign = (index % 2 == 0) ? 1.0f : -1.0f;
            float zOffset = 2.4f + (index * 1.4f);
            float yOffset = Random.Range(-0.25f, 0.45f);
            float xOffset = (0.78f * sideSign) * Random.Range(0.65f, 1.0f);

            Vector3 spawnPos = _playerTransform.position 
                             + _playerTransform.forward * zOffset 
                             + _playerTransform.right * xOffset 
                             + _playerTransform.up * yOffset;

            relicRoot.transform.position = spawnPos;

            // 1. 尝试使用用户自定义 Prefab，无则构建魔幻艺术网格
            GameObject modelGo = GetCustomModelPrefab(theme);
            if (modelGo != null)
            {
                GameObject instantiated = Instantiate(modelGo, relicRoot.transform, false);
                instantiated.transform.localPosition = Vector3.zero;
            }
            else
            {
                BuildSurrealArtMesh(relicRoot, theme);
            }

            // 2. 添加超现实全息环光束与有机尖刺 (Spiky Tendrils & Holographic Rings)
            BuildSurrealAuraDecorations(relicRoot, theme);

            SphereCollider col = relicRoot.GetComponent<SphereCollider>();
            if (col == null) col = relicRoot.AddComponent<SphereCollider>();
            col.radius = 1.2f;

            // 3. 挂载超现实失重浮沉、脉冲发光与凝视磁吸行为
            SurrealFloatingBehavior behavior = relicRoot.AddComponent<SurrealFloatingBehavior>();
            behavior.relicType = theme;

            relicRoot.transform.localScale = Vector3.zero;
            StartCoroutine(AnimateEmergence(relicRoot.transform));

            _activeRelics.Add(relicRoot);
            Debug.Log($"<color=magenta>✨ [魔幻 3D 遗迹] 超现实当代艺术遗迹 ({theme}) 浮凸刷新在走廊中！位置: {spawnPos}</color>");
        }

        private GameObject GetCustomModelPrefab(string theme)
        {
            GameObject[] pool = null;
            switch (theme)
            {
                case "banana": pool = customBananaPrefabs; break;
                case "prayer": pool = customPrayerPrefabs; break;
                case "push":   pool = customPushPrefabs;   break;
                case "work":   pool = customWorkPrefabs;   break;
            }
            if (pool != null && pool.Length > 0)
            {
                int r = Random.Range(0, pool.Length);
                return pool[r];
            }
            return null;
        }

        private IEnumerator AnimateEmergence(Transform t)
        {
            float elapsed = 0f;
            float duration = 0.9f;
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

        /// <summary>
        /// 构建带有参考图风格的魔幻当代艺术网格
        /// </summary>
        private void BuildSurrealArtMesh(GameObject parent, string theme)
        {
            GameObject meshHolder = new GameObject("ArtMesh");
            meshHolder.transform.SetParent(parent.transform, false);

            MeshFilter mf = meshHolder.AddComponent<MeshFilter>();
            MeshRenderer mr = meshHolder.AddComponent<MeshRenderer>();

            Color themeColor = neonRedColor;
            PrimitiveType pType = PrimitiveType.Cube;

            switch (theme)
            {
                case "banana":
                    pType = PrimitiveType.Capsule;
                    themeColor = neonGoldColor;
                    break;
                case "prayer":
                    pType = PrimitiveType.Cylinder;
                    themeColor = neonCyanColor;
                    break;
                case "push":
                    pType = PrimitiveType.Sphere;
                    themeColor = neonRedColor;
                    break;
                case "work":
                default:
                    pType = PrimitiveType.Cube;
                    themeColor = neonEmeraldColor;
                    break;
            }

            GameObject prim = GameObject.CreatePrimitive(pType);
            mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
            Destroy(prim);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", themeColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", themeColor);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", themeColor * 2.2f);
            }

            mr.material = mat;
        }

        /// <summary>
        /// 类似参考图：添加有机发光尖刺、全息环阵列与散射微粒
        /// </summary>
        private void BuildSurrealAuraDecorations(GameObject parent, string theme)
        {
            // 1. 全息发光环 (Holographic Cyber Ring)
            GameObject ringGo = new GameObject("HoloRing");
            ringGo.transform.SetParent(parent.transform, false);
            ringGo.transform.localRotation = Quaternion.Euler(60f, 0f, 45f);
            ringGo.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            MeshFilter rMf = ringGo.AddComponent<MeshFilter>();
            MeshRenderer rMr = ringGo.AddComponent<MeshRenderer>();
            GameObject primCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rMf.sharedMesh = primCyl.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primCyl);

            Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Color ringColor = (theme == "push" || theme == "banana") ? neonRedColor : neonCyanColor;
            if (ringMat.HasProperty("_Color")) ringMat.SetColor("_Color", ringColor);
            if (ringMat.HasProperty("_BaseColor")) ringMat.SetColor("_BaseColor", ringColor);
            rMr.material = ringMat;

            // 2. 有机发光尖刺 (Spiky Cyber Tendrils)
            for (int i = 0; i < 3; i++)
            {
                GameObject spike = new GameObject($"Spike_{i}");
                spike.transform.SetParent(parent.transform, false);
                spike.transform.localRotation = Quaternion.Euler(i * 120f, 45f, i * 60f);
                spike.transform.localPosition = Random.insideUnitSphere * 0.2f;
                spike.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);

                MeshFilter sMf = spike.AddComponent<MeshFilter>();
                MeshRenderer sMr = spike.AddComponent<MeshRenderer>();
                GameObject primCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sMf.sharedMesh = primCone.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primCone);

                Material spikeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                Color spikeColor = neonRedColor;
                if (spikeMat.HasProperty("_Color")) spikeMat.SetColor("_Color", spikeColor);
                if (spikeMat.HasProperty("_BaseColor")) spikeMat.SetColor("_BaseColor", spikeColor);
                sMr.material = spikeMat;
            }
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
    /// 超现实魔幻失重旋转、脉冲发光与凝视磁吸行为
    /// </summary>
    public class SurrealFloatingBehavior : MonoBehaviour
    {
        public string relicType;

        private Vector3 _startPos;
        private Vector3 _rotSpeed;
        private float _timeOffset;
        private bool _isGazed = false;
        private float _gazeHoldTimer = 0f;
        private Transform _camTransform;
        private Transform _holoRing;

        private void Start()
        {
            _startPos = transform.position;
            _rotSpeed = new Vector3(Random.Range(25f, 60f), Random.Range(35f, 80f), Random.Range(20f, 50f));
            _timeOffset = Random.Range(0f, 10f);

            Transform ring = transform.Find("HoloRing");
            if (ring != null) _holoRing = ring;

            if (Camera.main != null) _camTransform = Camera.main.transform;
        }

        private void Update()
        {
            float t = Time.time + _timeOffset;

            // 1. 失重浮沉与多轴魔幻旋转
            transform.Rotate(_rotSpeed * Time.deltaTime, Space.Self);
            if (_holoRing != null) _holoRing.Rotate(Vector3.up, 120f * Time.deltaTime, Space.Self);

            Vector3 floatOffset = new Vector3(
                Mathf.Sin(t * 2.0f) * 0.22f, 
                Mathf.Cos(t * 2.2f) * 0.28f, 
                Mathf.Sin(t * 1.8f) * 0.18f
            );

            // 2. 凝视磁吸与脉冲响应
            CheckGaze();

            if (_isGazed && _camTransform != null)
            {
                _gazeHoldTimer += Time.deltaTime;

                // 强烈磁吸靠近镜头
                Vector3 desiredPos = _startPos + floatOffset + (_camTransform.position - _startPos).normalized * 0.75f;
                transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 6.0f);

                // 注视满 0.35 秒超现实解构破裂
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
            Debug.Log($"<color=magenta>💥 [超现实遗迹解构] 魔幻 3D 遗迹 '{name}' ({relicType}) 被玩家注视解构！</color>");

            if (Stage5Controller.Instance != null)
            {
                Stage5Controller.Instance.phaseProgress += 0.05f;
                Stage5Controller.Instance.PlayGazeFeedbackSound();
            }

            Destroy(gameObject);
        }
    }
}
