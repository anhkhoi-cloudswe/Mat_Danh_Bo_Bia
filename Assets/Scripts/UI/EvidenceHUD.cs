using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD hiển thị liên tục trên màn hình: tổng điểm bằng chứng đã thu thập.
/// Nằm ở góc trên-trái, luôn hiện khi đang trong game.
///
/// Pattern: World Space canvas gắn trước mặt camera + material UI ZTest=Always
/// (giống DialogueScreenUI & ClueNotificationManager) — cách duy nhất render
/// ổn định trong setup URP của project này.
/// </summary>
public class EvidenceHUD : MonoBehaviour
{
    // --- Kích thước & vị trí canvas (local so với camera) ---
    private const float CanvasWidth  = 480f;
    private const float CanvasHeight = 120f;
    private const float CanvasScale  = 0.00052f;
    private const float DistanceFromCamera = 0.5f;

    // Vị trí góc trên-trái
    private const float PosX = -0.32f;
    private const float PosY =  0.20f;

    // Hiệu ứng "pop" khi cộng điểm
    private const float PopScale    = 1.25f;
    private const float PopDuration = 0.35f;

    private Camera attachedCamera;
    private CanvasGroup canvasGroup;
    private Text scoreText;
    private Text labelText;
    private int displayedScore;
    private int displayedCount;
    private Coroutine popRoutine;
    private CaseManager subscribedManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Vô hiệu hóa HUD hiển thị liên tục (điểm số, số lượng chứng cứ) theo yêu cầu của user.
        return;
    }

    private void Awake()
    {
        BuildUI();
        UpdateDisplay(0, 0);
    }

    private void Update()
    {
        // Gắn canvas vào camera chính (camera có thể xuất hiện muộn / bị đổi)
        if (attachedCamera == null || !attachedCamera.isActiveAndEnabled)
        {
            attachedCamera = Camera.main;
            if (attachedCamera != null)
            {
                transform.SetParent(attachedCamera.transform, false);
                transform.localPosition = new Vector3(PosX, PosY, DistanceFromCamera);
                transform.localRotation = Quaternion.identity;
                transform.localScale    = new Vector3(CanvasScale, CanvasScale, CanvasScale);
            }
        }

        // Ẩn HUD nếu đang ở Main Menu hoặc Cutscene
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (canvasGroup != null)
        {
            bool shouldShow = (currentScene != "Main_Menu" && currentScene != "Intro_CutScene" && currentScene != "MiddleCutScene" && currentScene != "Final_CutScene");
            canvasGroup.alpha = shouldShow ? 1f : 0f;
        }

        // Đăng ký lắng nghe CaseManager khi nó xuất hiện
        if (subscribedManager == null && CaseManager.Instance != null)
        {
            subscribedManager = CaseManager.Instance;
            subscribedManager.OnScoreChanged      += HandleScoreChanged;
            subscribedManager.OnEvidenceCollected += HandleEvidenceCollected;
            subscribedManager.OnNpcClueCollected  += HandleNpcClueCollected;

            // Đồng bộ giá trị ban đầu
            UpdateDisplay(subscribedManager.EvidenceScore, subscribedManager.TotalEvidenceCount);
        }
    }

    private void OnDestroy()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnScoreChanged      -= HandleScoreChanged;
            subscribedManager.OnEvidenceCollected -= HandleEvidenceCollected;
            subscribedManager.OnNpcClueCollected  -= HandleNpcClueCollected;
        }
    }

    // ===================================================================
    // EVENT HANDLERS
    // ===================================================================

    private void HandleScoreChanged(int newScore)
    {
        int count = subscribedManager != null ? subscribedManager.TotalEvidenceCount : displayedCount;
        UpdateDisplay(newScore, count);
        PlayPopEffect();
    }

    private void HandleEvidenceCollected(EvidenceData _)
    {
        int count = subscribedManager != null ? subscribedManager.TotalEvidenceCount : displayedCount + 1;
        UpdateDisplay(displayedScore, count);
    }

    private void HandleNpcClueCollected()
    {
        int count = subscribedManager != null ? subscribedManager.TotalEvidenceCount : displayedCount + 1;
        UpdateDisplay(displayedScore, count);
    }

    // ===================================================================
    // DISPLAY
    // ===================================================================

    private void UpdateDisplay(int score, int count)
    {
        displayedScore = score;
        displayedCount = count;

        if (scoreText != null)
        {
            scoreText.text = $"{score}";
        }

        if (labelText != null)
        {
            labelText.text = $"CHỨNG CỨ  ({count} manh mối)";
        }
    }

    private void PlayPopEffect()
    {
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        if (scoreText == null) yield break;

        RectTransform rect = scoreText.GetComponent<RectTransform>();
        Vector3 originalScale = Vector3.one;

        // Phóng to
        float t = 0f;
        float halfDur = PopDuration * 0.5f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(1f, PopScale, Mathf.Clamp01(t / halfDur));
            rect.localScale = originalScale * k;
            yield return null;
        }

        // Thu nhỏ lại
        t = 0f;
        while (t < halfDur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(PopScale, 1f, Mathf.Clamp01(t / halfDur));
            rect.localScale = originalScale * k;
            yield return null;
        }

        rect.localScale = originalScale;
        popRoutine = null;
    }

    // ===================================================================
    // BUILD UI
    // ===================================================================

    private void BuildUI()
    {
        // Canvas world-space — pattern duy nhất render ổn định trong project này
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.WorldSpace;
        canvas.sortingOrder  = 550;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha           = 1f;
        canvasGroup.interactable    = false;
        canvasGroup.blocksRaycasts  = false;

        // Material ZTest=Always: luôn vẽ đè, không bị che
        Material alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // --- Nền bán trong suốt (charcoal đen mờ) ---
        GameObject panel = new GameObject("HUDPanel");
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panel.AddComponent<Image>();
        background.color    = new Color(0.08f, 0.08f, 0.10f, 0.75f);
        background.material = alwaysOnTop;

        // --- Viền trái accent vàng ---
        GameObject accentObj = new GameObject("AccentBar");
        accentObj.transform.SetParent(panel.transform, false);
        RectTransform accentRect = accentObj.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot     = new Vector2(0f, 0.5f);
        accentRect.offsetMin = new Vector2(0f, 0f);
        accentRect.offsetMax = new Vector2(8f, 0f);
        Image accentImage = accentObj.AddComponent<Image>();
        accentImage.color    = new Color(0.2f, 0.75f, 0.45f, 1f); // Xanh lá nhấn
        accentImage.material = alwaysOnTop;

        // --- Icon folder (text emoji) ---
        GameObject iconObj = new GameObject("IconText");
        iconObj.transform.SetParent(panel.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin        = new Vector2(0f, 0.5f);
        iconRect.anchorMax        = new Vector2(0f, 0.5f);
        iconRect.pivot            = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(22f, 0f);
        iconRect.sizeDelta        = new Vector2(55f, 55f);

        Text iconText = iconObj.AddComponent<Text>();
        iconText.material  = alwaysOnTop;
        iconText.font      = legacyFont;
        iconText.fontSize  = 40;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color     = new Color(0.2f, 0.75f, 0.45f, 1f);
        iconText.text      = "📁";

        // --- Label "CHỨNG CỨ (X manh mối)" ---
        GameObject labelObj = new GameObject("LabelText");
        labelObj.transform.SetParent(panel.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin        = new Vector2(0f, 1f);
        labelRect.anchorMax        = new Vector2(1f, 1f);
        labelRect.pivot            = new Vector2(0.5f, 1f);
        labelRect.offsetMin        = new Vector2(80f, -52f);
        labelRect.offsetMax        = new Vector2(-15f, -8f);

        labelText = labelObj.AddComponent<Text>();
        labelText.material           = alwaysOnTop;
        labelText.font               = legacyFont;
        labelText.fontSize           = 22;
        labelText.alignment          = TextAnchor.MiddleLeft;
        labelText.color              = new Color(0.65f, 0.65f, 0.70f, 1f); // Xám nhạt
        labelText.supportRichText    = true;
        labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
        labelText.verticalOverflow   = VerticalWrapMode.Overflow;

        Outline labelOutline = labelObj.AddComponent<Outline>();
        labelOutline.effectColor    = Color.black;
        labelOutline.effectDistance  = new Vector2(1f, -1f);
        labelOutline.useGraphicAlpha = true;

        // --- Số điểm lớn ---
        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(panel.transform, false);
        RectTransform scoreRect = scoreObj.AddComponent<RectTransform>();
        scoreRect.anchorMin        = new Vector2(0f, 0f);
        scoreRect.anchorMax        = new Vector2(1f, 1f);
        scoreRect.offsetMin        = new Vector2(80f, 5f);
        scoreRect.offsetMax        = new Vector2(-15f, -48f);

        scoreText = scoreObj.AddComponent<Text>();
        scoreText.material           = alwaysOnTop;
        scoreText.font               = legacyFont;
        scoreText.fontSize           = 42;
        scoreText.fontStyle          = FontStyle.Bold;
        scoreText.alignment          = TextAnchor.MiddleLeft;
        scoreText.color              = Color.white;
        scoreText.supportRichText    = true;
        scoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
        scoreText.verticalOverflow   = VerticalWrapMode.Overflow;

        Outline scoreOutline = scoreObj.AddComponent<Outline>();
        scoreOutline.effectColor    = Color.black;
        scoreOutline.effectDistance  = new Vector2(1.5f, -1.5f);
        scoreOutline.useGraphicAlpha = true;

        // --- Đơn vị "điểm" bên cạnh số ---
        GameObject unitObj = new GameObject("UnitText");
        unitObj.transform.SetParent(panel.transform, false);
        RectTransform unitRect = unitObj.AddComponent<RectTransform>();
        unitRect.anchorMin        = new Vector2(0f, 0f);
        unitRect.anchorMax        = new Vector2(1f, 1f);
        unitRect.offsetMin        = new Vector2(130f, 5f);
        unitRect.offsetMax        = new Vector2(-15f, -48f);

        Text unitText = unitObj.AddComponent<Text>();
        unitText.material           = alwaysOnTop;
        unitText.font               = legacyFont;
        unitText.fontSize           = 26;
        unitText.alignment          = TextAnchor.MiddleLeft;
        unitText.color              = new Color(0.65f, 0.65f, 0.70f, 1f);
        unitText.text               = "điểm";
        unitText.supportRichText    = true;

        Outline unitOutline = unitObj.AddComponent<Outline>();
        unitOutline.effectColor    = Color.black;
        unitOutline.effectDistance  = new Vector2(1f, -1f);
        unitOutline.useGraphicAlpha = true;
    }
}
