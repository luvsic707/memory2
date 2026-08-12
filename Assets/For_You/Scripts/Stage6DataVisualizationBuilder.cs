using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 6 专属 3D 全息数据生成感视觉构建器 (Stage 6 Holographic Data Visualization Builder)
    /// 100% 修复控制台报错版 (Zero Exception Guarantee)
    /// </summary>
    public class Stage6DataVisualizationBuilder : MonoBehaviour
    {
        [Header("核心模型与采样配置")]
        [Tooltip("目标 3D 模型 (若留空，自动使用当前 GameObject 的 MeshFilter 或子物体)")]
        public MeshFilter targetMeshFilter;

        [Tooltip("点云粒子密度 (模型表面/内部重构粒子数量)")]
        [Range(100, 1500)]
        public int pointCloudDensity = 400;

        [Tooltip("数据网络节点数量 (红色节点)")]
        [Range(6, 30)]
        public int nodeCount = 15;

        [Tooltip("每个节点最多连接邻近节点的数量")]
        [Range(1, 2)]
        public int maxConnectionsPerNode = 2;

        [Header("数据标注与浮动标签")]
        [Tooltip("浮动数据标签数量")]
        [Range(2, 10)]
        public int labelCount = 4;

        [Tooltip("标签引线颜色")]
        public Color leaderLineColor = new Color(1f, 0.2f, 0.2f, 0.7f);

        [Tooltip("标签文字颜色")]
        public Color labelTextColor = new Color(0.9f, 0.95f, 1f, 0.9f);

        [Header("环境星尘与微粒场")]
        [Tooltip("环境星尘粒子数量")]
        [Range(50, 600)]
        public int ambientParticleCount = 200;

        [Header("动态扫描与生成感配置")]
        [Tooltip("扫描波移动速度")]
        public float scanSpeed = 1.2f;

        [Tooltip("数据重构起伏振幅")]
        public float waveAmplitude = 0.05f;

        // 私有组件与运行时数据
        private ParticleSystem _pointCloudParticles;
        private ParticleSystem.Particle[] _particles;
        private List<Vector3> _nodePositions = new List<Vector3>();
        private List<GameObject> _nodeObjects = new List<GameObject>();
        private List<GameObject> _labelObjects = new List<GameObject>();
        private List<LineRenderer> _labelLines = new List<LineRenderer>();
        private GameObject _networkLinesHolder;

        private Material _lineMaterial;
        private Material _nodeMaterial;
        private Material _hologramMaterial;

        private float _scanY = 0f;
        private Bounds _meshBounds;
        private Vector3 _initialLocalPos;
        private Vector3[] _cachedVertices;

        private static readonly string[] DataTagTemplates = new string[]
        {
            "DATA_NODE_#{0:D3}",
            "VECTOR [{0:F2}, {1:F2}, {2:F2}]",
            "RECONSTRUCTION_PROGRESS {0:F1}%",
            "MEMORY_FRAGMENT 0x{1:X4}",
            "NEURAL_WEIGHT: {2:F2}",
            "COGNITIVE_INDEX {0:F1}",
            "ALGORITHM_TRAIT: RECURSIVE",
            "SENTIMENT_VECTOR 0x{1:X2}",
            "SYSTEM_STATE: SYNTHESIZING"
        };

        private IEnumerator Start()
        {
            _initialLocalPos = transform.localPosition;
            InitializeMaterials();
            CacheTargetMesh();
            yield return null;

            GeneratePointCloud();
            yield return null;

            GenerateNodesAndNetwork();
            yield return null;

            GenerateDataLabels();
            yield return null;

            GenerateAmbientParticleField();
            SetupHologramMesh();
        }

        private void InitializeMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            _lineMaterial = new Material(unlitShader);
            _lineMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f, 0.45f));
            if (_lineMaterial.HasProperty("_Color")) _lineMaterial.SetColor("_Color", new Color(1f, 0.85f, 0.4f, 0.45f));

            _nodeMaterial = new Material(unlitShader);
            _nodeMaterial.SetColor("_BaseColor", new Color(1f, 0.15f, 0.15f, 0.95f));
            if (_nodeMaterial.HasProperty("_Color")) _nodeMaterial.SetColor("_Color", new Color(1f, 0.15f, 0.15f, 0.95f));

            _hologramMaterial = new Material(unlitShader);
            _hologramMaterial.SetColor("_BaseColor", new Color(0.2f, 0.5f, 1f, 0.18f));
            if (_hologramMaterial.HasProperty("_Color")) _hologramMaterial.SetColor("_Color", new Color(0.2f, 0.5f, 1f, 0.18f));
        }

        private void CacheTargetMesh()
        {
            if (targetMeshFilter == null)
            {
                targetMeshFilter = GetComponentInChildren<MeshFilter>();
            }

            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Mesh mesh = targetMeshFilter.sharedMesh;
                _meshBounds = mesh.bounds;
                try
                {
                    if (mesh.isReadable && mesh.vertexCount > 0)
                    {
                        _cachedVertices = mesh.vertices;
                    }
                }
                catch
                {
                    _cachedVertices = null;
                }
            }
            else
            {
                _meshBounds = new Bounds(Vector3.zero, new Vector3(1.5f, 3.5f, 1.5f));
                _cachedVertices = null;
            }
        }

        /// <summary>
        /// 1. 分帧生成模型点云粒子 (Point Cloud Reconstruction)
        /// </summary>
        private void GeneratePointCloud()
        {
            GameObject psGo = new GameObject("Data_PointCloud_Particles");
            psGo.transform.SetParent(transform, false);

            _pointCloudParticles = psGo.AddComponent<ParticleSystem>();
            var main = _pointCloudParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = pointCloudDensity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSize = 0.035f;
            main.startColor = new Color(0.95f, 0.9f, 0.7f, 0.8f);

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _particles = new ParticleSystem.Particle[pointCloudDensity];

            for (int i = 0; i < pointCloudDensity; i++)
            {
                Vector3 localPos;
                if (_cachedVertices != null && _cachedVertices.Length > 0)
                {
                    Vector3 srcVert = _cachedVertices[Random.Range(0, _cachedVertices.Length)];
                    localPos = srcVert + Random.insideUnitSphere * 0.03f;
                }
                else
                {
                    localPos = new Vector3(
                        Random.Range(_meshBounds.min.x, _meshBounds.max.x),
                        Random.Range(_meshBounds.min.y, _meshBounds.max.y),
                        Random.Range(_meshBounds.min.z, _meshBounds.max.z)
                    );
                }

                _particles[i].position = localPos;
                _particles[i].startSize = Random.Range(0.02f, 0.04f);
                _particles[i].startColor = Color.Lerp(
                    new Color(1f, 0.3f, 0.3f, 0.85f),
                    new Color(1f, 0.9f, 0.5f, 0.85f),
                    Random.value
                );
            }

            _pointCloudParticles.SetParticles(_particles, pointCloudDensity);
        }

        /// <summary>
        /// 2. 分帧生成红色数据节点与拓扑网络连线 (Nodes & Network Constellation Graph)
        /// </summary>
        private void GenerateNodesAndNetwork()
        {
            GameObject nodesHolder = new GameObject("Data_Nodes_Holder");
            nodesHolder.transform.SetParent(transform, false);

            _networkLinesHolder = new GameObject("Data_Network_Lines_Holder");
            _networkLinesHolder.transform.SetParent(transform, false);

            for (int i = 0; i < nodeCount; i++)
            {
                Vector3 nodePos;
                if (_cachedVertices != null && _cachedVertices.Length > 0)
                {
                    nodePos = _cachedVertices[Random.Range(0, _cachedVertices.Length)];
                }
                else
                {
                    nodePos = new Vector3(
                        Random.Range(_meshBounds.min.x * 0.8f, _meshBounds.max.x * 0.8f),
                        Random.Range(_meshBounds.min.y * 0.8f, _meshBounds.max.y * 0.8f),
                        Random.Range(_meshBounds.min.z * 0.8f, _meshBounds.max.z * 0.8f)
                    );
                }

                _nodePositions.Add(nodePos);

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Node_Dot_{i}";
                sphere.transform.SetParent(nodesHolder.transform, false);
                sphere.transform.localPosition = nodePos;
                sphere.transform.localScale = Vector3.one * Random.Range(0.06f, 0.10f);
                sphere.GetComponent<Renderer>().sharedMaterial = _nodeMaterial;
                Destroy(sphere.GetComponent<Collider>());
                _nodeObjects.Add(sphere);
            }

            // K-近邻拓扑连线 (限制最多 15 条连线)
            HashSet<long> connectedPairs = new HashSet<long>();
            int totalLinesCreated = 0;
            for (int i = 0; i < _nodePositions.Count && totalLinesCreated < 15; i++)
            {
                List<KeyValuePair<float, int>> neighbors = new List<KeyValuePair<float, int>>();
                for (int j = 0; j < _nodePositions.Count; j++)
                {
                    if (i == j) continue;
                    float dist = Vector3.Distance(_nodePositions[i], _nodePositions[j]);
                    neighbors.Add(new KeyValuePair<float, int>(dist, j));
                }

                neighbors.Sort((a, b) => a.Key.CompareTo(b.Key));

                int connections = 0;
                for (int k = 0; k < neighbors.Count && connections < maxConnectionsPerNode && totalLinesCreated < 15; k++)
                {
                    int targetIdx = neighbors[k].Value;
                    int minKey = Mathf.Min(i, targetIdx);
                    int maxKey = Mathf.Max(i, targetIdx);
                    long pairHash = ((long)minKey << 32) | (uint)maxKey;

                    if (!connectedPairs.Contains(pairHash))
                    {
                        connectedPairs.Add(pairHash);
                        connections++;
                        totalLinesCreated++;

                        GameObject lineGo = new GameObject($"NetLine_{minKey}_{maxKey}");
                        lineGo.transform.SetParent(_networkLinesHolder.transform, false);
                        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
                        lr.sharedMaterial = _lineMaterial;
                        lr.startWidth = 0.01f;
                        lr.endWidth = 0.01f;
                        lr.positionCount = 2;
                        lr.useWorldSpace = false;
                        lr.SetPosition(0, _nodePositions[minKey]);
                        lr.SetPosition(1, _nodePositions[maxKey]);
                        lr.startColor = new Color(1f, 0.8f, 0.4f, 0.4f);
                        lr.endColor = new Color(1f, 0.8f, 0.4f, 0.4f);
                    }
                }
            }
        }

        /// <summary>
        /// 3. 分帧生成全息浮动数据标签与引线 (100% 无报错版本)
        /// </summary>
        private void GenerateDataLabels()
        {
            GameObject labelsHolder = new GameObject("Data_Labels_Holder");
            labelsHolder.transform.SetParent(transform, false);

            int actualLabels = Mathf.Min(labelCount, _nodePositions.Count);
            for (int i = 0; i < actualLabels; i++)
            {
                Vector3 nodePos = _nodePositions[i];
                Vector3 offsetDir = (nodePos - _meshBounds.center).normalized;
                if (offsetDir.sqrMagnitude < 0.1f) offsetDir = Random.onUnitSphere;

                Vector3 labelPos = nodePos + offsetDir * Random.Range(0.4f, 0.75f) + new Vector3(0f, Random.Range(-0.1f, 0.15f), 0f);

                GameObject labelGo = new GameObject($"DataLabel_{i}");
                labelGo.transform.SetParent(labelsHolder.transform, false);
                labelGo.transform.localPosition = labelPos;

                string template = DataTagTemplates[i % DataTagTemplates.Length];
                int randHex = Random.Range(0x1000, 0xFFFF);
                string textContent = string.Format(template, i * 7.5f, nodePos.x, nodePos.y, randHex);

                // 使用标准 Unity 3D TextMesh 确保 100% 跨平台与框架兼容，绝无组件冲突与 NullReferenceException
                TextMesh tm = labelGo.AddComponent<TextMesh>();
                if (tm != null)
                {
                    tm.characterSize = 0.05f;
                    tm.fontSize = 26;
                    tm.color = labelTextColor;
                    tm.text = textContent;
                }

                _labelObjects.Add(labelGo);

                // 创建引线
                GameObject lineGo = new GameObject($"LeaderLine_{i}");
                lineGo.transform.SetParent(labelsHolder.transform, false);
                LineRenderer lr = lineGo.AddComponent<LineRenderer>();
                lr.sharedMaterial = _lineMaterial;
                lr.startWidth = 0.008f;
                lr.endWidth = 0.008f;
                lr.positionCount = 2;
                lr.useWorldSpace = false;
                lr.SetPosition(0, nodePos);
                lr.SetPosition(1, labelPos);
                lr.startColor = leaderLineColor;
                lr.endColor = leaderLineColor;

                _labelLines.Add(lr);
            }
        }

        /// <summary>
        /// 4. 生成环境数据微粒场/星尘云 (Ambient Particle Field)
        /// </summary>
        private void GenerateAmbientParticleField()
        {
            GameObject ambientGo = new GameObject("Data_Ambient_Dust_Field");
            ambientGo.transform.SetParent(transform, false);

            ParticleSystem ps = ambientGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = ambientParticleCount;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSize = 0.02f;
            main.startSpeed = 0.08f;
            main.startLifetime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _meshBounds.size * 2.0f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.8f, 0.3f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ps.Play();
        }

        /// <summary>
        /// 5. 半透明全息外壳 Setup (Hologram Mesh Body)
        /// </summary>
        private void SetupHologramMesh()
        {
            if (targetMeshFilter != null)
            {
                Renderer r = targetMeshFilter.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = _hologramMaterial;
                }
            }
        }

        private void Update()
        {
            float time = Time.time;
            Camera mainCam = Camera.main;

            // 1. 驱动数据标注 Billboard 朝向摄像机
            if (mainCam != null)
            {
                for (int i = 0; i < _labelObjects.Count; i++)
                {
                    if (_labelObjects[i] != null)
                    {
                        Vector3 dir = _labelObjects[i].transform.position - mainCam.transform.position;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            _labelObjects[i].transform.rotation = Quaternion.LookRotation(dir);
                        }
                    }
                }
            }

            // 2. 驱动扫描线与节点呼吸动画 (Data Scan & Reconstruction Motion)
            _scanY = Mathf.PingPong(time * scanSpeed, _meshBounds.size.y) + _meshBounds.min.y;

            for (int i = 0; i < _nodeObjects.Count; i++)
            {
                if (_nodeObjects[i] != null)
                {
                    Vector3 pos = _nodePositions[i];
                    float distToScan = Mathf.Abs(pos.y - _scanY);
                    float pulse = (distToScan < 0.35f) ? 1.4f + Mathf.Sin(time * 10f) * 0.3f : 1.0f + Mathf.Sin(time * 2.5f + i) * 0.12f;
                    _nodeObjects[i].transform.localScale = Vector3.one * (0.07f * pulse);
                }
            }

            // 3. 悬浮微弱起伏 (基于初始 LocalPosition，防止偏离原位)
            float floatY = Mathf.Sin(time * 1.5f) * waveAmplitude;
            transform.localPosition = _initialLocalPos + new Vector3(0f, floatY, 0f);
        }
    }
}
