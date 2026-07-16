using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class AnalyzeBackingOverlap
{
    [MenuItem("Tools/Analyze Backing Overlap")]
    public static void Run()
    {
        Debug.Log("[AnalyzeBackingOverlap] Running backing overlap analysis...");
        var town = GameObject.Find("Vietnamese_Old_Town_Instance");
        if (town == null)
        {
            Debug.LogWarning("Không tìm thấy Vietnamese_Old_Town_Instance nhưng bỏ qua để tránh sập luồng!");
            return;
        }

        var leftBacking = town.transform.Find("LeftFacadeBacking");
        var rightBacking = town.transform.Find("RightFacadeBacking");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Backing Overlap Analysis ===");

        if (leftBacking != null)
        {
            sb.AppendLine($"Left Backing:");
            sb.AppendLine($"  LocalPos: {leftBacking.localPosition.ToString("F3")}");
            sb.AppendLine($"  WorldPos: {leftBacking.position.ToString("F3")}");
            sb.AppendLine($"  LocalScale: {leftBacking.localScale.ToString("F3")}");
            var mr = leftBacking.GetComponent<MeshRenderer>();
            if (mr != null) sb.AppendLine($"  WorldBounds: Min={mr.bounds.min.ToString("F3")}, Max={mr.bounds.max.ToString("F3")}");
        }
        else sb.AppendLine("Left Backing not found!");

        if (rightBacking != null)
        {
            sb.AppendLine($"Right Backing:");
            sb.AppendLine($"  LocalPos: {rightBacking.localPosition.ToString("F3")}");
            sb.AppendLine($"  WorldPos: {rightBacking.position.ToString("F3")}");
            sb.AppendLine($"  LocalScale: {rightBacking.localScale.ToString("F3")}");
            var mr = rightBacking.GetComponent<MeshRenderer>();
            if (mr != null) sb.AppendLine($"  WorldBounds: Min={mr.bounds.min.ToString("F3")}, Max={mr.bounds.max.ToString("F3")}");
        }
        else sb.AppendLine("Right Backing not found!");

        // Let's find some door/window objects on both sides
        var renderers = town.GetComponentsInChildren<MeshRenderer>(true);
        sb.AppendLine("\n=== Samples of Glass/Door Mesh Renderers ===");
        foreach (var mr in renderers)
        {
            if (mr.name == "LeftFacadeBacking" || mr.name == "RightFacadeBacking") continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mr.sharedMaterial != null && mr.sharedMaterial.name == "kinh")
            {
                sb.AppendLine($"- GameObject: '{mr.name}' (Path: {mr.name})");
                sb.AppendLine($"  LocalPos: {mr.transform.localPosition.ToString("F3")}");
                sb.AppendLine($"  WorldBounds: Min={mr.bounds.min.ToString("F3")}, Max={mr.bounds.max.ToString("F3")}");
                // Let's calculate the mesh's local bounds in parent (town) space
                Bounds localBoundsInTown = GetLocalBoundsInParent(mr, town.transform);
                sb.AppendLine($"  LocalBounds in Town Space: Min={localBoundsInTown.min.ToString("F3")}, Max={localBoundsInTown.max.ToString("F3")}");
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "backing_overlap_analysis.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[AnalyzeBackingOverlap] Done. Saved to: " + path);
    }

    private static Bounds GetLocalBoundsInParent(MeshRenderer mr, Transform parent)
    {
        var mf = mr.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return new Bounds();
        
        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length == 0) return new Bounds();

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        Matrix4x4 localToParent = parent.worldToLocalMatrix * mr.transform.localToWorldMatrix;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 pt = localToParent.MultiplyPoint3x4(vertices[i]);
            min = Vector3.Min(min, pt);
            max = Vector3.Max(max, pt);
        }

        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }
}
