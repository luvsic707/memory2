using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Phase 4 "献祭物召唤"系统 (Chaos Offering Spawner)
    /// 根据前四关行为数据 (PlayerBehaviorData) 判定出的主导行为倾向，持续生成对应主题的简单 3D 原型物体
    /// (banana→胶囊体, prayer→圆柱体, push→球体, work→方块)，从摄像机前方四周飞来/环绕消失，
    /// 营造"这就是你的献祭品/沉迷证据"的仪式感——是 Phase4"彻底混沌/cult 感"的核心视觉元素之一。
    ///
    /// 设计上完全独立运作：只读取 Stage5Controller.phaseProgress 与 PlayerBehaviorData 这两个已有的公开只读接口，
    /// 不依赖、不修改 CustomCorridorBinder 的任何内部状态，保持低耦合。所有生成参数均可在 Inspector 里自由调校。
    /// </summary>
    public class Stage5ChaosOfferingSpawner : MonoBehaviour
    {
        [Header("自定义模型插槽 (Inspector 自由拖拽，未填则自动回退为几何体充当)")]
        [Tooltip("香蕉偏好主题的自定义 3D 模型（可多个，随机选一个生成）")]
        public GameObject[] customBananaPrefabs;

        [Tooltip("宗教题材主题的自定义 3D 模型")]
        public GameObject[] customPrayerPrefabs;

        [Tooltip("推石偏好主题的自定义 3D 模型")]
        public GameObject[] customPushPrefabs;

        [Tooltip("职场打字偏好主题的自定义 3D 模型")]
        public GameObject[] customWorkPrefabs;

        [Header("各主题独立缩放倍率 (不同模型在 FBX 里的原始尺寸往往差异很大，每个主题单独调，不影响其他)")]
        [Tooltip("香蕉主题的额外缩放倍率（在 Object Scale 基础上再乘上这个值，默认模型尺寸偏小时可以把这个调大）")]
        public float bananaScaleMultiplier = 1f;

        [Tooltip("宗教主题的额外缩放倍率")]
        public float prayerScaleMultiplier = 1f;

        [Tooltip("推石主题的额外缩放倍率")]
        public float pushScaleMultiplier = 1f;

        [Tooltip("职场主题的额外缩放倍率")]
        public float workScaleMultiplier = 1f;

        [Header("触发条件")]
        [Tooltip("phaseProgress 达到此值才开始召唤献祭物体 (默认与 Phase4 起点一致)")]
        [Range(0f, 1f)] public float activationThreshold = 0.96f;

        [Header("生成参数 (Data-Driven，可自由调校)")]
        [Tooltip("同屏最多存在的献祭物体数量")]
        public int maxActiveObjects = 24;

        [Tooltip("每秒生成速率 (个/秒)")]
        public float baseSpawnRate = 3f;

        [Tooltip("物体从摄像机前方多远处生成 (米)")]
        public float spawnDistance = 14f;

        [Tooltip("物体生成时的横向散布半径 (米)")]
        public float spawnSpreadRadius = 4f;

        [Tooltip("物体飞向摄像机的基础速度 (米/秒)")]
        public float flySpeed = 5f;

        [Tooltip("物体基础缩放大小")]
        public float objectScale = 0.5f;

        [Tooltip("物体飞行时的自转速度 (度/秒)")]
        public float spinSpeed = 90f;

        [Header("颜色 (主题染色留空未实现，暂时统一用中性色调)")]
        public Color objectColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        private readonly List<GameObject> _activeObjects = new List<GameObject>();
        private float _spawnAccumulator = 0f;
        private Camera _mainCam;
        private Material _sharedMat;

        private void Start()
        {
            _mainCam = Camera.main;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            _sharedMat = new Material(shader);
            if (_sharedMat.HasProperty("_Color")) _sharedMat.SetColor("_Color", objectColor);
            if (_sharedMat.HasProperty("_BaseColor")) _sharedMat.SetColor("_BaseColor", objectColor);
        }

        private void Update()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            float phaseProgress = Stage5Controller.Instance != null ? Stage5Controller.Instance.phaseProgress : 0f;
            bool active = phaseProgress >= activationThreshold;

            if (!active)
            {
                if (_activeObjects.Count > 0)
                {
                    foreach (var go in _activeObjects)
                    {
                        if (go != null) Destroy(go);
                    }
                    _activeObjects.Clear();
                    _spawnAccumulator = 0f;
                }
                return;
            }

            _spawnAccumulator += Time.deltaTime * baseSpawnRate;
            while (_spawnAccumulator >= 1f && _activeObjects.Count < maxActiveObjects)
            {
                _spawnAccumulator -= 1f;
                SpawnOfferingObject();
            }

            for (int i = _activeObjects.Count - 1; i >= 0; i--)
            {
                GameObject go = _activeObjects[i];
                if (go == null)
                {
                    _activeObjects.RemoveAt(i);
                    continue;
                }

                Vector3 toCam = _mainCam.transform.position - go.transform.position;
                float dist = toCam.magnitude;
                Vector3 dir = dist > 0.01f ? toCam / dist : _mainCam.transform.forward;

                go.transform.position += dir * flySpeed * Time.deltaTime;
                go.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
                go.transform.Rotate(Vector3.right, spinSpeed * 0.6f * Time.deltaTime, Space.Self);

                if (dist < 1.0f)
                {
                    Destroy(go);
                    _activeObjects.RemoveAt(i);
                }
            }
        }

        private void SpawnOfferingObject()
        {
            string theme = GetDominantTheme();

            GameObject customPrefab = GetCustomModelPrefab(theme);
            GameObject go;

            if (customPrefab != null)
            {
                go = Instantiate(customPrefab);
                go.name = "ChaosOffering_" + theme;

                // 自定义模型可能自带 Collider，逐个移除避免飞行途中与其他物体/玩家发生物理碰撞
                Collider[] cols = go.GetComponentsInChildren<Collider>();
                foreach (var c in cols) Destroy(c);
            }
            else
            {
                PrimitiveType primType = GetPrimitiveForTheme(theme);
                go = GameObject.CreatePrimitive(primType);
                go.name = "ChaosOffering_" + theme;

                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null && _sharedMat != null) mr.material = _sharedMat;
            }

            Vector3 randomOffset = _mainCam.transform.right * Random.Range(-spawnSpreadRadius, spawnSpreadRadius)
                                  + _mainCam.transform.up * Random.Range(-spawnSpreadRadius * 0.6f, spawnSpreadRadius * 0.6f);

            go.transform.position = _mainCam.transform.position + _mainCam.transform.forward * spawnDistance + randomOffset;
            go.transform.localScale = Vector3.one * objectScale * GetThemeScaleMultiplier(theme) * Random.Range(0.7f, 1.4f);
            go.transform.rotation = Random.rotation;

            _activeObjects.Add(go);
        }

        /// <summary>
        /// 每个主题独立的额外缩放倍率，用于补偿不同模型在 FBX 里差异很大的原始尺寸。
        /// </summary>
        private float GetThemeScaleMultiplier(string theme)
        {
            switch (theme)
            {
                case "banana": return bananaScaleMultiplier;
                case "prayer": return prayerScaleMultiplier;
                case "push": return pushScaleMultiplier;
                case "work": return workScaleMultiplier;
                default: return 1f;
            }
        }

        /// <summary>
        /// 从 Inspector 里拖拽好的自定义模型池中随机抽一个；若对应主题没有填写任何模型，返回 null 让调用方 fallback 回几何体。
        /// </summary>
        private GameObject GetCustomModelPrefab(string theme)
        {
            GameObject[] pool = null;
            switch (theme)
            {
                case "banana": pool = customBananaPrefabs; break;
                case "prayer": pool = customPrayerPrefabs; break;
                case "push": pool = customPushPrefabs; break;
                case "work": pool = customWorkPrefabs; break;
            }
            if (pool != null && pool.Length > 0)
            {
                for (int attempt = 0; attempt < pool.Length; attempt++)
                {
                    int r = Random.Range(0, pool.Length);
                    if (pool[r] != null) return pool[r];
                }
            }
            return null;
        }

        private PrimitiveType GetPrimitiveForTheme(string theme)
        {
            switch (theme)
            {
                case "banana": return PrimitiveType.Capsule;
                case "prayer": return PrimitiveType.Cylinder;
                case "push": return PrimitiveType.Sphere;
                case "work":
                default: return PrimitiveType.Cube;
            }
        }

        /// <summary>
        /// 与 CustomCorridorBinder / Stage5RelicSpawner 里各自独立实现的主导主题判定逻辑一致，
        /// 刻意不共享代码以避免跨脚本强耦合（保持每个子系统独立可插拔）。
        /// </summary>
        private string GetDominantTheme()
        {
            int bananas = 0, prayers = 0, pushes = 0, works = 0;
            if (PlayerBehaviorData.Instance != null)
            {
                bananas = PlayerBehaviorData.Instance.bananaCount;
                prayers = PlayerBehaviorData.Instance.prayerCount;
                pushes = PlayerBehaviorData.Instance.pushCount;
                works = PlayerBehaviorData.Instance.workCount;
            }
            if (bananas + prayers + pushes + works == 0) works = 45;

            int max = Mathf.Max(Mathf.Max(bananas, prayers), Mathf.Max(pushes, works));
            if (max == bananas) return "banana";
            if (max == prayers) return "prayer";
            if (max == pushes) return "push";
            return "work";
        }

        private void OnDestroy()
        {
            foreach (var go in _activeObjects)
            {
                if (go != null) Destroy(go);
            }
            _activeObjects.Clear();
        }
    }
}
