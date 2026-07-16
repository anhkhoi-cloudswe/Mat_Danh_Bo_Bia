using UnityEngine;
using UnityEditor;

/// <summary>
/// Dedicated fix for the Shipper character's animations.
/// Run via: Tools/Fix Shipper Animations
/// </summary>
public class FixShipperAnimations
{
    [MenuItem("Tools/Fix Shipper Animations")]
    public static void FixShipper()
    {
        Debug.Log("=== Starting Shipper Animation Fix ===");

        const string jogFbxPath    = "Assets/Models/Nhan_Vat_Phu/shipper/Joggingshipper@Jog Forward.fbx";
        const string idleFbxPath   = "Assets/Models/Nhan_Vat_Phu/shipper/Joggingshipper@Standing W_Briefcase Idle.fbx";
        // The shipper prefab IS an instance of Joggingshipper.fbx — avatar must match
        const string avatarFbxPath = "Assets/Models/Nhan_Vat_Phu/shipper/Joggingshipper.fbx";
        const string controllerPath = "Assets/Animators/Shipper_Animator.controller";
        const string prefabPath    = "Assets/Prefabs/shipper.prefab";

        // ---- Step 1: Ensure Jog FBX is Humanoid + loop ----
        ModelImporter jogImporter = AssetImporter.GetAtPath(jogFbxPath) as ModelImporter;
        if (jogImporter == null) { Debug.LogError("FAILED: Cannot load importer for Jog Forward FBX"); return; }
        bool jogChanged = false;
        if (jogImporter.animationType != ModelImporterAnimationType.Human)
        { jogImporter.animationType = ModelImporterAnimationType.Human; jogChanged = true; }
        var jogClips = jogImporter.clipAnimations;
        if (jogClips == null || jogClips.Length == 0) jogClips = jogImporter.defaultClipAnimations;
        foreach (var c in jogClips) { if (!c.loopTime) { c.loopTime = true; jogChanged = true; } }
        jogImporter.clipAnimations = jogClips;
        if (jogChanged) { jogImporter.SaveAndReimport(); Debug.Log("Jog Forward FBX: reimported as Humanoid + loop."); }
        else Debug.Log("Jog Forward FBX: already correct.");

        // ---- Step 2: Ensure Idle FBX is Humanoid + loop ----
        ModelImporter idleImporter = AssetImporter.GetAtPath(idleFbxPath) as ModelImporter;
        if (idleImporter == null) { Debug.LogError("FAILED: Cannot load importer for Standing Idle FBX"); return; }
        bool idleChanged = false;
        if (idleImporter.animationType != ModelImporterAnimationType.Human)
        { idleImporter.animationType = ModelImporterAnimationType.Human; idleChanged = true; }
        var idleClips = idleImporter.clipAnimations;
        if (idleClips == null || idleClips.Length == 0) idleClips = idleImporter.defaultClipAnimations;
        foreach (var c in idleClips) { if (!c.loopTime) { c.loopTime = true; idleChanged = true; } }
        idleImporter.clipAnimations = idleClips;
        if (idleChanged) { idleImporter.SaveAndReimport(); Debug.Log("Standing Idle FBX: reimported as Humanoid + loop."); }
        else Debug.Log("Standing Idle FBX: already correct.");

        // ---- Step 3: Retrieve animation clips ----
        AnimationClip walkClip = null, standClip = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(jogFbxPath))
            if (a is AnimationClip ac && !ac.name.StartsWith("__preview__")) { walkClip = ac; break; }
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(idleFbxPath))
            if (a is AnimationClip ac && !ac.name.StartsWith("__preview__")) { standClip = ac; break; }

        if (walkClip == null)  { Debug.LogError("FAILED: Could not find AnimationClip inside Jog Forward FBX."); return; }
        if (standClip == null) { Debug.LogError("FAILED: Could not find AnimationClip inside Standing Idle FBX."); return; }
        Debug.Log($"Walk clip: '{walkClip.name}' | Stand clip: '{standClip.name}'");

        // ---- Step 4: Retrieve avatar from Joggingshipper.fbx (must match prefab skeleton) ----
        Avatar shipperAvatar = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(avatarFbxPath))
            if (a is Avatar av) { shipperAvatar = av; break; }
        if (shipperAvatar == null) { Debug.LogError($"FAILED: No Avatar found in {avatarFbxPath}"); return; }
        Debug.Log($"Avatar: '{shipperAvatar.name}' (isHuman={shipperAvatar.isHuman})");

        // ---- Step 5: Rebuild Animator Controller with correct clips ----
        var animController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        if (animController == null) { Debug.LogError($"FAILED: No controller found at {controllerPath}"); return; }

        // Clear all states
        var sm = animController.layers[0].stateMachine;
        for (int i = sm.states.Length - 1; i >= 0; i--)
            sm.RemoveState(sm.states[i].state);

        // Clear and re-add Blend parameter
        for (int i = animController.parameters.Length - 1; i >= 0; i--)
            animController.RemoveParameter(i);
        animController.AddParameter("Blend", AnimatorControllerParameterType.Float);

        // Add states
        var idleState = sm.AddState("Idle");
        idleState.motion = standClip;
        var walkState = sm.AddState("Walk");
        walkState.motion = walkClip;
        sm.defaultState = idleState;

        // Transitions
        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, "Blend");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, "Blend");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;

        EditorUtility.SetDirty(animController);
        AssetDatabase.SaveAssets();
        Debug.Log("Animator Controller rebuilt: Idle → Walk states with Blend parameter.");

        // ---- Step 6: Fix the Prefab's Animator ----
        var prefabGO = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabGO == null) { Debug.LogError($"FAILED: Cannot load prefab at {prefabPath}"); return; }

        // The Animator is on the nested root (instance of Joggingshipper.fbx), not the top-level
        Animator anim = prefabGO.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            // Add on root if none found
            anim = prefabGO.AddComponent<Animator>();
            Debug.Log("Added Animator component to prefab root.");
        }

        bool prefabDirty = false;
        if (anim.runtimeAnimatorController != animController)
        { anim.runtimeAnimatorController = animController; prefabDirty = true; Debug.Log("Prefab: Assigned controller."); }
        if (anim.avatar != shipperAvatar)
        { anim.avatar = shipperAvatar; prefabDirty = true; Debug.Log("Prefab: Assigned avatar."); }
        if (anim.applyRootMotion)
        { anim.applyRootMotion = false; prefabDirty = true; Debug.Log("Prefab: Disabled root motion."); }

        if (prefabDirty)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
            Debug.Log("Prefab saved successfully.");
        }
        else Debug.Log("Prefab: no changes needed.");
        PrefabUtility.UnloadPrefabContents(prefabGO);

        // ---- Step 7: Fix any shipper instances already in open scenes ----
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (!go.name.ToLower().Contains("shipper")) continue;
            var sceneAnim = go.GetComponent<Animator>();
            if (sceneAnim == null) sceneAnim = go.AddComponent<Animator>();
            bool changed = false;
            if (sceneAnim.runtimeAnimatorController != animController)
            { sceneAnim.runtimeAnimatorController = animController; changed = true; }
            if (sceneAnim.avatar != shipperAvatar)
            { sceneAnim.avatar = shipperAvatar; changed = true; }
            if (sceneAnim.applyRootMotion)
            { sceneAnim.applyRootMotion = false; changed = true; }
            if (changed)
            {
                EditorUtility.SetDirty(sceneAnim);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
                Debug.Log($"Scene: Fixed Animator on '{go.name}'.");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== Shipper Animation Fix COMPLETE ===");
        Debug.Log("Verify: Press Play and check that Shipper has Idle + Walk animations.");
    }
}
