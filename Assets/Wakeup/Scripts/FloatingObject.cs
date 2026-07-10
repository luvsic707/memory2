using UnityEngine;

/// <summary>
/// 一个足够独立的脚本，用于控制物体进行缓和的上下浮动和自转动画。
/// 非常适合用在拾取物、手机模型或重点交互道具上。
/// </summary>
public class FloatingObject : MonoBehaviour
{
    [Header("上下浮动")]
    [Tooltip("浮动幅度（米）")]
    public float floatAmplitude = 0.15f;

    [Tooltip("浮动快慢/速度")]
    public float floatSpeed = 1f;

    [Header("自转")]
    [Tooltip("每秒旋转多少度")]
    public float rotateSpeed = 10f;

    [Tooltip("绕哪个轴进行自转")]
    public Vector3 rotateAxis = new Vector3(0, 1, 0);

    private Vector3 startPos;

    void Start()
    {
        // 记录物体的初始本地位置，避免累积误差
        startPos = transform.localPosition;
    }

    void Update()
    {
        // 1. 上下浮动（基于正弦波 Sin）
        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.localPosition = startPos + new Vector3(0, offsetY, 0);

        // 2. 缓慢自转
        transform.Rotate(rotateAxis * rotateSpeed * Time.deltaTime, Space.Self);
    }
}
