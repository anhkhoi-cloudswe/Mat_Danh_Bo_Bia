using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public class PlayerStateInspector
{
    static PlayerStateInspector()
    {
        EditorApplication.delayCall += InspectPlayerAndAnimations;
    }

    [MenuItem("Tools/Inspect Player and Animations")]
    public static void InspectPlayerAndAnimations()
    {
        Debug.Log("[PlayerStateInspector] Starting detailed inspection...");

        InspectionResult result = new InspectionResult();

        // 1. Inspect Player GameObject in Hierarchy
        GameObject player = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (player != null)
        {
            result.playerFound = true;
            result.playerPos = player.transform.position;
            result.playerLocalPos = player.transform.localPosition;
            
            // Check Parent
            if (player.transform.parent != null)
            {
                result.parentName = player.transform.parent.name;
                result.parentPos = player.transform.parent.position;
            }

            // Check CharacterController
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null && player.transform.parent != null)
            {
                cc = player.transform.parent.GetComponent<CharacterController>();
            }

            if (cc != null)
            {
                result.hasCC = true;
                result.ccAttachedTo = cc.gameObject.name;
                result.ccCenter = cc.center;
                result.ccHeight = cc.height;
                result.ccRadius = cc.radius;
                result.ccEnabled = cc.enabled;
            }

            // Check Hips bone
            Transform hips = FindDeepChild(player.transform, "Hips");
            if (hips != null)
            {
                result.hasHips = true;
                result.hipsLocalPos = hips.localPosition;
                result.hipsPos = hips.position;
            }

            // Check Animator
            Animator animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                result.hasAnimator = true;
                result.applyRootMotion = animator.applyRootMotion;
                if (animator.avatar != null)
                {
                    result.avatarName = animator.avatar.name;
                    result.avatarValid = animator.avatar.isValid;
                    result.avatarHuman = animator.avatar.isHuman;
                }
            }
        }

        // 2. Inspect imported models in Assets/Models
        string[] guids = AssetDatabase.FindAssets("t:ModelImporter");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Models")) continue;

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                ModelInfo modelInfo = new ModelInfo();
                modelInfo.fileName = Path.GetFileName(path);
                modelInfo.animationType = importer.animationType.ToString();

                var clips = importer.clipAnimations;
                if (clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }

                foreach (var clip in clips)
                {
                    ClipInfo clipInfo = new ClipInfo();
                    clipInfo.name = clip.name;
                    clipInfo.loopTime = clip.loopTime;
                    clipInfo.bakeIntoPoseY = clip.lockRootHeightY;
                    clipInfo.basedUponY = clip.keepOriginalPositionY ? "Original" : (clip.heightFromFeet ? "Feet" : "CenterOfMass");
                    clipInfo.offsetY = clip.heightOffset;
                    modelInfo.clips.Add(clipInfo);
                }

                result.models.Add(modelInfo);
            }
        }

        string json = JsonUtility.ToJson(result, true);
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "player_inspection_results.json");
        File.WriteAllText(outputPath, json);
        Debug.Log("[PlayerStateInspector] Inspection complete! Saved to: " + outputPath);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    [System.Serializable]
    public class InspectionResult
    {
        public bool playerFound = false;
        public Vector3 playerPos;
        public Vector3 playerLocalPos;
        public string parentName = "";
        public Vector3 parentPos;
        public bool hasCC = false;
        public string ccAttachedTo = "";
        public Vector3 ccCenter;
        public float ccHeight;
        public float ccRadius;
        public bool ccEnabled;
        public bool hasHips = false;
        public Vector3 hipsLocalPos;
        public Vector3 hipsPos;
        public bool hasAnimator = false;
        public bool applyRootMotion = false;
        public string avatarName = "";
        public bool avatarValid = false;
        public bool avatarHuman = false;
        public List<ModelInfo> models = new List<ModelInfo>();
    }

    [System.Serializable]
    public class ModelInfo
    {
        public string fileName;
        public string animationType;
        public List<ClipInfo> clips = new List<ClipInfo>();
    }

    [System.Serializable]
    public class ClipInfo
    {
        public string name;
        public bool loopTime;
        public bool bakeIntoPoseY;
        public string basedUponY;
        public float offsetY;
    }
}
