using UnityEngine;
using UnityEditor;
using System.IO;

public class FixAnimationSettings
{
    // NOTE: auto-run on load was removed. It used to assign the Avatar from the
    // base (T-pose) FBX onto scene objects whose skeleton comes from the walking
    // FBX (Mixamo rig) — mismatched rigs, so animations stopped playing.
    // Run manually from the menu only if you know what you are doing.

    [MenuItem("Tools/Fix and Configure All Animators")]
    public static void FixAllAnimators()
    {
        Debug.Log("=== Starting Animation and Animator Repair ===");

        // 1. Repair Huy_seo
        RepairCharacter(
            "Huy_seo",
            "Assets/Models/Nhan_Vat_Phu/Huy_seo",
            "HuySeo@Walking.fbx",
            "HuySeo@Walking.fbx",
            "Huy_seo",
            "Assets/Animators/Huy_seo_Walk.controller",
            "Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeo@Breathing Idle.fbx"
        );

        // 2. Repair Nganpc
        RepairCharacter(
            "Nganpc",
            "Assets/Models/Nhan_Vat_Phu/Nganpc",
            "BaNga@Walking.fbx",
            "BaNga@Walking.fbx",
            "Nganpc",
            "Assets/Animators/Nganpc_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/Nganpc/BaNga@Idle.fbx"
        );


        // 3. Repair Npc1 (Argentine Footballer / codongvien / Messi_Fan)
        RepairCharacter(
            "Npc1",
            "Assets/Models/Nhan_Vat_Phu/Messi_Fan",
            "MessiFan@Standing_Idle.fbx",
            "Messi_Fan@Walking.fbx",
            "Npc1",
            "Assets/Animators/Npc1_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/Messi_Fan/MessiFan@Standing_Idle.fbx"
        );

        // 4. Repair npc2
        RepairCharacter(
            "npc2",
            "Assets/Models/Nhan_Vat_Phu/npc2",
            "Meshy_AI_Open_Arms_in_Denim_0610094857_texture.fbx",
            null,
            "npc2",
            "Assets/Animators/Npc2_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/npc2/AnhXamMinh@Standing W_Briefcase Idle.fbx"
        );

        // 5. Repair shipper
        RepairCharacter(
            "shipper",
            "Assets/Models/Nhan_Vat_Phu/shipper",
            "Meshy_AI_Grab_Rider_in_T_Pose_0611034406_texture.fbx",
            "Joggingshipper@Jog Forward.fbx",
            "Joggingshipper",
            "Assets/Animators/Shipper_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/shipper/Joggingshipper@Standing W_Briefcase Idle.fbx"
        );

        // 6. Repair anh_aoxanh
        RepairCharacter(
            "anh_aoxanh",
            "Assets/Models/Nhan_Vat_Phu/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx",
            "Meshy_AI_T_Pose_Avatar_0612101506_texture.fbx",
            "aoxanhwalking.fbx",
            "anh_aoxanh",
            "Assets/Animators/anh_aoxanh_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/Standing Idle.fbx"
        );

        // 7. Repair Miu Le
        RepairCharacter(
            "Miu Le",
            "Assets/Models/Nhan_Vat_Phu/Miu Le",
            "MeLiu@Walking.fbx",
            "MeLiu@Walking.fbx",
            "Miu Le",
            "Assets/Animators/MiuLe_Animator.controller",
            "Assets/Models/Nhan_Vat_Phu/Miu Le/MeLiu@Idle.fbx"
        );

        Debug.Log("=== Completed Animation and Animator Repair ===");
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Refresh the inspection window report
        CheckAnimationSettings.CheckSettings();
    }

