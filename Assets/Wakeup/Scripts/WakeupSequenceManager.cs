using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using TMPro;
using TheLastCompact.Core;

namespace TheLastCompact.Wakeup
{
    public class WakeupSequenceManager : MonoBehaviour
    {
        [Header("References")]
        public WakeupDialogueUI dialogueUI;
        public WakeupDialogueData dialogueData;
        public PlayableDirector timeline;

        [Header("Debug / Sequence Toggles")]
        [Tooltip("是否启用开场睁眼动画效果（解耦版本）")]
        public bool useEyeOpeningEffect = true;

        [Tooltip("是否跳过开场对话（直接进入 Timeline 或自由探索）")]
        public bool skipDialogue = true;
        
        [Tooltip("是否跳过 Timeline 切镜（直接进入自由探索）")]
        public bool skipTimeline = true;

        [Tooltip("是否允许点击屏幕任意位置直接推进对话（忽略选择支按钮）")]
        public bool clickAnywhereToAdvance = true;

        private int currentNodeIndex = 0;
        private bool isWaitingForInput = false;
        private AudioSource audioSource; // 音频组件

        private void Awake()
        {
            // 防御机制：检测并修正可能错误的 Prefab 引用，自动寻找场景中的 Dialogue UI 实例
#if UNITY_2023_1_OR_NEWER
            if (dialogueUI == null || !dialogueUI.gameObject.scene.IsValid())
            {
                dialogueUI = FindAnyObjectByType<WakeupDialogueUI>(FindObjectsInactive.Include);
                if (dialogueUI != null)
                {
                    Debug.Log($"[WakeupSequence] 成功自动从场景中寻找到并重新绑定 Dialogue UI 实例: {dialogueUI.name}");
                }
            }
#else
            if (dialogueUI == null || !dialogueUI.gameObject.scene.IsValid())
            {
                dialogueUI = FindObjectOfType<WakeupDialogueUI>(true);
                if (dialogueUI != null)
                {
                    Debug.Log($"[WakeupSequence] 成功自动从场景中寻找到并重新绑定 Dialogue UI 实例: {dialogueUI.name}");
                }
            }
#endif
        }

        private void Start()
        {
            // 初始化音频组件
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            // 确保 Global Managers 存在 (Bootstrap 检查)
            if (GlobalMentalState.Instance == null)
            {
                Debug.LogWarning("GlobalMentalState missing! Please start from Bootstrap scene.");
                // 测试用：可能需要生成一个假的或直接返回
            }

            // 锁定光标
            CursorService.Unlock();

            StartCoroutine(StartSequence());
        }

        private IEnumerator StartSequence()
        {
            Debug.Log("[WakeupSequence] 开始序列...");

            // 1. 优先执行由事件驱动的睁眼效果
            if (useEyeOpeningEffect)
            {
                bool isEffectDone = false;
                System.Action<string> onSceneEvent = null;
                onSceneEvent = (evtId) =>
                {
                    if (evtId == "EyeOpeningComplete")
                    {
                        isEffectDone = true;
                    }
                };

                // 订阅全局事件总线
                NarrationAnnouncer.OnSceneEventTriggered += onSceneEvent;

                Debug.Log("[WakeupSequence] 广播事件: StartEyeOpening，触发外部睁眼组件进行处理...");
                NarrationAnnouncer.TriggerSceneEvent("StartEyeOpening");

                // 等待完成，加个超时防御（防止场景中缺失响应器或组件导致流程无限卡死）
                float timer = 0f;
                float timeout = 10f; // 10秒超时降级
                while (!isEffectDone && timer < timeout)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }

                // 取消订阅
                NarrationAnnouncer.OnSceneEventTriggered -= onSceneEvent;

                if (timer >= timeout)
                {
                    Debug.LogWarning("[WakeupSequence] 睁眼效果等待超时！自动优雅降级，直接开始主流程。");
                }
            }
            else
            {
                // 如果不使用睁眼效果，则使用原版的初始黑屏等待
                yield return new WaitForSeconds(1.0f);
            }

            // 2. 检查是否跳过开场对话
            if (skipDialogue)
            {
                Debug.Log("[WakeupSequence] skipDialogue 已启用，正在跳过对话步骤...");
                EndSequence();
                yield break;
            }

