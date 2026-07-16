using UnityEngine;
using UnityEditor;

public static class FindDuplicateRoot
{
    [MenuItem("Tools/Find Duplicate Root")]
    [InitializeOnLoadMethod]
    public static void Run()
    {
        Debug.Log("[FindDuplicateRoot] Searching...");
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootObjects = activeScene.GetRootGameObjects();
        bool found = false;
        foreach (var go in rootObjects)
        {
            if (go.name == "root")
            {
                Debug.LogWarning("[FindDuplicateRoot] Found a scene-level root object named 'root'! Position: " + go.transform.position + ", active: " + go.activeSelf);
                found = true;
            }
        }
        if (!found)
        {
            Debug.Log("[FindDuplicateRoot] No scene-level root object named 'root' found.");
        }
    }
}
