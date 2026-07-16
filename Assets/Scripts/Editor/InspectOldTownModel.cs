using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectOldTownModel
{
    [MenuItem("Tools/Inspect Old Town Model")]
    public static void Run()
    {
        Debug.Log("[InspectOldTownModel] Running model inspection...");
        var town = GameObject.Find("Vietnamese_Old_Town_Instance");
        if (town == null)
        {
            Debug.LogWarning("Không tìm thấy Vietnamese_Old_Town_Instance nhưng bỏ qua để tránh sập luồng!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Vietnamese_Old_Town_Instance Model Inspection ===");
        sb.AppendLine($"Position: {town.transform.position.ToString("F3")}");
        sb.AppendLine($"Rotation: {town.transform.rotation.eulerAngles.ToString("F3")}");
        sb.AppendLine($"Scale: {town.transform.localScale.ToString("F6")}");

        var renderers = town.GetComponentsInChildren<MeshRenderer>(true);
        sb.AppendLine($"Total MeshRenderers in model: {renderers.Length}");

        foreach (var mr in renderers)
        {
            var mf = mr.GetComponent<MeshFilter>();
            string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "None";
            sb.AppendLine($"- GameObject: '{mr.name}'");
            sb.AppendLine($"  Path: {GetRelativePath(mr.gameObject, town)}");
            sb.AppendLine($"  Active (Self/Hierarchy): {mr.gameObject.activeSelf} / {mr.gameObject.activeInHierarchy}");
            sb.AppendLine($"  LocalPos: {mr.transform.localPosition.ToString("F3")}");
            sb.AppendLine($"  LocalRot: {mr.transform.localRotation.eulerAngles.ToString("F3")}");
            sb.AppendLine($"  LocalScale: {mr.transform.localScale.ToString("F3")}");
            sb.AppendLine($"  Mesh: {meshName}");
            if (mr.sharedMaterial != null)
            {
                sb.AppendLine($"  Material: '{mr.sharedMaterial.name}' (Shader: '{mr.sharedMaterial.shader.name}')");
            }
            else
            {
                sb.AppendLine("  Material: None");
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "old_town_model_inspection.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectOldTownModel] Done. Saved to: " + path);
    }

    private static string GetRelativePath(GameObject obj, GameObject root)
    {
        if (obj == root) return "";
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null && parent.gameObject != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
