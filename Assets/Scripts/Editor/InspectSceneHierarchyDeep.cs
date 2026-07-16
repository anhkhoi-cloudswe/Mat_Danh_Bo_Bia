using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectSceneHierarchyDeep
{
    [MenuItem("Tools/Inspect Scene Hierarchy Deep")]
    public static void Run()
    {
        Debug.Log("[InspectSceneHierarchyDeep] Running deep hierarchy scan...");
        var town = GameObject.Find("Vietnamese_Old_Town_Instance");
        if (town == null)
        {
            Debug.LogWarning("Không tìm thấy Vietnamese_Old_Town_Instance nhưng bỏ qua để tránh sập luồng!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Vietnamese_Old_Town_Instance Deep Hierarchy ===");
        PrintHierarchy(town.transform, 0, sb);

        string path = Path.Combine(Directory.GetCurrentDirectory(), "deep_hierarchy.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectSceneHierarchyDeep] Done. Saved to: " + path);
    }

    private static void PrintHierarchy(Transform t, int indent, StringBuilder sb)
    {
        string indentStr = new string(' ', indent * 2);
        sb.AppendLine($"{indentStr}- GameObject: '{t.name}' (ActiveSelf={t.gameObject.activeSelf}, LocalPos={t.localPosition.ToString("F3")}, LocalRot={t.localRotation.eulerAngles.ToString("F1")}, Scale={t.localScale.ToString("F3")})");
        for (int i = 0; i < t.childCount; i++)
        {
            PrintHierarchy(t.GetChild(i), indent + 1, sb);
        }
    }
}
