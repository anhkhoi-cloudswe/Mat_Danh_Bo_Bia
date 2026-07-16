using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectSceneObjects
{
    [MenuItem("Tools/Inspect Scene Objects")]
    [InitializeOnLoadMethod]
    public static void Run()
    {
        Debug.Log("[InspectSceneObjects] Scanning banner objects...");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Banner Objects in Scene ===");

        var renderers = GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var mr in renderers)
        {
            bool isBanner = false;
            if (mr.sharedMaterial != null)
            {
                string matName = mr.sharedMaterial.name.ToLower();
                if (matName.Contains("bangquancao") || matName.Contains("poster") || mr.name.ToLower().Contains("banner") || mr.name.ToLower().Contains("quancao"))
                {
                    isBanner = true;
                }
            }

            if (isBanner)
            {
                count++;
                sb.AppendLine($"- Banner #{count}: '{mr.name}'");
                sb.AppendLine($"  Path: {GetFullPath(mr.gameObject)}");
                sb.AppendLine($"  Active Self/Hierarchy: {mr.gameObject.activeSelf} / {mr.gameObject.activeInHierarchy}");
                sb.AppendLine($"  World Pos: {mr.transform.position.ToString("F3")}");
                sb.AppendLine($"  World Rot: {mr.transform.rotation.eulerAngles.ToString("F1")}");
                sb.AppendLine($"  Local Pos: {mr.transform.localPosition.ToString("F3")}");
                sb.AppendLine($"  Local Rot: {mr.transform.localRotation.eulerAngles.ToString("F1")}");
                sb.AppendLine($"  Scale: {mr.transform.lossyScale.ToString("F3")}");
                sb.AppendLine($"  Material: '{(mr.sharedMaterial != null ? mr.sharedMaterial.name : "null")}'");
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "scene_banners_info.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectSceneObjects] Done. Saved to: " + path);
    }

    private static string GetFullPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

}
