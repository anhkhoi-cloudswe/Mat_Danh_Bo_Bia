using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Script Editor một lần: xây lại Animator Controller cho Huy Sẹo với đầy đủ
/// states (Idle, Walk, FastRun, HuySeoOpening, HuySeoLeftTurn, Talking, React, Throw)
/// và triggers (toReact, toThrow) đúng theo yêu cầu của StoryPhase1Manager.
///
/// Chạy qua menu: Tools → RebuildHuySeoAnimatorController
/// </summary>
public static class RebuildHuySeoAnimatorController
{
    [MenuItem("Tools/Rebuild Huy Seo Animator Controller")]
    public static void Rebuild()
    {
        const string controllerPath = "Assets/Animators/Huy_seo_Walk.controller";

        // Load tất cả AnimationClip cần thiết
        AnimationClip clipFastRun  = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeoFastRun.anim");
        AnimationClip clipOpening  = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeoOpening.anim");
        AnimationClip clipLeftTurn = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeoLeftTurn.anim");
        AnimationClip clipReacting = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeoReacting.anim");
        AnimationClip clipThrow    = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Nhan_Vat_Phu/Huy_seo/HuySeoThrow.anim");

        // Walk clip lấy từ Strut Walking.fbx
        AnimationClip clipWalk = null;
        var walkAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Nhan_Vat_Phu/Huy_seo/Strut Walking.fbx");
        foreach (var a in walkAssets)
        {
            if (a is AnimationClip ac && !ac.name.Contains("__preview__"))
            {
                clipWalk = ac;
                break;
            }
        }

        // Clip Idle dùng tạm HuySeoOpening (đứng yên giả định) nếu không có Idle riêng
        AnimationClip clipIdle = clipOpening;

        // Clip Talking dùng HuySeoOpening (đứng yên quay mặt) làm placeholder
        AnimationClip clipTalking = clipOpening;

        Debug.Log("[RebuildHuySeoAnimatorController] Clips loaded:");
        Debug.Log($"  FastRun: {(clipFastRun != null ? "OK" : "MISSING")}");
        Debug.Log($"  Opening: {(clipOpening != null ? "OK" : "MISSING")}");
        Debug.Log($"  LeftTurn: {(clipLeftTurn != null ? "OK" : "MISSING")}");
        Debug.Log($"  Reacting: {(clipReacting != null ? "OK" : "MISSING")}");
        Debug.Log($"  Throw: {(clipThrow != null ? "OK" : "MISSING")}");
        Debug.Log($"  Walk: {(clipWalk != null ? "OK" : "MISSING")}");

        // Tạo hoặc load lại controller
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        else
        {
            // Xoá sạch states và parameters cũ
            var rootSM = controller.layers[0].stateMachine;
            // Xoá toàn bộ states
            var states = new System.Collections.Generic.List<AnimatorState>(rootSM.states.Length);
            foreach (var cs in rootSM.states) states.Add(cs.state);
            foreach (var st in states) rootSM.RemoveState(st);

            // Xoá parameters cũ
            while (controller.parameters.Length > 0)
                controller.RemoveParameter(0);
        }

        // === THÊM PARAMETERS ===
        controller.AddParameter("toReact", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("toThrow", AnimatorControllerParameterType.Trigger);

        // === LẤY ROOT STATE MACHINE ===
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // === TẠO STATES ===
        // Vị trí lưới trong Animator window cho dễ nhìn
        AnimatorState stateIdle       = sm.AddState("Idle",           new Vector3(200,  0,  0));
        AnimatorState stateWalk       = sm.AddState("Walk",           new Vector3(200, 80,  0));
        AnimatorState stateFastRun    = sm.AddState("FastRun",        new Vector3(200, 160, 0));
        AnimatorState stateOpening    = sm.AddState("HuySeoOpening",  new Vector3(450,  0,  0));
        AnimatorState stateLeftTurn   = sm.AddState("HuySeoLeftTurn", new Vector3(450, 80,  0));
        AnimatorState stateTalking    = sm.AddState("Talking",        new Vector3(450, 160, 0));
        AnimatorState stateReact      = sm.AddState("React",          new Vector3(700,  0,  0));
        AnimatorState stateThrow      = sm.AddState("Throw",          new Vector3(700, 80,  0));

        // Gắn clips
        stateIdle.motion      = clipIdle;
        stateWalk.motion      = clipWalk;
        stateFastRun.motion   = clipFastRun;
        stateOpening.motion   = clipOpening;
        stateLeftTurn.motion  = clipLeftTurn;
        stateTalking.motion   = clipTalking;
        stateReact.motion     = clipReacting;
        stateThrow.motion     = clipThrow;

        // State mặc định là Idle
        sm.defaultState = stateIdle;

        // === TRANSITIONS ===
        // Từ Idle: toReact → React
        AddTriggerTransition(sm, stateIdle, stateReact, "toReact");
        // Từ Idle ↔ Walk
        AddInstantTransition(stateIdle, stateWalk);
        AddInstantTransition(stateWalk, stateIdle);
        // Walk → FastRun (không cần trigger, CrossFade từ code)
        AddInstantTransition(stateWalk, stateFastRun);
        AddInstantTransition(stateFastRun, stateWalk);
        AddInstantTransition(stateFastRun, stateIdle);
        // Idle → HuySeoOpening
        AddInstantTransition(stateIdle, stateOpening);
        AddInstantTransition(stateOpening, stateIdle);
        // Idle → HuySeoLeftTurn
        AddInstantTransition(stateIdle, stateLeftTurn);
        AddInstantTransition(stateLeftTurn, stateIdle);
        // HuySeoOpening ↔ Talking
        AddInstantTransition(stateOpening, stateTalking);
        AddInstantTransition(stateTalking, stateOpening);
        // React → Idle (react xong quay idle)
        AddExitTimeTransition(stateReact, stateIdle);
        // Idle → Throw (trigger toThrow)
        AddTriggerTransition(sm, stateIdle, stateThrow, "toThrow");
        // Throw → Idle (khi xong)
        AddExitTimeTransition(stateThrow, stateIdle);

        // Lưu asset
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[RebuildHuySeoAnimatorController] ✅ Done! Animator controller rebuilt at: " + controllerPath);
        Debug.Log("States added: Idle, Walk, FastRun, HuySeoOpening, HuySeoLeftTurn, Talking, React, Throw");
        Debug.Log("Triggers added: toReact, toThrow");
    }

    private static void AddInstantTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.offset = 0f;
    }

    private static void AddExitTimeTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.1f;
    }

    private static void AddTriggerTransition(AnimatorStateMachine sm, AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }
}
