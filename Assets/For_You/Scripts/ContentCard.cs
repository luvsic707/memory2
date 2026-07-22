using UnityEngine;
using TMPro;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 单张内容卡片的行为控制。
    /// 负责：漂移运动、注视吸引、愉悦反馈音效、Phase C 声音抽走、视觉呈现。
    /// 由 ContentCardSpawner 生成并配置。
    /// </summary>
    public class ContentCard : MonoBehaviour
    {
        [Header("运动参数")]
        [Tooltip("缓慢漂向玩家的基础速度")]
        public float driftSpeed = 1.5f;

        [Tooltip("被注视时的吸引加速度")]
        public float gazeAttractSpeed = 8f;

        [Tooltip("到达此距离后开始淡出销毁")]
        public float fadeStartDistance = 2.5f;

        [Tooltip("到达此距离后立刻销毁")]
        public float destroyDistance = 1.2f;

        [Tooltip("存活时间上限（秒），超过自动销毁")]
        public float maxLifetime = 25f;

        [Header("视觉")]
        public Color cardColor = Color.white;
        public string cardText = "";

        [Header("类型标记")]
        [Tooltip("是否是 Phase C 的私密数据卡")]
        public bool isPrivateDataCard = false;

        [Tooltip("是否是结尾选择卡（留下/继续）")]
        public bool isChoiceCard = false;
        public string choiceAction = ""; // "stay" or "continue"

        // 内部状态
        private Transform _player;
        private bool _isBeingGazed = false;
        private float _lifetime = 0f;
        private Renderer _renderer;
        private TextMeshPro _tmp;
        private MaterialPropertyBlock _mpb;
        private float _alpha = 1f;

        // 回调：通知 Stage5Controller 发生了注视交互
        public System.Action<ContentCard> OnGazeInteract;
        // 回调：通知选择卡被激活
        public System.Action<string> OnChoiceSelected;

        private Vector3 _baseScale;

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;

            // 找到玩家摄像机
            _player = Camera.main != null ? Camera.main.transform : null;

            // 如果有文字内容，创建 TextMeshPro 子物体
            if (!string.IsNullOrEmpty(cardText))
            {
                CreateTextLabel();
            }

            // 应用初始颜色
            ApplyColor(cardColor);
        }

        private void Update()
        {
            _lifetime += Time.deltaTime;
            if (_lifetime > maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_player == null) return;

            // 漂移运动
            float speed = _isBeingGazed ? gazeAttractSpeed : driftSpeed;
            Vector3 toPlayer = (_player.position - transform.position).normalized;
            transform.position += toPlayer * speed * Time.deltaTime;

            // 始终正面向玩家（修正镜像问题）
            transform.rotation = Quaternion.LookRotation(transform.position - _player.position);

            // 被注视时放大反馈
            float targetScaleMult = _isBeingGazed ? 1.25f : 1.0f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * targetScaleMult, Time.deltaTime * 8f);

            // 距离检测
            float dist = Vector3.Distance(transform.position, _player.position);

            // 淡出
            if (dist < fadeStartDistance)
            {
                float fadeT = Mathf.InverseLerp(fadeStartDistance, destroyDistance, dist);
                _alpha = 1f - fadeT;
                ApplyAlpha(_alpha);
            }

            // 销毁
            if (dist < destroyDistance)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 由 Stage5Controller 在射线命中时调用
        /// </summary>
        public void OnGazeEnter()
        {
            if (_isBeingGazed) return;
            _isBeingGazed = true;

            // 通知外部
            OnGazeInteract?.Invoke(this);

            // 如果是选择卡，触发选择
            if (isChoiceCard && !string.IsNullOrEmpty(choiceAction))
            {
                OnChoiceSelected?.Invoke(choiceAction);
            }
        }

        /// <summary>
        /// 注视离开
        /// </summary>
        public void OnGazeExit()
        {
            _isBeingGazed = false;
        }

        private void CreateTextLabel()
        {
            GameObject textGo = new GameObject("CardText");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            textGo.transform.localRotation = Quaternion.Euler(0, 180, 0); // 修正文字反向
            textGo.transform.localScale = Vector3.one * 0.4f;

            _tmp = textGo.AddComponent<TextMeshPro>();
            _tmp.text = cardText;
            _tmp.fontSize = 5.5f;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.color = Color.white;
            _tmp.enableWordWrapping = true;

            RectTransform rt = _tmp.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(4f, 3f);
        }

        private void ApplyColor(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_mpb);

            if (_renderer.material != null && _renderer.material.HasProperty("_Color"))
            {
                _renderer.material.color = c;
            }
        }

        private void ApplyAlpha(float a)
        {
            if (_renderer == null) return;
            Color c = cardColor;
            c.a = a;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            _renderer.SetPropertyBlock(_mpb);

            if (_renderer.material != null && _renderer.material.HasProperty("_Color"))
            {
                _renderer.material.color = c;
            }

            // 文字也淡出
            if (_tmp != null)
            {
                Color tc = _tmp.color;
                tc.a = a;
                _tmp.color = tc;
            }
        }
    }
}
