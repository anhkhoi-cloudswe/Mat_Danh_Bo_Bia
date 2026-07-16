using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector cho <see cref="StoryPhase1Manager"/>.
/// Bổ sung khu vực "--- DEV CHEAT TOOLS ---" với 2 nút nhảy nhanh tới từng phân đoạn Phase 1
/// để hỗ trợ test, không cần chơi lại từ đầu buổi sáng. Các nút chỉ bấm được khi đang ở chế độ PLAY.
/// </summary>
[CustomEditor(typeof(StoryPhase1Manager))]
public class StoryPhase1ManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Vẽ toàn bộ Inspector mặc định trước
        DrawDefaultInspector();

        StoryPhase1Manager manager = (StoryPhase1Manager)target;

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
            if (GUILayout.Button("[CHEAT] Nhảy đến Tối Ngày 1 (Bắt Đầu Tối)", GUILayout.Height(30f)))
            {
                manager.Cheat_SkipToEveningIntro();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Nhảy đến Tối (Huy Seo Chạy)", GUILayout.Height(30f)))
            {
                manager.Cheat_SkipToEveningCutscene();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("[CHEAT] Nhảy đến Bới Rác Tìm Vật Chứng", GUILayout.Height(30f)))
            {
                manager.Cheat_SkipToTrashInvestigation();
            }
        }
    }
}
