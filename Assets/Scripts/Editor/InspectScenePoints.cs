using UnityEditor;
using UnityEngine;

public class InspectScenePoints
{
    [MenuItem("Tools/Inspect Scene Points")]
    public static void Inspect()
    {
        Debug.Log("=== Inspecting Scene Points in Active Scene ===");
        PrintPosition("Diem_Spawn_Khach");
        PrintPosition("Waypoint_Spawn_TopRight");
        PrintPosition("Waypoint_Spawn_BottomRight");
        PrintPosition("Waypoint_Road_Corner");
        PrintPosition("Waypoint_Road_Approach");
        PrintPosition("Diem_Tiep_Can_Khach");
        PrintPosition("Vi_Tri_Khach_Dung");
        PrintPosition("_MeLiuVillaDoor");
        
        // Find CustomerManager and get its spawn points
        // CustomerManager mgr = Object.FindFirstObjectByType<CustomerManager>();
        // if (mgr != null)
        // {
        //     Debug.Log($"CustomerManager: spawnPoint={GetPos(mgr.SpawnPoint)}, spawnPoint1={GetPos(mgr.SpawnPoint1)}, spawnPoint2={GetPos(mgr.SpawnPoint2)}, targetQueuePoint={GetPos(mgr.TargetQueuePoint)}, approachPoint={GetPos(mgr.ApproachPoint)}");
        // }
        Debug.Log("=== Finished Inspecting Scene Points ===");
    }

    private static void PrintPosition(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            Debug.Log($"GameObject '{name}': Position={go.transform.position}");
        }
        else
        {
            Debug.LogWarning($"GameObject '{name}' not found in active scene!");
        }
    }

    private static string GetPos(Transform t)
    {
        return t != null ? t.position.ToString() : "null";
    }
}
