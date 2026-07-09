using UnityEngine;

public class InfiniteCommunity : MonoBehaviour
{
    public GameObject housePrefab; // 拖入你的房子 Prefab
    public int rows = 20; // 行数
    public int cols = 20; // 列数
    public float spacingX = 10f; // 房子之间的 X 轴间距
    public float spacingZ = 10f; // 房子之间的 Z 轴间距

    void Start()
    {
        for (int x = 0; x < rows; x++)
        {
            for (int z = 0; z < cols; z++)
            {
                // 计算位置
                Vector3 pos = new Vector3(x * spacingX, 0, z * spacingZ);
                // 生成房子，并作为脚本所在物体的子物体
                Instantiate(housePrefab, pos, Quaternion.identity, transform);
            }
        }
    }
}