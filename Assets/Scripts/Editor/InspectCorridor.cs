using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectCorridor
{
    [MenuItem("Tools/Inspect Corridor")]
    public static void Run()
    {
        Debug.Log("[InspectCorridor] Scanning walls...");
        var container = GameObject.Find("Sketch_Layout_Walls_Trees");
        if (container == null)
        {
            Debug.LogError("Sketch_Layout_Walls_Trees not found!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Walls in Sketch_Layout_Walls_Trees ===");
        
        for (int i = 0; i < container.transform.childCount; i++)
        {
            var child = container.transform.GetChild(i);
            sb.AppendLine($"- GameObject: '{child.name}'");
            sb.AppendLine($"  Active: {child.gameObject.activeSelf}");
            sb.AppendLine($"  Position: {child.position.ToString("F3")}");
            sb.AppendLine($"  Rotation: {child.rotation.eulerAngles.ToString("F3")}");
            sb.AppendLine($"  LocalScale: {child.localScale.ToString("F3")}");
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                sb.AppendLine($"  Bounds: Min={mr.bounds.min.ToString("F3")}, Max={mr.bounds.max.ToString("F3")}");
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "corridor_info.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectCorridor] Done. Saved to: " + path);
    }
}
