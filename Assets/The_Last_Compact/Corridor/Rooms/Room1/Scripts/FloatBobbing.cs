using UnityEngine;

/// <summary>
/// 增强版漂浮组件：
/// 支持在三维轴上自定义幅度、速度进行平移和旋转。
/// 挂在任意 GameObject 上即可，自带相位错开。
/// </summary>
public class FloatBobbing : MonoBehaviour
{
    [Header("平移设置 (Translation)")]
    [Tooltip("XYZ方向的位移幅度，如 (0, 0.3, 0) 为仅上下浮动")]
    public Vector3 posAmplitude = new Vector3(0f, 0.3f, 0f);
    [Tooltip("XYZ方向的位移速度，如 (1, 1.5, 1)")]
    public Vector3 posSpeed = new Vector3(1f, 1.5f, 1f);

    [Header("旋转设置 (Rotation)")]
    [Tooltip("围绕XYZ轴的旋转幅度（度数），如 (5, 15, 5)")]
    public Vector3 rotAmplitude = new Vector3(2f, 5f, 2f);
    [Tooltip("围绕XYZ轴的旋转速度")]
    public Vector3 rotSpeed = new Vector3(0.5f, 0.8f, 0.5f);

    [Header("全局设置")]
    [Tooltip("每个物体错开相位避免同步（不用改，脚本会自动计算）")]
    public float phaseOffset = 0f;

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
        
        // 用物体的 InstanceID 自动错开相位，避免千篇一律的同步
        phaseOffset = (GetInstanceID() % 100) * 0.1f;
    }

    void Update()
    {
        float t = Time.time;

        // --- 平移计算 ---
        // 将原版的单轴平移拓展为多轴分别计算
        float px = Mathf.Sin(t * posSpeed.x + phaseOffset) * posAmplitude.x;
        float py = Mathf.Sin(t * posSpeed.y + phaseOffset) * posAmplitude.y;
        float pz = Mathf.Sin(t * posSpeed.z + phaseOffset) * posAmplitude.z;
        
        transform.localPosition = startPos + new Vector3(px, py, pz);

        // --- 旋转计算 ---
        // 添加略微错开一点相位的微小旋转
        float rx = Mathf.Sin(t * rotSpeed.x + phaseOffset * 1.5f) * rotAmplitude.x;
        float ry = Mathf.Sin(t * rotSpeed.y + phaseOffset * 1.1f) * rotAmplitude.y;
        float rz = Mathf.Sin(t * rotSpeed.z + phaseOffset * 0.8f) * rotAmplitude.z;
        
        // 将产生的欧拉角增量加在原始旋转上
        transform.localRotation = startRot * Quaternion.Euler(rx, ry, rz);
    }
}
