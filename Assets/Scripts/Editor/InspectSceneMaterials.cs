using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectSceneMaterials
{
    [MenuItem("Tools/Inspect Scene Materials")]
    public static void Run()
    {
        Debug.Log("[InspectSceneMaterials] Inspecting backing materials...");
        var town = GameObject.Find("Vietnamese_Old_Town_Instance");
        if (town == null)
        {
            Debug.LogWarning("Không tìm thấy Vietnamese_Old_Town_Instance nhưng bỏ qua để tránh sập luồng!");
            return;
        }

        var left = town.transform.Find("LeftFacadeBacking");
        var right = town.transform.Find("RightFacadeBacking");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Scene Backing Materials Inspection ===");

        InspectRenderer(left, "Left", sb);
        InspectRenderer(right, "Right", sb);

        string path = Path.Combine(Directory.GetCurrentDirectory(), "scene_materials_inspection.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectSceneMaterials] Done. Saved to: " + path);
    }

    private static void InspectRenderer(Transform t, string label, StringBuilder sb)
    {
        if (t == null)
        {
            sb.AppendLine($"{label} Backing Transform not found!");
            return;
        }

        sb.AppendLine($"{label} Backing '{t.name}':");
        var mr = t.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            sb.AppendLine("  No MeshRenderer found!");
            return;
        }

        var mat = mr.sharedMaterial;
        if (mat == null)
        {
            sb.AppendLine("  Material is null!");
            return;
        }

        sb.AppendLine($"  Material Name: {mat.name}");
        sb.AppendLine($"  Shader Name: {mat.shader.name}");
        if (mat.HasProperty("_Color")) sb.AppendLine($"  Color: {mat.color}");
        if (mat.HasProperty("_BaseColor")) sb.AppendLine($"  BaseColor: {mat.GetColor("_BaseColor")}");
        if (mat.mainTexture != null) sb.AppendLine($"  Texture: {mat.mainTexture.name}");
        else sb.AppendLine("  Texture: None");

        // Print rendering queue and other details
        sb.AppendLine($"  Render Queue: {mat.renderQueue}");
        sb.AppendLine($"  Shader Keywords: {string.Join(", ", mat.shaderKeywords)}");
    }
}
