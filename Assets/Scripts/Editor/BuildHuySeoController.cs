using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class BuildHuySeoController
{
    [MenuItem("Tools/Build Huy Seo Animator Controller")]
    public static void BuildController()
    {
        string controllerPath = "Assets/Animators/Huy_seo_Walk.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        // Add parameters
        bool hasToReact = false;
        bool hasToThrow = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "toReact") hasToReact = true;
            if (param.name == "toThrow") hasToThrow = true;
        }
        if (!hasToReact) controller.AddParameter("toReact", AnimatorControllerParameterType.Trigger);
        if (!hasToThrow) controller.AddParameter("toThrow", AnimatorControllerParameterType.Trigger);

        var rootStateMachine = controller.layers[0].stateMachine;

        // Helper to find animation clip inside FBX
        AnimationClip GetClipFromFbx(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                {
                    return asset as AnimationClip;
                }
            }
            return null;
        }

        // Helper to add or update state
        AnimatorState GetOrAddState(string name, string animPath)
        {
            AnimationClip clip = GetClipFromFbx(animPath);
            if (clip == null)
            {
                Debug.LogWarning($"Could not find animation clip in FBX: {animPath}");
            }

            // Find existing state
            foreach (var childState in rootStateMachine.states)
            {
                if (childState.state.name == name)
                {
                    childState.state.motion = clip;
                    return childState.state;
                }
            }
            // Add new state
            var state = rootStateMachine.AddState(name);
            state.motion = clip;
            return state;
        }

        // Add states
        var walkState = GetOrAddState("Walk", "Assets/Models/Huy_seo/Strut Walking.fbx");
        var fastRunState = GetOrAddState("FastRun", "Assets/Models/Huy_seo/HuySeoFastRun.fbx");
        var openingState = GetOrAddState("HuySeoOpening", "Assets/Models/Huy_seo/HuySeoOpening.fbx");
        var leftTurnState = GetOrAddState("HuySeoLeftTurn", "Assets/Models/Huy_seo/HuySeoLeftTurn.fbx");
        var talkingState = GetOrAddState("Talking", "Assets/Models/Huy_seo/HuySeoLeftTurn.fbx"); // Reusing LeftTurn as talking
        var reactingState = GetOrAddState("Reacting", "Assets/Models/Huy_seo/HuySeoReacting.fbx");
        var throwState = GetOrAddState("Throw", "Assets/Models/Huy_seo/HuySeoThrow.fbx");

        // Set default state
        rootStateMachine.defaultState = walkState;

        // Clear existing any state transitions to avoid duplication
        for (int i = rootStateMachine.anyStateTransitions.Length - 1; i >= 0; i--)
        {
            rootStateMachine.RemoveAnyStateTransition(rootStateMachine.anyStateTransitions[i]);
        }

        // Add transitions
        var reactTransition = rootStateMachine.AddAnyStateTransition(reactingState);
        reactTransition.AddCondition(AnimatorConditionMode.If, 0, "toReact");
        reactTransition.duration = 0.1f;

        var throwTransition = rootStateMachine.AddAnyStateTransition(throwState);
        throwTransition.AddCondition(AnimatorConditionMode.If, 0, "toThrow");
        throwTransition.duration = 0.1f;

        // Transitions back to Walk
        bool hasReactToWalk = false;
        foreach (var t in reactingState.transitions)
        {
            if (t.destinationState == walkState) hasReactToWalk = true;
        }
        if (!hasReactToWalk)
        {
            var t = reactingState.AddTransition(walkState);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.2f;
        }

        bool hasThrowToWalk = false;
        foreach (var t in throwState.transitions)
        {
            if (t.destinationState == walkState) hasThrowToWalk = true;
        }
        if (!hasThrowToWalk)
        {
            var t = throwState.AddTransition(walkState);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.2f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("=== Huy Seo Animator Controller Built Successfully ===");
    }
}
