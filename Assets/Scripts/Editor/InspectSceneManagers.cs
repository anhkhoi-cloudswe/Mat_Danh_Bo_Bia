using UnityEditor;
using UnityEngine;
using System.Text;

[InitializeOnLoad]
public static class InspectSceneManagers
{
    static InspectSceneManagers()
    {
        EditorApplication.delayCall += RunInspection;
    }

    [MenuItem("Tools/Inspect Scene Managers Custom")]
    public static void RunInspection()
    {
        Debug.Log("=== [InspectSceneManagers] Starting Diagnostic Scan ===");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Manager Diagnostic Scan ===");

        // 1. Inspect CustomerManager
        CustomerManager cm = Object.FindAnyObjectByType<CustomerManager>();
        if (cm != null)
        {
            sb.AppendLine("[CustomerManager] found:");
            sb.AppendLine($"  - enabled: {cm.enabled}");
            sb.AppendLine($"  - gameObject active: {cm.gameObject.activeInHierarchy}");
            
            // Access fields via SerializedObject to bypass private protection
            SerializedObject so = new SerializedObject(cm);
            sb.AppendLine($"  - autoStartOnPlay: {so.FindProperty("autoStartOnPlay")?.boolValue}");
            sb.AppendLine($"  - initialSpawnDelay: {so.FindProperty("initialSpawnDelay")?.floatValue}");
            sb.AppendLine($"  - spawnIntervalMin: {so.FindProperty("spawnIntervalMin")?.floatValue}");
            sb.AppendLine($"  - spawnIntervalMax: {so.FindProperty("spawnIntervalMax")?.floatValue}");
            
            var spawnPointProp = so.FindProperty("spawnPoint");
            sb.AppendLine($"  - spawnPoint: {(spawnPointProp?.objectReferenceValue != null ? spawnPointProp.objectReferenceValue.name : "null")}");
            
            var targetQueuePointProp = so.FindProperty("targetQueuePoint");
            sb.AppendLine($"  - targetQueuePoint: {(targetQueuePointProp?.objectReferenceValue != null ? targetQueuePointProp.objectReferenceValue.name : "null")}");
            
            var prefabsProp = so.FindProperty("customerPrefabs");
            if (prefabsProp != null)
            {
                sb.AppendLine($"  - customerPrefabs array size: {prefabsProp.arraySize}");
                for (int i = 0; i < prefabsProp.arraySize; i++)
                {
                    var elem = prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    sb.AppendLine($"    - [{i}]: {(elem != null ? elem.name : "NULL/MISSING")}");
                }
            }
            else
            {
                sb.AppendLine("  - customerPrefabs property not found!");
            }
        }
        else
        {
            sb.AppendLine("[CustomerManager] NOT found in active scene!");
        }

        // 2. Inspect StoryPhase1Manager
        StoryPhase1Manager s1 = Object.FindAnyObjectByType<StoryPhase1Manager>();
        if (s1 != null)
        {
            sb.AppendLine("[StoryPhase1Manager] found:");
            sb.AppendLine($"  - enabled: {s1.enabled}");
            sb.AppendLine($"  - currentTime: {s1.currentTime}");
            sb.AppendLine($"  - currentState: {s1.CurrentState}");
            
            SerializedObject so = new SerializedObject(s1);
            var playerProp = so.FindProperty("player");
            sb.AppendLine($"  - player: {(playerProp?.objectReferenceValue != null ? playerProp.objectReferenceValue.name : "null")}");
            
            var cameraProp = so.FindProperty("storyCamera");
            sb.AppendLine($"  - storyCamera: {(cameraProp?.objectReferenceValue != null ? cameraProp.objectReferenceValue.name : "null")}");
            
            var disableProp = so.FindProperty("disableDuringCutscene");
            if (disableProp != null)
            {
                sb.AppendLine($"  - disableDuringCutscene array size: {disableProp.arraySize}");
                for (int i = 0; i < disableProp.arraySize; i++)
                {
                    var elem = disableProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    sb.AppendLine($"    - [{i}]: {(elem != null ? elem.GetType().Name + " on " + ((Component)elem).gameObject.name : "NULL")}");
                }
            }
        }
        else
        {
            sb.AppendLine("[StoryPhase1Manager] NOT found in active scene!");
        }

        // 3. Inspect BoBiaMechanic
        BoBiaMechanic bm = Object.FindAnyObjectByType<BoBiaMechanic>();
        if (bm != null)
        {
            sb.AppendLine("[BoBiaMechanic] found:");
            sb.AppendLine($"  - enabled: {bm.enabled}");
            
            SerializedObject so = new SerializedObject(bm);
            var playerTransformProp = so.FindProperty("playerTransform");
            sb.AppendLine($"  - playerTransform: {(playerTransformProp?.objectReferenceValue != null ? playerTransformProp.objectReferenceValue.name : "null")}");
            
            var stallCenterProp = so.FindProperty("stallCenter");
            sb.AppendLine($"  - stallCenter: {(stallCenterProp?.objectReferenceValue != null ? stallCenterProp.objectReferenceValue.name : "null")}");
            
            sb.AppendLine($"  - CanStartRolling: {bm.CanStartRolling}");
            sb.AppendLine($"  - IsPlayerNearby: {bm.IsPlayerNearby}");
        }
        else
        {
            sb.AppendLine("[BoBiaMechanic] NOT found in active scene!");
        }

        Debug.Log(sb.ToString());
        Debug.Log("=== [InspectSceneManagers] Diagnostic Scan Complete ===");
    }
}
