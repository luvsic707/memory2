using UnityEngine;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 挂载到 6_Future 场景里 "Data" 这个 TextMeshPro 物体上 (VI. The Dig 碑文所在的文字物体)。
    ///
    /// 在已有的静态诗句文本末尾，追加一段真实的 PlayerBehaviorData 计数读数，
    /// 让诗句里"它把这一切编号、分类、喂回给他"这句话，有真实的数字作为字面证据，
    /// 而不只是抽象的意象。不修改诗句本身的静态文本，只在末尾追加。
    /// </summary>
    public class Stage6DataAppender : MonoBehaviour
    {
        [Header("追加的数据日志格式 (Data-Driven，可在 Inspector 里调整措辞)")]
        public string logHeader = "\n\n[ARCHIVE LOG]";

        public string bananaLabel = "BANANA_CYCLES";
        public string prayerLabel = "KNEELING_CYCLES";
        public string pushLabel = "ASCENT_CYCLES";
        public string workLabel = "KEYSTROKE_CYCLES";

        private void Start()
        {
            var tmp = GetComponent<TextMeshPro>();
            if (tmp == null)
            {
                Debug.LogWarning("[Stage6DataAppender] 本物体上没有找到 TextMeshPro 组件。");
                return;
            }

            string baseText = tmp.text;
            var data = PlayerBehaviorData.Instance;

            int banana = data != null ? data.bananaCount : 0;
            int prayer = data != null ? data.prayerCount : 0;
            int push = data != null ? data.pushCount : 0;
            int work = data != null ? data.workCount : 0;

            string log = logHeader +
                "\n" + bananaLabel + ": " + banana +
                "\n" + prayerLabel + ": " + prayer +
                "\n" + pushLabel + ": " + push +
                "\n" + workLabel + ": " + work;

            tmp.text = baseText + log;
        }
    }
}