            // 确保对话 UI 处于活动状态，防止因为之前被禁用而导致无法启动协程
            if (dialogueUI != null)
            {
                Debug.Log($"[WakeupSequence] dialogueUI 路径: {GetGameObjectPath(dialogueUI.gameObject)}, activeSelf: {dialogueUI.gameObject.activeSelf}, activeInHierarchy: {dialogueUI.gameObject.activeInHierarchy}");
                dialogueUI.gameObject.SetActive(true);
                
                // 递归激活所有父级，如果父级被禁用了，SetActive(true) 对子级无效！
                Transform parent = dialogueUI.transform.parent;
                while (parent != null)
                {
                    if (!parent.gameObject.activeSelf)
                    {
                        Debug.LogWarning($"[WakeupSequence] 发现父节点被禁用了，自动激活父节点: {GetGameObjectPath(parent.gameObject)}");
                        parent.gameObject.SetActive(true);
                    }
                    parent = parent.parent;
                }

                Debug.Log($"[WakeupSequence] 激活操作后 - activeSelf: {dialogueUI.gameObject.activeSelf}, activeInHierarchy: {dialogueUI.gameObject.activeInHierarchy}");
            }

            // 在淡入之前，提前把第一句话的文字塞进去，防止淡入时看到默认的假文本闪烁！
            if (dialogueData != null && dialogueData.nodes.Count > 0 && dialogueUI != null)
            {
                dialogueUI.ShowLine(dialogueData.nodes[0].speakerName, dialogueData.nodes[0].dialogueText);
            }

            // UI 淡入
            if (dialogueUI != null) dialogueUI.FadeIn(1.0f);
            yield return new WaitForSeconds(1.0f);

            // 开始对话（正式播放语音和开启交互）
            if (dialogueData != null && dialogueData.nodes.Count > 0)
            {
                Debug.Log($"[WakeupSequence] 开始对话，节点数: {dialogueData.nodes.Count}");
                ShowNode(0);
            }
            else
            {
                Debug.LogWarning("[WakeupSequence] 没有分配 Dialogue Data！直接结束序列。");
                EndSequence();
            }
        }

        private void ShowNode(int index)
        {
            Debug.Log($"[WakeupSequence] 显示节点索引: {index}");
            if (index < 0 || index >= dialogueData.nodes.Count)
            {
                Debug.LogWarning("[WakeupSequence] 索引越界，结束序列。");
                EndSequence();
                return;
            }

            currentNodeIndex = index;
            DialogueNode node = dialogueData.nodes[index];

            // 播放配音
            if (audioSource != null)
            {
                audioSource.Stop(); // 停止上一句
                if (node.voiceover != null)
                {
                    audioSource.PlayOneShot(node.voiceover);
                }
            }

            // 先显示文本（即使是结束节点也要显示）
            dialogueUI.ShowLine(node.speakerName, node.dialogueText); 

            if (clickAnywhereToAdvance)
            {
                isWaitingForInput = true;
                dialogueUI.ShowChoices(new string[0]); // 隐藏选项按钮，允许任意点击
            }
            else if (node.choices != null && node.choices.Count > 0)
            {
                string[] choiceTexts = new string[node.choices.Count];
                for (int i = 0; i < node.choices.Count; i++)
                {
                    choiceTexts[i] = node.choices[i].choiceText;
                }
                
                dialogueUI.ShowChoices(choiceTexts);
                // 重要：订阅有效的选择
                dialogueUI.OnChoiceSelected = (idx) => OnChoiceSelected(idx);
                isWaitingForInput = false;
            }
            else
            {
                // 没有选项，等待点击推进
                isWaitingForInput = true;
                dialogueUI.ShowChoices(new string[0]); // 隐藏选项
            }
        }

