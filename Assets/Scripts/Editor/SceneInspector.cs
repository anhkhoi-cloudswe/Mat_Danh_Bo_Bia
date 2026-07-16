// Force compile clean configure v2
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

[InitializeOnLoad]
public class SceneInspector
{
    static SceneInspector()
    {
        EditorApplication.delayCall += InspectScene;
    }

    [MenuItem("Tools/Inspect Scene Structure")]
    public static void InspectScene()
    {
        Debug.Log("[SceneInspector] Running scene analysis...");
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== SCENE COMPONENT INSPECTION ===");

        // Find AnhBoBia GameObject
        GameObject parentObj = GameObject.Find("AnhBoBia");
        if (parentObj != null)
        {
            sb.AppendLine($"Parent Object: '{parentObj.name}'");
            sb.AppendLine($"  Position: {parentObj.transform.position}");
            sb.AppendLine($"  LocalPosition: {parentObj.transform.localPosition}");
            sb.AppendLine("  Components:");
            foreach (var comp in parentObj.GetComponents<Component>())
            {
                if (comp != null)
                {
                    sb.AppendLine($"    - {comp.GetType().Name}");
                    if (comp is CharacterController cc)
                    {
                        sb.AppendLine($"      * Center: {cc.center}");
                        sb.AppendLine($"      * Height: {cc.height}");
                        sb.AppendLine($"      * Radius: {cc.radius}");
                        sb.AppendLine($"      * Enabled: {cc.enabled}");
                    }
                }
            }

            // Inspect its children
            for (int i = 0; i < parentObj.transform.childCount; i++)
            {
                Transform child = parentObj.transform.GetChild(i);
                sb.AppendLine($"  Child: '{child.name}'");
                sb.AppendLine($"    LocalPosition: {child.localPosition}");
                sb.AppendLine($"    LocalRotation: {child.localRotation.eulerAngles}");
                sb.AppendLine("    Components:");
                foreach (var comp in child.GetComponents<Component>())
                {
                    if (comp != null)
                    {
                        sb.AppendLine($"      - {comp.GetType().Name}");
                        if (comp is Animator animator)
                        {
                            sb.AppendLine($"        * Apply Root Motion: {animator.applyRootMotion}");
                            sb.AppendLine($"        * Avatar: {(animator.avatar != null ? animator.avatar.name : "null")}");
                        }
                    }
                }
            }
        }
        else
        {
            sb.AppendLine("GameObject 'AnhBoBia' not found in root.");
        }

        // Also check Player named Meshy...
        GameObject playerObj = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (playerObj != null)
        {
            sb.AppendLine($"\nPlayer Object: '{playerObj.name}'");
            sb.AppendLine($"  Position: {playerObj.transform.position}");
            sb.AppendLine($"  LocalPosition: {playerObj.transform.localPosition}");
            sb.AppendLine($"  Parent: {(playerObj.transform.parent != null ? playerObj.transform.parent.name : "None")}");
            sb.AppendLine("  Components:");
            foreach (var comp in playerObj.GetComponents<Component>())
            {
                if (comp != null)
                {
                    sb.AppendLine($"    - {comp.GetType().Name}");
                    if (comp is CharacterController cc)
                    {
                        sb.AppendLine($"      * Center: {cc.center}");
                        sb.AppendLine($"      * Height: {cc.height}");
                        sb.AppendLine($"      * Radius: {cc.radius}");
                        sb.AppendLine($"      * Enabled: {cc.enabled}");
                    }
                    if (comp is Animator animator)
                    {
                        sb.AppendLine($"      * Apply Root Motion: {animator.applyRootMotion}");
                        sb.AppendLine($"      * Avatar: {(animator.avatar != null ? animator.avatar.name : "null")}");
                    }
                }
            }
        }

        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "scene_inspection.txt");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        Debug.Log("[SceneInspector] Inspection finished! Saved to: " + outputPath);
    }
}
