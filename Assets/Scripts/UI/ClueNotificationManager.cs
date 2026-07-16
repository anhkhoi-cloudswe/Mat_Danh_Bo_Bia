using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton quản lý Toast Notification hiển thị manh mối/bằng chứng thu thập được.
/// Pop-up trượt từ ngoài góc trên-phải màn hình vào, đứng im 3.5 giây, rồi tự
/// Fade-out (CanvasGroup.alpha về 0) và ẩn đi.
///
/// Lưu ý kỹ thuật (giống <see cref="DialogueScreenUI"/>): Screen Space Overlay
/// canvas tạo lúc runtime KHÔNG render trong setup URP của project này, nên ở đây
/// dùng World Space canvas gắn trước mặt camera + material UI với ZTest=Always để
/// luôn vẽ đè lên geometry. Tự khởi tạo khi vào Play mode — không cần kéo thả gì.
/// </summary>
public class ClueNotificationManager : MonoBehaviour
{
    public static ClueNotificationManager Instance { get; private set; }

    // --- Kích thước & vị trí canvas (local so với camera) ---
    private const float CanvasWidth = 620f;
    private const float CanvasHeight = 210f;
    private const float CanvasScale = 0.00062f;
    private const float DistanceFromCamera = 0.5f;

    // Vị trí khi hiện (góc trên-phải) và khi ẩn (trượt ra ngoài bên phải).
    private const float ShownX = 0.30f;
    private const float HiddenX = 0.78f;
    private const float PosY = 0.19f;

    // --- Thời gian hiệu ứng ---
    private const float SlideInDuration = 0.4f;
    private const float HoldDuration = 3.5f;
    private const float FadeOutDuration = 0.6f;

    private static readonly Color CharcoalColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);

    private Camera attachedCamera;
    private CanvasGroup canvasGroup;
    private Text contentText;
    private Coroutine activeRoutine;
    private Vector3 currentCameraLocalPosition;

    private Vector3 ShownLocalPos => new Vector3(ShownX, PosY, DistanceFromCamera);
    private Vector3 HiddenLocalPos => new Vector3(HiddenX, PosY, DistanceFromCamera);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ClueNotificationManager");
        go.AddComponent<ClueNotificationManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
        currentCameraLocalPosition = HiddenLocalPos;
    }

    private void Update()
    {
        // Bám theo camera bằng world transform nhưng KHÔNG parent vào camera.
        // Nhờ đó manager vẫn nằm trong DontDestroyOnLoad khi đổi scene.
        if (attachedCamera == null || !attachedCamera.isActiveAndEnabled)
        {
            attachedCamera = Camera.main;
        }

        if (attachedCamera != null)
        {
            transform.position = attachedCamera.transform.TransformPoint(currentCameraLocalPosition);
            transform.rotation = attachedCamera.transform.rotation;
            transform.localScale = Vector3.one * CanvasScale;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Hiển thị một thông báo Toast với nội dung <paramref name="message"/>.
    /// Nếu đang có thông báo hiển thị, thông báo mới sẽ thay thế ngay lập tức.
    /// </summary>
    public void ShowNotification(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (contentText != null)
        {
            contentText.text = message;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(NotificationRoutine());
    }

    private IEnumerator NotificationRoutine()
    {
        // --- Trượt vào ---
        canvasGroup.alpha = 1f;
        float t = 0f;
        while (t < SlideInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / SlideInDuration));
            currentCameraLocalPosition = Vector3.Lerp(HiddenLocalPos, ShownLocalPos, k);
            yield return null;
        }
        currentCameraLocalPosition = ShownLocalPos;

        // --- Đứng im ---
        yield return new WaitForSeconds(HoldDuration);

        // --- Fade-out ---
        t = 0f;
        while (t < FadeOutDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / FadeOutDuration));
            yield return null;
        }

        // --- Ẩn & reset về vị trí ngoài màn hình ---
        canvasGroup.alpha = 0f;
        currentCameraLocalPosition = HiddenLocalPos;
        activeRoutine = null;
    }

    private void BuildUI()
    {
        // Canvas world-space — pattern duy nhất render ổn định trong project này.
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 600;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Material UI với ZTest=Always: luôn vẽ đè lên geometry, không bị che mất.
        Material alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // --- Nền đen Charcoal mờ phủ toàn canvas ---
        GameObject panel = new GameObject("CluePanel");
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image background = panel.AddComponent<Image>();
        background.color = CharcoalColor;
        background.material = alwaysOnTop;

        // --- Thanh viền nhấn (accent) màu vàng hồ sơ ở mép trái ---
        GameObject accentObj = new GameObject("AccentBar");
        accentObj.transform.SetParent(panel.transform, false);
        RectTransform accentRect = accentObj.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.offsetMin = new Vector2(0f, 0f);
        accentRect.offsetMax = new Vector2(10f, 0f); // Độ dày thanh viền 10px
        Image accentImage = accentObj.AddComponent<Image>();
        accentImage.color = new Color(1f, 0.84f, 0.27f, 1f); // Vàng hồ sơ
        accentImage.material = alwaysOnTop;

        // --- Text tiêu đề ---
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(35f, -75f);
        titleRect.offsetMax = new Vector2(-25f, -15f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.material = alwaysOnTop;
        titleText.font = legacyFont;
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(1f, 0.84f, 0.27f, 1f);
        titleText.supportRichText = true;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.text = "📁 HỒ SƠ ĐIỀU TRA CẬP NHẬT";

        Outline titleOutline = titleObj.AddComponent<Outline>();
        titleOutline.effectColor = Color.black;
        titleOutline.effectDistance = new Vector2(1.5f, -1.5f);
        titleOutline.useGraphicAlpha = true;

        // --- Text nội dung (biến message) ---
        GameObject contentObj = new GameObject("ContentText");
        contentObj.transform.SetParent(panel.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(35f, 18f);
        contentRect.offsetMax = new Vector2(-25f, -80f);

        contentText = contentObj.AddComponent<Text>();
        contentText.material = alwaysOnTop;
        contentText.font = legacyFont;
        contentText.fontSize = 28;
        contentText.lineSpacing = 1.15f;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.color = Color.white;
        contentText.supportRichText = true;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline contentOutline = contentObj.AddComponent<Outline>();
        contentOutline.effectColor = Color.black;
        contentOutline.effectDistance = new Vector2(1.5f, -1.5f);
        contentOutline.useGraphicAlpha = true;
    }
}
