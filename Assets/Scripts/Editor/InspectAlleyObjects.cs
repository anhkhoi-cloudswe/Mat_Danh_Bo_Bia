using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectAlleyObjects
{
    [MenuItem("Tools/Inspect Alley Objects")]
    public static void Run()
    {
        Debug.Log("[InspectAlleyObjects] Scanning alley-related objects...");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Alley Objects in Scene ===");

        // Print active scene name
        sb.AppendLine($"Active Scene: {UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path}");

        // Find CustomerManager in scene
        var customerManager = Object.FindAnyObjectByType<CustomerManager>();
        if (customerManager != null)
        {
            sb.AppendLine("\n=== CustomerManager Waypoints ===");
            SerializedObject so = new SerializedObject(customerManager);
            string[] props = {
                "spawnPoint", "approachPoint", "targetQueuePoint",
                "spawnPoint1", "spawnPoint2", "cornerPoint", "roadApproachPoint"
            };
            foreach (var pName in props)
            {
                var prop = so.FindProperty(pName);
                if (prop != null && prop.objectReferenceValue != null)
                {
                    Transform t = null;
                    if (prop.objectReferenceValue is GameObject go) t = go.transform;
                    else if (prop.objectReferenceValue is Transform tr) t = tr;
                    else if (prop.objectReferenceValue is Component comp) t = comp.transform;

                    if (t != null)
                        sb.AppendLine($"- {pName}: '{t.name}' | Pos: {t.position.ToString("F3")} | Rot: {t.rotation.eulerAngles.ToString("F1")}");
                    else
                        sb.AppendLine($"- {pName}: '{prop.objectReferenceValue.name}' (no transform)");
                }
                else
                {
                    sb.AppendLine($"- {pName}: null");
                }
            }
        }

        // Find BoBiaMechanic / xe bo bia
        var bobia = GameObject.Find("xe_bo_bia");
        if (bobia == null) bobia = GameObject.Find("Xe_Bo_Bia");
        if (bobia == null) bobia = GameObject.Find("xebobia");
        if (bobia != null)
        {
            sb.AppendLine($"\nXe Bo Bia: '{bobia.name}' | Pos: {bobia.transform.position.ToString("F3")} | Scale: {bobia.transform.lossyScale.ToString("F3")}");
        }

        // Search for all waypoints/points
        string[] searchNames = new string[] {
            "Point1_Alley",
            "Point2_TrashArea",
            "Point3_House",
            "Waypoint_Road_Corner",
            "_StoryPhase1Manager",
            "Huy_seo",
            "anhbanhmi",
            "xebanhmi_fbx",
            "batlua"
        };

        sb.AppendLine("\n=== General Search ===");
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string name in searchNames)
        {
            bool found = false;
            foreach (var go in allObjects)
            {
                if (go.name == name)
                {
                    found = true;
                    sb.AppendLine($"- Found: '{go.name}' | Pos: {go.transform.position.ToString("F3")} | Rot: {go.transform.rotation.eulerAngles.ToString("F1")} | Scale: {go.transform.lossyScale.ToString("F3")}");
                }
            }
            if (!found)
            {
                sb.AppendLine($"- Not found: '{name}'");
            }
        }

        // Scan all Lights in scene
        sb.AppendLine("\n=== Lights in Scene ===");
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            sb.AppendLine($"- Light: '{l.name}' | Type: {l.type} | Color: {l.color} | Intensity: {l.intensity} | Range: {l.range} | Pos: {l.transform.position.ToString("F3")} | Parent: {(l.transform.parent != null ? l.transform.parent.name : "None")}");
        }

        // Scan for objects containing 'cotden' or 'lamp'
        sb.AppendLine("\n=== GameObjects matching 'cotden' or 'lamp' or 'light' or 'xebanhmi' ===");
        foreach (var go in allObjects)
        {
            string nameLower = go.name.ToLower();
            if (nameLower.Contains("cotden") || nameLower.Contains("lamp") || nameLower.Contains("street_light") || nameLower.Contains("xebanhmi"))
            {
                sb.AppendLine($"- '{go.name}' | Pos: {go.transform.position.ToString("F3")} | Parent: {(go.transform.parent != null ? go.transform.parent.name : "None")}");
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), "alley_inspection.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectAlleyObjects] Done. Saved to: " + path);
    }
}
