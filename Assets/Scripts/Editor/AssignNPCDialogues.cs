using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Editor utility để gán NPCDialogue component với lời thoại và điểm bằng chứng
/// và tự động cấu hình World Space Canvas Dialogue Bubble cho tất cả NPC prefabs. Chạy từ menu Tools.
/// </summary>
[InitializeOnLoad]
public class AssignNPCDialogues
{
    static AssignNPCDialogues()
    {
        // Chỉ tự chạy khi prefab chưa có lời thoại — chạy vô điều kiện trong
        // static ctor sẽ ghi đè toàn bộ prefab sau MỖI lần compile (và thao tác
        // asset ở thời điểm này không an toàn). Dùng delayCall để chờ AssetDatabase sẵn sàng.
        EditorApplication.delayCall += () =>
        {
            if (NeedsAssignment())
            {
                AssignAll();
            }
        };
    }

    private static bool NeedsAssignment()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Nganpc.prefab");
        if (prefab == null) return false;
        NPCDialogue dialogue = prefab.GetComponent<NPCDialogue>();
        return dialogue == null || string.IsNullOrEmpty(dialogue.DialogueData.welcomeText);
    }

    private struct DialogueEntry
    {
        public string prefabPath;
        public string welcomeText;
        public string evidenceText;
        public int evidencePoints;
    }

    [MenuItem("Tools/Assign All NPC Dialogues")]
    public static void AssignAll()
    {
        Debug.Log("=== Bắt đầu gán lời thoại và bong bóng thoại cho NPC ===");

        DialogueEntry[] entries = new DialogueEntry[]
        {
            // Element 0: Nganpc (Cô Nga đồ bộ)
            new DialogueEntry
            {
                prefabPath = "Assets/Prefabs/Nganpc.prefab",
                welcomeText = "Bán cô 2 cuốn nhiều tương ớt nha cháu!",
                evidenceText = "Mấy tay to bên phố cũ dọn hết qua khu Villa bên kia tụ tập bí mật lắm!",
                evidencePoints = 20
            },
            // Element 1: shipper (Tiến Shipper)
            new DialogueEntry
            {
                prefabPath = "Assets/Prefabs/shipper.prefab",
                welcomeText = "Giao đơn đuối quá, cho 3 cuốn mang đi lẹ anh ơi!",
                evidenceText = "Hẻm Villa đối diện bảo vệ gác nghiêm ngặt, đổi ca liên tục luôn!",
                evidencePoints = 20
            },
            // Element 2: Npc1 (Cổ động viên)
            new DialogueEntry
            {
                prefabPath = "Assets/Prefabs/Npc1.prefab",
                welcomeText = "Cho em 2 cuốn bò bía đi anh, cuốn là số dách!",
                evidenceText = "Tối qua đi bão thấy mấy xe hơi đen biển lạ lượn lờ hẻm Villa mờ ám lắm!",
                evidencePoints = 20
            },
            // Element 3: npc2 (Thanh niên denim)
            new DialogueEntry
            {
                prefabPath = "Assets/Prefabs/npc2.prefab",
                welcomeText = "Anh ơi cho em 1 cuốn ăn liền nha!",
                evidenceText = "Khu Villa đối diện mới đổi camera quét đêm dữ lắm, ai qua cũng bị ghi hình!",
                evidencePoints = 20
            },
            // Element 4: Npc3 (Thanh niên thể thao)
            new DialogueEntry
            {
                prefabPath = "Assets/Prefabs/Npc3.prefab",
                welcomeText = "Cho em 2 cuốn nạp năng lượng chạy bộ tiếp!",
                evidenceText = "Chạy ngang Villa thấy mấy gã mặc vest gác, tay giữ tai nghe cảnh giới!",
                evidencePoints = 20
            }
        };

        int successCount = 0;
        foreach (var entry in entries)
        {
            if (AssignDialogueToPrefab(entry.prefabPath, entry.welcomeText, entry.evidenceText, entry.evidencePoints))
                successCount++;
        }

        // Cấu hình bóng hội thoại cho anh_aoxanh.prefab (dù không có hội thoại mặc định ban đầu)
        string aoxanhPath = "Assets/Prefabs/anh_aoxanh.prefab";
        GameObject aoxanhPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(aoxanhPath);
        if (aoxanhPrefab != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(aoxanhPrefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);
            SetupDialogueBubbleCanvas(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            PrefabUtility.UnloadPrefabContents(instance);
            Debug.Log("[AssignNPCDialogues] ✅ Đã cấu hình bong bóng thoại cho 'anh_aoxanh'");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"=== Hoàn tất gán lời thoại và UI: {successCount}/{entries.Length} NPC ===");
    }

    private static bool AssignDialogueToPrefab(string prefabPath, string welcomeText, string evidenceText, int evidencePoints)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[AssignNPCDialogues] Không tìm thấy prefab tại: {prefabPath}");
            return false;
        }

        // Mở prefab để chỉnh sửa
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);

        // Thêm hoặc lấy NPCDialogue component
        NPCDialogue dialogue = instance.GetComponent<NPCDialogue>();
        if (dialogue == null)
        {
            dialogue = instance.AddComponent<NPCDialogue>();
        }

        // Gán dữ liệu hội thoại
        dialogue.DialogueData = new NPCDialogue.DialogueInfo
        {
            welcomeText = welcomeText,
            evidenceText = evidenceText,
            evidencePoints = evidencePoints
        };
        EditorUtility.SetDirty(dialogue);

        // Cấu hình World Space Canvas Dialogue Bubble
        SetupDialogueBubbleCanvas(instance);

        EditorUtility.SetDirty(instance);

        // Lưu thay đổi vào prefab
        PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        PrefabUtility.UnloadPrefabContents(instance);

        Debug.Log($"[AssignNPCDialogues] ✅ Đã gán lời thoại & UI cho '{prefab.name}' ({evidencePoints} điểm)");
        return true;
    }

    private static void SetupDialogueBubbleCanvas(GameObject instance)
    {
        // Kiểm tra xem đã có DialogueBubbleCanvas chưa
        DialogueBubble bubble = instance.GetComponentInChildren<DialogueBubble>(true);
        if (bubble != null)
        {
            // Cập nhật vị trí và tỉ lệ chuẩn
            bubble.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            bubble.transform.localRotation = Quaternion.identity;
            bubble.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
            EditorUtility.SetDirty(bubble.transform);

            // Cập nhật kích thước Canvas để chứa chữ lớn hơn
            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            if (bubbleRect != null)
            {
                bubbleRect.sizeDelta = new Vector2(600f, 250f);
                EditorUtility.SetDirty(bubbleRect);
            }

            // Cập nhật cỡ chữ lớn hơn cho Text component hiện có
            Text existingText = bubble.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                existingText.fontSize = 50; // Đặt cỡ chữ vừa phải (50) để hiển thị tốt trong game
                existingText.horizontalOverflow = HorizontalWrapMode.Wrap;
                existingText.verticalOverflow = VerticalWrapMode.Overflow; // Overflow
                existingText.supportRichText = true;
                EditorUtility.SetDirty(existingText);

                // Thêm hoặc cập nhật Outline cho Text component hiện có
                Outline existingOutline = existingText.GetComponent<Outline>();
                if (existingOutline == null)
                {
                    existingOutline = existingText.gameObject.AddComponent<Outline>();
                }
                existingOutline.effectColor = Color.black;
                existingOutline.effectDistance = new Vector2(2f, -2f);
                existingOutline.useGraphicAlpha = true;
                EditorUtility.SetDirty(existingOutline);
            }

            // Cập nhật màu nền cho Panel con của bubble thành trong suốt
            Image existingBg = bubble.GetComponentInChildren<Image>(true);
            if (existingBg != null)
            {
                existingBg.color = new Color(0f, 0f, 0f, 0f);
                EditorUtility.SetDirty(existingBg);
            }
            EditorUtility.SetDirty(bubble);
            return;
        }

        // 1. Tạo Canvas GameObject
        GameObject canvasObj = new GameObject("DialogueBubbleCanvas");
        canvasObj.transform.SetParent(instance.transform);
        
        // Vị trí: X=0, Y=2.1, Z=0 (cao hơn đầu NPC một chút), scale rất nhỏ để vẽ nét chữ rõ ràng trong world space
        canvasObj.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

        // 2. Cấu hình Canvas Component
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 250f);

        // 3. Cấu hình CanvasScaler & GraphicRaycaster
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f; // Chữ siêu sắc nét

        canvasObj.AddComponent<GraphicRaycaster>();

        // 4. Thêm CanvasGroup để ẩn hiện alpha mượt mà
        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 5. Tạo BubblePanel nền
        GameObject panelObj = new GameObject("BubblePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bgImage = panelObj.AddComponent<Image>();
        // Gán Sprite bo góc mặc định của Unity UI
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (uiSprite != null)
        {
            bgImage.sprite = uiSprite;
            bgImage.type = Image.Type.Sliced;
        }
        // Màu nền trong suốt hoàn toàn giống ảnh mẫu
        bgImage.color = new Color(0f, 0f, 0f, 0f);

        // 6. Tạo Text Component (DialogueText)
        GameObject textObj = new GameObject("DialogueText");
        textObj.transform.SetParent(panelObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(25f, 15f);
        textRect.offsetMax = new Vector2(-25f, -15f);

        Text uiText = textObj.AddComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = 50; // Đặt cỡ chữ vừa phải (50) để hiển thị tốt trong game
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.color = Color.white;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow; // Overflow
        uiText.supportRichText = true;

        // Thêm Outline viền đen
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        // 7. Thêm DialogueBubble script điều khiển hiển thị
        bubble = canvasObj.AddComponent<DialogueBubble>();

        // Gán biến thông qua SerializedObject trong Editor
        SerializedObject serializedBubble = new SerializedObject(bubble);
        serializedBubble.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedBubble.FindProperty("textMesh").objectReferenceValue = uiText;
        serializedBubble.ApplyModifiedProperties();
    }
}
