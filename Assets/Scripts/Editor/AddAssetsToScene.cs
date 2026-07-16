using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;

[InitializeOnLoad]
public class AddAssetsToScene
{
    static AddAssetsToScene()
    {
        // EditorApplication.delayCall += ReplaceWallWithTownhouses;
    }

    private static void ReplaceWallWithTownhouses()
    {
        string scenePath = "Assets/Scenes/BaoScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Failed to open scene: " + scenePath);
            return;
        }

        Debug.Log("--- Start Replacing Wall with Townhouses ---");

        // 1. Find the template Vietnamese_Townhouse
        GameObject templateHouse = GameObject.Find("Vietnamese_Townhouse");
        if (templateHouse == null)
        {
            Debug.LogError("Template object 'Vietnamese_Townhouse' not found in scene!");
            return;
        }

        // 2. Clean up any existing generated group to ensure idempotency
        GameObject existingGroup = GameObject.Find("Generated_Townhouses_Right_Side");
        if (existingGroup != null)
        {
            Debug.Log("Found existing Generated_Townhouses_Right_Side group. Destroying it to regenerate...");
            GameObject.DestroyImmediate(existingGroup);
        }

        // 3. Create parent group
        GameObject parentGroup = new GameObject("Generated_Townhouses_Right_Side");
        parentGroup.transform.position = Vector3.zero;
        parentGroup.transform.rotation = Quaternion.identity;
        parentGroup.transform.localScale = Vector3.one;

        // 4. Instantiate 19 instances spaced 1.95 units apart along Z axis
        float startX = 13.51f;
        float startY = 0.00f;
        float centerZ = 2.930f;
        float spacingZ = 1.95f;
        int count = 19;
        int midIndex = 9; // Index for centering around centerZ

        for (int i = 0; i < count; i++)
        {
            GameObject inst = GameObject.Instantiate(templateHouse);
            inst.name = $"Vietnamese_Townhouse_RightSide_{i}";
            inst.transform.SetParent(parentGroup.transform);
            
            float zPos = centerZ + (i - midIndex) * spacingZ;
            inst.transform.position = new Vector3(startX, startY, zPos);
            inst.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            inst.transform.localScale = Vector3.one;
            inst.SetActive(true);

            // If it is a prefab instance, unpack it or leave it. Since template is not a prefab, simple clone is fine.
        }
        Debug.Log($"Successfully instantiated {count} townhouses under 'Generated_Townhouses_Right_Side'.");

        // 5. Find and deactivate the old Wall_East_Right_Side object
        GameObject oldWall = GameObject.Find("Wall_East_Right_Side");
        if (oldWall != null)
        {
            oldWall.SetActive(false);
            Debug.Log("Deactivated old wall object: Wall_East_Right_Side");
        }
        else
        {
            // Try finding by path if name search fails
            oldWall = GameObject.Find("/Sketch_Layout_Walls_Trees/Wall_East_Right_Side");
            if (oldWall != null)
            {
                oldWall.SetActive(false);
                Debug.Log("Deactivated old wall object via path: /Sketch_Layout_Walls_Trees/Wall_East_Right_Side");
            }
            else
            {
                Debug.LogWarning("Old wall object 'Wall_East_Right_Side' was not found in the scene.");
            }
        }

        // 6. Save the scene and project assets
        EditorSceneManager.MarkSceneDirty(scene);
        bool saveSuccess = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Scene saved successfully: {saveSuccess}. Townhouse placement finished!");

        // Write status to a file for verification
        string logPath = Path.Combine(Directory.GetCurrentDirectory(), "execution_status.txt");
        File.WriteAllText(logPath, $"SUCCESS: Instantiated {count} townhouses at X=13.51. Deactivated Wall_East_Right_Side. Scene saved: {saveSuccess}");
    }
}
