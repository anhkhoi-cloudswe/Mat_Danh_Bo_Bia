using UnityEngine;
using UnityEditor;
using System.IO;

public class SetupHuySeoAnimations
{
    [MenuItem("Tools/Setup Huy Seo Animations")]
    public static void ConfigureAnimations()
    {
        Debug.Log("=== Starting Huy Seo Animations Setup ===");

        string folderPath = "Assets/Models/Huy_seo";
        string baseFbxName = "HuySeoOpening.fbx";
        string baseFbxPath = $"{folderPath}/{baseFbxName}";

        // 1. Configure the base FBX to create its own Avatar
        ModelImporter baseImporter = AssetImporter.GetAtPath(baseFbxPath) as ModelImporter;
        if (baseImporter == null)
        {
            Debug.LogError($"Could not find base model FBX at: {baseFbxPath}");
            return;
        }

        baseImporter.animationType = ModelImporterAnimationType.Human;
        baseImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        baseImporter.SaveAndReimport();
        Debug.Log($"Configured base model {baseFbxName} as Humanoid (Create From This Model).");

        // 2. Load the generated Avatar
        Avatar baseAvatar = null;
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(baseFbxPath);
        foreach (var asset in subAssets)
        {
            if (asset is Avatar)
            {
                baseAvatar = asset as Avatar;
                break;
            }
        }

        if (baseAvatar == null)
        {
            Debug.LogError($"Failed to generate or load Avatar from base model: {baseFbxPath}");
            return;
        }
        Debug.Log($"Successfully loaded base Avatar: {baseAvatar.name}");

        // 3. Define the files and their settings
        string[] fbxFiles = new string[]
        {
            "HuySeoFastRun.fbx",
            "HuySeoLeftTurn.fbx",
            "HuySeoOpening.fbx",
            "HuySeoReacting.fbx",
            "HuySeoThrow.fbx"
        };

        foreach (string fileName in fbxFiles)
        {
            string path = $"{folderPath}/{fileName}";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Could not find FBX at: {path}");
                continue;
            }

            // Rig settings
            importer.animationType = ModelImporterAnimationType.Human;
            if (fileName == baseFbxName)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = baseAvatar;
            }

            // Animation settings (Bake Into Pose and Loop Time)
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    // Bake Into Pose for In-Place motion
                    clips[i].lockRootPositionXZ = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].lockRootRotation = true;

                    // Keep original settings for positions
                    clips[i].keepOriginalPositionXZ = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].keepOriginalOrientation = true;

                    // Loop Settings
                    if (fileName.Contains("FastRun"))
                    {
                        clips[i].loopTime = true;
                    }
                    else
                    {
                        clips[i].loopTime = false;
                    }
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            Debug.Log($"Configured humanoid rig and clips for: {fileName}");

            // 4. Extract Animation Clip to standalone .anim file
            ExtractAnimationClip(path, folderPath, fileName);
        }

        Debug.Log("=== Saving Assets and Project ===");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== Completed Huy Seo Animations Setup successfully ===");
    }

    private static void ExtractAnimationClip(string fbxPath, string folderPath, string fileName)
    {
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip originalClip = null;

        foreach (var asset in subAssets)
        {
            if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
            {
                originalClip = asset as AnimationClip;
                break;
            }
        }

        if (originalClip == null)
        {
            Debug.LogWarning($"No animation clip found inside FBX: {fileName}");
            return;
        }

        string animClipName = Path.GetFileNameWithoutExtension(fileName);
        string savePath = $"{folderPath}/{animClipName}.anim";

        // Create standalone AnimationClip
        AnimationClip extractedClip = Object.Instantiate(originalClip);

        // Ensure name is clean
        extractedClip.name = animClipName;

        AssetDatabase.CreateAsset(extractedClip, savePath);
        Debug.Log($"Extracted AnimationClip to: {savePath}");
    }
}
