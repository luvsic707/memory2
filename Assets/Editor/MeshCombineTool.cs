#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 编辑器工具：把选中的所有相同材质的 MeshRenderer 合并成一个网格。
/// 用法：在 Hierarchy 里选中 Houses 根节点 → 菜单 Tools → Combine Meshes
/// 合并后生成 CombinedMesh.asset 并在场景中替换原来的物体。
/// </summary>
public class MeshCombineTool : EditorWindow
{
    [MenuItem("Tools/Combine Selected Meshes into One")]
    static void CombineSelected()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "请先在 Hierarchy 里选中要合并的父物体（Houses 根节点）", "OK");
            return;
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(false);
        if (filters.Length == 0)
        {
            EditorUtility.DisplayDialog("Combine Meshes", "选中物体内没有找到任何 MeshFilter。", "OK");
            return;
        }

        // 按材质分组
        Dictionary<Material, List<CombineInstance>> groups = new();

        foreach (MeshFilter mf in filters)
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;

            Material mat = mr.sharedMaterial;
            if (!groups.ContainsKey(mat)) groups[mat] = new List<CombineInstance>();

            groups[mat].Add(new CombineInstance
            {
                mesh      = mf.sharedMesh,
                transform = mf.transform.localToWorldMatrix
            });
        }

        // 确保保存目录
        string saveDir = "Assets/Corridor_room1/CombinedMeshes";
        if (!System.IO.Directory.Exists(Application.dataPath + "/Corridor_room1/CombinedMeshes"))
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Corridor_room1/CombinedMeshes");

        // 创建合并后的父物体
        GameObject combinedParent = new GameObject(root.name + "_Combined");
        combinedParent.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(combinedParent, "Combine Meshes");

        int groupIdx = 0;
        foreach (var kvp in groups)
        {
            Mesh combined = new Mesh();
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 支持超过 65535 个顶点
            combined.CombineMeshes(kvp.Value.ToArray(), true, true);
            combined.RecalculateBounds();
            combined.RecalculateNormals();

            // 保存 Mesh Asset
            string assetPath = $"{saveDir}/Combined_{kvp.Key?.name ?? "mat"}_{groupIdx}.asset";
            AssetDatabase.CreateAsset(combined, assetPath);

            // 创建新 GameObject
            GameObject go = new GameObject($"CombinedGroup_{groupIdx}");
            go.transform.SetParent(combinedParent.transform, false);
            go.isStatic = true;

            MeshFilter newMf = go.AddComponent<MeshFilter>();
            newMf.sharedMesh = combined;

            MeshRenderer newMr = go.AddComponent<MeshRenderer>();
            newMr.sharedMaterial = kvp.Key;

            groupIdx++;
        }

        AssetDatabase.SaveAssets();

        // 记录原物体（不自动删除，让用户确认后手动删）
        Debug.Log($"[MeshCombine] 完成！合并了 {filters.Length} 个 mesh，生成 {groupIdx} 个材质组。" +
                  $"\n合并结果：'{combinedParent.name}'。确认没问题后，手动删除原来的 '{root.name}'。");

        Selection.activeGameObject = combinedParent;
        EditorUtility.DisplayDialog("完成！",
            $"合并完成！\n" +
            $"原始物体数量：{filters.Length}\n" +
            $"合并后材质组：{groupIdx}\n\n" +
            $"合并的 GameObject 叫 '{combinedParent.name}'。\n" +
            $"确认效果没问题后，手动删除原来的 '{root.name}'。",
            "OK");
    }
}
#endif
