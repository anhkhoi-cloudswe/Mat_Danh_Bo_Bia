using UnityEditor;
using UnityEngine;

public class InspectMiuLePrefab
{
    [MenuItem("Tools/Inspect Miu Le Prefab")]
    public static void Inspect()
    {
        Debug.Log("=== Inspecting Miu Le Prefab ===");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Miu Le.prefab");
        if (prefab == null)
        {
            Debug.LogError("Miu Le prefab not found at Assets/Prefabs/Miu Le.prefab");
            return;
        }

        PrintTransforms(prefab.transform, "");
        Debug.Log("=== Inspection Finished ===");
    }

    private static void PrintTransforms(Transform t, string indent)
    {
        SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        string rendererInfo = "";
        if (smr != null) rendererInfo += $" | SkinnedMeshRenderer (bounds center: {smr.localBounds.center}, extents: {smr.localBounds.extents})";
        if (mr != null) rendererInfo += $" | MeshRenderer (bounds center: {mr.bounds.center - t.position})";

        Debug.Log($"{indent}Name: {t.name} | LocalPos: {t.localPosition} | LocalScale: {t.localScale}{rendererInfo}");
        for (int i = 0; i < t.childCount; i++)
        {
            PrintTransforms(t.GetChild(i), indent + "  ");
        }
    }
}
