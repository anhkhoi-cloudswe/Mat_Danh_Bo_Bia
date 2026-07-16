using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor tự động thiết lập mini-game LỤC SOÁT ĐỐNG RÁC (Phase 1).
/// Bấm menu "Tools/Setup Trash Investigation (Phase 1)" để:
///   - Tạo/tìm GameObject "_TrashInvestigation" + component <see cref="TrashInvestigation"/>.
///   - Tự gán 2 prefab vật chứng (syringe.prefab, bichmaithuy.prefab) qua SerializedObject.
///   - Gán mốc bãi rác (BaiRac_ThungRac), PlayerMovement, StoryPhase1Manager.
///   - Nối ngược tham chiếu trashInvestigation vào _StoryPhase1Manager.
///   - Đánh dấu dirty + lưu Scene.
///
/// Các field private [SerializeField] được gán an toàn qua SerializedObject (đúng chuẩn Editor).
/// </summary>
public static class SetupTrashInvestigation
{
    private const string InvestigationObjectName = "_TrashInvestigation";
    private const string ManagerObjectName = "_StoryPhase1Manager";
    private const string SyringePrefabPath = "Assets/Prefabs/syringe.prefab";
    private const string DrugBagPrefabPath = "Assets/Prefabs/bichmaithuy.prefab";

    [MenuItem("Tools/Setup Trash Investigation (Phase 1)")]
    public static void Setup()
    {
        Debug.Log("=== [SetupTrashInvestigation] Bắt đầu thiết lập mini-game bới rác ===");

        // --- Manager ---
        GameObject managerGO = FindInScene(ManagerObjectName);
        if (managerGO == null)
        {
            EditorUtility.DisplayDialog("Lỗi",
                $"Không tìm thấy '{ManagerObjectName}' trong Scene. Hãy chạy 'Tools/Full Automate Story Phase 1' trước.", "OK");
            return;
        }
        StoryPhase1Manager manager = managerGO.GetComponent<StoryPhase1Manager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Lỗi", $"'{ManagerObjectName}' chưa có component StoryPhase1Manager.", "OK");
            return;
        }

        // --- Tạo/tìm GameObject _TrashInvestigation ---
        GameObject investigationGO = FindInScene(InvestigationObjectName);
        if (investigationGO == null)
        {
            investigationGO = new GameObject(InvestigationObjectName);
            Undo.RegisterCreatedObjectUndo(investigationGO, "Create _TrashInvestigation");
            Debug.Log($"[SetupTrashInvestigation] Đã tạo GameObject '{InvestigationObjectName}'.");
        }

        TrashInvestigation investigation = investigationGO.GetComponent<TrashInvestigation>();
        if (investigation == null)
        {
            investigation = investigationGO.AddComponent<TrashInvestigation>();
            Debug.Log("[SetupTrashInvestigation] Đã thêm component TrashInvestigation.");
        }

        // --- Mốc bãi rác ---
        GameObject trash = FindFirstInScene("BaiRac_ThungRac", "thungrac", "thungrac1");
        if (trash != null)
        {
            // Đặt _TrashInvestigation tại vị trí đống rác cho gọn hierarchy
            investigationGO.transform.position = trash.transform.position;
        }
        else
        {
            Debug.LogWarning("[SetupTrashInvestigation] Không tìm thấy GameObject bãi rác (BaiRac_ThungRac).");
        }

        // --- Prefab vật chứng ---
        GameObject syringe = AssetDatabase.LoadAssetAtPath<GameObject>(SyringePrefabPath);
        GameObject drugBag = AssetDatabase.LoadAssetAtPath<GameObject>(DrugBagPrefabPath);
        if (syringe == null) Debug.LogWarning($"[SetupTrashInvestigation] Không tìm thấy prefab '{SyringePrefabPath}'.");
        if (drugBag == null) Debug.LogWarning($"[SetupTrashInvestigation] Không tìm thấy prefab '{DrugBagPrefabPath}'.");

        // --- PlayerMovement ---
        PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null) Debug.LogWarning("[SetupTrashInvestigation] Không tìm thấy PlayerMovement trong Scene.");

        // --- Gán field qua SerializedObject ---
        SerializedObject so = new SerializedObject(investigation);
        SetRef(so, "syringePrefab", syringe);
        SetRef(so, "drugBagPrefab", drugBag);
        if (trash != null)
        {
            SetRef(so, "trashAnchor", trash.transform);
            SetRef(so, "spawnPoint", trash.transform);
        }
        if (playerMovement != null) SetRef(so, "playerMovement", playerMovement);
        SetRef(so, "storyManager", manager);

        // phase2TransitionZone: cố gắng tìm nếu đã có sẵn (chưa có cũng không sao)
        GameObject phase2 = FindFirstInScene("Phase2_Transition", "Phase2TransitionZone", "_Phase2_Transition");
        if (phase2 != null)
        {
            SetRef(so, "phase2TransitionZone", phase2);
            Debug.Log($"[SetupTrashInvestigation] Đã gán vùng chuyển tiếp Phase 2: '{phase2.name}'.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[SetupTrashInvestigation] Đã gán prefab + tham chiếu cho TrashInvestigation.");

        // --- Nối ngược vào StoryPhase1Manager ---
        SerializedObject soManager = new SerializedObject(manager);
        SetRef(soManager, "trashInvestigation", investigation);
        soManager.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[SetupTrashInvestigation] Đã gán trashInvestigation vào _StoryPhase1Manager.");

        // --- Dirty + Save ---
        EditorUtility.SetDirty(investigation);
        EditorUtility.SetDirty(investigationGO);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("=== [SetupTrashInvestigation] HOÀN TẤT! Scene đã thiết lập & lưu. ===");
        EditorUtility.DisplayDialog("Hoàn tất",
            "Đã thiết lập xong mini-game lục soát đống rác và lưu Scene.\nBấm Play, chơi tới sau khi trả hột quẹt để test nha!",
            "Tuyệt!");
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[SetupTrashInvestigation] Không tìm thấy field '{field}'.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    private static GameObject FindInScene(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform t = FindRecursive(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static GameObject FindFirstInScene(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = FindInScene(n);
            if (go != null) return go;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform r = FindRecursive(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
