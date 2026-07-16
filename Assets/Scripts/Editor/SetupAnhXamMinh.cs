using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor thiết lập NPC anhxamminh đứng trước xe bánh mì:
///   - Đổi Animator sang controller ĐỨNG (anhbanhmi_Animator) để hết lỗi "đi bộ tại chỗ".
///   - Thêm <see cref="NpcTalkInteraction"/> + gán tham chiếu (NPCDialogue, DialogueBubble,
///     Animator, rivalNpc=anhbanhmi, StoryPhase1Manager) qua SerializedObject.
///   - Lưu Scene.
/// Menu: Tools/Setup AnhXamMinh (NPC nói chuyện)
/// </summary>
public static class SetupAnhXamMinh
{
    private const string NpcName = "anhxamminh";
    private const string RivalName = "anhbanhmi";
    private const string IdleControllerPath = "Assets/Animators/anhbanhmi_Animator.controller";

    [MenuItem("Tools/Setup AnhXamMinh (NPC nói chuyện)")]
    public static void Setup()
    {
        Debug.Log("=== [SetupAnhXamMinh] Bắt đầu thiết lập NPC anhxamminh ===");

        GameObject npc = FindInScene(NpcName);
        if (npc == null)
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy '{NpcName}' trong Scene.", "OK");
            return;
        }

        // --- 1. Đổi Animator sang controller đứng (idle) ---
        Animator anim = npc.GetComponent<Animator>();
        if (anim != null)
        {
            var idle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
            if (idle != null)
            {
                anim.runtimeAnimatorController = idle;
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
                Debug.Log("[SetupAnhXamMinh] Đã đổi Animator anhxamminh sang controller ĐỨNG (anhbanhmi_Animator).");
            }
            else
            {
                Debug.LogWarning($"[SetupAnhXamMinh] Không tìm thấy controller '{IdleControllerPath}'.");
            }
        }
        else
        {
            Debug.LogWarning("[SetupAnhXamMinh] anhxamminh thiếu Animator.");
        }

        // --- 2. Thêm component tương tác nói chuyện ---
        NpcTalkInteraction talk = npc.GetComponent<NpcTalkInteraction>();
        if (talk == null)
        {
            talk = npc.AddComponent<NpcTalkInteraction>();
            Debug.Log("[SetupAnhXamMinh] Đã thêm component NpcTalkInteraction.");
        }

        // --- 3. Gán tham chiếu qua SerializedObject ---
        NPCDialogue dialogue = npc.GetComponent<NPCDialogue>();
        DialogueBubble bubble = npc.GetComponentInChildren<DialogueBubble>(true);
        GameObject rival = FindInScene(RivalName);
        StoryPhase1Manager story = Object.FindFirstObjectByType<StoryPhase1Manager>();

        SerializedObject so = new SerializedObject(talk);
        if (dialogue != null) SetRef(so, "npcDialogue", dialogue);
        if (bubble != null) SetRef(so, "bubble", bubble);
        if (anim != null) SetRef(so, "npcAnimator", anim);
        if (rival != null) SetRef(so, "rivalNpc", rival.transform);
        if (story != null) SetRef(so, "storyManager", story);
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[SetupAnhXamMinh] Đã gán tham chiếu cho NpcTalkInteraction.");

        // --- 4. Dirty + Save ---
        EditorUtility.SetDirty(talk);
        EditorUtility.SetDirty(npc);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("=== [SetupAnhXamMinh] HOÀN TẤT! Scene đã lưu. ===");
        EditorUtility.DisplayDialog("Hoàn tất",
            "Đã đổi anhxamminh sang animation ĐỨNG và cho phép bấm E để nói chuyện.\nBấm Play, lại gần anhxamminh để test nha!",
            "Tuyệt!");
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[SetupAnhXamMinh] Không tìm thấy field '{field}'.");
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
