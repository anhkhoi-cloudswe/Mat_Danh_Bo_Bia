using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor tự động hóa 100% việc thiết lập Scene cho kịch bản Phase 1.
/// Bấm menu "Tools/Full Automate Story Phase 1" để tự gán toàn bộ tham chiếu cho
/// component <see cref="StoryPhase1Manager"/> mà KHÔNG cần kéo tay trong Inspector.
///
/// Các field private [SerializeField] được gán an toàn qua SerializedObject
/// (đúng chuẩn Editor, không dùng reflection).
/// </summary>
public static class AutomateStoryPhase1Setup
{
    private const string ManagerObjectName = "_StoryPhase1Manager";
    private const string HuySeoPrefabPath = "Assets/Prefabs/Huy_seo.prefab";

    // Các script điều khiển Player/Camera sẽ bị tắt trong lúc diễn cutscene.
    private static readonly string[] ControlScriptTypeNames =
    {
        "PlayerMovement", "FirstPersonCamera", "ThirdPersonCamera", "CartController"
    };

    [MenuItem("Tools/Set Scene to Morning")]
    public static void SetSceneToMorning()
    {
        GameObject managerGO = FindInScene(ManagerObjectName);
        if (managerGO == null) return;
        StoryPhase1Manager manager = managerGO.GetComponent<StoryPhase1Manager>();
        if (manager == null) return;
        
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty timeProp = so.FindProperty("currentTime");
        if (timeProp != null)
        {
            timeProp.enumValueIndex = (int)StoryPhase1Manager.TimeOfDay.Morning_Day1;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[AutomateStoryPhase1] Set currentTime to Morning_Day1");
        }

        // Set DayNightCycle to noon in the editor as well
        DayNightCycle dayNight = Object.FindAnyObjectByType<DayNightCycle>();
        if (dayNight != null)
        {
            SerializedObject soDN = new SerializedObject(dayNight);
            SerializedProperty timeOfDayProp = soDN.FindProperty("timeOfDay");
            SerializedProperty autoProgressProp = soDN.FindProperty("autoProgress");
            if (timeOfDayProp != null) timeOfDayProp.floatValue = 12f;
            if (autoProgressProp != null) autoProgressProp.boolValue = false;
            soDN.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dayNight);
            Debug.Log("[AutomateStoryPhase1] Set DayNightCycle timeOfDay to 12f (noon)");
        }
        
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(managerGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Full Automate Story Phase 1")]
    public static void AutomateSetup()
    {
        Debug.Log("=== [AutomateStoryPhase1] Bắt đầu tự động thiết lập Scene ===");

        // --- Tìm Manager ---
        GameObject managerGO = FindInScene(ManagerObjectName);
        if (managerGO == null)
        {
            EditorUtility.DisplayDialog("Lỗi",
                $"Không tìm thấy GameObject '{ManagerObjectName}' trong Scene. Hãy tạo nó (kèm component StoryPhase1Manager) trước.",
                "OK");
            return;
        }

        StoryPhase1Manager manager = managerGO.GetComponent<StoryPhase1Manager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Lỗi",
                $"GameObject '{ManagerObjectName}' chưa có component StoryPhase1Manager.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(manager);

        // ===========================================================
        // 1. Ba mốc lộ trình (Points)
        // ===========================================================
        Transform p1 = GetOrCreatePoint("Point1_Alley");
        Transform p2 = GetOrCreatePoint("Point2_TrashArea");
        Transform p3 = GetOrCreatePoint("Point3_House");

        SetRef(so, "point1_Alley", p1);
        SetRef(so, "point2_TrashArea", p2);
        SetRef(so, "point3_House", p3);
        Debug.Log("[AutomateStoryPhase1] Đã gán 3 mốc lộ trình.");

        // ===========================================================
        // 2. Lôi Huy_seo vào Scene + Animator/NavMeshAgent/Hand
        // ===========================================================
        GameObject huySeo = FindInScene("Huy_seo");
        if (huySeo == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HuySeoPrefabPath);
            if (prefab != null)
            {
                huySeo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                huySeo.name = "Huy_seo";
                if (p1 != null) huySeo.transform.position = p1.position;
                Undo.RegisterCreatedObjectUndo(huySeo, "Spawn Huy_seo");
                Debug.Log("[AutomateStoryPhase1] Đã kéo prefab Huy_seo vào Scene.");
            }
            else
            {
                Debug.LogWarning($"[AutomateStoryPhase1] Không tìm thấy prefab tại '{HuySeoPrefabPath}'.");
            }
        }
        else
        {
            Debug.Log("[AutomateStoryPhase1] Huy_seo đã có sẵn trong Scene.");
        }

        if (huySeo != null)
        {
            SetRef(so, "huySeo", huySeo);

            Animator animator = huySeo.GetComponentInChildren<Animator>();
            if (animator != null) SetRef(so, "huySeoAnimator", animator);
            else Debug.LogWarning("[AutomateStoryPhase1] Huy_seo thiếu Animator.");

            NavMeshAgent agent = huySeo.GetComponent<NavMeshAgent>();
            if (agent == null) agent = huySeo.AddComponent<NavMeshAgent>();
            SetRef(so, "huySeoAgent", agent);

            Transform hand = FindHandBone(huySeo.transform);
            if (hand != null)
            {
                SetRef(so, "huySeoHand", hand);
                Debug.Log($"[AutomateStoryPhase1] Đã gán xương tay: '{hand.name}'.");
            }
            else
            {
                Debug.LogWarning("[AutomateStoryPhase1] Không tìm thấy xương bàn tay (Hand) trong Huy_seo.");
            }
        }

        // ===========================================================
        // 3. Bãi rác + khóa Player
        // ===========================================================
        GameObject trash = FindFirstInScene("BaiRac_ThungRac", "thungrac", "thungrac1");
        if (trash != null)
        {
            SetRef(so, "trashPileTarget", trash.transform);
            SetRef(so, "trashInteractZone", trash);
            Debug.Log($"[AutomateStoryPhase1] Đã gán bãi rác: '{trash.name}'.");
        }
        else
        {
            Debug.LogWarning("[AutomateStoryPhase1] Không tìm thấy GameObject bãi rác.");
        }

        // Thu thập các script điều khiển Player/Camera để khóa khi cutscene
        List<MonoBehaviour> controls = CollectControlScripts(out GameObject playerGO, out Camera mainCam);
        SetRefArray(so, "disableDuringCutscene", controls.ToArray());
        Debug.Log($"[AutomateStoryPhase1] Đã gán {controls.Count} script vào disableDuringCutscene.");

        // Tự động tìm và gán các Prefabs cho hột quẹt và ma túy
        GameObject batLua = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/batlua.prefab");
        if (batLua != null)
        {
            SetRef(so, "batLuaPrefab", batLua);
            Debug.Log("[AutomateStoryPhase1] Đã gán prefab hột quẹt (batlua).");
        }
        GameObject bichmaithuy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/bichmaithuy.prefab");
        if (bichmaithuy != null)
        {
            SetRef(so, "bichRac2Prefab", bichmaithuy);
            Debug.Log("[AutomateStoryPhase1] Đã gán prefab ma túy (bichmaithuy).");
        }

        // Tự động tìm và gán các Prefabs cho NPC buổi sáng
        GameObject coNga = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Nganpc.prefab");
        if (coNga != null)
        {
            SetRef(so, "coNgaPrefab", coNga);
            Debug.Log("[AutomateStoryPhase1] Đã gán prefab Cô Nga (Nganpc).");
        }
        GameObject anhShipper = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/shipper.prefab");
        if (anhShipper != null)
        {
            SetRef(so, "anhShipperPrefab", anhShipper);
            Debug.Log("[AutomateStoryPhase1] Đã gán prefab Anh Shipper (shipper).");
        }
        GameObject coDongVien = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Npc1.prefab");
        if (coDongVien != null)
        {
            SetRef(so, "coDongVienPrefab", coDongVien);
            Debug.Log("[AutomateStoryPhase1] Đã gán prefab Cổ Động Viên (Npc1).");
        }

        // Gán khoảng cách kích hoạt cảnh ném ma túy là 1.8m
        SetFloat(so, "triggerDistance", 1.8f);

        // Tiện thể gán luôn player & camera & NPC (tuy runtime có thể tự dò)
        if (playerGO != null) SetRef(so, "player", playerGO.transform);
        if (mainCam != null) SetRef(so, "storyCamera", mainCam);
        GameObject banhMi = FindInScene("anhbanhmi");
        if (banhMi != null) SetRef(so, "anhBanhMi", banhMi);
        GameObject xamMinh = FindInScene("anhxamminh");
        if (xamMinh != null) SetRef(so, "anhXamMinh", xamMinh);

        // Áp dụng toàn bộ thay đổi field
        so.ApplyModifiedPropertiesWithoutUndo();

        // ===========================================================
        // 4. Nối Event cho nút "Mua Bánh Mì"
        // ===========================================================
        WireBuyButton(manager);

        // ===========================================================
        // 5. Đánh dấu dirty + lưu Scene
        // ===========================================================
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(managerGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("=== [AutomateStoryPhase1] HOÀN TẤT! Scene đã được thiết lập & lưu. Anh bấm Play để test nha! ===");
        EditorUtility.DisplayDialog("Hoàn tất",
            "Đã tự động thiết lập xong toàn bộ liên kết cho Story Phase 1 và lưu Scene.\nAnh có thể bấm Play để test ngay!",
            "Tuyệt!");
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    /// <summary>Tìm hoặc tạo mới một mốc Point tại gốc tọa độ.</summary>
    private static Transform GetOrCreatePoint(string name)
    {
        GameObject go = FindInScene(name);
        if (go == null)
        {
            go = new GameObject(name);
            go.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Debug.Log($"[AutomateStoryPhase1] Tạo mới mốc '{name}' tại gốc tọa độ (anh tự dịch chuyển sau).");
        }
        return go.transform;
    }

    /// <summary>Duyệt các con tìm xương bàn tay (ưu tiên RightHand, sau đó bất kỳ tên chứa 'Hand').</summary>
    private static Transform FindHandBone(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        // Ưu tiên tay phải (Mixamo: mixamorig:RightHand)
        foreach (Transform t in all)
            if (t.name.IndexOf("RightHand", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        // Bất kỳ xương nào có 'Hand' nhưng không phải ngón (Finger/Thumb/Index...)
        foreach (Transform t in all)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("hand") && !n.Contains("finger") && !n.Contains("thumb") &&
                !n.Contains("index") && !n.Contains("middle") && !n.Contains("ring") && !n.Contains("pinky"))
                return t;
        }
        return null;
    }

    /// <summary>Thu thập các MonoBehaviour điều khiển Player/Camera trong Scene.</summary>
    private static List<MonoBehaviour> CollectControlScripts(out GameObject playerGO, out Camera mainCam)
    {
        playerGO = null;
        mainCam = Camera.main;

        var result = new List<MonoBehaviour>();
        var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            foreach (string ctrl in ControlScriptTypeNames)
            {
                if (typeName == ctrl)
                {
                    result.Add(mb);
                    if (typeName == "PlayerMovement" && playerGO == null)
                        playerGO = mb.gameObject;
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Tự động thêm OnBuyBanhMiPressed vào onClick của nút "Mua Bánh Mì".</summary>
    private static void WireBuyButton(StoryPhase1Manager manager)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogWarning("[AutomateStoryPhase1] Không tìm thấy Button nào trong Scene để nối nút 'Mua Bánh Mì'.");
            return;
        }

        // Ưu tiên nút có tên chứa 'Mua' / 'Bánh' / 'BanhMi'
        Button target = null;
        foreach (Button b in buttons)
        {
            string n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("mua") || n.Contains("bánh") || n.Contains("banhmi") || n.Contains("banh mi"))
            {
                target = b;
                break;
            }
        }
        if (target == null) target = buttons[0]; // fallback: nút đầu tiên

        // Tránh thêm trùng listener
        int count = target.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (target.onClick.GetPersistentTarget(i) == manager &&
                target.onClick.GetPersistentMethodName(i) == "OnBuyBanhMiPressed")
            {
                Debug.Log($"[AutomateStoryPhase1] Nút '{target.gameObject.name}' đã có listener — bỏ qua.");
                return;
            }
        }

        UnityAction action = manager.OnBuyBanhMiPressed;
        UnityEventTools.AddPersistentListener(target.onClick, action);
        EditorUtility.SetDirty(target);
        Debug.Log($"[AutomateStoryPhase1] Đã nối OnBuyBanhMiPressed vào onClick của nút '{target.gameObject.name}'.");
    }

    // --- SerializedObject setters ---

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[AutomateStoryPhase1] Không tìm thấy field '{field}' trên StoryPhase1Manager.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[AutomateStoryPhase1] Không tìm thấy field '{field}' trên StoryPhase1Manager.");
            return;
        }
        prop.floatValue = value;
    }

    private static void SetRefArray(SerializedObject so, string field, Object[] values)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[AutomateStoryPhase1] Không tìm thấy field '{field}'.");
            return;
        }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // --- Scene lookup (bao gồm cả object inactive) ---

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
