using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;

[InitializeOnLoad]
public class HierarchyScanner : EditorWindow
{
    static HierarchyScanner()
    {
        // Run the scan automatically when the project compiles or loads
        EditorApplication.delayCall += ScanHierarchy;
    }

    [MenuItem("Tools/Scan Hierarchy")]
    public static void ScanHierarchy()
    {
        Debug.Log("[HierarchyScanner] Starting Hierarchy scan...");
        
        List<GameObjectInfo> allObjects = new List<GameObjectInfo>();
        
        // Find all Root GameObjects
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        
        foreach (var root in rootObjects)
        {
            TraverseHierarchy(root, "", allObjects);
        }

        string json = JsonUtility.ToJson(new Wrapper { objects = allObjects }, true);
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "hierarchy_scan.json");
        
        File.WriteAllText(outputPath, json, Encoding.UTF8);
        Debug.Log($"[HierarchyScanner] Successfully scanned {allObjects.Count} objects. Saved to: {outputPath}");
    }

    private static void TraverseHierarchy(GameObject obj, string parentPath, List<GameObjectInfo> list)
    {
        string currentPath = string.IsNullOrEmpty(parentPath) ? obj.name : $"{parentPath}/{obj.name}";
        
        List<string> components = new List<string>();
        foreach (var comp in obj.GetComponents<Component>())
        {
            if (comp != null)
            {
                components.Add(comp.GetType().Name);
            }
        }

        list.Add(new GameObjectInfo
        {
            name = obj.name,
            path = currentPath,
            instanceId = UnityEngine.EntityId.ToULong(obj.GetEntityId()),
            activeSelf = obj.activeSelf,
            activeInHierarchy = obj.activeInHierarchy,
            tag = obj.tag,
            layer = obj.layer,
            components = components
        });

        for (int i = 0; i < obj.transform.childCount; i++)
        {
            TraverseHierarchy(obj.transform.GetChild(i).gameObject, currentPath, list);
        }
    }

    [System.Serializable]
    public class GameObjectInfo
    {
        public string name;
        public string path;
        public ulong instanceId;
        public bool activeSelf;
        public bool activeInHierarchy;
        public string tag;
        public int layer;
        public List<string> components;
    }

    [System.Serializable]
    public class Wrapper
    {
        public List<GameObjectInfo> objects;
    }
}
