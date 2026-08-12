using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage 5 双向流动性与多维视频投影系统 (Dual-Direction Flow & Video Projected Geometry System)
    /// 1. 【双向流动性】：支持 Player 向前穿梭 + 周围 3D 几何图形/环境向 Player 扑面涌现 (Surge Inward) 双重对流！
    /// 2. 【多维视频/图像纹理投影】：周围浮空的 3D 卡片与几何体表面，直接投影 mediaDatabase 里的动态视频与图像素材！
    /// </summary>
    public class Stage5PopGeometryEffect : MonoBehaviour
    {
        public static Stage5PopGeometryEffect Instance { get; private set; }

        [Header("媒体数据库（视频/图像投影）")]
        public CardMediaDatabase mediaDatabase;

        [Header("双向流动性控制 (Inspector 自由开关与调速)")]
        [Tooltip("启用环境与几何元素向 Player 方向扑面涌现 (Surge Inward)")]
        public bool enableEnvironmentSurgeInward = true;

        [Tooltip("环境扑面涌现的速度 (米/秒)")]
        public float surgeSpeed = 3.5f;

        [Header("Pop 几何符号配置")]
        [Tooltip("走廊内同时存在的 Pop 几何符号最大数量")]
        public int maxGeometryCount = 24;

        [Tooltip("符号生成半径")]
        public float spawnRadius = 0.95f;

        [Header("视频同款配色")]
        public Color neonGreen = new Color(0.2f, 1.0f, 0.4f, 1.0f);
        public Color neonPink = new Color(1.0f, 0.15f, 0.6f, 1.0f);
        public Color neonCyan = new Color(0.0f, 0.95f, 1.0f, 1.0f);
        public Color neonYellow = new Color(1.0f, 0.9f, 0.1f, 1.0f);

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

            UpdateReticleLockBox();

            if (_speedLineParticles != null)
            {
                _speedLineParticles.transform.position = _camTransform.position + _camTransform.forward * 3.5f;
            }

            // 🌟 核心突破：双向流动——周围 3D 几何图形向 Player 扑面涌现 (Surge Inward) 与循环
            for (int i = 0; i < _activeElements.Count; i++)
            {
                if (_activeElements[i] != null)
                {
                    PopElementFloating floating = _activeElements[i].GetComponent<PopElementFloating>();

                    if (enableEnvironmentSurgeInward)
                    {
                        // 向 Player 扑面涌现移动
                        _activeElements[i].transform.position -= _camTransform.forward * (surgeSpeed * Time.deltaTime);
                        if (floating != null) floating.ShiftStartPos(-_camTransform.forward * (surgeSpeed * Time.deltaTime));
                    }

                    // 当扑面涌现经过 Player 落在身后时，无缝重新生成在前方深处，并随机刷新媒体 Texture
                    float distZ = Vector3.Dot(_activeElements[i].transform.position - _camTransform.position, _camTransform.forward);
                    if (distZ < -2.0f)
                    {
                        float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
                        float xOffset = (spawnRadius * sideSign) * Random.Range(0.6f, 1.0f);
                        float yOffset = Random.Range(-0.4f, 0.6f);
                        Vector3 newPos = _camTransform.position + _camTransform.forward * Random.Range(8.0f, 13.0f) + _camTransform.right * xOffset + _camTransform.up * yOffset;
                        
                        _activeElements[i].transform.position = newPos;
                        if (floating != null)
                        {
                            floating.ResetStartPos(newPos);
                            floating.ApplyRandomMediaTexture(mediaDatabase); // 动态刷新投影材质！
                        }
                    }
                }
            }

            _activeElements.RemoveAll(item => item == null);
        }

        private IEnumerator SpawnPopGeometrySequence()
        {
            yield return new WaitForSeconds(0.2f);
            FindCamera();

            for (int i = 0; i < maxGeometryCount; i++)
            {
                SpawnPopGeometryElement();
                yield return new WaitForSeconds(0.12f);
            }
        }

        public void SpawnPopGeometryElement()
        {
            if (_camTransform == null) return;

            GameObject elemGo = new GameObject($"PopElem_Media_{Time.time:F1}");
            elemGo.transform.SetParent(transform, false);

            float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
            float zOffset = Random.Range(3.0f, 12.0f);
            float yOffset = Random.Range(-0.4f, 0.6f);
            float xOffset = spawnRadius * sideSign * Random.Range(0.6f, 1.0f);

            Vector3 spawnPos = _camTransform.position 
                             + _camTransform.forward * zOffset 
                             + _camTransform.right * xOffset 
                             + _camTransform.up * yOffset;

            elemGo.transform.position = spawnPos;

            int symbolType = Random.Range(0, 4);
            Color symbolColor = GetRandomNeonColor();

            BuildSymbolMeshAndMaterial(elemGo, symbolType, symbolColor);

            SphereCollider col = elemGo.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            PopElementFloating floating = elemGo.AddComponent<PopElementFloating>();
            floating.color = symbolColor;
            floating.ApplyRandomMediaTexture(mediaDatabase); // 应用媒体纹理投影

            _activeElements.Add(elemGo);
        }

        private void BuildSymbolMeshAndMaterial(GameObject parent, int type, Color color)
        {
            MeshFilter mf = parent.AddComponent<MeshFilter>();
            MeshRenderer mr = parent.AddComponent<MeshRenderer>();

            PrimitiveType pType = PrimitiveType.Cube;
            Vector3 scale = Vector3.one * Random.Range(0.3f, 0.55f);

            switch (type)
            {
                case 0: pType = PrimitiveType.Cube; scale = new Vector3(0.5f, 0.5f, 0.05f); break; // 视频卡片板
                case 1: pType = PrimitiveType.Cylinder; scale = new Vector3(0.4f, 0.02f, 0.4f); break;
                case 2: pType = PrimitiveType.Cube; scale = new Vector3(0.5f, 0.1f, 0.1f); break;
                case 3: default: pType = PrimitiveType.Sphere; scale = Vector3.one * 0.3f; break;
            }

            GameObject prim = GameObject.CreatePrimitive(pType);
            mf.sharedMesh = prim.GetComponent<MeshFilter>().sharedMesh;
            Destroy(prim);

            parent.transform.localScale = scale;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
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
            Color lockColor = new Color(0.2f, 1.0f, 0.3f, 0.9f);
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

        private void CreateSpeedLineParticleRain()
        {
            GameObject particleGo = new GameObject("SpeedLineRainParticles");
            particleGo.transform.SetParent(transform, false);
            if (_camTransform != null) particleGo.transform.position = _camTransform.position + _camTransform.forward * 4f;

            _speedLineParticles = particleGo.AddComponent<ParticleSystem>();
            var main = _speedLineParticles.main;
            main.startSpeed = 16f;
            main.startSize = 0.08f;
            main.startColor = new Color(1.0f, 1.0f, 1.0f, 0.75f);
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _speedLineParticles.emission;
            emission.rateOverTime = 45;

            var shape = _speedLineParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.5f, 4.5f, 1f);

            ParticleSystemRenderer psr = particleGo.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 4.5f;
        }
    }

    public class PopElementFloating : MonoBehaviour
    {
        public Color color;
        private Vector3 _rotSpeed;
        private Vector3 _startPos;
        private float _timeOffset;
        private MeshRenderer _renderer;

        private void Start()
        {
            _startPos = transform.position;
            _rotSpeed = new Vector3(Random.Range(30f, 90f), Random.Range(40f, 100f), Random.Range(20f, 60f));
            _timeOffset = Random.Range(0f, 10f);
            _renderer = GetComponent<MeshRenderer>();
        }

        public void ShiftStartPos(Vector3 delta)
        {
            _startPos += delta;
        }

        public void ResetStartPos(Vector3 pos)
        {
            _startPos = pos;
        }

        /// <summary>
        /// 将 mediaDatabase 中的视频/图片素材动态投影到该 3D 几何体表面
        /// </summary>
        public void ApplyRandomMediaTexture(CardMediaDatabase db)
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null || db == null) return;

            Texture tex = db.GetEntertainmentTexture();
            if (tex != null && _renderer.material != null)
            {
                if (_renderer.material.HasProperty("_MainTex")) _renderer.material.SetTexture("_MainTex", tex);
                if (_renderer.material.HasProperty("_BaseMap")) _renderer.material.SetTexture("_BaseMap", tex);
            }
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