    private static void RepairCharacter(string name, string folderPath, string baseFbxName, string animFbxName, string sceneKeyword, string controllerPath, string idleFbxPath = null)
    {
        string baseFbxPath = $"{folderPath}/{baseFbxName}";
        string animFbxPath = !string.IsNullOrEmpty(animFbxName) ? $"{folderPath}/{animFbxName}" : null;

        Debug.Log($"Repairing character settings for: {name}");
        Debug.Log($"baseFbxPath: {baseFbxPath}");
        Debug.Log($"animFbxPath: {animFbxPath}");
        Debug.Log($"idleFbxPath: {idleFbxPath}");
        Debug.Log($"controllerPath: {controllerPath}");

        // 1. Force Base FBX to Humanoid
        ModelImporter baseImporter = AssetImporter.GetAtPath(baseFbxPath) as ModelImporter;
        if (baseImporter == null)
        {
            Debug.LogError($"Base ModelImporter is NULL for path: {baseFbxPath}");
        }
        else
        {
            bool modified = false;
            if (baseImporter.animationType != ModelImporterAnimationType.Human)
            {
                baseImporter.animationType = ModelImporterAnimationType.Human;
                modified = true;
            }
            if (modified)
            {
                baseImporter.SaveAndReimport();
                Debug.Log($"Set base model {baseFbxName} to Humanoid.");
            }
        }

        // 2. Force Animation FBX to Humanoid and enable Loop Time
        AnimationClip animClip = null;
        if (!string.IsNullOrEmpty(animFbxPath))
        {
            ModelImporter animImporter = AssetImporter.GetAtPath(animFbxPath) as ModelImporter;
            if (animImporter != null)
            {
                bool modified = false;
                if (animImporter.animationType != ModelImporterAnimationType.Human)
                {
                    animImporter.animationType = ModelImporterAnimationType.Human;
                    modified = true;
                }

                // Retrieve and configure clip loops and bake into pose
                var clips = animImporter.clipAnimations;
                if (clips == null || clips.Length == 0) clips = animImporter.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        bool clipModified = false;
                        if (!clips[i].loopTime) { clips[i].loopTime = true; clipModified = true; }
                        if (!clips[i].lockRootRotation) { clips[i].lockRootRotation = true; clipModified = true; }
                        if (!clips[i].lockRootHeightY) { clips[i].lockRootHeightY = true; clipModified = true; }
                        if (!clips[i].lockRootPositionXZ) { clips[i].lockRootPositionXZ = true; clipModified = true; }
                        if (!clips[i].keepOriginalOrientation) { clips[i].keepOriginalOrientation = true; clipModified = true; }
                        if (clips[i].keepOriginalPositionY) { clips[i].keepOriginalPositionY = false; clipModified = true; }
                        if (!clips[i].heightFromFeet) { clips[i].heightFromFeet = true; clipModified = true; }
                        if (!clips[i].keepOriginalPositionXZ) { clips[i].keepOriginalPositionXZ = true; clipModified = true; }
                        if (clipModified) modified = true;
                    }
                    animImporter.clipAnimations = clips;
                }

                if (modified)
                {
                    animImporter.SaveAndReimport();
                    Debug.Log($"Configured animation model {animFbxName} as Humanoid with Loop Time enabled.");
                }
            }

