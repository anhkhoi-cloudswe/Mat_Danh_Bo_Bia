using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị lời thoại NPC dưới dạng panel "dính" vào camera người chơi
/// (world-space canvas đặt cố định trước mặt camera). Bóng thoại trên đầu NPC
/// vẫn hoạt động, nhưng người chơi thường nhìn vào xe bò bía nên không thấy —
/// panel này đảm bảo lời thoại LUÔN trong tầm nhìn bất kể camera quay hướng nào.
/// Lưu ý: dùng world-space thay vì Screen Space Overlay vì overlay canvas tạo
/// lúc runtime không render trong setup URP của project này.
/// Tự khởi tạo khi vào Play mode, không cần kéo thả gì trong scene.
/// </summary>
public class DialogueScreenUI : MonoBehaviour
{
    // Đặt panel RẤT gần camera (ngay sau near plane ~0.3) để không bị tường/vật
    // thể che mất khi camera đứng sát geometry — world-space UI bị depth-test.
    private const float CanvasWidth = 950f;
    private const float CanvasHeight = 160f;
    private const float CanvasScale = 0.00062f;
    private const float DistanceFromCamera = 0.5f;
    private const float VerticalOffset = -0.14f;

    private CustomerManager subscribedManager;
    private Camera attachedCamera;
    private UnityEngine.EventSystems.EventSystem eventSystem;
    private CanvasGroup canvasGroup;
    private Text dialogueText;
    private Text interactPromptText;
    private float hideTime;
    private bool isShowing;

    private GameObject ingredientPanel;
    private Button buttonPastry;
    private Button buttonCandy;
    private Button buttonCoconut;
    private Button buttonSesame;

