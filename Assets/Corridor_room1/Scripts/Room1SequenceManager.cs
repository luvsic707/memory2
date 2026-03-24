using System.Collections;
using UnityEngine;

/// <summary>
/// Corridor Room 1 专属定序器 (Director)
/// 负责处理精确的时间轴：包括基于固定时间，以及基于特定语音结束后的延迟触发。
/// 完全解耦：不直接引用 Shader 控制器等具体组件，全部依赖事件广播！
/// </summary>
public class Room1SequenceManager : MonoBehaviour
{
    [Header("场景节点引用")]
    [Tooltip("第一幕节点 (Act One)")]
    public GameObject actOneRoot;
    
    [Tooltip("第二幕节点 (Act Two)")]
    public GameObject actTwoRoot;
    
    [Tooltip("最终幕节点 (Act Final)")]
    public GameObject actFinalRoot;

    [Header("声音配置 - 组名 Group ID (用于触发播放)")]
    [Tooltip("你数据库里的大类名称，比如 Act_One")]
    public string actOneGroupId = "Act_One";
    public string actTwoGroupId = "Act_Two";
    public string actThreeGroupId = "Act_Three";

    [Header("声音配置 - 音频 Entry ID (用于监听播放结束)")]
    [Tooltip("你数据库里具体的 Id 名称，比如 act_one_voice")]
    public string actOneAudioId = "act_one_voice";
    public string actTwoAudioId = "act_two_voice";
    public string actThreeAudioId = "act_three_voice";

    [Header("时间设置 (手动调整节奏)")]
    public float delayBeforeActTwo = 5f;
    public float delayBeforeAudio3 = 10f;
    [Tooltip("Audio 3 播放到一半的时间（秒），此时发送 StartMelt 广播")]
    public float audio3HalfTime = 5f; 
    [Tooltip("Shader开始运作后，需要等几秒跳转最后阶段 Act Final")]
    public float delayBeforeActFinal = 15f;

    private bool _isWaitingForAudio = false;

    void Start()
    {
        NarrationAnnouncer.OnNarrationEnded += HandleNarrationEnded;
        StartCoroutine(RunSequence());
    }

    void OnDestroy()
    {
        NarrationAnnouncer.OnNarrationEnded -= HandleNarrationEnded;
    }

    private void HandleNarrationEnded(string entryId)
    {
        if (entryId == actOneAudioId || entryId == actTwoAudioId || entryId == actThreeAudioId)
        {
            _isWaitingForAudio = false;
        }
    }

    private IEnumerator RunSequence()
    {
        // 0. 初始状态
        if (actOneRoot != null) actOneRoot.SetActive(true);
        if (actTwoRoot != null) actTwoRoot.SetActive(false);
        if (actFinalRoot != null) actFinalRoot.SetActive(false);

        // 1. 发出通告，开始播放第一段语音 (Act One)
        Debug.Log($"[Director] 广播: 播放旁白组 {actOneGroupId}");
        _isWaitingForAudio = true;
        NarrationAnnouncer.Announce(actOneGroupId); 
        
        // 2. 挂起等待...
        yield return new WaitWhile(() => _isWaitingForAudio);

        // 3. 第一段播放完毕后的 5 秒之后，切换 Act
        yield return new WaitForSeconds(delayBeforeActTwo);
        
        if (actOneRoot != null) actOneRoot.SetActive(false);
        if (actTwoRoot != null) actTwoRoot.SetActive(true);

        // 4. 切到 Act Two 后一开始就要播 audio 2
        Debug.Log($"[Director] 广播: 播放旁白组 {actTwoGroupId}");
        _isWaitingForAudio = true;
        NarrationAnnouncer.Announce(actTwoGroupId);

        // 5. 挂起等待...
        yield return new WaitWhile(() => _isWaitingForAudio);

        // 6. 结束了等个 10 秒，开始播放 audio 3
        yield return new WaitForSeconds(delayBeforeAudio3);

        Debug.Log($"[Director] 广播: 播放旁白组 {actThreeGroupId}");
        NarrationAnnouncer.Announce(actThreeGroupId);

        // 7. audio3 播放到一半之后（默认 5 秒，全看面板设置）开始 shader 运作
        yield return new WaitForSeconds(audio3HalfTime); 
        Debug.Log("[Director] 广播开始运作 Shader (StartMelt) ！");
        NarrationAnnouncer.TriggerSceneEvent("StartMelt");

        // 8. 运作之后 15 秒（默认），跳转 act final
        yield return new WaitForSeconds(delayBeforeActFinal);
        Debug.Log("[Director] 激活 Act Final");
        if (actTwoRoot != null) actTwoRoot.SetActive(false);
        if (actFinalRoot != null) actFinalRoot.SetActive(true);
    }
}
