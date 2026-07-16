using UnityEngine;
using UnityEditor;
using System.IO;

public class CotDenPrefabCreator
{
    [MenuItem("Tools/Inspect Cot Den Model")]
    public static void InspectModel()
    {
        Debug.Log("=== Inspecting Cot Den Model Hierarchy ===");
        string modelPath = "Assets/Prefabs/cotden/street_lamp.glb";
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError($"Could not load model at: {modelPath}");
            return;
        }

        GameObject go = GameObject.Instantiate(model);
        
        // Print hierarchy and renderers bounds at default import rotation
        PrintHierarchy(go.transform, "");
        
        Debug.Log("--- Renderer Bounds & Materials (Root at Import Rotation) ---");
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            string matNames = "";
            foreach (var m in r.sharedMaterials)
            {
                matNames += (m != null ? m.name : "null") + ", ";
            }
            Debug.Log($"Renderer: {r.name} | Materials: {matNames} | Bounds: Center={r.bounds.center}, Size={r.bounds.size}");
        }

        GameObject.DestroyImmediate(go);
        Debug.Log("=== Finished Inspecting Cot Den Model Hierarchy ===");
    }

    private static void PrintHierarchy(Transform t, string indent)
    {
        Debug.Log($"{indent}- {t.name} (Position: {t.localPosition}, Rotation: {t.localRotation.eulerAngles}, Scale: {t.localScale})");
        for (int i = 0; i < t.childCount; i++)
        {
            PrintHierarchy(t.GetChild(i), indent + "  ");
        }
    }

    [MenuItem("Tools/Create Cot Den Prefab")]
    public static void CreateCotDenPrefab()
    {
        Debug.Log("=== Starting Cot Den Prefab Creation ===");

        string prefabsFolder = "Assets/Prefabs/Environment";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Environment");
        }

        string modelPath = "Assets/Prefabs/cotden/street_lamp.glb";
        string prefabPath = $"{prefabsFolder}/cotden.prefab";

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError($"Could not load model at: {modelPath}");
            return;
        }

        // 1. Instantiate the model
        GameObject go = GameObject.Instantiate(model);
        go.name = "cotden";

        // Keep it at its natural import rotation (which is Euler(270, 0, 0)) to align properly
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
        go.transform.localScale = Vector3.one;

        // Target height of the street lamp should be slightly taller than the boundary walls (which are 1.2 units tall).
        // A height of 1.4 units is perfect (slightly taller than the 1.2m wall).
        float targetHeight = 1.4f;
        float defaultHeight = 11.47f; // Height of Pillar_0 renderer (Object_4)
        float scaleFactor = targetHeight / defaultHeight;

        // 2. Add BoxCollider to root and fit it ONLY to the pillar (Object_4)
        BoxCollider boxCollider = go.AddComponent<BoxCollider>();
        Transform pillarTransform = go.transform.Find("root/GLTF_SceneRootNode/Pillar_0/Object_4");
        if (pillarTransform != null)
        {
            Renderer pillarRenderer = pillarTransform.GetComponent<Renderer>();
            if (pillarRenderer != null)
            {
                Bounds bounds = pillarRenderer.bounds;
                // Convert world bounds to root-local space
                Vector3 localCenter = go.transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = go.transform.InverseTransformVector(bounds.size);
                
                // Ensure all size components are positive
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

                boxCollider.center = localCenter;
                boxCollider.size = localSize;
                Debug.Log($"Created fitted BoxCollider on Pillar: Center={localCenter}, Size={localSize}");
            }
            else
            {
                Debug.LogWarning("Pillar renderer not found on Object_4");
            }
        }
        else
        {
            Debug.LogWarning("Pillar transform (root/GLTF_SceneRootNode/Pillar_0/Object_4) not found!");
        }

        // 3. Add a premium warm SpotLight child pointing downwards
        GameObject lightGo = new GameObject("StreetLight");
        lightGo.transform.SetParent(go.transform);
        
        // Position it slightly below the bulb center (bulb center is -3.04, 12.22, -0.01)
        // Placing it at Y = 12.0f keeps it just below the glass/housing, preventing shadow self-occlusion
        lightGo.transform.localPosition = new Vector3(-3.04f, 12.0f, 0f);
        lightGo.transform.rotation = Quaternion.LookRotation(Vector3.down);

        Light streetLight = lightGo.AddComponent<Light>();
        streetLight.type = LightType.Spot;
        streetLight.color = new Color(1.0f, 0.82f, 0.58f); // Warm street light
        streetLight.range = 4.0f;        // Reduced range for scaled-down street lamp (fits 1.4m height)
        streetLight.intensity = 10f;     // Reduced intensity to feel natural at close distance
        streetLight.spotAngle = 70f;
        streetLight.innerSpotAngle = 45f;
        streetLight.shadows = LightShadows.Soft; // Premium soft shadows
        Debug.Log("Added premium warm SpotLight to the prefab with soft shadows enabled.");

        // 4. Make the lamp bulb glow by setting its emission
        Transform bulbTransform = go.transform.Find("root/GLTF_SceneRootNode/Lamp_2/Object_8");
        if (bulbTransform != null)
        {
            Renderer bulbRenderer = bulbTransform.GetComponent<Renderer>();
            if (bulbRenderer != null)
            {
                // Accessing .material creates an instance of the material that will be saved with the prefab
                Material bulbMat = bulbRenderer.material;
                bulbMat.EnableKeyword("_EMISSION");
                bulbMat.SetColor("_EmissionColor", new Color(1.0f, 0.85f, 0.6f) * 4.0f); // Bright warm yellow emission
                EditorUtility.SetDirty(bulbRenderer);
                Debug.Log("Configured bulb material to glow with emission.");
            }
            else
            {
                Debug.LogWarning("Bulb renderer not found on Object_8");
            }
        }
        else
        {
            Debug.LogWarning("Bulb transform (root/GLTF_SceneRootNode/Lamp_2/Object_8) not found!");
        }

        // Apply scale factor to root GameObject
        go.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        Debug.Log($"Applied scale factor {scaleFactor} to resize cotden to {targetHeight}m height.");

        // 5. Save as Prefab
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Debug.Log($"Successfully created and saved Cot Den Prefab: {prefabPath}");

        // Cleanup
        GameObject.DestroyImmediate(go);
        
        AssetDatabase.Refresh();
        Debug.Log("=== Completed Cot Den Prefab Creation ===");
    }
}
