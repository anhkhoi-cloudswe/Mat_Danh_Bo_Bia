using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class AnalyzeBaoScene
{
    [MenuItem("Tools/Analyze BaoScene")]
    public static void Run()
    {
        Debug.Log("[AnalyzeBaoScene] Running analysis...");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== BaoScene Root Objects ===");

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sb.AppendLine($"Active Scene: {activeScene.name} (Path: {activeScene.path})");

        var rootObjects = activeScene.GetRootGameObjects();
        sb.AppendLine($"Total Root GameObjects: {rootObjects.Length}");
        foreach (var go in rootObjects)
        {
            sb.AppendLine($"- Root GameObject: '{go.name}'");
            sb.AppendLine($"  Active (Self/Hierarchy): {go.activeSelf} / {go.activeInHierarchy}");
            sb.AppendLine($"  Position: {go.transform.position.ToString("F3")}");
            sb.AppendLine($"  Rotation (Euler): {go.transform.rotation.eulerAngles.ToString("F3")}");
            sb.AppendLine($"  Scale: {go.transform.localScale.ToString("F6")}");
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "baoscene_roots.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[AnalyzeBaoScene] Done. Saved to: " + path);
    }
}