        private void Update()
        {
            if (isWaitingForInput && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
            {
                DialogueNode currentNode = dialogueData.nodes[currentNodeIndex];
                
                // 如果当前节点是结束节点，点击后结束序列
                if (currentNode.isEndNode)
                {
                    EndSequence();
                    return;
                }
                
                // 否则线性推进到下一个节点
                if (clickAnywhereToAdvance && currentNode.choices != null && currentNode.choices.Count > 0)
                {
                    // 默认沿首个选项所指向的节点索引跳转
                    ShowNode(currentNode.choices[0].nextNodeIndex);
                }
                else
                {
                    ShowNode(currentNodeIndex + 1);
                }
            }
        }

        private void OnChoiceSelected(int choiceIndex)
        {
            DialogueNode node = dialogueData.nodes[currentNodeIndex];
            if (choiceIndex >= 0 && choiceIndex < node.choices.Count)
            {
                // 如果当前节点是结束节点，选择后结束序列
                if (node.isEndNode)
                {
                    EndSequence();
                    return;
                }
                
                int nextIndex = node.choices[choiceIndex].nextNodeIndex;
                ShowNode(nextIndex);
            }
        }

        private void EndSequence()
        {
            if (audioSource != null) audioSource.Stop(); // 停止说话

            // 2. 对话 UI 淡出（如果是跳过模式则直接禁用物体，防止闪烁和残留）
            if (dialogueUI != null)
            {
                if (skipDialogue)
                {
                    dialogueUI.gameObject.SetActive(false);
                }
                else
                {
                    dialogueUI.FadeOut(1.0f);
                }
            }

            // 3. 启动 Timeline
            if (!skipTimeline && timeline != null)
            {
                timeline.Play();
                // Timeline 应该通过信号发射器或监听其结束的脚本来处理最终过渡。
                // 但现在，我们可以使用协程来等待它。
                StartCoroutine(WaitForTimelineAndExploration(timeline.duration));
            }
            else
            {
                if (skipDialogue)
                {
                    if (timeline == null)
                        Debug.LogWarning("No Timeline assigned! Playing Ape Prologue immediately.");
                    else
                        Debug.Log("[WakeupSequence] skipTimeline 已启用，跳过 Timeline，播放猩猩低吼字幕后解锁探索模式。");
                    
                    // 播放猩猩低吼和字幕翻译，播完后解锁探索
                    StartCoroutine(PlayApePrologueAndThenExploration());
                }
                else
                {
                    Debug.Log("[WakeupSequence] skipTimeline 已启用。由于正式对话已播放完毕，直接解锁探索模式。");
                    // 开启探索模式（只解锁玩家移动，不开启 Mental UI）
                    StartExploration();
                }
            }
        }

        private IEnumerator WaitForTimelineAndExploration(double duration)
        {
            yield return new WaitForSeconds((float)duration);
            StartExploration();
        }

        private IEnumerator PlayApePrologueAndThenExploration()
        {
            bool isPrologueDone = false;
            System.Action<string> onSceneEvent = null;
            onSceneEvent = (evtId) =>
            {
                if (evtId == "ApePrologueComplete")
                {
                    isPrologueDone = true;
                }
            };

            // 订阅全局总线事件，监听大猩猩台词播放完毕
            NarrationAnnouncer.OnSceneEventTriggered += onSceneEvent;

            Debug.Log("[WakeupSequence] 广播事件: StartApePrologue，等待大猩猩发表感言...");
            NarrationAnnouncer.TriggerSceneEvent("StartApePrologue");

            // 超时防御：设置 15 秒超时，防止没有放置响应器而永远卡死
            float timer = 0f;
            float timeout = 15f;
            while (!isPrologueDone && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 取消订阅
            NarrationAnnouncer.OnSceneEventTriggered -= onSceneEvent;

            if (timer >= timeout)
            {
                Debug.LogWarning("[WakeupSequence] 大猩猩感言及字幕播放超时！优雅降级直接解锁探索模式。");
            }

            // 进入自由探索模式
            StartExploration();
        }

        private void StartExploration()
        {
            Debug.Log("[Wakeup] 进入自由探索模式。等待玩家阅读信件。");
            
            // 找到玩家并解锁控制
            // 注意：因为场景里可能没有 UniversalPlayer (如果直接从 Wakeup 启动且没有生成)，
            // 但正常流程下应该有。
            UniversalPlayer player = FindAnyObjectByType<UniversalPlayer>();
            if (player != null)
            {
                player.EnableControl();
                
                // 确保鼠标锁定 (以便 FPS 控制)
                CursorService.Lock();
            }
            else
            {
                Debug.LogError("找不到 UniversalPlayer！无法进入探索模式。");
            }

            // 注意：此时不要调用 GlobalUIManager.EnableGameplay()
            // 那个由 WakeupLetter 触发
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform t = obj.transform;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
