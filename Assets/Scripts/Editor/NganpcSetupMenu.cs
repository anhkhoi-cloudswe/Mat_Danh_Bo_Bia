using UnityEngine;
using UnityEditor;
using System.IO;

public class NganpcSetupMenu
{
    // NOTE: auto-run on load was removed. CreatePrefabs() overwrote the NPC
    // prefabs every editor session and assigned a mismatched Avatar (base
    // T-pose FBX avatar onto the walking FBX's Mixamo skeleton), which broke
    // their animations. Run manually from the menu only if needed.

    [MenuItem("Tools/Setup All NPCs (Materials + Prefabs)")]
    public static void SetupAll()
    {
        AssignAllMaterials();
        CreatePrefabs();
    }

    [MenuItem("Tools/Assign All NPC Materials")]
    public static void AssignAllMaterials()
    {
        Debug.Log("=== Starting All NPC Materials Assignment ===");

        // Setup Huy_seo
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/Huy_seo",
            "Meshy_AI_Arms_Outstretched_0601082535_texture",
            new string[] { "Meshy_AI_Arms_Outstretched_0601082535_texture.fbx", "Strut Walking.fbx" },
            "Huy_seo"
        );

        // Setup Nganpc
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/Nganpc",
            "Meshy_AI_T_Pose_in_Pajamas_0611030230_texture",
            new string[] { "Meshy_AI_T_Pose_in_Pajamas_0611030230_texture.fbx", "NgaWalking.fbx" },
            "Nga"
        );

        // Setup Npc1 (Argentine Footballer / codongvien)
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/Npc1",
            "Meshy_AI_Argentine_Footballer__0610100041_texture",
            new string[] { "Meshy_AI_Argentine_Footballer__0610100041_texture.fbx", "codongvienwalking.fbx" },
            "codongvien"
        );

        // Setup npc2
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/npc2",
            "Meshy_AI_Open_Arms_in_Denim_0610094857_texture",
            new string[] { "Meshy_AI_Open_Arms_in_Denim_0610094857_texture.fbx", "npcwalking.fbx" },
            "npc2"
        );

        // Setup shipper
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/shipper",
            "Meshy_AI_Grab_Rider_in_T_Pose_0611034406_texture",
            new string[] { "Meshy_AI_Grab_Rider_in_T_Pose_0611034406_texture.fbx", "Joggingshipper.fbx" },
            "shipper"
        );

        // Setup anh_aoxanh
        SetupCharacterMaterial(
            "Assets/Models/Nhan_Vat_Phu/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx",
            "Meshy_AI_T_Pose_Avatar_0612101506_texture",
            new string[] { "Meshy_AI_T_Pose_Avatar_0612101506_texture.fbx", "aoxanhwalking.fbx" },
            "anh_aoxanh"
        );

        Debug.Log("=== Completed All NPC Materials Assignment ===");
    }

    [MenuItem("Tools/Create NPC Prefabs")]
    public static void CreatePrefabs()
    {
        Debug.Log("=== Starting NPC Prefab Creation ===");

        string prefabsFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Define NPCs to create prefabs for
        CreatePrefabForNPC("Huy_seo", "Assets/Models/Nhan_Vat_Phu/Huy_seo", "Strut Walking.fbx", "Huy_seo", "Assets/Animators/Huy_seo_Walk.controller");
        CreatePrefabForNPC("Nganpc", "Assets/Models/Nhan_Vat_Phu/Nganpc", "Meshy_AI_T_Pose_in_Pajamas_0611030230_texture.fbx", "NgaWalking", "Assets/Animators/Nganpc_Animator.controller");
        CreatePrefabForNPC("Npc1", "Assets/Models/Nhan_Vat_Phu/Npc1", "Meshy_AI_Argentine_Footballer__0610100041_texture.fbx", "codongvien", "Assets/Animators/Npc1_Animator.controller");
        CreatePrefabForNPC("npc2", "Assets/Models/Nhan_Vat_Phu/npc2", "Meshy_AI_Open_Arms_in_Denim_0610094857_texture.fbx", "npcwalking", "Assets/Animators/Npc2_Animator.controller");
        CreatePrefabForNPC("shipper", "Assets/Models/Nhan_Vat_Phu/shipper", "Meshy_AI_Grab_Rider_in_T_Pose_0611034406_texture.fbx", "Joggingshipper", "Assets/Animators/Shipper_Animator.controller");
        CreatePrefabForNPC("anh_aoxanh", "Assets/Models/Nhan_Vat_Phu/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx", "Meshy_AI_T_Pose_Avatar_0612101506_texture.fbx", "anh_aoxanh", "Assets/Animators/anh_aoxanh_Animator.controller");

        Debug.Log("=== Completed NPC Prefab Creation ===");
        AssetDatabase.Refresh();
    }

    private static void SetupCharacterMaterial(string folderPath, string texturePrefix, string[] fbxNames, string sceneNameKeyword)
    {
        string albedoPath = $"{folderPath}/{texturePrefix}.png";
        string normalPath = $"{folderPath}/{texturePrefix}_normal.png";
        string metallicPath = $"{folderPath}/{texturePrefix}_metallic.png";
        string materialPath = $"{folderPath}/{Path.GetFileName(folderPath)}_Material.mat";

        // 1. Configure Normal Map import settings
        TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (normalImporter != null)
        {
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }
        }

        // 2. Create or Load Material
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
        }

        // Assign textures
        Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);

        if (albedoTex != null)
        {
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetTexture("_MainTex", albedoTex);
        }
        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (metallicTex != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallicTex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 3. Remap Materials in FBX Importer settings
        foreach (var fbxName in fbxNames)
        {
            string fbxPath = $"{folderPath}/{fbxName}";
            ModelImporter fbxImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (fbxImporter != null)
            {
                fbxImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                fbxImporter.materialLocation = ModelImporterMaterialLocation.InPrefab;
                
                // Remap standard names to our new material
                fbxImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.001"), mat);
                fbxImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "lambert1"), mat);
                fbxImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material"), mat);
                
                fbxImporter.SaveAndReimport();
            }
        }

        // 4. Apply to any active GameObjects in the scene matching sceneNameKeyword
        GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
        int appliedCount = 0;
        foreach (var go in sceneObjects)
        {
            if (go.name.IndexOf(sceneNameKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    Undo.RecordObject(r, "Assign Material");
                    Material[] sharedMats = r.sharedMaterials;
                    for (int i = 0; i < sharedMats.Length; i++)
                    {
                        sharedMats[i] = mat;
                    }
                    r.sharedMaterials = sharedMats;
                    EditorUtility.SetDirty(r);
                    appliedCount++;
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }

    private static void CreatePrefabForNPC(string name, string folderPath, string baseFbxName, string sceneKeyword, string controllerPath)
    {
        string baseFbxPath = $"{folderPath}/{baseFbxName}";
        string materialPath = $"{folderPath}/{Path.GetFileName(folderPath)}_Material.mat";
        string prefabPath = $"Assets/Prefabs/{name}.prefab";

        // Check if prefab already exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log($"Prefab already exists at {prefabPath}");
            return;
        }

        // Find existing scene object or instantiate new one temporarily
        GameObject go = null;
        GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var obj in sceneObjects)
        {
            if (obj.name.IndexOf(sceneKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                go = obj;
                break;
            }
        }

        bool tempCreated = false;
        if (go == null)
        {
            GameObject baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(baseFbxPath);
            if (baseModel != null)
            {
                go = GameObject.Instantiate(baseModel);
                go.name = name;
                tempCreated = true;
            }
            else
            {
                Debug.LogError($"Could not find base model FBX at {baseFbxPath}");
                return;
            }
        }

        // Configure GameObject settings to ensure it has correct Material and Animator
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat != null)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Material[] sharedMats = r.sharedMaterials;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    sharedMats[i] = mat;
                }
                r.sharedMaterials = sharedMats;
            }
        }

        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
        }

        if (!string.IsNullOrEmpty(controllerPath))
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
        }

        // Only fill in a missing avatar. Never replace the avatar of a model
        // instance: its skeleton may come from a different FBX (walking anim)
        // than baseFbxPath, and a mismatched avatar silently breaks animation.
        if (animator.avatar == null)
        {
            Avatar charAvatar = null;
            var charAssets = AssetDatabase.LoadAllAssetsAtPath(baseFbxPath);
            foreach (var asset in charAssets)
            {
                if (asset is Avatar)
                {
                    charAvatar = asset as Avatar;
                    break;
                }
            }
            if (charAvatar != null)
            {
                animator.avatar = charAvatar;
            }
        }

        // Save as prefab asset
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Debug.Log($"Created and saved Prefab: {prefabPath}");

        // Cleanup if temp
        if (tempCreated)
        {
            GameObject.DestroyImmediate(go);
        }
    }
}
