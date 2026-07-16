using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Điều khiển cutscene mở đầu chuyên án ma túy tại hẻm 113 đường 22 Yên Phụ.
/// Quản lý chuyển đổi camera Cinemachine chuyên nghiệp, hiển thị phụ đề tiếng Việt
/// qua DialogueScreenUI và tự động chuyển cảnh sang gameplay chính (Phase1).
/// </summary>
public class IntroCutsceneController : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [Tooltip("Camera toàn cảnh phòng họp.")]
    public CinemachineCamera vcamWide;

    [Tooltip("Camera cận cảnh Anh Công An (OTS từ vai nhân vật chính).")]
    public CinemachineCamera vcamOfficerOTS;

    [Tooltip("Camera cận cảnh Nhân vật chính (OTS từ vai công an).")]
    public CinemachineCamera vcamAgentOTS;

    [Tooltip("Camera cận cảnh bản đồ / tài liệu chuyên án trên bàn.")]
    public CinemachineCamera vcamMapFocus;

    [Header("UI Reference")]
    [Tooltip("CanvasGroup điều khiển fade in/out màn hình.")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Settings")]
    [Tooltip("Tên scene tiếp theo sau khi kết thúc cutscene.")]
    public string nextSceneName = "BaoScene";

    [Tooltip("Tốc độ fade in/out (1.0 = 1 giây).")]
    public float fadeSpeed = 1.0f;

    [Header("Text To Speech Audio")]
    [Tooltip("Danh sách các file âm thanh giọng đọc tương ứng với từng câu thoại.")]
    public AudioClip[] ttsAudioClips;
    private AudioSource ttsAudioSource;

    private DialogueScreenUI dialogueUI;
    private bool isSkipping = false;
    private Coroutine cutsceneCoroutine;

    // Các biến phục vụ logic Skip thông minh
    private float lastSkipPressTime = -999f;
    private const float SkipConfirmWindow = 3.0f;
    private CanvasGroup skipPromptCanvasGroup;
    private UnityEngine.UI.Text skipPromptText;
    private Coroutine skipPromptFadeCoroutine;

    private void Start()
    {
        // Buộc chuyển sang BaoScene để bỏ qua giá trị cũ trong Inspector
        nextSceneName = "BaoScene";

        // Intro có thể được bấm Play trực tiếp từ Editor, nên tự khôi phục âm lượng an toàn
        // thay vì phụ thuộc Main Menu. 55% đủ rõ thoại nhưng không quá lớn.
        int savedVolume = PlayerPrefs.GetInt("PrefVolume", 55);
        if (savedVolume <= 0)
        {
            savedVolume = 55;
            PlayerPrefs.SetInt("PrefVolume", savedVolume);
            PlayerPrefs.Save();
        }
        AudioListener.volume = Mathf.Clamp01(savedVolume / 100f);

        // Khởi động AudioSource và load giọng đọc
        ttsAudioSource = gameObject.AddComponent<AudioSource>();
        ttsAudioSource.playOnAwake = false;
        ttsAudioSource.loop = false;
        ttsAudioSource.volume = 1.0f;

        if (ttsAudioClips == null || ttsAudioClips.Length == 0)
        {
            var list = new List<AudioClip>();
            for (int i = 1; i <= 6; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>($"Soundtrack/IntroTTS/intro_officer_{i}");
                if (clip != null) list.Add(clip);
            }
            ttsAudioClips = list.ToArray();
        }

        // Tìm DialogueScreenUI trong scene (nếu chưa tự tạo sẽ tự động khởi tạo)
        dialogueUI = FindAnyObjectByType<DialogueScreenUI>();
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueScreenUI");
            dialogueUI = go.AddComponent<DialogueScreenUI>();
            DontDestroyOnLoad(go);
        }

        // Tự động tìm kiếm các tham chiếu Cinemachine Camera nếu bị thiếu (đề phòng mất GUID khi kéo code)
        if (vcamWide == null) vcamWide = GameObject.Find("vcamWide")?.GetComponent<CinemachineCamera>();
        if (vcamOfficerOTS == null) vcamOfficerOTS = GameObject.Find("vcamOfficerOTS")?.GetComponent<CinemachineCamera>();
        if (vcamAgentOTS == null) vcamAgentOTS = GameObject.Find("vcamAgentOTS")?.GetComponent<CinemachineCamera>();
        if (vcamMapFocus == null) vcamMapFocus = GameObject.Find("vcamMapFocus")?.GetComponent<CinemachineCamera>();

        // Tự động tìm kiếm CanvasGroup điều khiển Fade
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
            if (fadeCanvasGroup == null)
            {
                var canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    fadeCanvasGroup = canvas.GetComponentInChildren<CanvasGroup>();
                }
            }
        }
        
        // Khởi tạo UI hiển thị thông tin Bỏ qua (Skip)
        CreateSkipPromptUI();

        // Bắt đầu chuỗi cắt cảnh
        cutsceneCoroutine = StartCoroutine(CutsceneRoutine());
    }

    private void Update()
    {
        // Nhấp chuột hoặc bấm Space/Escape để bỏ qua
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)) && !isSkipping)
        {
            HandleSkipAttempt();
        }
    }

    private void CreateSkipPromptUI()
    {
        // Tạo Canvas riêng cho prompt để tránh xung đột
        GameObject canvasObj = new GameObject("SkipPromptCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Luôn hiển thị trên cùng

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        skipPromptCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        skipPromptCanvasGroup.alpha = 0f; // Bắt đầu ẩn hoàn toàn

        // Panel nền Glassmorphism nhỏ ở góc phải dưới
        GameObject panelObj = new GameObject("PromptPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.sizeDelta = new Vector2(420, 70);
        panelRect.anchoredPosition = new Vector2(-40f, 40f); // Cách góc 40px

        var bgImage = panelObj.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.85f); // Than tối trong suốt nhẹ

        var outline = panelObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outline.effectDistance = new Vector2(1f, -1f);

        // Text hướng dẫn
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panelObj.transform, false);
        var txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(15f, 5f);
        txtRect.offsetMax = new Vector2(-15f, -5f);

        skipPromptText = textObj.AddComponent<UnityEngine.UI.Text>();
        skipPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skipPromptText.fontSize = 20;
        skipPromptText.alignment = TextAnchor.MiddleCenter;
        skipPromptText.color = Color.white;
        skipPromptText.text = "Nhấp chuột hoặc bấm ESC/SPACE để bỏ qua";

        var shadow = textObj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void HandleSkipAttempt()
    {
        bool hasSeenCutscene = PlayerPrefs.GetInt("HasSeenCutscene", 0) == 1;

        // Nếu đã từng xem qua rồi, cho phép skip lập tức
        if (hasSeenCutscene)
        {
            StartCoroutine(SkipCutsceneRoutine());
            return;
        }

        // Nếu là lần đầu tiên chơi:
        float timeSinceLastPress = Time.time - lastSkipPressTime;
        if (timeSinceLastPress < SkipConfirmWindow)
        {
            // Lần bấm thứ 2 hợp lệ trong khoảng 3 giây -> Tiến hành skip
            HideSkipPrompt();
            StartCoroutine(SkipCutsceneRoutine());
        }
        else
        {
            // Bấm lần đầu: Ghi nhận thời gian và hiển thị prompt xác nhận
            lastSkipPressTime = Time.time;
            ShowSkipPrompt("Xác nhận bỏ qua? Bấm thêm lần nữa...");
        }
    }

    private void ShowSkipPrompt(string message)
    {
        if (skipPromptText != null)
        {
            skipPromptText.text = message;
            // Tô màu vàng cam nổi bật cho text xác nhận
            skipPromptText.color = new Color(1f, 0.75f, 0.3f, 1f); 
        }

        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
        }
        skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(1f, SkipConfirmWindow));
    }

    private void HideSkipPrompt()
    {
        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
        }
        skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(0f, 0f));
    }

    private IEnumerator FadeSkipPromptRoutine(float targetAlpha, float holdDuration)
    {
        float elapsed = 0f;
        float startAlpha = skipPromptCanvasGroup.alpha;

        // Fade In
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            skipPromptCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / 0.3f);
            yield return null;
        }
        skipPromptCanvasGroup.alpha = targetAlpha;

        if (holdDuration > 0f)
        {
            // Đợi 3 giây trước khi tự động fade out
            yield return new WaitForSeconds(holdDuration - 0.6f);

            // Fade Out tự động
            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                skipPromptCanvasGroup.alpha = Mathf.Lerp(targetAlpha, 0f, elapsed / 0.3f);
                yield return null;
            }
            skipPromptCanvasGroup.alpha = 0f;
        }
    }

    private void SetActiveCamera(CinemachineCamera activeCam)
    {
        // Reset priority của tất cả camera về 10
        if (vcamWide != null) vcamWide.Priority = 10;
        if (vcamOfficerOTS != null) vcamOfficerOTS.Priority = 10;
        if (vcamAgentOTS != null) vcamAgentOTS.Priority = 10;
        if (vcamMapFocus != null) vcamMapFocus.Priority = 10;

        // Tăng camera được chọn lên 20 để Cinemachine tự động blend qua
        if (activeCam != null) activeCam.Priority = 20;
    }

    private IEnumerator CutsceneRoutine()
    {
        // 1. Khởi động: Màn hình đen, bắt đầu Fade In
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;
        }

        SetActiveCamera(vcamWide);
        yield return new WaitForSeconds(0.5f);

        // Fade in màn hình (màu đen nhạt dần lộ ra phòng họp)
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime * fadeSpeed;
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed);
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
        }

        // Tự động hiển thị hướng dẫn skip nhạt ở góc màn hình sau khi fade in xong
        if (skipPromptCanvasGroup != null)
        {
            skipPromptText.text = "Nhấp chuột hoặc bấm ESC/SPACE để bỏ qua";
            skipPromptText.color = new Color(1f, 1f, 1f, 0.6f); // Trắng trong mờ để không gây xao nhãng
            skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(1f, 4f)); // Hiển thị 4 giây rồi ẩn
        }

        yield return new WaitForSeconds(0.5f);

        // Đoạn 1: Chào hỏi, đặt vấn đề (Camera toàn cảnh)
        float d1 = PlayTTSAndShowSubtitle(0, "Chào đồng chí. Hôm nay tôi giao cho đồng chí một chuyên án đặc biệt quan trọng.", 4.5f);
        yield return new WaitForSeconds(d1);

        // Đoạn 2: Giới thiệu vụ án (Cận cảnh Công An giao việc)
        SetActiveCamera(vcamOfficerOTS);
        float d2 = PlayTTSAndShowSubtitle(1, "Chúng ta cần triệt phá một đường dây ma túy quy mô lớn mới phát hiện.", 5.0f);
        yield return new WaitForSeconds(d2);

        // Đoạn 3: Địa điểm (Camera zoom vào bản đồ và tài liệu chuyên án trên bàn)
        SetActiveCamera(vcamMapFocus);
        float d3 = PlayTTSAndShowSubtitle(2, "Địa bàn hoạt động của chúng tại hẻm 113, đường 22, Yên Phụ, Hà Nội.", 5.5f);
        yield return new WaitForSeconds(d3);

        // Đoạn 4: Nhiệm vụ cải trang (Cận cảnh nhân vật chính lắng nghe quyết tâm)
        SetActiveCamera(vcamAgentOTS);
        float d4 = PlayTTSAndShowSubtitle(3, "Đồng chí hãy cải trang thành người bán bò bía để tiếp cận và thu thập bằng chứng.", 6.0f);
        yield return new WaitForSeconds(d4);

        // Đoạn 5: Cảnh báo an toàn (Quay lại cận cảnh công an, tạo sự nghiêm túc)
        SetActiveCamera(vcamOfficerOTS);
        float d5 = PlayTTSAndShowSubtitle(4, "Yêu cầu tuyệt đối bảo mật, không bứt dây động rừng và đảm bảo an toàn.", 5.0f);
        yield return new WaitForSeconds(d5);

        // Đoạn 6: Agent cam kết (Cận cảnh nhân vật chính chào nghiêm nghị)
        SetActiveCamera(vcamAgentOTS);
        float d6 = PlayTTSAndShowSubtitle(5, "Rõ! Tôi xin hứa sẽ hoàn thành xuất sắc nhiệm vụ!", 3.0f);
        yield return new WaitForSeconds(d6);

        // Xóa phụ đề cuối cùng
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow("", 0f);
        }

        // Đánh dấu đã xem xong cutscene một cách trọn vẹn
        PlayerPrefs.SetInt("HasSeenCutscene", 1);
        PlayerPrefs.Save();

        // Fade out màn hình về đen trước khi chuyển cảnh
        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime * fadeSpeed;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(0.8f);

        // Tải scene gameplay chính
        SceneManager.LoadScene(nextSceneName);
    }

    private float PlayTTSAndShowSubtitle(int index, string message, float fallbackDuration)
    {
        float duration = fallbackDuration;
        if (ttsAudioClips != null && index < ttsAudioClips.Length && ttsAudioClips[index] != null)
        {
            AudioClip clip = ttsAudioClips[index];
            duration = clip.length + 0.5f; // Đợi thêm 0.5s cho tự nhiên
            if (ttsAudioSource != null)
            {
                ttsAudioSource.clip = clip;
                ttsAudioSource.Play();
            }
        }
        ShowSubtitle(message, duration);
        return duration;
    }

    private void ShowSubtitle(string message, float duration)
    {
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow(message, duration);
        }
    }

    private IEnumerator SkipCutsceneRoutine()
    {
        isSkipping = true;

        // Lưu trạng thái đã xem cutscene để lần sau được skip lập tức
        PlayerPrefs.SetInt("HasSeenCutscene", 1);
        PlayerPrefs.Save();

        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
        }
        
        // Cần đảm bảo nếu dialogueUI đang hiển thị phụ đề thì phải ẩn đi lập tức
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow("", 0f);
        }

        // Tắt giọng đọc ngay lập tức nếu đang phát để tránh tiếng bị kéo sang scene sau
        if (ttsAudioSource != null && ttsAudioSource.isPlaying)
        {
            ttsAudioSource.Stop();
        }

        // Fade sang đen nhanh chóng để chuyển cảnh mượt mà
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            float elapsed = 0f;
            float startAlpha = fadeCanvasGroup.alpha;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime * 2f;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / 0.5f);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(nextSceneName);
    }
}
