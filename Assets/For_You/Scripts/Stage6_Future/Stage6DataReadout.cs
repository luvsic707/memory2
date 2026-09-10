using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    public enum DataReadoutTheme
    {
        Banana,
        Prayer,
        Push,
        Work
    }

    /// <summary>
    /// Stage6 最简数据读数牌 (Data Readout Label)
    ///
    /// 先不做任何复杂视觉效果，只做最基础的事：在模型旁边生成一块始终朝向摄像机的文字牌，
    /// 直接显示 PlayerBehaviorData 里对应的真实计数。手动挂到每个复用模型旁边，
    /// 与 Stage6ModelGlitchMatrix 完全平级、互不引用，保持低耦合。
    ///
    /// 等确认"数据真的对/摆放位置对/能看清"之后，再考虑要不要往上叠加视觉效果。
    /// </summary>
    public class Stage6DataReadout : MonoBehaviour
    {
        [Header("主题选择 (决定读取哪个 PlayerBehaviorData 计数器)")]
        public DataReadoutTheme theme = DataReadoutTheme.Banana;

        [Header("位置与外观")]
        [Tooltip("文字牌相对本物体 Transform 的局部偏移")]
        public Vector3 offset = new Vector3(1.5f, 0.5f, 0f);

        [Tooltip("文字大小")]
        public float fontSize = 6f;

        [Tooltip("文字颜色")]
        public Color textColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("是否始终朝向摄像机 (billboard)")]
        public bool alwaysFaceCamera = true;

        private TextMeshPro _tmp;
        private Transform _anchor;

        private void Start()
        {
            // host 模型本身可能有极端/非均匀缩放 (例如某些复用的旧模型 lossyScale 很夸张)，
            // 用一个干净的锚点 (世界位置 + 单位缩放 + 单位旋转) 来承载文字，避免被拖变形。
            GameObject anchorGo = new GameObject("DataReadoutAnchor_" + theme);
            anchorGo.transform.position = transform.position;
            anchorGo.transform.rotation = Quaternion.identity;
            anchorGo.transform.localScale = Vector3.one;
            if (transform.parent != null) anchorGo.transform.SetParent(transform.parent, true);
            _anchor = anchorGo.transform;

            GameObject textGo = new GameObject("DataReadoutText_" + theme);
            textGo.transform.SetParent(_anchor, false);
            textGo.transform.localPosition = offset;

            _tmp = textGo.AddComponent<TextMeshPro>();
            _tmp.fontSize = fontSize;
            _tmp.color = textColor;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.enableWordWrapping = false;

            RectTransform rt = textGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 100f);

            UpdateText();
        }

        private void Update()
        {
            if (alwaysFaceCamera && _tmp != null && Camera.main != null)
            {
                _tmp.transform.rotation = Quaternion.LookRotation(_tmp.transform.position - Camera.main.transform.position);
            }
        }

        private void UpdateText()
        {
            if (_tmp == null) return;
            int count = GetCountForTheme();
            _tmp.text = BuildLabel(count);
        }

        private int GetCountForTheme()
        {
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

        private string BuildLabel(int count)
        {
            switch (theme)
            {
                case DataReadoutTheme.Banana:
                    return "BANANA CYCLES: " + count;
                case DataReadoutTheme.Prayer:
                    return "KNEELING CYCLES: " + count;
                case DataReadoutTheme.Push:
                    return "ASCENT CYCLES: " + count;
                case DataReadoutTheme.Work:
                    return "KEYSTROKE CYCLES: " + count;
                default:
                    return "CYCLES: " + count;
            }
        }
    }
}
