using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor script để swap prefab nguyên liệu trong BoBiaMechanic:
///   - Dừa Nạo.prefab   → Dừa bào.prefab
///   - 1 Thanh Kẹo Mạch nha.prefab → Thanh mạch nha.prefab
/// Áp dụng cho tất cả BoBiaMechanic trong scene và project prefabs.
/// </summary>
public class SwapBoBiaPrefabs
{
    private const string OldDuaNaoPath = "Assets/Models/Environment/Props/Making_BoBia/Process_Making_BoBia/Dừa Nạo.prefab";
    private const string NewDuaBaoPath = "Assets/Models/Environment/Props/Making_BoBia/Process_Making_BoBia/Texture Dừa bào/Dừa bào.prefab";

    private const string OldKeoMachNhaPath = "Assets/Models/Environment/Props/Making_BoBia/Process_Making_BoBia/1 Thanh Kẹo Mạch nha.prefab";
    private const string NewThanhMachNhaPath = "Assets/Models/Environment/Props/Making_BoBia/Process_Making_BoBia/Thanh mạch nha.prefab";

    [MenuItem("Tools/Swap BoBia Ingredient Prefabs")]
    public static void SwapPrefabs()
    {
        // Load new prefabs
        GameObject newDuaBao = AssetDatabase.LoadAssetAtPath<GameObject>(NewDuaBaoPath);
        GameObject newThanhMachNha = AssetDatabase.LoadAssetAtPath<GameObject>(NewThanhMachNhaPath);

        if (newDuaBao == null)
        {
            Debug.LogError($"[SwapBoBiaPrefabs] KHÔNG tìm thấy prefab mới tại: {NewDuaBaoPath}");
            return;
        }
        if (newThanhMachNha == null)
        {
            Debug.LogError($"[SwapBoBiaPrefabs] KHÔNG tìm thấy prefab mới tại: {NewThanhMachNhaPath}");
            return;
        }

        // Load old prefabs to detect reference by comparing asset paths
        GameObject oldDuaNao = AssetDatabase.LoadAssetAtPath<GameObject>(OldDuaNaoPath);
        GameObject oldKeoMachNha = AssetDatabase.LoadAssetAtPath<GameObject>(OldKeoMachNhaPath);

        if (oldDuaNao == null)
            Debug.LogWarning($"[SwapBoBiaPrefabs] Không tìm thấy prefab cũ '{OldDuaNaoPath}' — sẽ bỏ qua so sánh theo object, chỉ swap theo GUID.");
        if (oldKeoMachNha == null)
            Debug.LogWarning($"[SwapBoBiaPrefabs] Không tìm thấy prefab cũ '{OldKeoMachNhaPath}' — sẽ bỏ qua so sánh theo object, chỉ swap theo GUID.");

        int totalSwapped = 0;

        // ─── 1. Scan all BoBiaMechanic in the active scene ───────────────────
        BoBiaMechanic[] sceneMechanics = Object.FindObjectsByType<BoBiaMechanic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BoBiaMechanic mech in sceneMechanics)
        {
            bool changed = false;
            SerializedObject so = new SerializedObject(mech);

            changed |= SwapField(so, "prefab_DuaNao", oldDuaNao, newDuaBao);
            changed |= SwapField(so, "prefab_ThanhKeoMachNha", oldKeoMachNha, newThanhMachNha);

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mech);
                totalSwapped++;
                Debug.Log($"[SwapBoBiaPrefabs] Đã swap BoBiaMechanic trên scene object '{mech.gameObject.name}'");
            }
        }

        // ─── 2. Scan all prefab assets in the project containing BoBiaMechanic ──
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null) continue;

            BoBiaMechanic[] mechanics = prefabAsset.GetComponentsInChildren<BoBiaMechanic>(true);
            if (mechanics.Length == 0) continue;

            bool anyChanged = false;
            foreach (BoBiaMechanic mech in mechanics)
            {
                SerializedObject so = new SerializedObject(mech);
                bool changed = false;

                changed |= SwapField(so, "prefab_DuaNao", oldDuaNao, newDuaBao);
                changed |= SwapField(so, "prefab_ThanhKeoMachNha", oldKeoMachNha, newThanhMachNha);

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(mech);
                    anyChanged = true;
                    totalSwapped++;
                    Debug.Log($"[SwapBoBiaPrefabs] Đã swap BoBiaMechanic trong prefab '{path}'");
                }
            }

            if (anyChanged)
            {
                PrefabUtility.SavePrefabAsset(prefabAsset);
            }
        }

        // ─── 3. Save scene if anything changed ───────────────────────────────
        if (totalSwapped > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[SwapBoBiaPrefabs] ✅ Hoàn tất! Đã swap {totalSwapped} BoBiaMechanic component(s).");
            Debug.Log($"  • prefab_DuaNao: '{OldDuaNaoPath}' → '{NewDuaBaoPath}'");
            Debug.Log($"  • prefab_ThanhKeoMachNha: '{OldKeoMachNhaPath}' → '{NewThanhMachNhaPath}'");
            Debug.Log("Nhớ nhấn Ctrl+S để lưu scene lại!");
        }
        else
        {
            Debug.LogWarning("[SwapBoBiaPrefabs] Không tìm thấy BoBiaMechanic nào có prefab cũ cần swap. Có thể field đã được gán đúng hoặc chưa gán gì.");
        }
    }

    /// <summary>
    /// So sánh giá trị field hiện tại với oldRef:
    ///   - Nếu trùng → gán newRef → return true
    ///   - Nếu null → cũng gán newRef (trường hợp chưa assign) → return true  
    ///   - Nếu đã là newRef → bỏ qua
    /// </summary>
    private static bool SwapField(SerializedObject so, string fieldName, GameObject oldRef, GameObject newRef)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[SwapBoBiaPrefabs] Không tìm thấy field '{fieldName}' trong SerializedObject '{so.targetObject.name}'");
            return false;
        }

        GameObject currentVal = prop.objectReferenceValue as GameObject;

        // Already assigned to the new prefab — skip
        if (currentVal == newRef)
            return false;

        // If it matches the old reference OR is null (unassigned), swap to new
        if (currentVal == oldRef || currentVal == null)
        {
            prop.objectReferenceValue = newRef;
            return true;
        }

        return false;
    }
}
