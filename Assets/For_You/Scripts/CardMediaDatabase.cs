using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 卡片媒体资源数据库（ScriptableObject）
    /// 
    /// 使用方法：
    ///   Assets 右键 → Create → TheLastCompact → Card Media Database
    ///   将生成的 .asset 文件拖入 ContentCardSpawner Inspector 的 mediaDatabase 槽。
    ///   然后将对应的图片/Texture2D 拖入各分类数组。
    /// 
    /// 内容分类：
    ///   Phase 1 → 娱乐内容（干净缤纷，TikTok 风）
    ///   Phase 2 → 娱乐内容 + 逐渐渗入玩家偏好主题
    ///   Phase 3 → 强制切换到玩家行为数据对应的荒诞主题内容
    /// </summary>
    [CreateAssetMenu(fileName = "CardMediaDatabase", menuName = "For_You_stage5/Card Media Database")]
    public class CardMediaDatabase : ScriptableObject
    {
        [Header("Phase 1 — 娱乐内容（TikTok 风，色彩缤纷）")]
        [Tooltip("猫咪/食物/网红/搞笑/萌宠截图等，Phase 1 主力内容")]
        public Texture2D[] entertainmentTextures;

        [Header("Phase 3 — 香蕉主题（荒诞滑稽，适用于香蕉行为最多的玩家）")]
        [Tooltip("各角度香蕉、香蕉配奇怪物品、猴子吃香蕉、香蕉特写慢镜头截图等")]
        public Texture2D[] bananaTextures;

        [Header("Phase 3 — 祈祷/灵性主题（适用于祈祷次数最多的玩家）")]
        [Tooltip("蜡烛特写、佛像/神像、冥想人物、星空、神圣光芒等")]
        public Texture2D[] prayerTextures;

        [Header("Phase 3 — 推石头/西西弗斯主题（适用于推岩石次数最多的玩家）")]
        [Tooltip("石头特写、雕塑局部、大理石纹路、希腊神话图像、山坡等")]
        public Texture2D[] pushTextures;

        [Header("Phase 3 — 工作/打字机主题（适用于打字次数最多的玩家）")]
        [Tooltip("键盘特写、显示器蓝屏、Excel截图、会议室空镜、打卡机、堆叠文件等")]
        public Texture2D[] workTextures;

        [Header("Phase 3 — 私密数据显形专用")]
        [Tooltip("黑底红字、数据条纹、像素乱码、二进制图案等，专门用于私密数据卡片")]
        public Texture2D[] privateDataTextures;

        // ─────────────────────────────────────────────────────────────────────────
        // 取样接口
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>随机取一张娱乐内容图（Phase 1 用）</summary>
        public Texture2D GetEntertainmentTexture()
        {
            if (entertainmentTextures == null || entertainmentTextures.Length == 0) return null;
            return entertainmentTextures[Random.Range(0, entertainmentTextures.Length)];
        }

        /// <summary>根据主题字符串随机取图（Phase 2/3 用）</summary>
        /// <param name="theme">"banana" / "prayer" / "push" / "work"</param>
        public Texture2D GetThemeTexture(string theme)
        {
            Texture2D[] pool = theme switch
            {
                "banana" => bananaTextures,
                "prayer" => prayerTextures,
                "push"   => pushTextures,
                "work"   => workTextures,
                _        => null
            };

            if (pool != null && pool.Length > 0)
                return pool[Random.Range(0, pool.Length)];

            // 降级：主题库为空时退回娱乐内容
            return GetEntertainmentTexture();
        }

        /// <summary>随机取一张私密数据显形图</summary>
        public Texture2D GetPrivateDataTexture()
        {
            if (privateDataTextures != null && privateDataTextures.Length > 0)
                return privateDataTextures[Random.Range(0, privateDataTextures.Length)];
            return null;
        }

        /// <summary>检查某主题的图库是否已填充了素材</summary>
        public bool HasThemeAssets(string theme)
        {
            Texture2D[] pool = theme switch
            {
                "banana" => bananaTextures,
                "prayer" => prayerTextures,
                "push"   => pushTextures,
                "work"   => workTextures,
                _        => null
            };
            return pool != null && pool.Length > 0;
        }
    }
}
