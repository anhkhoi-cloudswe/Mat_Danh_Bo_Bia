using UnityEngine;
using UnityEditor;

/// <summary>
/// Menu debug để kiểm tra hệ thống hội thoại NPC mà không cần chơi game thật.
/// Dùng trong Play mode: Tools > Debug Dialogue > ...
/// </summary>
public static class DebugDialogueMenu
{
    [MenuItem("Tools/Debug Dialogue/1 - Log Queue State")]
    public static void LogQueueState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        CustomerManager manager = CustomerManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[DebugDialogue] Không tìm thấy CustomerManager.Instance!");
            return;
        }

        Debug.Log($"[DebugDialogue] QueueCount={manager.QueueCount}, HasCustomerWaiting={manager.HasCustomerWaiting}");

        var next = manager.NextCustomer;
        if (next == null)
        {
            Debug.Log("[DebugDialogue] Chưa có khách trong hàng đợi.");
            return;
        }

        Debug.Log($"[DebugDialogue] Khách kế tiếp: {next.displayName}, npcInstance={(next.npcInstance != null ? next.npcInstance.name : "NULL")}");

        if (next.npcInstance != null)
        {
            NPCDialogue dialogue = next.npcInstance.GetComponent<NPCDialogue>();
            Debug.Log($"[DebugDialogue] NPCDialogue trên root: {(dialogue != null ? "CÓ" : "KHÔNG")}");
            if (dialogue != null)
            {
                string welcome = dialogue.DialogueData.welcomeText;
                string evidence = dialogue.DialogueData.evidenceText;
                Debug.Log($"[DebugDialogue] welcomeText: \"{(string.IsNullOrEmpty(welcome) ? "(RỖNG)" : welcome)}\"");
                Debug.Log($"[DebugDialogue] evidenceText: \"{(string.IsNullOrEmpty(evidence) ? "(RỖNG)" : evidence)}\"");
            }

            DialogueBubble bubble = next.npcInstance.GetComponentInChildren<DialogueBubble>(true);
            Debug.Log($"[DebugDialogue] DialogueBubble trong con: {(bubble != null ? "CÓ (" + bubble.gameObject.name + ", active=" + bubble.gameObject.activeInHierarchy + ")" : "KHÔNG")}");
        }
    }

    [MenuItem("Tools/Debug Dialogue/2 - Serve Customer With Dialogue")]
    public static void ServeWithDialogue()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        CustomerManager manager = CustomerManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[DebugDialogue] Không tìm thấy CustomerManager.Instance!");
            return;
        }

        Debug.Log($"[DebugDialogue] Gọi ServeNextCustomerWithDialogue()... (QueueCount={manager.QueueCount})");
        manager.ServeNextCustomerWithDialogue();
    }

    [MenuItem("Tools/Debug Dialogue/4 - Log ScreenUI State")]
    public static void LogScreenUIState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        DialogueScreenUI ui = Object.FindObjectOfType<DialogueScreenUI>();
        if (ui == null)
        {
            Debug.LogError("[DebugDialogue] KHÔNG tìm thấy DialogueScreenUI trong scene!");
            return;
        }

        Debug.Log($"[DebugDialogue] DialogueScreenUI: {ui.GetDebugState()}");
    }

    [MenuItem("Tools/Debug Dialogue/5 - Force Show Screen Panel")]
    public static void ForceShowScreenPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        DialogueScreenUI ui = Object.FindObjectOfType<DialogueScreenUI>();
        if (ui == null)
        {
            Debug.LogError("[DebugDialogue] KHÔNG tìm thấy DialogueScreenUI trong scene!");
            return;
        }

        ui.ForceShow("TEST: Panel hội thoại trên màn hình hoạt động!", 15f);
        Debug.Log("[DebugDialogue] Đã gọi ForceShow trên DialogueScreenUI.");
    }

    [MenuItem("Tools/Debug Dialogue/6 - Force Show Clue Toast")]
    public static void ForceShowClueToast()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        ClueNotificationManager mgr = ClueNotificationManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("[DebugDialogue] KHÔNG tìm thấy ClueNotificationManager.Instance! " +
                           "Script tự khởi tạo sau AfterSceneLoad — hãy chắc chắn đã vào Play mode.");
            return;
        }

        string testMessage = "\"Tôi thấy một chiếc xe lạ đậu trước cửa hàng lúc 2 giờ sáng...\" — Manh mối #1";
        mgr.ShowNotification(testMessage);
        Debug.Log($"[DebugDialogue] Đã gọi ShowNotification: \"{testMessage}\"");
    }

    [MenuItem("Tools/Debug Dialogue/3 - Force Show Bubble On All NPCs")]
    public static void ForceShowAllBubbles()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        DialogueBubble[] bubbles = Object.FindObjectsOfType<DialogueBubble>(true);
        Debug.Log($"[DebugDialogue] Tìm thấy {bubbles.Length} DialogueBubble trong scene.");
        foreach (var bubble in bubbles)
        {
            bubble.Show("TEST: Xin chào! Đây là bóng thoại thử nghiệm.", 10f);
            Debug.Log($"[DebugDialogue] Đã gọi Show() trên '{bubble.transform.root.name}/{bubble.gameObject.name}'");
        }
    }

    [MenuItem("Tools/Debug Dialogue/7 - Add 10 Evidence Points")]
    public static void AddTestEvidencePoints()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugDialogue] Chỉ dùng được trong Play mode.");
            return;
        }

        CaseManager mgr = CaseManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("[DebugDialogue] KHÔNG tìm thấy CaseManager.Instance!");
            return;
        }

        mgr.AddScore(10);
        Debug.Log($"[DebugDialogue] Đã cộng +10 điểm bằng chứng. Tổng: {mgr.EvidenceScore}");
    }
}
