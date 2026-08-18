using UnityEngine;
using UnityEngine.Video;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 媒体资源数据库 (支持图片 Texture2D 与 视频 VideoClip)
    /// 供 Stage 5 流体走廊动态提取与播放
    /// </summary>
    [CreateAssetMenu(fileName = "CardMediaDatabase", menuName = "For_You_stage5/Card Media Database")]
    public class CardMediaDatabase : ScriptableObject
    {
        [Header("Phase 1 — 娱乐图片（TikTok 风，色彩缤纷）")]
        public Texture2D[] entertainmentTextures;

        [Header("Phase 1 — 娱乐短视频片段 (Video Clips)")]
        [Tooltip("放入 MP4 / VideoClip 素材，Phase 1 会间歇性播放这些视频")]
        public VideoClip[] entertainmentVideos;

        [Header("Phase 3 — 香蕉主题内容")]
        public Texture2D[] bananaTextures;
        public VideoClip[] bananaVideos;

        [Header("Phase 3 — 祈祷/灵性主题内容")]
        public Texture2D[] prayerTextures;
        public VideoClip[] prayerVideos;

        [Header("Phase 3 — 推石头/西西弗斯主题内容")]
        public Texture2D[] pushTextures;
        public VideoClip[] pushVideos;

        [Header("Phase 3 — 工作/打字机主题内容")]
        public Texture2D[] workTextures;
        public VideoClip[] workVideos;

        [Header("Phase 3 — 私密数据显形专用")]
        public Texture2D[] privateDataTextures;

        // 随机获取图片
        public Texture2D GetEntertainmentTexture()
        {
            if (entertainmentTextures == null || entertainmentTextures.Length == 0) return null;
            return entertainmentTextures[Random.Range(0, entertainmentTextures.Length)];
        }

        // 随机获取视频
        public VideoClip GetEntertainmentVideo()
        {
            if (entertainmentVideos == null || entertainmentVideos.Length == 0) return null;
            return entertainmentVideos[Random.Range(0, entertainmentVideos.Length)];
        }

        public Texture2D GetThemeTexture(string theme)
        {
            Texture2D[] targetPool = entertainmentTextures;

            switch (theme.ToLower())
            {
                case "banana":
                    if (bananaTextures != null && bananaTextures.Length > 0) targetPool = bananaTextures;
                    break;
                case "prayer":
                    if (prayerTextures != null && prayerTextures.Length > 0) targetPool = prayerTextures;
                    break;
                case "push":
                    if (pushTextures != null && pushTextures.Length > 0) targetPool = pushTextures;
                    break;
                case "work":
                    if (workTextures != null && workTextures.Length > 0) targetPool = workTextures;
                    break;
            }

            if (targetPool == null || targetPool.Length == 0) return GetEntertainmentTexture();
            return targetPool[Random.Range(0, targetPool.Length)];
        }

        public VideoClip GetThemeVideo(string theme)
        {
            VideoClip[] targetPool = entertainmentVideos;

            switch (theme.ToLower())
            {
                case "banana":
                    if (bananaVideos != null && bananaVideos.Length > 0) targetPool = bananaVideos;
                    break;
                case "prayer":
                    if (prayerVideos != null && prayerVideos.Length > 0) targetPool = prayerVideos;
                    break;
                case "push":
                    if (pushVideos != null && pushVideos.Length > 0) targetPool = pushVideos;
                    break;
                case "work":
                    if (workVideos != null && workVideos.Length > 0) targetPool = workVideos;
                    break;
            }

            if (targetPool == null || targetPool.Length == 0) return GetEntertainmentVideo();
            return targetPool[Random.Range(0, targetPool.Length)];
        }

        public Texture2D GetPrivateDataTexture()
        {
            if (privateDataTextures == null || privateDataTextures.Length == 0) return null;
            return privateDataTextures[Random.Range(0, privateDataTextures.Length)];
        }
    }
}
