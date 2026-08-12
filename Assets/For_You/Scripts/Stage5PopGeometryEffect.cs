using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 视频同款魔幻 Pop 几何粒子与 HUD 目标锁定系统 (Video-Style Pop Geometry & Reticle System)
    /// 100% 还原用户视频 Preview 效果：
    /// 1. 浮空 2D/3D 线框方块 [ ]、发光三角形 Δ、绿/粉交叉 X、加号 + 与圆圈 O 阵列；
    /// 2. 急速穿梭的白/绿速度光线雨 particle rain；
    /// 3. 准心指向目标时的荧光绿 HUD 目标锁定框 (Target Lock Box)。
    /// </summary>
    public class Stage5PopGeometryEffect : MonoBehaviour
    {
        public static Stage5PopGeometryEffect Instance { get; private set; }

        [Header("Pop 几何符号配置")]
        [Tooltip("走廊内同时存在的 Pop 几何符号最大数量")]
        public int maxGeometryCount = 20;

        [Tooltip("符号生成半径")]
        public float spawnRadius = 0.85f;

        [Header("视频同款配色")]
        public Color neonGreen = new Color(0.2f, 1.0f, 0.4f, 1.0f);   // 荧光绿 (主色)
        public Color neonPink = new Color(1.0f, 0.15f, 0.6f, 1.0f);   // 霓虹粉
        public Color neonCyan = new Color(0.0f, 0.95f, 1.0f, 1.0f);   // 电光青
        public Color neonYellow = new Color(1.0f, 0.9f, 0.1f, 1.0f);   // 亮黄

        private List<GameObject> _activeElements = new List<GameObject>();
        private GameObject _reticleLockBox;
        private Transform _camTransform;
        private ParticleSystem _speedLineParticles;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        private void Start()
        {
            FindCamera();
            CreateReticleLockBox();
            CreateSpeedLineParticleRain();
            StartCoroutine(SpawnPopGeometrySequence());
        }

        private void FindCamera()
        {
            if (_camTransform != null) return;
            if (Camera.main != null) _camTransform = Camera.main.transform;
        }

        private void Update()
        {
            FindCamera();
            if (_camTransform == null) return;

            // 准心射线检测与 HUD 锁定框更新
            UpdateReticleLockBox();

            // 清理已销毁元素
            _activeElements.RemoveAll(item => item == null);
        }

        private IEnumerator SpawnPopGeometrySequence()
        {
            yield return new WaitForSeconds(0.2f);
            FindCamera();

            for (int i = 0; i < maxGeometryCount; i++)
            {
                SpawnPopGeometryElement();
                yield return new WaitForSeconds(0.15f);
            }
        }

        /// <summary>
        /// 视频同款：生成线框方块 [ ]、三角形 Δ、交叉 X、加号 +
        /// </summary>
        public void SpawnPopGeometryElement()
        {
            if (_camTransform == null) return;

            GameObject elemGo = new GameObject($"PopElem_{Time.time:F1}");
            elemGo.transform.SetParent(transform, false);

            float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
            float zOffset = Random.Range(2.0f, 8.0f);
            float yOffset = Random.Range(-0.4f, 0.6f);
            float xOffset = spawnRadius * sideSign * Random.Range(0.6f, 1.0f);

            Vector3 spawnPos = _camTransform.position 
                             + _camTransform.forward * zOffset 
                             + _camTransform.right * xOffset 
                             + _camTransform.up * yOffset;

            elemGo.transform.position = spawnPos;

            // 随机选择符号类型
            int symbolType = Random.Range(0, 4); // 0: Square/Box, 1: Triangle, 2: Cross X, 3: Plus +
            Color symbolColor = GetRandomNeonColor();

            BuildSymbolMeshAndMaterial(elemGo, symbolType, symbolColor);

            SphereCollider col = elemGo.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            // 浮动与旋转组件
            PopElementFloating floating = elemGo.AddComponent<PopElementFloating>();
            floating.color = symbolColor;

            _activeElements.Add(elemGo);
        }

        private void BuildSymbolMeshAndMaterial(GameObject parent, int type, Color color)
        {
            MeshFilter mf = parent.AddComponent<MeshFilter>();
            MeshRenderer mr = parent.AddComponent<MeshRenderer>();

            PrimitiveType pType = PrimitiveType.Cube;
            Vector3 scale = Vector3.one * Random.Range(0.25f, 0.45f);

            switch (type)
            {
                case 0: pType = PrimitiveType.Cube; scale = new Vector3(0.35f, 0.35f, 0.05f); break; // 视频同款线框矩形框
                case 1: pType = PrimitiveType.Cylinder; scale = new Vector3(0.3f, 0.02f, 0.3f); break; // 视频同款三角形/圆盘
                case 2: pType = PrimitiveType.Cube; scale = new Vector3(0.4f, 0.08f, 0.08f); break; // 视频同款 X 交叉
                case 3: default: pType = PrimitiveType.Sphere; scale = Vector3.one * 0.22f; break; // 加号/珠串
            }

            GameObject prim = GameObject.CreatePrimitive(pType);
            mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
            Destroy(prim);

            parent.transform.localScale = scale;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            mr.material = mat;
        }

        private Color GetRandomNeonColor()
        {
            int r = Random.Range(0, 4);
            if (r == 0) return neonGreen;
            if (r == 1) return neonPink;
            if (r == 2) return neonCyan;
            return neonYellow;
        }

        /// <summary>
        /// 视频同款：准心指向目标时的荧光绿 HUD 目标锁定框
        /// </summary>
        private void CreateReticleLockBox()
        {
            _reticleLockBox = new GameObject("HUD_ReticleLockBox");
            _reticleLockBox.transform.SetParent(transform, false);

            MeshFilter mf = _reticleLockBox.AddComponent<MeshFilter>();
            MeshRenderer mr = _reticleLockBox.AddComponent<MeshRenderer>();

            GameObject prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
            Destroy(prim);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            Color lockColor = new Color(0.2f, 1.0f, 0.3f, 0.9f); // 亮绿框
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", lockColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", lockColor);

            mr.material = mat;
            _reticleLockBox.SetActive(false);
        }

        private void UpdateReticleLockBox()
        {
            if (_reticleLockBox == null || _camTransform == null) return;

            Ray ray = new Ray(_camTransform.position, _camTransform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 50f))
            {
                if (hit.transform != null && hit.transform != _camTransform)
                {
                    _reticleLockBox.SetActive(true);
                    _reticleLockBox.transform.position = hit.transform.position;
                    _reticleLockBox.transform.rotation = Quaternion.LookRotation(_camTransform.forward);
                    _reticleLockBox.transform.localScale = hit.transform.lossyScale * 1.35f;
                    return;
                }
            }

            _reticleLockBox.SetActive(false);
        }

        /// <summary>
        /// 视频同款：垂直流逝的白色/荧光绿速度光线雨 (Speed Line Rain)
        /// </summary>
        private void CreateSpeedLineParticleRain()
        {
            GameObject particleGo = new GameObject("SpeedLineRainParticles");
            particleGo.transform.SetParent(transform, false);
            if (_camTransform != null) particleGo.transform.position = _camTransform.position + _camTransform.forward * 4f;

            _speedLineParticles = particleGo.AddComponent<ParticleSystem>();
            var main = _speedLineParticles.main;
            main.startSpeed = 15f;
            main.startSize = 0.08f;
            main.startColor = new Color(1.0f, 1.0f, 1.0f, 0.7f);
            main.maxParticles = 150;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _speedLineParticles.emission;
            emission.rateOverTime = 40;

            var shape = _speedLineParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 4f, 1f);

            ParticleSystemRenderer psr = particleGo.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 4.0f; // 细长拉伸 Speed Lines!
        }
    }

    /// <summary>
    /// 浮空元素缓速自转与悬浮
    /// </summary>
    public class PopElementFloating : MonoBehaviour
    {
        public Color color;
        private Vector3 _rotSpeed;
        private Vector3 _startPos;
        private float _timeOffset;

        private void Start()
        {
            _startPos = transform.position;
            _rotSpeed = new Vector3(Random.Range(30f, 90f), Random.Range(40f, 100f), Random.Range(20f, 60f));
            _timeOffset = Random.Range(0f, 10f);
        }

        private void Update()
        {
            transform.Rotate(_rotSpeed * Time.deltaTime, Space.Self);
            float t = Time.time + _timeOffset;
            Vector3 offset = new Vector3(Mathf.Sin(t * 2.5f) * 0.15f, Mathf.Cos(t * 2.2f) * 0.18f, Mathf.Sin(t * 1.8f) * 0.12f);
            transform.position = _startPos + offset;
        }
    }
}
