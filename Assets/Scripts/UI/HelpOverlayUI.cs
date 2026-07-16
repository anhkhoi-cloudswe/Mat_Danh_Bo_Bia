using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class HelpOverlayUI : MonoBehaviour
{
    private const float ViewWidth = 1400f; 
    private const float ViewHeight = 900f; 
    private const float CanvasScale = 0.00062f;
    private const float DistanceFromCamera = 0.5f;
    private const float VerticalOffset = 0f;

    private Camera attachedCamera;
    
    // CanvasGroup quản lý độc lập
    private CanvasGroup overlayCanvasGroup;
    private CanvasGroup hintCanvasGroup;
    
    private Text hintText;
    private GameObject overlayPanel;
    private GameObject hintCanvasObj; // Lưu trữ GameObject canvas overlay
    private bool isOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<HelpOverlayUI>() != null) return;
        var go = new GameObject("HelpOverlayUI");
        go.AddComponent<HelpOverlayUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        BuildUI();
    }

    private void OnDestroy()
    {
        // Dọn dẹp canvas overlay độc lập khi HelpOverlayUI bị hủy
        if (hintCanvasObj != null)
        {
            Destroy(hintCanvasObj);
        }
    }

    private void Update()
    {
        // Chỉ hoạt động và hiển thị ở scene BaoScene
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "BaoScene")
        {
            if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 0f;
            if (hintCanvasGroup != null) hintCanvasGroup.alpha = 0f;
            return;
        }

        // Nhấn H để đóng/mở hướng dẫn
        if (Input.GetKeyDown(KeyCode.H))
        {
            isOpen = !isOpen;
            if (overlayPanel != null) overlayPanel.SetActive(isOpen);
        }

        // 1. Quản lý hiển thị của hintText ở góc trái (chỉ hiện khi bảng hướng dẫn tắt)
        if (hintCanvasGroup != null)
        {
            float targetHintAlpha = isOpen ? 0f : 1f;
            hintCanvasGroup.alpha = Mathf.MoveTowards(hintCanvasGroup.alpha, targetHintAlpha, 5f * Time.deltaTime);
        }

        // Cập nhật nội dung hintText động theo tiến trình cuốn bánh dạng 0/N, 1/N
        if (hintText != null)
        {
            if (BoBiaMechanic.Instance != null)
            {
                bool isRolling = BoBiaMechanic.Instance.IsRolling;
                int current = BoBiaMechanic.Instance.CompletedRollCount;
                
                if (CustomerManager.Instance != null && CustomerManager.Instance.NextCustomer != null)
                {
                    int orderQty = CustomerManager.Instance.NextCustomer.orderQuantity;
                    if (orderQty > 1)
                    {
                        if (isRolling)
                        {
                            hintText.text = $"Nhấn <b>[H]</b> để mở bảng hướng dẫn\n<color=#FFAA00>Tiến trình: {current}/{orderQty} Bánh</color>";
                        }
                        else if (CustomerManager.Instance.CurrentCustomerState == CustomerManager.CustomerState.ReadyToRoll)
                        {
                            if (current == 0)
                            {
                                hintText.text = $"Nhấn <b>[H]</b> để mở bảng hướng dẫn\n<color=#FFAA00>Tiến trình: 0/{orderQty} Bánh (Nhấn [E] để bắt đầu)</color>";
                            }
                            else
                            {
                                hintText.text = $"Nhấn <b>[H]</b> để mở bảng hướng dẫn\n<color=#FFAA00>Tiến trình: {current}/{orderQty} Bánh (Nhấn [E] để cuốn tiếp)</color>";
                            }
                        }
                        else
                        {
                            hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn";
                        }
                    }
                    else
                    {
                        if (isRolling)
                        {
                            hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn\n<color=#FFAA00>Tiến trình: 0/1 Bánh</color>";
                        }
                        else
                        {
                            hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn";
                        }
                    }
                }
                else
                {
                    hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn";
                }
            }
            else
            {
                hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn";
            }
        }

        // 2. Quản lý hiển thị của bảng hướng dẫn ở giữa
        if (overlayCanvasGroup != null)
        {
            float targetOverlayAlpha = isOpen ? 1f : 0f;
            overlayCanvasGroup.alpha = Mathf.MoveTowards(overlayCanvasGroup.alpha, targetOverlayAlpha, 5f * Time.deltaTime);
            overlayCanvasGroup.interactable = isOpen;
            overlayCanvasGroup.blocksRaycasts = isOpen;
        }

        // Định vị Canvas World Space bám theo camera (chỉ bám bảng hướng dẫn 3D)
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
    }

    private void BuildUI()
    {
        // === 1. TẠO CANVAS OVERLAY RIÊNG CHO HINT TEXT (GÓC TRÁI TRÊN) ===
        // Canvas Screen Space Overlay độc lập hoàn toàn ở gốc, KHÔNG làm con của transform
        // để tránh bị Update di chuyển theo camera làm giật chữ khi người chơi di chuyển!
        hintCanvasObj = new GameObject("HelpHintCanvas");
        Canvas hintCanvas = hintCanvasObj.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hintCanvas.sortingOrder = 590; 
        DontDestroyOnLoad(hintCanvasObj); // Cùng tồn tại với HelpOverlayUI

        CanvasScaler hintScaler = hintCanvasObj.AddComponent<CanvasScaler>();
        hintScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hintScaler.referenceResolution = new Vector2(1920f, 1080f);

        hintCanvasGroup = hintCanvasObj.AddComponent<CanvasGroup>();
        hintCanvasGroup.alpha = 1f;

        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(hintCanvasObj.transform, false);
        RectTransform hintRect = hintObj.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 1f);
        hintRect.anchorMax = new Vector2(0f, 1f);
        hintRect.pivot = new Vector2(0f, 1f);
        // Đặt lề góc trên bên trái 60px từ cạnh (đủ thấp để nhìn rõ và không bị che)
        hintRect.anchoredPosition = new Vector2(60f, -60f);
        hintRect.sizeDelta = new Vector2(700f, 90f);

        Material alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        hintText = hintObj.AddComponent<Text>();
        hintText.material = alwaysOnTop;
        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 46; // Tăng cỡ chữ to hơn hẳn (46)
        hintText.alignment = TextAnchor.MiddleLeft;
        hintText.color = Color.white;
        hintText.text = "Nhấn <b>[H]</b> để mở bảng hướng dẫn";

        Outline hintOutline = hintObj.AddComponent<Outline>();
        hintOutline.effectColor = Color.black;
        hintOutline.effectDistance = new Vector2(2f, -2f);

        // === 2. TẠO CANVAS WORLD-SPACE CHO BẢNG HƯỚNG DẪN CHÍNH ===
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 600;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(ViewWidth, ViewHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        gameObject.AddComponent<GraphicRaycaster>();

        // 2. Bảng hướng dẫn chính (Overlay ở giữa màn hình)
        overlayPanel = new GameObject("OverlayPanel");
        overlayPanel.transform.SetParent(transform, false);
        RectTransform panelRect = overlayPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(150f, 100f); // Phóng to panel hướng dẫn
        panelRect.offsetMax = new Vector2(-150f, -100f);

        overlayCanvasGroup = overlayPanel.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;

        // Nền tối trong suốt sang trọng
        Image bgImage = overlayPanel.AddComponent<Image>();
        bgImage.color = new Color(0.02f, 0.02f, 0.02f, 0.9f);
        bgImage.material = alwaysOnTop;

        // Tiêu đề hướng dẫn
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(overlayPanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        titleRect.sizeDelta = new Vector2(0f, 80f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.material = alwaysOnTop;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 52; 
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.8f, 0f, 1f); 
        titleText.text = "HƯỚNG DẪN CHƠI & LÀM BÁNH";

        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = Color.black;
        titleOutline.effectDistance = new Vector2(3f, -3f);

        // Nội dung chi tiết hướng dẫn rút gọn, dễ hiểu
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(overlayPanel.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(50f, 50f);
        contentRect.offsetMax = new Vector2(-50f, -130f);

        Text contentText = contentObj.AddComponent<Text>();
        contentText.material = alwaysOnTop;
        contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        contentText.fontSize = 32; 
        contentText.lineSpacing = 1.4f;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.color = Color.white;
        contentText.supportRichText = true;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        
        contentText.text = 
            "<b>🎮 PHÍM ĐIỀU KHIỂN:</b>\n" +
            "• <b>W, A, S, D</b>: Di chuyển nhân vật.\n" +
            "• <b>Chuột</b>: Xoay góc nhìn.\n" +
            "• <b>E</b>: Tương tác.\n\n" +
            "<b>🌯 CÁC BƯỚC LÀM BÁNH:</b>\n" +
            "• <b>Bước 1:</b> Click <b>Bánh tráng</b>\n" +
            "• <b>Bước 2:</b> Click <b>Kẹo mạch nha</b>\n" +
            "• <b>Bước 3:</b> Click <b>Dừa nạo</b>\n" +
            "• <b>Bước 4:</b> Click <b>Mè đen</b>";

        Outline contentOutline = contentObj.AddComponent<Outline>();
        contentOutline.effectColor = Color.black;
        contentOutline.effectDistance = new Vector2(2f, -2f);

        // Mặc định ẩn overlay lúc bắt đầu
        overlayPanel.SetActive(false);
    }
}