            // Find the animation clip
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(animFbxPath);
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                {
                    animClip = asset as AnimationClip;
                    break;
                }
            }
        }

        // Force Idle FBX to Humanoid and retrieve clip
        AnimationClip idleClip = null;
        if (!string.IsNullOrEmpty(idleFbxPath))
        {
            ModelImporter idleImporter = AssetImporter.GetAtPath(idleFbxPath) as ModelImporter;
            if (idleImporter != null)
            {
                bool modified = false;
                if (idleImporter.animationType != ModelImporterAnimationType.Human)
                {
                    idleImporter.animationType = ModelImporterAnimationType.Human;
                    modified = true;
                }

                var clips = idleImporter.clipAnimations;
                if (clips == null || clips.Length == 0) clips = idleImporter.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        bool clipModified = false;
                        if (!clips[i].loopTime) { clips[i].loopTime = true; clipModified = true; }
                        if (!clips[i].lockRootRotation) { clips[i].lockRootRotation = true; clipModified = true; }
                        if (!clips[i].lockRootHeightY) { clips[i].lockRootHeightY = true; clipModified = true; }
                        if (!clips[i].lockRootPositionXZ) { clips[i].lockRootPositionXZ = true; clipModified = true; }
                        if (!clips[i].keepOriginalOrientation) { clips[i].keepOriginalOrientation = true; clipModified = true; }
                        if (clips[i].keepOriginalPositionY) { clips[i].keepOriginalPositionY = false; clipModified = true; }
                        if (!clips[i].heightFromFeet) { clips[i].heightFromFeet = true; clipModified = true; }
                        if (!clips[i].keepOriginalPositionXZ) { clips[i].keepOriginalPositionXZ = true; clipModified = true; }
                        if (clipModified) modified = true;
                    }
                    idleImporter.clipAnimations = clips;
                }

                if (modified)
                {
                    idleImporter.SaveAndReimport();
                    Debug.Log($"Configured idle animation model {Path.GetFileName(idleFbxPath)} as Humanoid with Loop Time enabled.");
                }
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(idleFbxPath);
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                {
                    idleClip = asset as AnimationClip;
                    break;
                }
            }
        }

        // 3. Create or Update Animator Controller
        RuntimeAnimatorController controller = null;
        if (!string.IsNullOrEmpty(controllerPath))
        {
            controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            Debug.Log($"[RepairCharacter] {name} controller loaded: {(controller != null ? controller.name : "null")}. animClip found: {(animClip != null ? animClip.name : "null")}. idleClip found: {(idleClip != null ? idleClip.name : "null")}");
            if (controller == null && animClip != null)
            {
                var newController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                
                // Add Blend parameter if it doesn't exist
                bool hasBlend = false;
                foreach (var p in newController.parameters)
                {
                    if (p.name == "Blend") { hasBlend = true; break; }
                }
                if (!hasBlend)
                {
                    newController.AddParameter("Blend", AnimatorControllerParameterType.Float);
                }

                var baseLayer = newController.layers[0];
                var stateMachine = baseLayer.stateMachine;

                // Add states
                var walkState = stateMachine.AddState("Walk");
                walkState.motion = animClip;

                if (idleClip != null)
                {
                    var idleState = stateMachine.AddState("Idle");
                    idleState.motion = idleClip;

                    // Set Idle as default state
                    stateMachine.defaultState = idleState;

                    // Add transitions
                    var toWalk = idleState.AddTransition(walkState);
                    toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, "Blend");
                    toWalk.duration = 0.15f;

                    var toIdle = walkState.AddTransition(idleState);
                    toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, "Blend");
                    toIdle.duration = 0.15f;
                }
                else
                {
                    stateMachine.defaultState = walkState;
                }
                
                controller = newController;
                Debug.Log($"Created Animator Controller with Idle/Walk states at: {controllerPath}");
            }
            else if (controller != null)
            {
                // Controller exists: let's force recreate/override or ensure Idle and Walk exist with correct transitions
                var animController = controller as UnityEditor.Animations.AnimatorController;
                if (animController != null)
                {
                    // Clear all existing parameters and add clean Blend parameter
                    for (int i = animController.parameters.Length - 1; i >= 0; i--)
                    {
                        animController.RemoveParameter(i);
                    }
                    animController.AddParameter("Blend", AnimatorControllerParameterType.Float);

                    var baseLayer = animController.layers[0];
                    var stateMachine = baseLayer.stateMachine;

                    // Clean all states to recreate them cleanly and avoid duplicates
                    for (int i = stateMachine.states.Length - 1; i >= 0; i--)
                    {
                        stateMachine.RemoveState(stateMachine.states[i].state);
                    }

                    // Add Walk and Idle states
                    var walkState = stateMachine.AddState("Walk");
                    walkState.motion = animClip;

                    if (idleClip != null)
                    {
                        var idleState = stateMachine.AddState("Idle");
                        idleState.motion = idleClip;

                        stateMachine.defaultState = idleState;

                        // Add transitions
                        var toWalk = idleState.AddTransition(walkState);
                        toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, "Blend");
                        toWalk.duration = 0.15f;

                        var toIdle = walkState.AddTransition(idleState);
                        toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, "Blend");
                        toIdle.duration = 0.15f;

                        Debug.Log($"Cleaned and rebuilt Animator Controller with Idle/Walk states at: {controllerPath}");
                    }
                    else
                    {
                        stateMachine.defaultState = walkState;
                        Debug.Log($"Cleaned and rebuilt Animator Controller with Walk state at: {controllerPath}");
                    }
                }
            }
        }

        // 4. Update Prefab Asset if it exists
        string prefabPath = $"Assets/Prefabs/{name}.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"Prefab asset was NOT found at path: {prefabPath}");
        }
        else
        {
            Debug.Log($"Found Prefab asset at path: {prefabPath}, updating it...");
            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            Animator prefabAnimator = instance.GetComponent<Animator>();
            if (prefabAnimator == null)
            {
                prefabAnimator = instance.AddComponent<Animator>();
            }

            bool prefabModified = false;
            if (prefabAnimator.applyRootMotion)
            {
                prefabAnimator.applyRootMotion = false;
                prefabModified = true;
                Debug.Log($"Prefab Animator applyRootMotion set to false");
            }

            if (controller != null && prefabAnimator.runtimeAnimatorController != controller)
            {
                prefabAnimator.runtimeAnimatorController = controller;
                prefabModified = true;
                Debug.Log($"Prefab Animator Controller will be updated to: {controller.name}");
            }

            string avatarSourcePath = baseFbxPath;
            Avatar charAvatar = null;
            var charAssets = AssetDatabase.LoadAllAssetsAtPath(avatarSourcePath);
            foreach (var asset in charAssets)
            {
                if (asset is Avatar)
                {
                    charAvatar = asset as Avatar;
                    break;
                }
            }

            if (charAvatar != null && prefabAnimator.avatar != charAvatar)
            {
                prefabAnimator.avatar = charAvatar;
                prefabModified = true;
                Debug.Log($"Prefab Avatar will be updated to: {charAvatar.name}");
            }

            if (prefabModified)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"Successfully saved Prefab Asset: {prefabPath}");
            }
            else
            {
                Debug.Log($"No changes needed for Prefab Asset: {prefabPath}");
            }
            PrefabUtility.UnloadPrefabContents(instance);
        }

        // 5. Update GameObjects in the scene
        GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var go in sceneObjects)
        {
            if (go.name.IndexOf(sceneKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Undo.RecordObject(go, "Repair Animator");
                Animator animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = go.AddComponent<Animator>();
                    Undo.RegisterCreatedObjectUndo(animator, "Add Animator");
                    Debug.Log($"Added missing Animator component to GameObject '{go.name}' in scene.");
                }

                // Assign controller
                if (controller != null && animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(animator);
                    Debug.Log($"Assigned Animator Controller to '{go.name}' in scene.");
                }

                if (animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                    EditorUtility.SetDirty(animator);
                    Debug.Log($"Disabled applyRootMotion for '{go.name}' in scene.");
                }

                // The Avatar must come from the same FBX as the object's skeleton.
                // Scene objects matched here are instances of the animation FBX,
                // so use its avatar (NOT the base/T-pose FBX, whose rig differs).
                string avatarSourcePath = baseFbxPath;
                Avatar charAvatar = null;
                var charAssets = AssetDatabase.LoadAllAssetsAtPath(avatarSourcePath);
                foreach (var asset in charAssets)
                {
                    if (asset is Avatar)
                    {
                        charAvatar = asset as Avatar;
                        break;
                    }
                }

                if (charAvatar != null && animator.avatar != charAvatar)
                {
                    animator.avatar = charAvatar;
                    EditorUtility.SetDirty(animator);
                    Debug.Log($"Assigned Avatar '{charAvatar.name}' to '{go.name}' in scene.");
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
