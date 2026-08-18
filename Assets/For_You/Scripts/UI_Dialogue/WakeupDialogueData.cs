using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastCompact.Wakeup
{
    [System.Serializable]
    public class DialogueNode
    {
        public string speakerName;
        [TextArea(3, 10)]
        public string dialogueText;
        public List<DialogueChoice> choices;
        public bool isEndNode;
        public float delayBeforeNext;
        public AudioClip voiceover; // 配音片段
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string choiceText;
        public int nextNodeIndex; // 简单的基于索引的导航
    }

    [CreateAssetMenu(fileName = "NewWakeupDialogue", menuName = "Wakeup/DialogueData")]
    public class WakeupDialogueData : ScriptableObject
    {
        public List<DialogueNode> nodes;
    }
}
