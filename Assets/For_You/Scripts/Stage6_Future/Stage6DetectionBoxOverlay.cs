using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// Stage6 机器视觉检测框实验 (Detection Box Overlay Experiment) — 真实数据版
    ///
    /// 在模型表面附近生成若干个"四角折角"检测框 + 小标签，模拟"AI 正在持续扫描这个物体"的观感。
    /// 每个检测框对应 PlayerBehaviorData 里四个真实行为类别之一 (BANANA/PRAYER/PUSH/WORK)，
    /// 置信度不再是随机数，而是该行为的真实计数相对于四项里最高一项的归一化比例——
    /// 数值本身就直接可视化"哪个行为最沉迷"，而不只是氛围装饰。
    ///
    /// 持续流动效果 (借鉴生成艺术)：
    /// - 每个框用 Perlin 噪声持续自由漂移 (永不静止)、大小呼吸式缩放
    /// - 切换检测目标时先淡出、换位、再淡入 (而不是瞬间跳变)
    /// - 框与框之间有低透明度连接线，跟随漂移实时更新，做出数据网络感
    /// - 持续上下扫描的扫描线、置信度数字实时轻微跳动
    ///
    /// 测试用法：勾选 Use Test Override，填四个 Test 计数值，然后右键组件标题栏，
    /// 选 "Rebuild Detection Boxes" 立即按新数值重新生成，不需要进 Play 模式。
    /// </summary>
    public class Stage6DetectionBoxOverlay : MonoBehaviour
    {
        [Header("主题选择 (决定顶部汇总标签读取哪个 PlayerBehaviorData 计数器)")]
        public DataReadoutTheme theme = DataReadoutTheme.Banana;

        [Header("测试专用 (不改真实数据，仅用于在编辑器里预览不同数值下的效果)")]
        public bool useTestOverride = false;
        public int testCountOverride = 20;
        [Tooltip("四个类别的测试计数，仅在 Use Test Override 勾选时用于驱动检测框的真实标签/置信度显示")]
        public int testBananaCount = 47;
        public int testPrayerCount = 32;
        public int testPushCount = 118;
        public int testWorkCount = 205;

        [Header("检测框数量 (Data-Driven，由本物体自身主题的计数驱动数量)")]
        public float countPerBox = 8f;
        public int maxBoxes = 8;

        [Header("跨主题相对强度 (让同一个 ArtCollection 组内的多个模型互相比较，数据越多的模型框越大越浮夸)")]
        [Tooltip("启用后，本物体的框数量/尺寸不再只看自己的绝对计数，而是跟其他三个主题比较后的相对比例")]
        public bool enableRelativeIntensity = true;

        [Tooltip("相对强度最低 (0，即四项里最不涉及的那一项) 时的框数量下限")]
        public int minBoxesAtLowIntensity = 1;

        [Tooltip("相对强度最低时的框体缩放倍率")]
        [Range(0.1f, 1f)] public float minSizeMultiplier = 0.35f;

        [Tooltip("相对强度最高 (1，即四项里最沉迷的那一项) 时的框体缩放倍率")]
        [Range(1f, 3f)] public float maxSizeMultiplier = 1.8f;

        [Header("检测框外观 (四角折角风格)")]
        public Vector2 boxSizeRange = new Vector2(1.2f, 3.5f);
        [Range(0.1f, 0.5f)] public float cornerArmRatio = 0.28f;
        public float lineWidth = 0.03f;

        public Color[] boxColors = new Color[]
        {
            new Color(0.31f, 0.82f, 0.91f),
            new Color(0.95f, 0.63f, 0.33f),
            new Color(0.62f, 0.55f, 0.94f),
            new Color(0.90f, 0.45f, 0.55f)
        };

        public float labelFontSize = 3f;

        private static readonly string[] RealCategoryTags = { "BANANA", "PRAYER", "PUSH", "WORK" };

        [Header("持续有机漂移 (Perlin 噪声驱动，解决画面死板问题的核心)")]
        public float driftAmount = 0.6f;
        public float driftSpeed = 0.15f;

        [Header("呼吸缩放")]
        [Range(0f, 0.5f)] public float sizeBreathAmount = 0.18f;
        public float sizeBreathSpeed = 0.8f;

        [Header("颜色呼吸脉冲")]
        public float pulseSpeed = 2.5f;
        [Range(0f, 1f)] public float pulseAmount = 0.35f;

        [Header("目标切换 (淡出-换位-淡入，而不是瞬间跳变)")]
        public Vector2 boxRefreshIntervalRange = new Vector2(2.5f, 6f);
        public float fadeDuration = 0.6f;

        [Header("置信度数字跳动 (在真实值基础上做轻微视觉跳动，不改变真实数值本身)")]
        public float confidenceJitterInterval = 0.25f;
        public float confidenceJitterAmount = 0.015f;

        [Header("扫描线")]
        public float scanLineSpeed = 1.2f;

        [Header("连接线 (框与框之间的低透明度连线，做出数据网络感)")]
        public bool enableConnectorLines = true;
        [Range(0f, 1f)] public float connectorAlpha = 0.18f;
        public float connectorWidth = 0.012f;

        private enum FadeState { Idle, FadingOut, FadingIn }

        private class DetectionBoxInstance
        {
            public GameObject root;
            public LineRenderer[] cornerLines;
            public Material lineMat;
            public TextMeshPro label;
            public Color baseColor;
            public float baseConfidence;
            public string tag;
            public float pulsePhase;
            public float breathPhase;
            public Vector3 basePosition;
            public float baseSize;
            public Vector3 noiseSeed;
            public float nextRefreshTime;
            public FadeState fadeState;
            public float fadeTimer;
            public float alpha = 1f;
        }

        private GameObject _generatedRoot;
        private readonly List<DetectionBoxInstance> _boxes = new List<DetectionBoxInstance>();
        private readonly List<LineRenderer> _connectors = new List<LineRenderer>();
        private LineRenderer _scanLine;
        private Bounds _bounds;
        private float _confidenceJitterTimer;
        private System.Random _rng;
        private int _nextCategoryIndex;
        private float _currentSizeMultiplier = 1f;

        private void Start()
        {
            RebuildOverlay();
        }

        [ContextMenu("Rebuild Detection Boxes")]
        public void RebuildOverlay()
        {
            if (_generatedRoot != null)
            {
                if (Application.isPlaying) Destroy(_generatedRoot);
                else DestroyImmediate(_generatedRoot);
                _generatedRoot = null;
            }
            _boxes.Clear();
            _connectors.Clear();
            _scanLine = null;
            _nextCategoryIndex = 0;

            int count = GetCountForTheme();

            float relativeWeight = 1f;
            if (enableRelativeIntensity)
            {
                int[] allCounts = GetAllCounts();
                int myCount = allCounts[(int)theme];
                int maxCount = Mathf.Max(1, Mathf.Max(Mathf.Max(allCounts[0], allCounts[1]), Mathf.Max(allCounts[2], allCounts[3])));
                relativeWeight = Mathf.Clamp01((float)myCount / maxCount);
            }

            _currentSizeMultiplier = enableRelativeIntensity ? Mathf.Lerp(minSizeMultiplier, maxSizeMultiplier, relativeWeight) : 1f;

            int boxCount;
            if (enableRelativeIntensity)
            {
                boxCount = count > 0 ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minBoxesAtLowIntensity, maxBoxes, relativeWeight)), 1, maxBoxes) : 0;
            }
            else
            {
                boxCount = Mathf.Clamp(Mathf.RoundToInt(count / Mathf.Max(0.01f, countPerBox)), count > 0 ? 1 : 0, maxBoxes);
            }

            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr == null)
            {
                Debug.LogWarning("[Stage6DetectionBoxOverlay] 本物体及子物体上没有找到 MeshRenderer，无法确定生成范围。");
                return;
            }
            _bounds = mr.bounds;
            _rng = new System.Random(count * 12347 + (int)theme * 733);

            GameObject root = new GameObject("DetectionBoxOverlay_" + theme);
            root.transform.position = _bounds.center;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _generatedRoot = root;

            for (int i = 0; i < boxCount; i++)
            {
                Vector3 pos = RandomPointInBounds();
                Color color = boxColors[i % boxColors.Length];
                CreateDetectionBox(root.transform, pos, color);
            }

            if (enableConnectorLines) CreateConnectorLines(root.transform);
            CreateScanLine(root.transform);
            CreateSummaryLabel(root.transform, count);
        }

        private void Update()
        {
            if (_generatedRoot == null) return;

            float t = Time.time;
            float dt = Time.deltaTime;

            if (_scanLine != null)
            {
                float y = _bounds.min.y + Mathf.PingPong(t * scanLineSpeed, _bounds.size.y);
                _scanLine.SetPosition(0, new Vector3(_bounds.min.x, y, _bounds.center.z));
                _scanLine.SetPosition(1, new Vector3(_bounds.max.x, y, _bounds.center.z));
            }

            _confidenceJitterTimer += dt;
            bool jitterTick = _confidenceJitterTimer >= confidenceJitterInterval;
            if (jitterTick) _confidenceJitterTimer = 0f;

            for (int i = 0; i < _boxes.Count; i++)
            {
                var box = _boxes[i];
                if (box.root == null) continue;

                if (box.fadeState == FadeState.FadingOut)
                {
                    box.fadeTimer += dt;
                    box.alpha = 1f - Mathf.Clamp01(box.fadeTimer / fadeDuration);
                    if (box.fadeTimer >= fadeDuration)
                    {
                        DoActualRefresh(box);
                        box.fadeState = FadeState.FadingIn;
                        box.fadeTimer = 0f;
                        box.alpha = 0f;
                    }
                }
                else if (box.fadeState == FadeState.FadingIn)
                {
                    box.fadeTimer += dt;
                    box.alpha = Mathf.Clamp01(box.fadeTimer / fadeDuration);
                    if (box.fadeTimer >= fadeDuration)
                    {
                        box.fadeState = FadeState.Idle;
                        box.alpha = 1f;
                    }
                }
                else if (t >= box.nextRefreshTime)
                {
                    box.fadeState = FadeState.FadingOut;
                    box.fadeTimer = 0f;
                }

                float nx = (Mathf.PerlinNoise(t * driftSpeed + box.noiseSeed.x, 0f) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(t * driftSpeed + box.noiseSeed.y, 1f) - 0.5f) * 2f;
                float nz = (Mathf.PerlinNoise(t * driftSpeed + box.noiseSeed.z, 2f) - 0.5f) * 2f;
                Vector3 driftOffset = new Vector3(nx, ny, nz) * driftAmount;
                Vector3 liveCenter = box.basePosition + driftOffset;

                float breath = 1f + sizeBreathAmount * Mathf.Sin(t * sizeBreathSpeed + box.breathPhase);
                float liveSize = box.baseSize * breath;

                LayoutCornerBrackets(box, liveCenter, liveSize);

                float pulse = 1f - pulseAmount * 0.5f + pulseAmount * 0.5f * Mathf.Sin(t * pulseSpeed + box.pulsePhase);
                Color pulsedColor = box.baseColor * pulse;
                pulsedColor.a = box.alpha;
                if (box.lineMat != null)
                {
                    if (box.lineMat.HasProperty("_BaseColor")) box.lineMat.SetColor("_BaseColor", pulsedColor);
                    if (box.lineMat.HasProperty("_Color")) box.lineMat.SetColor("_Color", pulsedColor);
                }
                if (box.label != null)
                {
                    box.label.color = pulsedColor;
                    box.label.transform.position = liveCenter + Vector3.up * (liveSize * 0.5f + 0.15f);
                }

                if (jitterTick && box.label != null && box.alpha > 0.05f)
                {
                    float jitter = ((float)_rng.NextDouble() * 2f - 1f) * confidenceJitterAmount;
                    float shown = Mathf.Clamp01(box.baseConfidence + jitter);
                    box.label.text = box.tag + "  " + shown.ToString("0.00");
                }
            }

            for (int c = 0; c < _connectors.Count; c++)
            {
                if (c + 1 >= _boxes.Count) break;
                var a = _boxes[c];
                var b = _boxes[c + 1];
                if (a.root == null || b.root == null) continue;

                Vector3 posA = a.label != null ? a.label.transform.position - Vector3.up * 0.15f : a.basePosition;
                Vector3 posB = b.label != null ? b.label.transform.position - Vector3.up * 0.15f : b.basePosition;
                _connectors[c].SetPosition(0, posA);
                _connectors[c].SetPosition(1, posB);

                float avgAlpha = (a.alpha + b.alpha) * 0.5f * connectorAlpha;
                Color cColor = Color.Lerp(a.baseColor, b.baseColor, 0.5f);
                cColor.a = avgAlpha;
                var mat = _connectors[c].material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", cColor);
                _connectors[c].startColor = cColor;
                _connectors[c].endColor = cColor;
            }
        }

        private void DoActualRefresh(DetectionBoxInstance box)
        {
            box.basePosition = RandomPointInBounds();
            box.baseSize = Mathf.Lerp(boxSizeRange.x, boxSizeRange.y, (float)_rng.NextDouble()) * _currentSizeMultiplier;

            var (tag, confidence) = NextRealDataPoint();
            box.tag = tag;
            box.baseConfidence = confidence;

            box.noiseSeed = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f);
            if (box.label != null) box.label.text = box.tag + "  " + box.baseConfidence.ToString("0.00");
            box.nextRefreshTime = Time.time + Random.Range(boxRefreshIntervalRange.x, boxRefreshIntervalRange.y);
        }

        private Vector3 RandomPointInBounds()
        {
            return new Vector3(
                _bounds.center.x + ((float)_rng.NextDouble() * 2f - 1f) * _bounds.extents.x * 0.8f,
                _bounds.center.y + ((float)_rng.NextDouble() * 2f - 1f) * _bounds.extents.y * 0.8f,
                _bounds.center.z + ((float)_rng.NextDouble() * 2f - 1f) * _bounds.extents.z * 0.8f
            );
        }

        /// <summary>
        /// 依次轮流分配四个真实类别 (BANANA/PRAYER/PUSH/WORK)，确保它们都有机会出现，
        /// 而不是纯随机可能导致某个类别一直不出现。置信度 = 该类别真实计数 / 四项最高计数。
        /// </summary>
        private (string tag, float confidence) NextRealDataPoint()
        {
            int index = _nextCategoryIndex % RealCategoryTags.Length;
            _nextCategoryIndex++;

            int[] counts = GetAllCounts();
            int maxCount = Mathf.Max(1, Mathf.Max(Mathf.Max(counts[0], counts[1]), Mathf.Max(counts[2], counts[3])));
            float confidence = Mathf.Clamp01((float)counts[index] / maxCount);

            return (RealCategoryTags[index], confidence);
        }

        private int[] GetAllCounts()
        {
            if (useTestOverride)
            {
                return new int[] { testBananaCount, testPrayerCount, testPushCount, testWorkCount };
            }

            var data = PlayerBehaviorData.Instance;
            if (data == null) return new int[] { 0, 0, 0, 0 };
            return new int[] { data.bananaCount, data.prayerCount, data.pushCount, data.workCount };
        }

        private void CreateDetectionBox(Transform parent, Vector3 worldCenter, Color color)
        {
            GameObject boxGo = new GameObject("Box");
            boxGo.transform.SetParent(parent, true);
            boxGo.transform.position = worldCenter;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            LineRenderer[] corners = new LineRenderer[4];
            for (int c = 0; c < 4; c++)
            {
                GameObject cornerGo = new GameObject("Corner_" + c);
                cornerGo.transform.SetParent(boxGo.transform, true);
                LineRenderer lr = cornerGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = false;
                lr.positionCount = 3;
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.numCapVertices = 2;
                lr.material = mat;
                lr.startColor = color;
                lr.endColor = color;
                corners[c] = lr;
            }

            var (tag, confidence) = NextRealDataPoint();

            var box = new DetectionBoxInstance
            {
                root = boxGo,
                cornerLines = corners,
                lineMat = mat,
                baseColor = color,
                tag = tag,
                baseConfidence = confidence,
                pulsePhase = (float)_rng.NextDouble() * Mathf.PI * 2f,
                breathPhase = (float)_rng.NextDouble() * Mathf.PI * 2f,
                basePosition = worldCenter,
                baseSize = Mathf.Lerp(boxSizeRange.x, boxSizeRange.y, (float)_rng.NextDouble()) * _currentSizeMultiplier,
                noiseSeed = new Vector3((float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f, (float)_rng.NextDouble() * 100f),
                nextRefreshTime = Time.time + Random.Range(boxRefreshIntervalRange.x, boxRefreshIntervalRange.y),
                fadeState = FadeState.Idle,
                alpha = 1f
            };

            LayoutCornerBrackets(box, worldCenter, box.baseSize);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(boxGo.transform, false);
            labelGo.transform.position = worldCenter + Vector3.up * (box.baseSize * 0.5f + 0.15f);

            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = box.tag + "  " + box.baseConfidence.ToString("0.00");
            tmp.fontSize = labelFontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            RectTransform rt = labelGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 40f);
            box.label = tmp;

            _boxes.Add(box);
        }

        private void LayoutCornerBrackets(DetectionBoxInstance box, Vector3 worldCenter, float size)
        {
            float half = size * 0.5f;
            float arm = half * cornerArmRatio * 2f;

            Vector3[] cornerPoints = new Vector3[]
            {
                worldCenter + new Vector3(-half, -half, 0f),
                worldCenter + new Vector3(half, -half, 0f),
                worldCenter + new Vector3(half, half, 0f),
                worldCenter + new Vector3(-half, half, 0f)
            };

            Vector3[] armDirX = new Vector3[] { Vector3.right, Vector3.left, Vector3.left, Vector3.right };
            Vector3[] armDirY = new Vector3[] { Vector3.up, Vector3.up, Vector3.down, Vector3.down };

            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = cornerPoints[c];
                Vector3 p1 = corner + armDirX[c] * arm;
                Vector3 p2 = corner + armDirY[c] * arm;
                box.cornerLines[c].SetPosition(0, p1);
                box.cornerLines[c].SetPosition(1, corner);
                box.cornerLines[c].SetPosition(2, p2);
            }
        }

        private void CreateConnectorLines(Transform parent)
        {
            for (int i = 0; i < _boxes.Count - 1; i++)
            {
                GameObject connGo = new GameObject("Connector_" + i);
                connGo.transform.SetParent(parent, true);

                LineRenderer lr = connGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = false;
                lr.positionCount = 2;
                lr.startWidth = connectorWidth;
                lr.endWidth = connectorWidth;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                Material mat = new Material(shader);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                lr.material = mat;

                _connectors.Add(lr);
            }
        }

        private void CreateScanLine(Transform parent)
        {
            GameObject scanGo = new GameObject("ScanLine");
            scanGo.transform.SetParent(parent, true);

            LineRenderer lr = scanGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.positionCount = 2;
            lr.startWidth = lineWidth * 1.4f;
            lr.endWidth = lineWidth * 1.4f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            Color scanColor = new Color(0.7f, 0.95f, 1f, 0.85f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", scanColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", scanColor);
            lr.material = mat;
            lr.startColor = scanColor;
            lr.endColor = scanColor;

            _scanLine = lr;
        }

        private void CreateSummaryLabel(Transform parent, int count)
        {
            GameObject labelGo = new GameObject("SummaryLabel");
            labelGo.transform.SetParent(parent, true);
            labelGo.transform.position = _bounds.center + Vector3.up * (_bounds.extents.y + 1.0f);

            TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = BuildSummaryText(count);
            tmp.fontSize = labelFontSize * 1.4f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            RectTransform rt = labelGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 60f);
        }

        private string BuildSummaryText(int count)
        {
            switch (theme)
            {
                case DataReadoutTheme.Banana: return "SCAN COMPLETE — BANANA_CYCLES: " + count;
                case DataReadoutTheme.Prayer: return "SCAN COMPLETE — KNEELING_CYCLES: " + count;
                case DataReadoutTheme.Push: return "SCAN COMPLETE — ASCENT_CYCLES: " + count;
                case DataReadoutTheme.Work: return "SCAN COMPLETE — KEYSTROKE_CYCLES: " + count;
                default: return "SCAN COMPLETE — CYCLES: " + count;
            }
        }

        private int GetCountForTheme()
        {
            if (useTestOverride)
            {
                switch (theme)
                {
                    case DataReadoutTheme.Banana: return testBananaCount;
                    case DataReadoutTheme.Prayer: return testPrayerCount;
                    case DataReadoutTheme.Push: return testPushCount;
                    case DataReadoutTheme.Work: return testWorkCount;
                    default: return testCountOverride;
                }
            }

            var data = PlayerBehaviorData.Instance;
            if (data == null) return 0;

            switch (theme)
            {
                case DataReadoutTheme.Banana: return data.bananaCount;
                case DataReadoutTheme.Prayer: return data.prayerCount;
                case DataReadoutTheme.Push: return data.pushCount;
                case DataReadoutTheme.Work: return data.workCount;
                default: return 0;
            }
        }
    }
}
