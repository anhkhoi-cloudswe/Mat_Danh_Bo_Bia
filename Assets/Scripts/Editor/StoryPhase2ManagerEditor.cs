using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector cho <see cref="StoryPhase2Manager"/>.
/// Bổ sung khu vực "--- DEV CHEAT TOOLS ---" với các nút nhảy nhanh tới từng phân đoạn Phase 2
/// (Sáng Ngày 2 / Red Monologue Mê Liu / Tối Ngày 2) để test, không cần chơi lại Phase 1.
/// Các nút chỉ bấm được khi đang ở chế độ PLAY.
/// </summary>
[CustomEditor(typeof(StoryPhase2Manager))]
public class StoryPhase2ManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Vẽ toàn bộ Inspector mặc định trước
        DrawDefaultInspector();

        StoryPhase2Manager manager = (StoryPhase2Manager)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("--- DEV CHEAT TOOLS ---", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Các nút Cheat chỉ dùng được khi đang ở chế độ PLAY.\nNhấn Play rồi quay lại bấm để nhảy nhanh tới phân đoạn cần test.",
                MessageType.Info);
        }

        // Vô hiệu hóa cả khối nút khi không ở Play Mode
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("[CHEAT] Bắt đầu Sáng Ngày 2 (3 Khách)", GUILayout.Height(30f)))
            {
                manager.Cheat_StartMorningDay2();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Nhảy tới Miu Lê (Bỏ qua 2 khách đầu)", GUILayout.Height(30f)))
            {
                manager.Cheat_SkipToMeLiu();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Test Monologue Đỏ (Mùi Khai Mê Liu)", GUILayout.Height(30f)))
            {
                manager.Cheat_TriggerMeLiuRedMonologue();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Nhảy đến Tối Ngày 2", GUILayout.Height(30f)))
            {
                manager.Cheat_SkipToEveningDay2();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("--- VILLA EVENING CHEATS ---", EditorStyles.boldLabel);

            if (GUILayout.Button("[CHEAT] Test Mật Phục (Hiding Zone)", GUILayout.Height(30f)))
            {
                manager.Cheat_TestSurveillance();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Teleport đến cửa Villa (Tối)", GUILayout.Height(30f)))
            {
                manager.Cheat_WarpToVilla();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Thu thập nhanh 3 chứng cứ Villa", GUILayout.Height(30f)))
            {
                manager.Cheat_CollectAllVillaEvidence();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Hoàn tất PHẦN 1 (gọi đồng đội)", GUILayout.Height(30f)))
            {
                manager.Cheat_FinishPhase1Gameplay();
            }
        }
    }
}
