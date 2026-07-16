using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Text;

[InitializeOnLoad]
public class AnimatorInspector
{
    static AnimatorInspector()
    {
        EditorApplication.delayCall += InspectAnimator;
    }

    [MenuItem("Tools/Inspect Animator Controller")]
    public static void InspectAnimator()
    {
        Debug.Log("[AnimatorInspector] Starting Animator Controller scan...");
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== ANIMATOR CONTROLLER INSPECTION ===");

        // Find Animator Controller asset
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Animators")) continue;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
            {
                sb.AppendLine($"Animator Controller: '{controller.name}' at path '{path}'");
                foreach (var layer in controller.layers)
                {
                    sb.AppendLine($"  Layer: '{layer.name}'");
                    InspectStateMachine(layer.stateMachine, "    ", sb);
                }
            }
        }

        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "animator_inspection.txt");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        Debug.Log("[AnimatorInspector] Inspection complete! Saved to: " + outputPath);
    }

    private static void InspectStateMachine(AnimatorStateMachine stateMachine, string indent, StringBuilder sb)
    {
        sb.AppendLine($"{indent}StateMachine: '{stateMachine.name}'");
        foreach (var state in stateMachine.states)
        {
            sb.AppendLine($"{indent}  State: '{state.state.name}'");
            Motion motion = state.state.motion;
            if (motion != null)
            {
                sb.AppendLine($"{indent}    Motion Type: {motion.GetType().Name}");
                sb.AppendLine($"{indent}    Motion Name: '{motion.name}'");
                if (motion is BlendTree blendTree)
                {
                    sb.AppendLine($"{indent}    BlendTree Parameter: '{blendTree.blendParameter}'");
                    foreach (var child in blendTree.children)
                    {
                        sb.AppendLine($"{indent}      Child Motion: '{child.motion?.name}' (Threshold: {child.threshold})");
                    }
                }
            }
            else
            {
                sb.AppendLine($"{indent}    Motion: null");
            }
        }

        foreach (var subMachine in stateMachine.stateMachines)
        {
            InspectStateMachine(subMachine.stateMachine, indent + "  ", sb);
        }
    }
}
