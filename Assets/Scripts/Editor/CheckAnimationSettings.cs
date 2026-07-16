using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

[InitializeOnLoad]
public class CheckAnimationSettings : EditorWindow
{
    static CheckAnimationSettings()
    {
        EditorApplication.delayCall += () => {
            if (!SessionState.GetBool("AnimationDiagnosticsRun", false))
            {
                CheckSettings();
                SessionState.SetBool("AnimationDiagnosticsRun", true);
            }
        };
    }

    [MenuItem("Tools/Check Animation and Animator Settings")]
    public static void CheckSettings()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== Animator and Animation Diagnostic Report ===");

        // Define characters to check
        CheckCharacter("Huy_seo", "Assets/Models/Nhan_Vat_Phu/Huy_seo", "HuySeo@Walking.fbx", "HuySeo@Walking.fbx", "Huy_seo", report);
        CheckCharacter("Nganpc", "Assets/Models/Nhan_Vat_Phu/Nganpc", "BaNga@Walking.fbx", "BaNga@Walking.fbx", "Nganpc", report);
        CheckCharacter("Npc1", "Assets/Models/Nhan_Vat_Phu/Messi_Fan", "MessiFan@Standing_Idle.fbx", "Messi_Fan@Walking.fbx", "codongvien", report);
        CheckCharacter("npc2", "Assets/Models/Nhan_Vat_Phu/npc2", "Meshy_AI_Open_Arms_in_Denim_0610094857_texture.fbx", "npc2", "npc2", report);
        CheckCharacter("shipper", "Assets/Models/Nhan_Vat_Phu/shipper", "Meshy_AI_Grab_Rider_in_T_Pose_0611034406_texture.fbx", "Joggingshipper@Jog Forward.fbx", "shipper", report);
        CheckCharacter("anh_aoxanh", "Assets/Models/Nhan_Vat_Phu/anh_aoxanh/Meshy_AI_T_Pose_Avatar_0612101506_texture_fbx", "Meshy_AI_T_Pose_Avatar_0612101506_texture.fbx", "aoxanhwalking.fbx", "anh_aoxanh", report);

        Debug.Log(report.ToString());
    }

    private static void CheckCharacter(string name, string folderPath, string baseFbxName, string animFbxName, string sceneKeyword, StringBuilder report)
    {
        report.AppendLine($"\n--- Character: {name} ---");

        // 1. Check Base Mesh FBX Importer settings
        string baseFbxPath = $"{folderPath}/{baseFbxName}";
        ModelImporter baseImporter = AssetImporter.GetAtPath(baseFbxPath) as ModelImporter;
        if (baseImporter != null)
        {
            report.AppendLine($"  Base Model: {baseFbxName}");
            report.AppendLine($"    Animation Type: {baseImporter.animationType} ({(baseImporter.animationType == ModelImporterAnimationType.Human ? "OK - Humanoid" : "WARNING - Should be Humanoid")})");
        }
        else
        {
            report.AppendLine($"  Base Model: {baseFbxName} (ERROR - File not found!)");
        }

        // 2. Check Animation FBX Importer settings
        if (!string.IsNullOrEmpty(animFbxName))
        {
            string animFbxPath = $"{folderPath}/{animFbxName}";
            ModelImporter animImporter = AssetImporter.GetAtPath(animFbxPath) as ModelImporter;
            if (animImporter != null)
            {
                report.AppendLine($"  Animation Model: {animFbxName}");
                report.AppendLine($"    Animation Type: {animImporter.animationType} ({(animImporter.animationType == ModelImporterAnimationType.Human ? "OK - Humanoid" : "WARNING - Should be Humanoid")})");

                // Check Clip settings (loop time)
                var clips = animImporter.clipAnimations;
                if (clips == null || clips.Length == 0) clips = animImporter.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    foreach (var clip in clips)
                    {
                        report.AppendLine($"    Clip '{clip.name}': Loop Time = {clip.loopTime} ({(clip.loopTime ? "OK" : "WARNING - Should be looping")})");
                    }
                }
                else
                {
                    report.AppendLine("    Clips: None found (WARNING)");
                }
            }
            else
            {
                report.AppendLine($"  Animation Model: {animFbxName} (ERROR - File not found!)");
            }
        }
        else
        {
            report.AppendLine("  Animation Model: None (Static character or animation not defined)");
        }

        // 3. Check scene GameObjects
        GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
        bool foundInScene = false;
        foreach (var go in sceneObjects)
        {
            if (go.name.IndexOf(sceneKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foundInScene = true;
                report.AppendLine($"  Active GameObject in Scene: '{go.name}'");
                
                Animator animator = go.GetComponent<Animator>();
                if (animator != null)
                {
                    report.AppendLine($"    Animator Component: Found");
                    report.AppendLine($"    Animator Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "WARNING - None assigned!")}");
                    report.AppendLine($"    Avatar: {(animator.avatar != null ? animator.avatar.name : "WARNING - None assigned!")}");
                }
                else
                {
                    report.AppendLine("    Animator Component: WARNING - Missing!");
                }
            }
        }

        if (!foundInScene)
        {
            report.AppendLine("  Scene GameObject: Not found in current active scene (WARNING)");
        }
    }
}
