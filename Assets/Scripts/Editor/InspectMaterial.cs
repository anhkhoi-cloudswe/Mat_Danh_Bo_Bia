using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectMaterial
{
    [MenuItem("Tools/Inspect Material")]
    [InitializeOnLoadMethod]
    public static void Run()
    {
        Debug.Log("[InspectMaterial] Running material inspection...");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FacadeBackingMaterial.mat");
        if (mat == null)
        {
            Debug.LogError("Assets/FacadeBackingMaterial.mat not found!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Material Inspection: FacadeBackingMaterial ===");
        sb.AppendLine($"Shader: {mat.shader.name}");
        if (mat.HasProperty("_Color")) sb.AppendLine($"Color: {mat.color}");
        if (mat.HasProperty("_BaseColor")) sb.AppendLine($"BaseColor: {mat.GetColor("_BaseColor")}");
        if (mat.mainTexture != null) sb.AppendLine($"Texture: {mat.mainTexture.name}");
        else sb.AppendLine("Texture: None");

        // Print keywords and properties
        string[] keywords = mat.shaderKeywords;
        sb.AppendLine("Keywords: " + string.Join(", ", keywords));

        string path = Path.Combine(Directory.GetCurrentDirectory(), "material_inspection.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectMaterial] Done. Saved to: " + path);
    }
}
