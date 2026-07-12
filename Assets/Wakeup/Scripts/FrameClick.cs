using System.Collections;
using UnityEngine;

/// <summary>
/// 逐帧动画交互组件 (解耦版本，配合 Q 键射线交互系统使用)
/// 玩家对准该物体按下 Q 键时，即可播放或停止逐帧动画。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class FrameClick : MonoBehaviour, IInteractable
{
    [Header("帧图序列")]
    [Tooltip("把多张帧图按顺序拖入此处")]
    public Texture[] frames;

    [Header("播放设置")]
    [Tooltip("每帧停留时间（秒）")]
    public float frameTime = 0.25f;

    [Tooltip("是否循环播放")]
    public bool loopForever = true;

    [Header("交互提示")]
    [Tooltip("准星对准该物体时显示的提示文字")]
    [SerializeField] private string interactHint = "看投影";

    private Renderer rend;
    private bool playing = false;

    // 实现 IInteractable 接口的属性
    public string InteractHint => interactHint;

    void Awake()
    {
        // 自动防御：如果物体上没有 Collider，射线交互将完全无法命中！
        // 我们在运行时自动为其添加 BoxCollider，保证交互正常运作。
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"[FrameClick] 检测到 '{gameObject.name}' 上没有 Collider！已自动添加 BoxCollider，以确保 Q 键射线检测可以正常工作。");
        }
    }

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (frames != null && frames.Length > 0)
        {
            rend.material.mainTexture = frames[0];
        }
    }

    // 实现 IInteractable 接口的方法：当玩家看准该物体并按下 Q 键时触发
    public void Interact()
    {
        if (!playing)
        {
            StartCoroutine(Play());
        }
        else
        {
            StopPlaying();
        }
    }

    private IEnumerator Play()
    {
        playing = true;
        Debug.Log($"[FrameClick] {gameObject.name} 开始播放帧动画。");

        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                // 如果在播放过程中中途被叫停，立即跳出
                if (!playing) break;

                rend.material.mainTexture = frames[i];
                yield return new WaitForSeconds(frameTime);
            }
        }
        while (loopForever && playing);

        playing = false;
    }

    public void StopPlaying()
    {
        StopAllCoroutines();
        playing = false;
        
        if (frames != null && frames.Length > 0)
        {
            rend.material.mainTexture = frames[0]; // 停回第一帧
        }
        Debug.Log($"[FrameClick] {gameObject.name} 已停止播放并重置至第一帧。");
    }
}
