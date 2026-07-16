using UnityEditor;
using UnityEngine;

/// <summary>
/// Tự động thiết lập cấu hình danh sách Scene trong Build Settings cho toàn bộ đội phát triển.
/// Đảm bảo đúng thứ tự: Main_Menu (0), Intro_CutScene (1), Phase1 (2).
/// </summary>
[InitializeOnLoad]
public class AutoConfigureBuildSettings
{
    static AutoConfigureBuildSettings()
    {
        // Chạy thiết lập khi Unity load xong/compile xong trong Editor
        EditorApplication.delayCall += ConfigureScenes;
    }

    [MenuItem("Tools/Configure Build Settings")]
    public static void ConfigureScenes()
    {
        string[] targetScenes = {
            "Assets/Scenes/Main_Menu.unity",
            "Assets/Scenes/Intro_CutScene.unity",
            "Assets/Scenes/BaoScene.unity",
            "Assets/Scenes/MiddleCutScene.unity",
            "Assets/Scenes/Final_CutScene.unity"
        };

        EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes;
        bool needsUpdate = false;

        if (originalScenes == null || originalScenes.Length != targetScenes.Length)
        {
            needsUpdate = true;
        }
        else
        {
            for (int i = 0; i < targetScenes.Length; i++)
            {
                if (originalScenes[i].path != targetScenes[i] || !originalScenes[i].enabled)
                {
                    needsUpdate = true;
                    break;
                }
            }
        }

        if (needsUpdate)
        {
            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[targetScenes.Length];
            for (int i = 0; i < targetScenes.Length; i++)
            {
                // Nạp scene vào danh sách và kích hoạt (enabled = true)
                newScenes[i] = new EditorBuildSettingsScene(targetScenes[i], true);
            }
            EditorBuildSettings.scenes = newScenes;
            Debug.Log("[AutoConfigureBuildSettings] Đã tự động cấu hình danh sách scene trong Build Settings:\n" +
                      " - Index 0: Main_Menu\n" +
                      " - Index 1: Intro_CutScene\n" +
                      " - Index 2: BaoScene\n" +
                      " - Index 3: MiddleCutScene\n" +
                      " - Index 4: Final_CutScene");
        }
    }
}
