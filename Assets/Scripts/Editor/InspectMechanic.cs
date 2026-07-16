using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public static class InspectMechanic
{
    [InitializeOnLoadMethod]
    public static void Run()
    {
        Debug.Log("[InspectMechanic] Scanning BoBiaMechanic component...");
        
        BoBiaMechanic mechanic = Object.FindAnyObjectByType<BoBiaMechanic>();
        if (mechanic == null)
        {
            Debug.LogWarning("[InspectMechanic] BoBiaMechanic not found in scene!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== BoBiaMechanic Settings ===");
        
        // Let's use SerializedObject to inspect all field values
        SerializedObject so = new SerializedObject(mechanic);
        
        SerializedProperty stallCenterProp = so.FindProperty("stallCenter");
        Transform stallCenter = stallCenterProp.objectReferenceValue as Transform;
        sb.AppendLine($"stallCenter: {(stallCenter != null ? stallCenter.name + " at position " + stallCenter.position : "null")}");
        
        SerializedProperty radiusProp = so.FindProperty("interactRadius");
        sb.AppendLine($"interactRadius: {radiusProp.floatValue}");
        
        SerializedProperty playerProp = so.FindProperty("playerTransform");
        Transform player = playerProp.objectReferenceValue as Transform;
        sb.AppendLine($"playerTransform: {(player != null ? player.name + " at position " + player.position : "null")}");
        
        string path = Path.Combine(Directory.GetCurrentDirectory(), "mechanic_info.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[InspectMechanic] Done. Saved to: " + path);
    }
}
