using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class FixHuySeoAnimations
{
    [MenuItem("Tools/Fix Huy Seo Animations")]
    public static void FixHuySeo()
    {
        Debug.Log("=== Starting Huy_seo Animation Fix ===");

        const string folderPath = "Assets/Models/Nhan_Vat_Phu/Huy_seo";
        const string baseFbxPath = "Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeo@Breathing Idle.fbx";
        const string walkFbxPath = "Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeo@Walking.fbx";
        const string runFbxPath = "Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeo@Running.fbx";
        const string controllerPath = "Assets/Animators/Huy_seo_Walk.controller";
        const string prefabPath = "Assets/Prefabs/Huy_seo.prefab";

        // Load custom anim files
        AnimationClip clipOpening  = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folderPath}/HuySeoOpening.anim");
        AnimationClip clipLeftTurn = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folderPath}/HuySeoLeftTurn.anim");
        AnimationClip clipReacting = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folderPath}/HuySeoReacting.anim");
        AnimationClip clipThrow    = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folderPath}/HuySeoThrow.anim");

        // Helper to import animation type and loop settings
        void ConfigureFbx(string path, string clipName, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogError($"Cannot find importer for: {path}"); return; }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    bool clipChanged = false;
                    if (clips[i].loopTime != loop) { clips[i].loopTime = loop; clipChanged = true; }
                    if (!clips[i].lockRootRotation) { clips[i].lockRootRotation = true; clipChanged = true; }
                    if (!clips[i].lockRootHeightY) { clips[i].lockRootHeightY = true; clipChanged = true; }
                    if (!clips[i].lockRootPositionXZ) { clips[i].lockRootPositionXZ = true; clipChanged = true; }
                    if (!clips[i].keepOriginalOrientation) { clips[i].keepOriginalOrientation = true; clipChanged = true; }
                    if (clips[i].keepOriginalPositionY) { clips[i].keepOriginalPositionY = false; clipChanged = true; }
                    if (!clips[i].heightFromFeet) { clips[i].heightFromFeet = true; clipChanged = true; }
                    if (!clips[i].keepOriginalPositionXZ) { clips[i].keepOriginalPositionXZ = true; clipChanged = true; }
                    
                    if (!string.IsNullOrEmpty(clipName))
                    {
                        clips[i].name = clipName;
                        clipChanged = true;
                    }
                    if (clipChanged) changed = true;
                }
                importer.clipAnimations = clips;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"Configured {path}");
            }
        }

        // Configure FBX animations
        ConfigureFbx(baseFbxPath, "Idle", true);
        ConfigureFbx(walkFbxPath, "Walk", true);
        ConfigureFbx(runFbxPath, "FastRun", true);

        // Helper to load clip from FBX path
        AnimationClip GetClipFromFbx(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                {
                    return asset as AnimationClip;
                }
            }
            return null;
        }

        AnimationClip clipIdle = GetClipFromFbx(baseFbxPath);
        AnimationClip clipWalk = GetClipFromFbx(walkFbxPath);
        AnimationClip clipRun = GetClipFromFbx(runFbxPath);

        Debug.Log($"Loaded Idle: {clipIdle != null}, Walk: {clipWalk != null}, Run: {clipRun != null}");

        // Load / Recreate Animator Controller
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        else
        {
            // Reset state machine
            var rootSM = controller.layers[0].stateMachine;
            var states = new System.Collections.Generic.List<AnimatorState>();
            foreach (var cs in rootSM.states) states.Add(cs.state);
            foreach (var st in states) rootSM.RemoveState(st);

            while (controller.parameters.Length > 0)
                controller.RemoveParameter(0);
        }

        // Add parameters
        controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
        controller.AddParameter("toReact", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("toThrow", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        // Add states
        AnimatorState stateIdle       = sm.AddState("Idle",           new Vector3(200,  0,  0));
        AnimatorState stateWalk       = sm.AddState("Walk",           new Vector3(200, 80,  0));
        AnimatorState stateFastRun    = sm.AddState("FastRun",        new Vector3(200, 160, 0));
        AnimatorState stateOpening    = sm.AddState("HuySeoOpening",  new Vector3(450,  0,  0));
        AnimatorState stateLeftTurn   = sm.AddState("HuySeoLeftTurn", new Vector3(450, 80,  0));
        AnimatorState stateTalking    = sm.AddState("Talking",        new Vector3(450, 160, 0));
        AnimatorState stateReact      = sm.AddState("React",          new Vector3(700,  0,  0));
        AnimatorState stateThrow      = sm.AddState("Throw",          new Vector3(700, 80,  0));

        // Assign clips
        stateIdle.motion = clipIdle;
        stateWalk.motion = clipWalk;
        stateFastRun.motion = clipRun;
        stateOpening.motion = clipOpening != null ? clipOpening : clipIdle;
        stateLeftTurn.motion = clipLeftTurn != null ? clipLeftTurn : clipIdle;
        stateTalking.motion = clipOpening != null ? clipOpening : clipIdle;
        stateReact.motion = clipReacting != null ? clipReacting : clipIdle;
        stateThrow.motion = clipThrow != null ? clipThrow : clipIdle;

        sm.defaultState = stateIdle;

        // Transitions
        void AddInstantTransition(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
        }

        void AddExitTimeTransition(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.1f;
        }

        // Idle ↔ Walk
        AddInstantTransition(stateIdle, stateWalk);
        AddInstantTransition(stateWalk, stateIdle);
        // Walk ↔ FastRun
        AddInstantTransition(stateWalk, stateFastRun);
        AddInstantTransition(stateFastRun, stateWalk);
        AddInstantTransition(stateFastRun, stateIdle);
        // Idle ↔ HuySeoOpening
        AddInstantTransition(stateIdle, stateOpening);
        AddInstantTransition(stateOpening, stateIdle);
        // Idle ↔ HuySeoLeftTurn
        AddInstantTransition(stateIdle, stateLeftTurn);
        AddInstantTransition(stateLeftTurn, stateIdle);
        // HuySeoOpening ↔ Talking
        AddInstantTransition(stateOpening, stateTalking);
        AddInstantTransition(stateTalking, stateOpening);

        // AnyState transitions for React and Throw (or manual triggers)
        var reactTransition = sm.AddAnyStateTransition(stateReact);
        reactTransition.AddCondition(AnimatorConditionMode.If, 0, "toReact");
        reactTransition.duration = 0.1f;

        var throwTransition = sm.AddAnyStateTransition(stateThrow);
        throwTransition.AddCondition(AnimatorConditionMode.If, 0, "toThrow");
        throwTransition.duration = 0.1f;

        AddExitTimeTransition(stateReact, stateIdle);
        AddExitTimeTransition(stateThrow, stateIdle);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Get avatar
        Avatar charAvatar = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(baseFbxPath))
        {
            if (asset is Avatar) { charAvatar = asset as Avatar; break; }
        }

        // Prefab repair
        GameObject prefabGO = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabGO != null)
        {
            Animator anim = prefabGO.GetComponent<Animator>();
            if (anim == null) anim = prefabGO.AddComponent<Animator>();

            bool dirty = false;
            if (anim.runtimeAnimatorController != controller) { anim.runtimeAnimatorController = controller; dirty = true; }
            if (charAvatar != null && anim.avatar != charAvatar) { anim.avatar = charAvatar; dirty = true; }
            if (anim.applyRootMotion) { anim.applyRootMotion = false; dirty = true; }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
                Debug.Log("Saved Huy_seo prefab.");
            }
            PrefabUtility.UnloadPrefabContents(prefabGO);
        }

        // Scene instance repair
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "Huy_seo" || go.name.Contains("Huy_seo"))
            {
                Animator anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();

                bool dirty = false;
                if (anim.runtimeAnimatorController != controller) { anim.runtimeAnimatorController = controller; dirty = true; }
                if (charAvatar != null && anim.avatar != charAvatar) { anim.avatar = charAvatar; dirty = true; }
                if (anim.applyRootMotion) { anim.applyRootMotion = false; dirty = true; }

                if (dirty)
                {
                    EditorUtility.SetDirty(anim);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
                    Debug.Log($"Fixed Huy_seo instance: {go.name}");
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("=== Huy_seo Animation Fix COMPLETE ===");
    }
}
