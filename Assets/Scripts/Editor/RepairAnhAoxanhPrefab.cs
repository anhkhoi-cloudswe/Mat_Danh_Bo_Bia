using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Rebuilds anh_aoxanh.prefab as a proper variant of the rigged FBX with the
/// correct Animator controller and material, resetting the local rotation so it stands upright.
/// </summary>
[InitializeOnLoad]
public static class RepairAnhAoxanhPrefab
{
    private const string PrefabPath = "Assets/Prefabs/anh_aoxanh.prefab";
    // The prefab MUST be built from the rigged walking FBX (Mixamo skeleton +
    // walk clip). The Meshy "*_texture.fbx" next to it is a static mesh with
    // zero bones — building from it produces a character that cannot animate.
    private const string FbxPath = "Assets/Models/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx/aoxanhwalking.fbx";
    private const string ControllerPath = "Assets/Animators/anh_aoxanh_Animator.controller";
    private const string MaterialPath = "Assets/Models/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx_Material.mat";
    private const float TargetHeight = 1.6f;

    static RepairAnhAoxanhPrefab()
    {
        EditorApplication.delayCall += () =>
        {
            // Only rebuild when the prefab is actually broken; an unconditional
            // run would overwrite manual fixes on every domain reload.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && IsPrefabBroken())
            {
                Repair();
            }
        };
    }

    [MenuItem("Tools/Full Setup and Scale anh_aoxanh")]
    public static void FullSetupAndScale()
    {
        Debug.Log("=== Starting Full Setup and Scale for anh_aoxanh ===");

        // 1. Assign Materials (creates material, sets normal map, remaps materials in FBX)
        NganpcSetupMenu.AssignAllMaterials();

        // 2. Fix Animators (forces FBX to humanoid, sets loop time, creates controller)
        FixAnimationSettings.FixAllAnimators();

        // 3. Rebuild Prefab (rebuilds prefab variant, resets rotation/position, scales to target height)
        Repair();

        // 4. Adjust Male Characters Scale (matches scale of male characters to Anh Bo Bia)
        AdjustMaleCharactersScale.ScaleAllMaleCharacters();

        Debug.Log("=== Completed Full Setup and Scale for anh_aoxanh ===");
    }

    private static bool IsPrefabBroken()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return true; // Recreate if missing
        // No skinned mesh means the prefab was built from a static FBX and cannot animate.
        return prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null;
    }

    [MenuItem("Tools/Repair anh_aoxanh Customer Prefab")]
    public static void Repair()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError($"[RepairAnhAoxanhPrefab] Model FBX not found at {FbxPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "anh_aoxanh";

        // CRITICAL FIX: Reset orientation so the character stands upright rather than lying flat
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localPosition = Vector3.zero;

        Animator animator = instance.GetComponent<Animator>();
        if (animator == null)
        {
            animator = instance.AddComponent<Animator>();
        }

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
        animator.applyRootMotion = false;

        // The model instance already carries the correct avatar from its own FBX.
        // Only fill it in when missing — never swap in another FBX's avatar
        // (mismatched rigs silently break animation).
        if (animator.avatar == null)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is Avatar avatar)
                {
                    animator.avatar = avatar;
                    break;
                }
            }
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                renderer.sharedMaterials = mats;
            }
        }

        // Normalize height so he matches the other customers regardless of FBX units.
        // Doing this after resetting rotation ensures height is measured upright along the Y-axis.
        float height = MeasureHeight(instance);
        if (height > 0.01f && (height < TargetHeight * 0.5f || height > TargetHeight * 2f))
        {
            float ratio = TargetHeight / height;
            instance.transform.localScale = new Vector3(ratio, ratio, ratio);
            Debug.Log($"[RepairAnhAoxanhPrefab] Normalized scale: height {height:F2} -> {TargetHeight} (x{ratio:F3})");
        }

        prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log("[RepairAnhAoxanhPrefab] Rebuilt anh_aoxanh.prefab as a rigged variant with Customer_Animator standing upright.");

        RelinkCustomerManagers(prefab);
    }

    private static float MeasureHeight(GameObject go)
    {
        var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skinned.Length == 0) return 0f;
        Bounds bounds = skinned[0].bounds;
        for (int i = 1; i < skinned.Length; i++) bounds.Encapsulate(skinned[i].bounds);
        return bounds.size.y;
    }

    private static void RelinkCustomerManagers(GameObject prefab)
    {
        if (prefab == null) return;

        foreach (var manager in Object.FindObjectsOfType<CustomerManager>())
        {
            var so = new SerializedObject(manager);
            var array = so.FindProperty("customerPrefabs");
            bool changed = false;

            if (array != null)
            {
                for (int i = 0; i < array.arraySize; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == null)
                    {
                        element.objectReferenceValue = prefab;
                        changed = true;
                        Debug.Log($"[RepairAnhAoxanhPrefab] Re-linked anh_aoxanh into customerPrefabs[{i}] on '{manager.name}'.");
                    }
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
        }
    }
}
