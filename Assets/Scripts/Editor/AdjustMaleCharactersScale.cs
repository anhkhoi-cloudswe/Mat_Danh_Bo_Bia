using UnityEngine;
using UnityEditor;
using System.IO;

public class AdjustMaleCharactersScale
{
    // NOTE: auto-run on load was removed — it re-saved the NPC prefabs every
    // editor session. Run manually from the menu when scales actually change.

    [MenuItem("Tools/Adjust Male Characters Scale")]
    public static void ScaleAllMaleCharacters()
    {
        Debug.Log("=== Starting Male Characters Scale Adjustment ===");

        // 1. Find the reference object (Anh Bo Bia)
        GameObject refObj = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (refObj == null)
        {
            // Fallback search
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go.name.Contains("Arms_Outstretched_biped") || go.name.Contains("AnhBoBia"))
                {
                    refObj = go;
                    break;
                }
            }
        }

        if (refObj == null)
        {
            Debug.LogError("Could not find reference character (Anh Bo Bia) in scene to compare height!");
            return;
        }

        // Get target height from bounds
        float targetHeight = GetCharacterHeight(refObj);
        if (targetHeight <= 0.1f)
        {
            targetHeight = 1.7f; // Fallback typical height in units
            Debug.LogWarning($"Could not determine height of reference object, using fallback: {targetHeight}");
        }
        else
        {
            Debug.Log($"Reference Character (Anh Bo Bia) Height: {targetHeight} units.");
        }

        // 2. Adjust Prefabs on disk
        AdjustPrefabScale("Huy_seo", "Assets/Prefabs/Huy_seo.prefab", targetHeight);
        AdjustPrefabScale("Npc1", "Assets/Prefabs/Npc1.prefab", targetHeight);
        AdjustPrefabScale("npc2", "Assets/Prefabs/npc2.prefab", targetHeight);
        AdjustPrefabScale("shipper", "Assets/Prefabs/shipper.prefab", targetHeight);
        AdjustPrefabScale("anh_aoxanh", "Assets/Prefabs/anh_aoxanh.prefab", targetHeight);

        // 3. Adjust Scene instances
        AdjustSceneInstanceScale("Huy_seo", targetHeight);
        AdjustSceneInstanceScale("codongvien", targetHeight);
        AdjustSceneInstanceScale("npcwalking", targetHeight);
        AdjustSceneInstanceScale("Joggingshipper", targetHeight);
        AdjustSceneInstanceScale("anh_aoxanh", targetHeight);

        Debug.Log("=== Completed Male Characters Scale Adjustment ===");
    }

    [MenuItem("Tools/Scale Huy_seo to anhbanhmi")]
    public static void ScaleHuySeoToAnhBanhMi()
    {
        Debug.Log("=== Starting Huy_seo to anhbanhmi Scale Adjustment ===");

        // 1. Find the reference object (anhbanhmi)
        GameObject refObj = GameObject.Find("anhbanhmi");
        if (refObj == null)
        {
            // Try fallback loading from prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/anhbanhmi.prefab");
            if (prefab != null)
            {
                refObj = prefab;
            }
        }

        if (refObj == null)
        {
            Debug.LogError("Could not find reference character (anhbanhmi) in scene or prefabs!");
            return;
        }

        // Get target height from bounds
        float targetHeight = GetCharacterHeight(refObj);
        if (targetHeight <= 0.1f)
        {
            targetHeight = 1.6f; // Fallback
            Debug.LogWarning($"Could not determine height of reference object, using fallback: {targetHeight}");
        }
        else
        {
            Debug.Log($"Reference Character (anhbanhmi) Height: {targetHeight} units.");
        }

        // 2. Adjust Huy_seo Prefab on disk
        AdjustPrefabScale("Huy_seo", "Assets/Prefabs/Huy_seo.prefab", targetHeight);

        // 3. Adjust Huy_seo Scene instances
        AdjustSceneInstanceScale("Huy_seo", targetHeight);

        Debug.Log("=== Completed Huy_seo to anhbanhmi Scale Adjustment ===");
    }

    private static float GetCharacterHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }
            return combinedBounds.size.y;
        }

        var meshRenderers = go.GetComponentsInChildren<MeshRenderer>(true);
        if (meshRenderers.Length > 0)
        {
            Bounds combinedBounds = meshRenderers[0].bounds;
            for (int i = 1; i < meshRenderers.Length; i++)
            {
                combinedBounds.Encapsulate(meshRenderers[i].bounds);
            }
            return combinedBounds.size.y;
        }

        return 0f;
    }

    private static void AdjustPrefabScale(string name, string prefabPath, float targetHeight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab not found at {prefabPath}");
            return;
        }

        // Instantiate temporarily in scene to calculate height and scale
        GameObject tempInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (tempInstance != null)
        {
            // Reset scale to default/1 before calculating height
            tempInstance.transform.localScale = Vector3.one;
            Physics.SyncTransforms(); // Force bounds update

            float currentHeight = GetCharacterHeight(tempInstance);
            if (currentHeight > 0.1f)
            {
                float ratio = targetHeight / currentHeight;
                tempInstance.transform.localScale = new Vector3(ratio, ratio, ratio);
                
                // Save changes back to prefab
                PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
                Debug.Log($"Adjusted Prefab '{name}' scale ratio: {ratio:F3} (Height changed from {currentHeight:F2} to {targetHeight:F2} units)");
            }
            else
            {
                Debug.LogWarning($"Could not determine height of temp instance for prefab: {name}");
            }

            GameObject.DestroyImmediate(tempInstance);
        }
    }

    private static void AdjustSceneInstanceScale(string sceneKeyword, float targetHeight)
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (var go in allObjects)
        {
            if (go.name.IndexOf(sceneKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0 && !EditorUtility.IsPersistent(go))
            {
                // To adjust scale dynamically, we first set scale to Vector3.one to check its unscaled height
                Vector3 originalScale = go.transform.localScale;
                go.transform.localScale = Vector3.one;
                Physics.SyncTransforms();

                float currentHeight = GetCharacterHeight(go);
                if (currentHeight > 0.1f)
                {
                    float ratio = targetHeight / currentHeight;
                    Undo.RecordObject(go.transform, "Adjust Scale to Anh Bo Bia");
                    go.transform.localScale = new Vector3(ratio, ratio, ratio);
                    EditorUtility.SetDirty(go);
                    count++;
                    Debug.Log($"Adjusted Scene Instance '{go.name}' scale to {ratio:F3}");
                }
                else
                {
                    // Restore original if we couldn't calculate bounds
                    go.transform.localScale = originalScale;
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
