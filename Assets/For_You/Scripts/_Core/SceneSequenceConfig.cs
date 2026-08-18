using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    /// <summary>
    /// 配置文件：存放支线 "For You" 的场景顺序。
    /// 在 Unity 编辑器中创建此 ScriptableObject 并填入场景名称（如 "Scene1", "Scene2" ... "Scene6"），
    /// 通过它实现完全 data‑driven、event‑driven 的场景切换。
    /// </summary>
    [CreateAssetMenu(fileName = "ForYouSceneSequence", menuName = "Wakeup/ForYou Scene Sequence", order = 10)]
    public class SceneSequenceConfig : ScriptableObject
    {
        /// <summary>
        /// 按顺序排列的场景名称（必须与 Build Settings 中的场景名称一致）。
        /// </summary>
        public List<string> sceneNames = new List<string>();
    }
}