    private GameObject rollingPanel;
    private Text rollingPromptText;
    private Slider cachedProgressSlider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<DialogueScreenUI>() != null) return;
        var go = new GameObject("DialogueScreenUI");
        go.AddComponent<DialogueScreenUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        BuildUI();
        EnsureEventSystem();
    }

    private void EnsureEventSystem()
    {
        eventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystem = eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[DialogueScreenUI] Tự động khởi tạo EventSystem cho scene.");
        }

        // Cả EventSystem có sẵn trong Main_Menu cũng phải được giữ lại.
        // Nếu nó còn là con của object thuộc scene, hãy tách ra trước.
        eventSystem.transform.SetParent(null, true);
        DontDestroyOnLoad(eventSystem.gameObject);
    }

    private void Update()
    {
        // EventSystem của scene cũ có thể bị hủy khi load scene mới.
        if (eventSystem == null)
        {
            EnsureEventSystem();
        }

        // CustomerManager xuất hiện sau khi scene load — đăng ký ngay khi tìm thấy
        if (subscribedManager == null && CustomerManager.Instance != null)
        {
            subscribedManager = CustomerManager.Instance;
            subscribedManager.OnDialogueTriggered += HandleDialogueTriggered;
            subscribedManager.OnDialogueEnded += HandleDialogueEnded;
        }

        // Bám theo camera bằng world transform nhưng KHÔNG parent vào camera.
        // Parent vào camera thuộc scene sẽ kéo object này ra khỏi DontDestroyOnLoad.
        if (attachedCamera == null || !attachedCamera.isActiveAndEnabled)
        {
            attachedCamera = Camera.main;
        }

        if (attachedCamera != null)
        {
            transform.position = attachedCamera.transform.TransformPoint(
                new Vector3(0f, VerticalOffset, DistanceFromCamera));
            transform.rotation = attachedCamera.transform.rotation;
            transform.localScale = Vector3.one * CanvasScale;
        }

        if (isShowing && Time.time >= hideTime)
        {
            isShowing = false;
            if (dialogueText != null)
            {
                dialogueText.text = "";
            }
        }

        // Cập nhật trạng thái prompt trước
        UpdateInteractPrompt();

        // CanvasGroup alpha nên là 1f nếu đang có hội thoại HOẶC đang hiện prompt tương tác HOẶC đang làm bánh
        bool isCooking = BoBiaMechanic.Instance != null && BoBiaMechanic.Instance.IsRolling;
        bool shouldBeVisible = isShowing || (interactPromptText != null && interactPromptText.gameObject.activeSelf) || isCooking;
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, 5f * Time.deltaTime);

        canvasGroup.interactable = isCooking;
        canvasGroup.blocksRaycasts = isCooking;
    }

    private void OnDestroy()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnDialogueTriggered -= HandleDialogueTriggered;
            subscribedManager.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    private void HandleDialogueTriggered(string npcName, string text, int evidencePoints, float duration)
    {
        ForceShow($"<b>{npcName}</b>\n{text}", duration);
    }

    private void HandleDialogueEnded()
    {
        isShowing = false;
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
    }

    /// <summary>Hiện panel với nội dung tùy ý (dùng cho cả event và debug).</summary>
    public void ForceShow(string message, float duration)
    {
        dialogueText.text = message;
        isShowing = true;
        hideTime = Time.time + duration;
    }

    /// <summary>Trạng thái nội bộ phục vụ debug.</summary>
    public string GetDebugState()
    {
        return $"subscribed={(subscribedManager != null)}, camera={(attachedCamera != null ? attachedCamera.name : "NULL")}, " +
               $"isShowing={isShowing}, alpha={canvasGroup.alpha:F2}, hideTime={hideTime:F1}, now={Time.time:F1}, " +
               $"worldPos={transform.position}, text=\"{(dialogueText != null ? dialogueText.text : "NULL")}\"";
    }

    private void BuildUI()
    {
        // Canvas world-space — pattern duy nhất render ổn định trong project này
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f; // Sửa lỗi chữ bị thu nhỏ quá mức trong World Space Canvas (từ 100f thành 1f)

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Panel nền màu than mờ phủ toàn canvas
        GameObject panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Material UI với ZTest=Always: panel luôn vẽ đè lên geometry, không bị
        // tường/vật thể gần camera che mất (world-space UI mặc định bị depth-test).
        Material alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f); // Hoàn toàn trong suốt giống ảnh mẫu
        background.material = alwaysOnTop;

        // Thêm thanh viền trên (Accent Bar) - Đặt trong suốt giống ảnh mẫu
        GameObject accentObj = new GameObject("AccentBar");
        accentObj.transform.SetParent(panel.transform, false);
        RectTransform accentRect = accentObj.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(0f, -4f); // Độ dày thanh viền là 4px
        accentRect.offsetMax = new Vector2(0f, 0f);
        Image accentImage = accentObj.AddComponent<Image>();
        accentImage.color = new Color(0f, 0f, 0f, 0f); // Trong suốt
        accentImage.material = alwaysOnTop;

        GameObject textObj = new GameObject("DialogueText");
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 8f);
        textRect.offsetMax = new Vector2(-40f, -12f); // Chừa khoảng trống phía trên cho thanh viền và thêm lề

        dialogueText = textObj.AddComponent<Text>();
        dialogueText.material = alwaysOnTop;
        dialogueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dialogueText.fontSize = 42; // Tăng cỡ chữ lên 42 để hiển thị to rõ dễ đọc
        dialogueText.lineSpacing = 1.2f;
        dialogueText.alignment = TextAnchor.MiddleCenter;
        dialogueText.color = Color.white;
        dialogueText.supportRichText = true;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;

        // Thêm viền chữ (Outline) sắc nét giống ảnh mẫu
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        // Tạo GameObject cho InteractPromptText
        GameObject promptObj = new GameObject("InteractPromptText");
        promptObj.transform.SetParent(panel.transform, false);
        RectTransform promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, 0f); // Ở giữa màn hình giống ảnh mẫu
        promptRect.sizeDelta = new Vector2(800f, 150f);

        interactPromptText = promptObj.AddComponent<Text>();
        interactPromptText.material = alwaysOnTop;
        interactPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        interactPromptText.fontSize = 50; // Đặt cỡ chữ nhắc nhở tương tác vừa phải (50) để dễ nhìn
        interactPromptText.lineSpacing = 1.25f;
        interactPromptText.alignment = TextAnchor.MiddleCenter;
        interactPromptText.color = Color.white;
        interactPromptText.supportRichText = true;
        interactPromptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        interactPromptText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline promptOutline = promptObj.AddComponent<Outline>();
        promptOutline.effectColor = Color.black;
        promptOutline.effectDistance = new Vector2(2f, -2f);
        promptOutline.useGraphicAlpha = true;

        interactPromptText.gameObject.SetActive(false);

        BuildIngredientUI(panel, alwaysOnTop);
    }

    private void BuildIngredientUI(GameObject parentPanel, Material alwaysOnTop)
    {
        // Container panel for ingredients
        ingredientPanel = new GameObject("IngredientPanel");
        ingredientPanel.transform.SetParent(parentPanel.transform, false);
        RectTransform ipRect = ingredientPanel.AddComponent<RectTransform>();
        ipRect.anchorMin = new Vector2(0.5f, 0f);
        ipRect.anchorMax = new Vector2(0.5f, 0f);
        ipRect.pivot = new Vector2(0.5f, 0f);
        ipRect.anchoredPosition = new Vector2(0f, 15f);
        ipRect.sizeDelta = new Vector2(700f, 60f);

        // Add Horizontal Layout Group to lay out the 4 buttons
        HorizontalLayoutGroup layout = ingredientPanel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 15f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Create the 4 buttons
        buttonPastry = CreateIngredientButton("Button_Pastry", "Bánh Tráng", ipRect, alwaysOnTop);
        buttonCandy = CreateIngredientButton("Button_Candy", "Kẹo Mạch Nha", ipRect, alwaysOnTop);
        buttonCoconut = CreateIngredientButton("Button_Coconut", "Dừa Bào", ipRect, alwaysOnTop);
        buttonSesame = CreateIngredientButton("Button_Sesame", "Mè Rang", ipRect, alwaysOnTop);

        // Bind button clicks to BoBiaMechanic methods
        buttonPastry.onClick.AddListener(() => {
            if (BoBiaMechanic.Instance != null) BoBiaMechanic.Instance.OnClickAddPastry();
        });
        buttonCandy.onClick.AddListener(() => {
            if (BoBiaMechanic.Instance != null) BoBiaMechanic.Instance.OnClickAddCandy();
        });
        buttonCoconut.onClick.AddListener(() => {
            if (BoBiaMechanic.Instance != null) BoBiaMechanic.Instance.OnClickAddCoconut();
        });
        buttonSesame.onClick.AddListener(() => {
            if (BoBiaMechanic.Instance != null) BoBiaMechanic.Instance.OnClickAddSesame();
        });

        // Hide by default
        ingredientPanel.SetActive(false);

        // Container panel for rolling
        rollingPanel = new GameObject("RollingPanel");
        rollingPanel.transform.SetParent(parentPanel.transform, false);
        RectTransform rpRect = rollingPanel.AddComponent<RectTransform>();
        rpRect.anchorMin = Vector2.zero;
        rpRect.anchorMax = Vector2.one;
        rpRect.offsetMin = Vector2.zero;
        rpRect.offsetMax = Vector2.zero;

        // Text for rolling instructions
        GameObject rpTextObj = new GameObject("RollingPromptText");
        rpTextObj.transform.SetParent(rollingPanel.transform, false);
        RectTransform rpTextRect = rpTextObj.AddComponent<RectTransform>();
        rpTextRect.anchorMin = Vector2.zero;
        rpTextRect.anchorMax = Vector2.one;
        rpTextRect.offsetMin = new Vector2(40f, 8f);
        rpTextRect.offsetMax = new Vector2(-40f, -8f);

        rollingPromptText = rpTextObj.AddComponent<Text>();
        rollingPromptText.material = alwaysOnTop;
        rollingPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rollingPromptText.fontSize = 32;
        rollingPromptText.alignment = TextAnchor.MiddleCenter;
        rollingPromptText.color = Color.white;
        rollingPromptText.supportRichText = true;
        rollingPromptText.text = "GIỮ CHUỘT TRÁI & KÉO THẲNG LÊN TRÊN ĐỂ CUỐN BÁNH\n<color=#FFFF00>▲</color>";

        Outline rpOutline = rpTextObj.AddComponent<Outline>();
        rpOutline.effectColor = Color.black;
        rpOutline.effectDistance = new Vector2(2f, -2f);

        rollingPanel.SetActive(false);
    }

    private Button CreateIngredientButton(string name, string label, RectTransform parent, Material mat)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.material = mat;
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark background by default

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Add text label
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text txt = textObj.AddComponent<Text>();
        txt.material = mat;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        return btn;
    }

    private Slider GetProgressSlider()
    {
        if (cachedProgressSlider == null)
        {
            var sliderGo = GameObject.Find("Rolling_Progress_Slider");
            if (sliderGo != null)
            {
                cachedProgressSlider = sliderGo.GetComponent<Slider>();
            }
            if (cachedProgressSlider == null)
            {
                cachedProgressSlider = Object.FindAnyObjectByType<Slider>();
            }
        }
        return cachedProgressSlider;
    }

    private void UpdateInteractPrompt()
    {
        if (interactPromptText == null) return;

        // Ẩn prompt khi đang hiện hội thoại
        if (isShowing)
        {
            interactPromptText.gameObject.SetActive(false);
            if (ingredientPanel != null) ingredientPanel.SetActive(false);
            if (rollingPanel != null) rollingPanel.SetActive(false);
            return;
        }

        // 1. Kiểm tra trạng thái cốt truyện Phase 1 buổi tối trước
        StoryPhase1Manager storyManager = FindObjectOfType<StoryPhase1Manager>();
        if (storyManager != null && storyManager.enabled && storyManager.currentTime == StoryPhase1Manager.TimeOfDay.Evening_Phase1)
        {
            if (storyManager.CurrentState == StoryPhase1Manager.Phase1State.Intro && !storyManager.IsSequenceRunning)
            {
                if (storyManager.IsBanhMiInteractable())
                {
                    interactPromptText.text = "Mua bánh mì <color=#FFFF00>[E]</color>";
                    interactPromptText.gameObject.SetActive(true);
                    if (ingredientPanel != null) ingredientPanel.SetActive(false);
                    if (rollingPanel != null) rollingPanel.SetActive(false);
                    return;
                }
            }
            else if (storyManager.CurrentState == StoryPhase1Manager.Phase1State.EncounterFlee && !storyManager.IsSequenceRunning)
            {
                if (!storyManager.HasPickedUpLighter)
                {
                    if (storyManager.IsPlayerNearLighter())
                    {
                        interactPromptText.text = "Nhặt hột quẹt <color=#FFFF00>[E]</color>";
                    }
                    else
                    {
                        interactPromptText.text = "Hãy nhặt chiếc hột quẹt rơi ở đầu ngõ";
                    }
                }
                else
                {
                    interactPromptText.text = "Đi theo dấu vết của kẻ tình nghi vào hẻm";
                }
                interactPromptText.gameObject.SetActive(true);
                if (ingredientPanel != null) ingredientPanel.SetActive(false);
                if (rollingPanel != null) rollingPanel.SetActive(false);
                return;
            }
            else if (storyManager.CurrentState == StoryPhase1Manager.Phase1State.DoorReturnDialogue && !storyManager.IsSequenceRunning)
            {
                if (storyManager.IsPlayerNearHouse())
                {
                    if (storyManager.HasPickedUpLighter)
                    {
                        interactPromptText.text = "Trả hột quẹt <color=#FFFF00>[E]</color>";
                    }
                    else
                    {
                        interactPromptText.text = "Tìm hột quẹt bị đánh rơi ở đầu ngõ";
                    }
                    interactPromptText.gameObject.SetActive(true);
                    if (ingredientPanel != null) ingredientPanel.SetActive(false);
                    if (rollingPanel != null) rollingPanel.SetActive(false);
                    return;
                }
            }
        }

        // 2. Logic khách hàng buổi sáng (khi không chạy cốt truyện buổi tối)
        CustomerManager manager = CustomerManager.Instance;
        if (manager == null || !manager.enabled || manager.QueueCount == 0 || !manager.HasCustomerWaiting)
        {
            interactPromptText.gameObject.SetActive(false);
            if (ingredientPanel != null) ingredientPanel.SetActive(false);
            if (rollingPanel != null) rollingPanel.SetActive(false);
            return;
        }

        if (!manager.IsPlayerNearCurrentCustomer)
        {
            interactPromptText.gameObject.SetActive(false);
            if (ingredientPanel != null) ingredientPanel.SetActive(false);
            if (rollingPanel != null) rollingPanel.SetActive(false);
            return;
        }

        // Nếu đang cầm bò bía hoàn chỉnh, hiện prompt giao bánh
        if (BoBiaMechanic.Instance != null && BoBiaMechanic.Instance.IsHoldingBoBia)
        {
            interactPromptText.text = "Giao bánh <color=#FFFF00>[E]</color>";
            interactPromptText.gameObject.SetActive(true);
            if (ingredientPanel != null) ingredientPanel.SetActive(false);
            if (rollingPanel != null) rollingPanel.SetActive(false);
            return;
        }

        switch (manager.CurrentCustomerState)
        {
            case CustomerManager.CustomerState.ArrivedWaitingToTalk:
            case CustomerManager.CustomerState.WaitingForEvidence:
                interactPromptText.text = "Nói chuyện <color=#FFFF00>[E]</color>";
                interactPromptText.gameObject.SetActive(true);
                if (ingredientPanel != null) ingredientPanel.SetActive(false);
                if (rollingPanel != null) rollingPanel.SetActive(false);
                break;
            case CustomerManager.CustomerState.ReadyToRoll:
                interactPromptText.text = "Cuốn bánh <color=#FFFF00>[E]</color>";
                interactPromptText.gameObject.SetActive(true);
                if (ingredientPanel != null) ingredientPanel.SetActive(false);
                if (rollingPanel != null) rollingPanel.SetActive(false);
                break;
            case CustomerManager.CustomerState.Rolling:
                interactPromptText.gameObject.SetActive(false);
                // Ẩn hoàn toàn tất cả các panel UI cũ (ingredient panel và drag prompt panel)
                if (ingredientPanel != null) ingredientPanel.SetActive(false);
                if (rollingPanel != null) rollingPanel.SetActive(false);
                break;
            default:
                interactPromptText.gameObject.SetActive(false);
                if (ingredientPanel != null) ingredientPanel.SetActive(false);
                if (rollingPanel != null) rollingPanel.SetActive(false);
                break;
        }
    }
}
