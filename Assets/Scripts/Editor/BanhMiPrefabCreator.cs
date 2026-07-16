using UnityEngine;
using UnityEditor;
using System.IO;

public class BanhMiPrefabCreator
{
    [MenuItem("Tools/Create Banh Mi Prefabs")]
    public static void CreatePrefabs()
    {
        Debug.Log("=== Starting Banh Mi Cart Prefab Creation ===");

        string prefabsFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // 1. Setup Material for BanhMiCart.fbx
        string modelFolder = "Assets/Models/xebanhmi";
        string fbxPath = $"{modelFolder}/source/BanhMiCart.fbx";
        string texturePath = $"{modelFolder}/textures/BanhMiCart.png";
        string materialPath = $"{modelFolder}/xebanhmi_Material.mat";

        // Create or configure the Material
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                urpShader = Shader.Find("Standard");
            }
            mat = new Material(urpShader);
            AssetDatabase.CreateAsset(mat, materialPath);
            Debug.Log($"Created Material: {materialPath}");
        }

        Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (albedoTex != null)
        {
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetTexture("_MainTex", albedoTex);
            Debug.Log($"Assigned Texture to Material: {texturePath}");
        }
        else
        {
            Debug.LogWarning($"Texture not found at: {texturePath}");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // Remap Materials in FBX Importer
        ModelImporter fbxImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (fbxImporter != null)
        {
            fbxImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            fbxImporter.materialLocation = ModelImporterMaterialLocation.InPrefab;
            
            // Remap standard sub-asset materials to our custom material
            fbxImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.001"), mat);
            fbxImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.002"), mat);
            
            fbxImporter.SaveAndReimport();
            Debug.Log($"Configured and reimported FBX materials for: {fbxPath}");
        }
        else
        {
            Debug.LogError($"FBX model not found at: {fbxPath}");
        }

        // 2. Create Prefab for FBX model
        // Upright rotation: (270, 0, 0) | Scale: (50, 50, 50)
        CreatePrefab(fbxPath, "xebanhmi_fbx", mat, Quaternion.Euler(270f, 0f, 0f), Vector3.one * 50f);

        // 3. Create Prefab for GLB model
        // Upright rotation: (90, 0, 0) | Scale: (1, 1, 1)
        string glbPath = $"{modelFolder}/low_poly_banh_mi_cart.glb";
        CreatePrefab(glbPath, "xebanhmi_glb", null, Quaternion.Euler(90f, 0f, 0f), Vector3.one);

        Debug.Log("=== Completed Banh Mi Cart Prefab Creation ===");
        AssetDatabase.Refresh();
    }

    private static void CreatePrefab(string modelPath, string prefabName, Material materialOverride, Quaternion rotation, Vector3 scale)
    {
        string prefabPath = $"Assets/Prefabs/{prefabName}.prefab";

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError($"Could not load model at: {modelPath}");
            return;
        }

        // Instantiate model
        GameObject go = GameObject.Instantiate(model);
        go.name = prefabName;

        // Apply material override if provided
        if (materialOverride != null)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Material[] sharedMats = r.sharedMaterials;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    sharedMats[i] = materialOverride;
                }
                r.sharedMaterials = sharedMats;
            }
        }

        // Setup BoxCollider based on mesh bounds (calculated clean with identity transform)
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        BoxCollider boxCollider = go.AddComponent<BoxCollider>();
        Renderer[] allRenderers = go.GetComponentsInChildren<Renderer>(true);
        if (allRenderers.Length > 0)
        {
            Bounds bounds = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                bounds.Encapsulate(allRenderers[i].bounds);
            }
            Vector3 localCenter = go.transform.InverseTransformPoint(bounds.center);
            boxCollider.center = localCenter;
            boxCollider.size = bounds.size;
        }

        // Apply correct scale and rotation for saving
        go.transform.rotation = rotation;
        go.transform.localScale = scale;

        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Debug.Log($"Created Prefab with fitted BoxCollider: {prefabPath}");

        // Cleanup instance
        GameObject.DestroyImmediate(go);
    }
}
