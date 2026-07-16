using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý UI Menu chính cho game.
/// Tự động xây dựng giao diện Menu đẹp mắt, sang trọng (Glassmorphism, viền sáng, hiệu ứng hover)
/// nếu chạy trong scene Main_Menu mà chưa có sẵn giao diện.
/// Kết nối mượt mà tới scene Intro_CutScene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [Tooltip("Tên scene tiếp theo sau khi nhấn Bắt Đầu.")]
    public string introSceneName = "Intro_CutScene";

    [Tooltip("Thời gian fade out trước khi load scene.")]
    public float fadeDuration = 1.0f;

    [Header("UI Design Colors (Premium Palette)")]
    public Color backgroundGradientTop = new Color(0.04f, 0.05f, 0.08f, 1f);      // Đen xanh sâu thẳm
    public Color backgroundGradientBottom = new Color(0.12f, 0.14f, 0.22f, 1f);   // Xanh hải quân tối
    public Color accentColor = new Color(0.48f, 0.98f, 0.6f, 1f);                 // Xanh lá sáng neon (đồng bộ)
    public Color buttonNormalColor = new Color(0.15f, 0.16f, 0.24f, 0.85f);        // Than tối xám xanh
    public Color buttonHoverColor = new Color(0.22f, 0.35f, 0.3f, 0.95f);          // Tone xanh đậm khi hover

    // UI sub-panel references for transitions
    private GameObject disclaimerPanel;
    private GameObject trollPanel;
    private GameObject menuPanel;

    private CanvasGroup menuCanvasGroup;
    private Image fadeOverlay;
    private bool isTransitioning = false;

    private Image trollImage;
    private Image bgImage;
    private AudioSource audioSource;

    private GameObject settingsPanel;
    private Text volumeText;
    private Text graphicsText;
    private Text resolutionText;
    private int currentVolume = 80;
    private int currentGraphics = 2;
    private int currentResolution = 0;

    [Header("3D Scene Setup")]
    [Tooltip("Audio clip troll chơi khi bấm Tôi đồng tính.")]
    public AudioClip trollSound;
    [Tooltip("Meme chihuahua gay sprite. Tự động tìm kiếm nếu để trống.")]
    public Sprite chihuahuaMemeSprite;

    [Header("Soundtrack Settings")]
    [Tooltip("Soundtrack phát lặp lại tại Main Menu.")]
    public AudioClip menuSoundtrack;

    public static bool hasAcceptedDisclaimer = false;

    private void Awake()
    {
        // Khởi tạo các cấu hình từ PlayerPrefs
        currentVolume = PlayerPrefs.GetInt("PrefVolume", 55);
        // Khôi phục cấu hình cũ bị mute để người chơi luôn nghe được thoại/cutscene.
        if (currentVolume <= 0)
        {
            currentVolume = 55;
            PlayerPrefs.SetInt("PrefVolume", currentVolume);
            PlayerPrefs.Save();
        }
        currentGraphics = PlayerPrefs.GetInt("PrefGraphics", 2);
        currentResolution = PlayerPrefs.GetInt("PrefResolution", 0);
        AudioListener.volume = currentVolume / 100f;
        QualitySettings.SetQualityLevel(currentGraphics);

        // Thiết lập AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Load soundtrack tự động
        if (menuSoundtrack == null)
        {
            menuSoundtrack = Resources.Load<AudioClip>("Soundtrack/Cinematic");
        }

        if (menuSoundtrack != null)
        {
            audioSource.clip = menuSoundtrack;
            audioSource.loop = true;
            audioSource.volume = currentVolume / 100f;
            audioSource.Play();
        }

        // Thiết lập cảnh 3D
        Setup3DScene();

        // Đảm bảo có EventSystem để nhận tương tác click chuột
        EnsureEventSystem();

        // Xây dựng UI nếu chưa có Canvas/Giao diện
        BuildMainMenuUI();

        if (hasAcceptedDisclaimer)
        {
            if (disclaimerPanel != null) disclaimerPanel.SetActive(false);
            if (trollPanel != null) trollPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
            if (bgImage != null)
            {
                bgImage.enabled = false;
            }
        }
        else
        {
            if (disclaimerPanel != null) disclaimerPanel.SetActive(true);
            if (trollPanel != null) trollPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
            if (bgImage != null)
            {
                bgImage.color = Color.black;
                bgImage.sprite = null;
                bgImage.enabled = true;
            }
        }
    }

    private void Start()
    {
        // Hiệu ứng mờ dần từ màu đen khi vừa mở game (hiện Disclaimer từ từ)
        if (fadeOverlay != null)
        {
            fadeOverlay.color = Color.black;
            fadeOverlay.gameObject.SetActive(true);
            StartCoroutine(StartFadeInRoutine());
        }
    }

    private IEnumerator StartFadeInRoutine()
    {
        float elapsed = 0f;
        float duration = 1.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, elapsed / duration));
            }
            yield return null;
        }
        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (menuSoundtrack == null)
        {
            menuSoundtrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Soundtrack/Cinematic.mp3");
        }
    }
#endif

    public void Setup3DScene()
    {
        // 1. Cấu hình Camera chính
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        // Dọn dẹp BoBiaMechanic trên Xe_Bo_Bia ở Main Menu để tránh lỗi đè Singleton
        GameObject menuCart = GameObject.Find("Xe_Bo_Bia_Root");
        if (menuCart != null)
        {
            var mech = menuCart.GetComponent<BoBiaMechanic>();
            if (mech != null) DestroyImmediate(mech);
        }
        GameObject menuCartChild = GameObject.Find("Xe_Bo_Bia");
        if (menuCartChild != null)
        {
            var mech = menuCartChild.GetComponent<BoBiaMechanic>();
            if (mech != null) DestroyImmediate(mech);
        }

        // Kiểm tra xem cảnh nền gameplay thực tế (Ground_Map01) đã được copy qua chưa
        if (GameObject.Find("Ground_Map01") != null)
        {
            // Thiết lập camera đúng góc nhìn chéo trước cửa nhà nhân vật chính nhìn ra ngõ (như hình 2)
            cam.transform.position = new Vector3(-5.51f, 1.13f, -3.49f);
            cam.transform.rotation = Quaternion.Euler(4.934f, 25.632f, -0.813f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);

            // Đảm bảo Directional Light giữ độ sáng màu tối ban đêm
            Light[] directionalLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in directionalLights)
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0.05f;
                    l.color = new Color(0.1f, 0.15f, 0.25f, 1f);
                }
            }

            // Bật sương mù tối sẫm màu
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.05f;

            // Cấu hình nhân vật MenuPlayerCharacter tránh bay lơ lửng
            GameObject player = GameObject.Find("MenuPlayerCharacter");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                var animator = player.GetComponent<Animator>();
                if (animator != null) animator.applyRootMotion = false;
            }

            // Tạo đèn Spot Light chiếu xuống đầu MenuPlayerCharacter
            GameObject playerSpot = GameObject.Find("PlayerSpotLight");
            if (playerSpot == null)
            {
                playerSpot = new GameObject("PlayerSpotLight");
            }
            Vector3 spotPos = new Vector3(-4.22f, 3.5f, -1.16f); // Vị trí mặc định
            if (player != null)
            {
                spotPos = player.transform.position + Vector3.up * 3.5f;
            }
            playerSpot.transform.position = spotPos;
            playerSpot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light pLight = playerSpot.GetComponent<Light>();
            if (pLight == null) pLight = playerSpot.AddComponent<Light>();
            pLight.type = LightType.Spot;
            pLight.range = 6f;
            pLight.spotAngle = 60f;
            pLight.intensity = 5.0f;
            pLight.color = new Color(1f, 0.95f, 0.8f);
            pLight.shadows = LightShadows.Soft;
            return;
        }

        // Tắt hoặc làm mờ Directional Light mặc định trong scene để tạo không khí âm u ( fallback )
        Light[] dLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in dLights)
        {
            if (l.type == LightType.Directional)
            {
                l.intensity = 0.05f; // Giảm độ sáng của ánh sáng toàn cảnh xuống cực thấp
                l.color = new Color(0.1f, 0.15f, 0.25f); // Xanh đêm tối
            }
        }

        // Vị trí và góc xoay Camera cho chế độ fallback (khi không copy map)
        cam.transform.position = new Vector3(-1.5f, 1.6f, -4f);
        cam.transform.rotation = Quaternion.Euler(8f, 18f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f); // Dark indigo/grey

        // 2. Tạo mặt đường Plane "Hẻm 113"
        GameObject road = GameObject.Find("AsphaltRoad");
        if (road == null)
        {
            road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            road.name = "AsphaltRoad";
        }
        road.transform.position = new Vector3(0f, 0f, 0f);
        road.transform.rotation = Quaternion.identity;
        road.transform.localScale = new Vector3(5f, 1f, 5f);

        // Tạo chất liệu Asphalt đen ướt bóng (Tương thích cả URP và Built-in)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material asphaltMat = new Material(shader);
        asphaltMat.color = new Color(0.05f, 0.05f, 0.07f, 1f); // Đen xám nhựa đường
        if (asphaltMat.HasProperty("_Smoothness")) asphaltMat.SetFloat("_Smoothness", 0.7f);
        else if (asphaltMat.HasProperty("_Glossiness")) asphaltMat.SetFloat("_Glossiness", 0.7f);
        if (asphaltMat.HasProperty("_Metallic")) asphaltMat.SetFloat("_Metallic", 0.2f);
        
        var renderer = road.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = asphaltMat;
        }

        // 3. Tải và tạo Xe Bò Bía từ FBX
        GameObject vehicle = GameObject.Find("Xe_Bo_Bia");
        if (vehicle == null)
        {
#if UNITY_EDITOR
            // Tải từ Assets folder bằng AssetDatabase trong Editor
            GameObject vehiclePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Xe_Bo_Bia/Meshy_AI_Snack_Delivery_Scoote_0608045356_texture.fbx");
            if (vehiclePrefab != null)
            {
                vehicle = Instantiate(vehiclePrefab);
                vehicle.name = "Xe_Bo_Bia";
            }
            else
            {
                Debug.LogWarning("Không tìm thấy file FBX Xe Bò Bía tại Assets/Models/Xe_Bo_Bia/...");
            }
#endif
        }

        if (vehicle != null)
        {
            // Đặt xe ở phía bên phải tầm nhìn camera, xoay hướng nhẹ về phía camera
            vehicle.transform.position = new Vector3(1.2f, 0f, 1f);
            vehicle.transform.rotation = Quaternion.Euler(0f, -145f, 0f);
            vehicle.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        }

        // 4. Tạo Spot Light rọi từ trên đỉnh xe xuống đất tạo bóng đổ
        GameObject spotObj = GameObject.Find("AtmosphericSpotLight");
        if (spotObj == null)
        {
            spotObj = new GameObject("AtmosphericSpotLight");
        }
        spotObj.transform.position = new Vector3(1.2f, 3.5f, 1f); // Ngay phía trên xe bò bia
        spotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Chiếu thẳng xuống
        
        Light spotLight = spotObj.GetComponent<Light>();
        if (spotLight == null)
        {
            spotLight = spotObj.AddComponent<Light>();
        }
        spotLight.type = LightType.Spot;
        spotLight.range = 8f;
        spotLight.spotAngle = 70f;
        spotLight.intensity = 3.5f;
        spotLight.color = new Color(0.48f, 0.98f, 0.6f); // Xanh neon đồng bộ với accent color để tạo không khí crime-thriller bí ẩn
        spotLight.shadows = LightShadows.Soft; // Đổ bóng mềm xuống mặt đường nhựa ẩm ướt

        // 5. Tạo nhà ở góc đường (tái hiện một góc nhìn trong game để làm phông nền)
        GameObject house = GameObject.Find("AlleyHouse");
        if (house == null)
        {
#if UNITY_EDITOR
            GameObject housePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Buildings/Area_01/House_Area_01_Fixed.fbx");
            if (housePrefab != null)
            {
                house = Instantiate(housePrefab);
                house.name = "AlleyHouse";
            }
#endif
        }

        if (house != null)
        {
            // Định vị nhà ở phía sau Xe Bò Bía và chéo một chút để tạo chiều sâu góc phố
            house.transform.position = new Vector3(-2f, 0f, 4.5f);
            house.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            house.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }

        // 6. Kích hoạt sương mù sẫm màu
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.05f, 1f); // Đồng bộ với màu nền camera
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.08f;
    }

    [ContextMenu("Setup 3D Scene")]
    public void EditorSetup3DScene()
    {
        Setup3DScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Đã khởi tạo và cấu hình cảnh 3D Xe Bò Bía và lưu vào scene.");
#endif
    }

    private void EnsureCamera()
    {
        // Đã được xử lý tích hợp bên trong Setup3DScene()
    }

    private void EnsureEventSystem()
    {
        // QUAN TRỌNG: Không có EventSystem thì UI Canvas sẽ KHÔNG NHẬN được click chuột!
        if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("Đã tự động khởi tạo EventSystem để xử lý tương tác click chuột.");
        }
    }

    private Sprite CreateGradientSprite(Color color1, Color color2)
    {
        Texture2D tex = new Texture2D(2, 128);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        
        for (int y = 0; y < 128; y++)
        {
            float t = (float)y / 127f;
            Color blendedColor = Color.Lerp(color1, color2, t);
            tex.SetPixel(0, y, blendedColor);
            tex.SetPixel(1, y, blendedColor);
        }
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, 2, 128), new Vector2(0.5f, 0.5f));
    }

    private void BuildMainMenuUI()
    {
        // 1. Tạo Canvas chính
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        menuCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

        // 2. Panel Nền Đen Đầy Màn Hình
        GameObject bgObj = new GameObject("BackgroundPanel");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        // ---------------------------------------------------------------------
        // A. PANEL DISCLAIMER (CẢNH BÁO)
        // ---------------------------------------------------------------------
        disclaimerPanel = new GameObject("DisclaimerPanel");
        disclaimerPanel.transform.SetParent(bgObj.transform, false);
        RectTransform discRect = disclaimerPanel.AddComponent<RectTransform>();
        discRect.anchorMin = Vector2.zero;
        discRect.anchorMax = Vector2.one;
        discRect.offsetMin = Vector2.zero;
        discRect.offsetMax = Vector2.zero;

        // Nội dung text cảnh báo
        GameObject discTitleObj = new GameObject("DisclaimerTitle");
        discTitleObj.transform.SetParent(disclaimerPanel.transform, false);
        RectTransform discTitleRect = discTitleObj.AddComponent<RectTransform>();
        discTitleRect.anchorMin = new Vector2(0.5f, 0.8f);
        discTitleRect.anchorMax = new Vector2(0.5f, 0.8f);
        discTitleRect.pivot = new Vector2(0.5f, 0.5f);
        discTitleRect.sizeDelta = new Vector2(1000, 80);
        Text discTitleText = discTitleObj.AddComponent<Text>();
        discTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        discTitleText.fontSize = 42;
        discTitleText.alignment = TextAnchor.MiddleCenter;
        discTitleText.color = Color.white;
        discTitleText.text = "WARNING / DISCLAIMER";

        GameObject discBodyObj = new GameObject("DisclaimerBody");
        discBodyObj.transform.SetParent(disclaimerPanel.transform, false);
        RectTransform discBodyRect = discBodyObj.AddComponent<RectTransform>();
        discBodyRect.anchorMin = new Vector2(0.5f, 0.5f);
        discBodyRect.anchorMax = new Vector2(0.5f, 0.5f);
        discBodyRect.pivot = new Vector2(0.5f, 0.5f);
        discBodyRect.sizeDelta = new Vector2(1200, 320);
        Text discBodyText = discBodyObj.AddComponent<Text>();
        discBodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        discBodyText.fontSize = 24;
        discBodyText.lineSpacing = 1.3f;
        discBodyText.alignment = TextAnchor.UpperLeft;
        discBodyText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        discBodyText.text = "1. Game có chứa một số nội dung và chủ đề nhạy cảm, có thể gây ám ảnh hoặc khó chịu cho một số người chơi.\n\n" +
                            "2. Cốt truyện được truyền cảm hứng từ các sự kiện có thật, nhưng đã được hư cấu hóa và kịch tính hóa hoàn toàn vì mục dịch giải trí.\n\n" +
                            "3. Game không nhằm đến, không cổ vũ, và không miệt thị bất kỳ cá nhân, tập thể hay tổ chức cụ thể nào, dù còn sống hay đã mất. Bất kỳ sự tương đồng nào với thực tế đều chỉ là ngẫu nhiên.";

        // Nút "Tôi đồng tính." (Accept)
        CreateDisclaimerButton(disclaimerPanel.transform, "Tôi đồng tính.", new Vector2(-250f, -220f), OnAcceptDisclaimer);

        // Nút "Tôi không đồng tính." (Decline/Quit)
        CreateDisclaimerButton(disclaimerPanel.transform, "Tôi không đồng tính.", new Vector2(250f, -220f), OnDeclineDisclaimer);

        // ---------------------------------------------------------------------
        // B. PANEL MENU CHÍNH (THIẾT KẾ MỚI THEO STYLE PHỞ ANH HAI)
        // ---------------------------------------------------------------------
        menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(bgObj.transform, false);
        RectTransform menuRect = menuPanel.AddComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;

        // Tiêu đề game: MẬT DANH BÒ BÍA (Cực ngầu, nằm bên trái trên góc hoặc chính giữa bên trái)
        GameObject titleObj = new GameObject("GameTitle");
        titleObj.transform.SetParent(menuPanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.75f);
        titleRect.anchorMax = new Vector2(0.4f, 0.75f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.sizeDelta = new Vector2(800, 160); // Tăng chiều rộng để vừa với font chữ lớn hơn

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 80; // Phóng to tiêu đề lên cỡ 80
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(1.0f, 0.15f, 0.15f, 1f); // Màu đỏ neon rực rỡ hơn
        titleText.text = "MẬT DANH BÒ BÍA";
        
        UnityEngine.UI.Outline titleOutline = titleObj.AddComponent<UnityEngine.UI.Outline>();
        titleOutline.effectColor = Color.black;
        titleOutline.effectDistance = new Vector2(2f, -2f);

        Shadow titleShadow = titleObj.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0.9f, 0.1f, 0.1f, 0.5f); // Bóng đổ hiệu ứng neon glow
        titleShadow.effectDistance = new Vector2(4f, -4f);

        // Thêm hiệu ứng nhấp nháy Neon cho tiêu đề
        titleObj.AddComponent<NeonFlickerEffect>();

        // Thanh ngang ngăn cách (Accent line) giữa tiêu đề và phụ đề
        GameObject accentLineObj = new GameObject("AccentLine");
        accentLineObj.transform.SetParent(menuPanel.transform, false);
        RectTransform lineRect = accentLineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.1f, 0.70f);
        lineRect.anchorMax = new Vector2(0.35f, 0.70f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.sizeDelta = new Vector2(0, 3); // Cao 3px, rộng tự động co giãn theo anchor
        Image lineImage = accentLineObj.AddComponent<Image>();
        lineImage.color = accentColor; // Màu xanh neon accent

        // Subtitle nhỏ mờ bên dưới tiêu đề
        GameObject subtitleObj = new GameObject("GameSubtitle");
        subtitleObj.transform.SetParent(menuPanel.transform, false);
        RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.1f, 0.66f);
        subtitleRect.anchorMax = new Vector2(0.4f, 0.66f);
        subtitleRect.pivot = new Vector2(0f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(600, 40);

        Text subtitleText = subtitleObj.AddComponent<Text>();
        subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subtitleText.fontSize = 18;
        subtitleText.alignment = TextAnchor.MiddleLeft;
        subtitleText.color = new Color(0.7f, 0.7f, 0.8f, 0.85f);
        subtitleText.text = "TACTICAL STEALTH CRIME TRIVIA GAME";

        // Layout các nút bấm xếp dọc bên trái màn hình kiểu Phở Anh Hai (Bỏ nút Endings)
        float startY = -40f;
        float spacingY = -75f;
        CreateLeftAlignedMenuButton(menuPanel.transform, "Mở Cửa", new Vector2(300f, startY), StartGame);
        CreateLeftAlignedMenuButton(menuPanel.transform, "Cài Đặt", new Vector2(300f, startY + spacingY), ShowSettingsPanel);
        CreateLeftAlignedMenuButton(menuPanel.transform, "Đóng Cửa", new Vector2(300f, startY + 2 * spacingY), ExitGame);

        // Copyright text bên góc dưới
        GameObject creditObj = new GameObject("CreditText");
        creditObj.transform.SetParent(menuPanel.transform, false);
        RectTransform creditRect = creditObj.AddComponent<RectTransform>();
        creditRect.anchorMin = new Vector2(0.1f, 0.08f);
        creditRect.anchorMax = new Vector2(0.4f, 0.08f);
        creditRect.pivot = new Vector2(0f, 0.5f);
        creditRect.sizeDelta = new Vector2(600, 30);

        Text creditText = creditObj.AddComponent<Text>();
        creditText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        creditText.fontSize = 14;
        creditText.alignment = TextAnchor.MiddleLeft;
        creditText.color = new Color(0.5f, 0.5f, 0.6f, 0.6f);
        creditText.text = "Ban Chuyên Án Phòng Chống Tội Phạm Ma Túy © 2026";


        // ---------------------------------------------------------------------
        // D. FADE OVERLAY
        // ---------------------------------------------------------------------
        GameObject fadeObj = new GameObject("FadeOverlay");
        fadeObj.transform.SetParent(canvasObj.transform, false);
        RectTransform fadeRect = fadeObj.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeOverlay = fadeObj.AddComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        fadeOverlay.raycastTarget = false;


        // ---------------------------------------------------------------------
        // E. PANEL CÀI ĐẶT (PRO MAX GLASSMORPHISM)
        // ---------------------------------------------------------------------
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(bgObj.transform, false);
        RectTransform setRect = settingsPanel.AddComponent<RectTransform>();
        setRect.anchorMin = new Vector2(0.5f, 0.5f);
        setRect.anchorMax = new Vector2(0.5f, 0.5f);
        setRect.pivot = new Vector2(0.5f, 0.5f);
        setRect.sizeDelta = new Vector2(700, 500);

        // Khung nền kính mờ (Glassmorphism)
        Image setBg = settingsPanel.AddComponent<Image>();
        setBg.color = new Color(0.08f, 0.09f, 0.15f, 0.92f); // Nền tối trong suốt
        
        UnityEngine.UI.Outline setOutline = settingsPanel.AddComponent<UnityEngine.UI.Outline>();
        setOutline.effectColor = accentColor; // Viền neon xanh sáng
        setOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Tiêu đề: CÀI ĐẶT hệ thống
        GameObject setHeader = new GameObject("Header");
        setHeader.transform.SetParent(settingsPanel.transform, false);
        RectTransform headerRect = setHeader.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.9f);
        headerRect.anchorMax = new Vector2(0.5f, 0.9f);
        headerRect.sizeDelta = new Vector2(600, 50);
        Text headerText = setHeader.AddComponent<Text>();
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 32;
        headerText.alignment = TextAnchor.MiddleCenter;
        headerText.color = accentColor;
        headerText.text = "THIẾT LẬP HỆ THỐNG";

        // Dòng Cài đặt 1: Âm lượng
        CreateSettingRow(settingsPanel.transform, "ÂM LƯỢNG", new Vector2(0, 100f), out volumeText, () => OnVolumeChange(-10), () => OnVolumeChange(10));
        UpdateVolumeUI();

        // Dòng Cài đặt 2: Đồ họa
        CreateSettingRowToggle(settingsPanel.transform, "ĐỒ HỌA", new Vector2(0, 10f), out graphicsText, OnGraphicsChange);
        UpdateGraphicsUI();

        // Dòng Cài đặt 3: Độ phân giải
        CreateSettingRowToggle(settingsPanel.transform, "ĐỘ PHÂN GIẢI", new Vector2(0, -80f), out resolutionText, OnResolutionChange);
        UpdateResolutionUI();

        // Nút "Quay lại & Lưu"
        CreateDisclaimerButton(settingsPanel.transform, "LƯU & QUAY LẠI", new Vector2(0f, -180f), HideSettingsPanel);

        // Ban đầu ẩn bảng cài đặt đi
        settingsPanel.SetActive(false);
    }

    private Sprite LoadTrollSprite()
    {
        Sprite sprite = chihuahuaMemeSprite;
        if (sprite != null) return sprite;

#if UNITY_EDITOR
        // Trong Editor, thử load trực tiếp Sprite hoặc Texture2D từ file
        sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/chihuahua_gay_meme.png");
        if (sprite == null)
        {
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/chihuahua_gay_meme.png");
        }
        
        if (sprite == null)
        {
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Image/chihuahua_gay_meme.png");
            if (tex == null)
            {
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/chihuahua_gay_meme.png");
            }
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
#else
        // Ở runtime/build, load từ Resources
        sprite = Resources.Load<Sprite>("chihuahua_gay_meme");
        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("chihuahua_gay_meme");
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
#endif
        return sprite;
    }

    private void CreateSettingRow(Transform parent, string title, Vector2 pos, out Text valText, UnityEngine.Events.UnityAction clickDec, UnityEngine.Events.UnityAction clickInc)
    {
        GameObject rowObj = new GameObject(title + "_Row");
        rowObj.transform.SetParent(parent, false);
        RectTransform rect = rowObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 50);
        rect.anchoredPosition = pos;

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform lblRect = labelObj.AddComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0, 0.5f);
        lblRect.anchorMax = new Vector2(0, 0.5f);
        lblRect.pivot = new Vector2(0, 0.5f);
        lblRect.sizeDelta = new Vector2(200, 40);
        Text lblText = labelObj.AddComponent<Text>();
        lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lblText.fontSize = 20;
        lblText.color = Color.white;
        lblText.text = title;

        // Nút giảm [-]
        GameObject btnDec = new GameObject("BtnDec");
        btnDec.transform.SetParent(rowObj.transform, false);
        RectTransform decRect = btnDec.AddComponent<RectTransform>();
        decRect.anchorMin = new Vector2(0.45f, 0.5f);
        decRect.anchorMax = new Vector2(0.45f, 0.5f);
        decRect.sizeDelta = new Vector2(40, 40);
        Image decImg = btnDec.AddComponent<Image>();
        decImg.color = buttonNormalColor;
        Button decBtnComp = btnDec.AddComponent<Button>();
        decBtnComp.onClick.AddListener(clickDec);
        var decTextObj = new GameObject("Text");
        decTextObj.transform.SetParent(btnDec.transform, false);
        RectTransform decTextRect = decTextObj.AddComponent<RectTransform>();
        decTextRect.anchorMin = Vector2.zero;
        decTextRect.anchorMax = Vector2.one;
        decTextRect.offsetMin = Vector2.zero;
        decTextRect.offsetMax = Vector2.zero;
        Text decText = decTextObj.AddComponent<Text>();
        decText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        decText.fontSize = 22;
        decText.color = Color.white;
        decText.alignment = TextAnchor.MiddleCenter;
        decText.text = "-";
        btnDec.AddComponent<ButtonHoverEffect>().normalColor = buttonNormalColor;
        btnDec.GetComponent<ButtonHoverEffect>().hoverColor = buttonHoverColor;

        // Giá trị text ở giữa
        GameObject valObj = new GameObject("ValText");
        valObj.transform.SetParent(rowObj.transform, false);
        RectTransform valRect = valObj.AddComponent<RectTransform>();
        valRect.anchorMin = new Vector2(0.7f, 0.5f);
        valRect.anchorMax = new Vector2(0.7f, 0.5f);
        valRect.sizeDelta = new Vector2(240, 40);
        valText = valObj.AddComponent<Text>();
        valText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valText.fontSize = 18;
        valText.color = accentColor;
        valText.alignment = TextAnchor.MiddleCenter;

        // Nút tăng [+]
        GameObject btnInc = new GameObject("BtnInc");
        btnInc.transform.SetParent(rowObj.transform, false);
        RectTransform incRect = btnInc.AddComponent<RectTransform>();
        incRect.anchorMin = new Vector2(0.95f, 0.5f);
        incRect.anchorMax = new Vector2(0.95f, 0.5f);
        incRect.sizeDelta = new Vector2(40, 40);
        Image incImg = btnInc.AddComponent<Image>();
        incImg.color = buttonNormalColor;
        Button incBtnComp = btnInc.AddComponent<Button>();
        incBtnComp.onClick.AddListener(clickInc);
        var incTextObj = new GameObject("Text");
        incTextObj.transform.SetParent(btnInc.transform, false);
        RectTransform incTextRect = incTextObj.AddComponent<RectTransform>();
        incTextRect.anchorMin = Vector2.zero;
        incTextRect.anchorMax = Vector2.one;
        incTextRect.offsetMin = Vector2.zero;
        incTextRect.offsetMax = Vector2.zero;
        Text incText = incTextObj.AddComponent<Text>();
        incText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        incText.fontSize = 22;
        incText.color = Color.white;
        incText.alignment = TextAnchor.MiddleCenter;
        incText.text = "+";
        btnInc.AddComponent<ButtonHoverEffect>().normalColor = buttonNormalColor;
        btnInc.GetComponent<ButtonHoverEffect>().hoverColor = buttonHoverColor;
    }

    private void CreateSettingRowToggle(Transform parent, string title, Vector2 pos, out Text valText, UnityEngine.Events.UnityAction clickToggle)
    {
        GameObject rowObj = new GameObject(title + "_Row");
        rowObj.transform.SetParent(parent, false);
        RectTransform rect = rowObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 50);
        rect.anchoredPosition = pos;

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform lblRect = labelObj.AddComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0, 0.5f);
        lblRect.anchorMax = new Vector2(0, 0.5f);
        lblRect.pivot = new Vector2(0, 0.5f);
        lblRect.sizeDelta = new Vector2(200, 40);
        Text lblText = labelObj.AddComponent<Text>();
        lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lblText.fontSize = 20;
        lblText.color = Color.white;
        lblText.text = title;

        // Nút nhấn để thay đổi giá trị
        GameObject btnToggle = new GameObject("BtnToggle");
        btnToggle.transform.SetParent(rowObj.transform, false);
        RectTransform toggleRect = btnToggle.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.7f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.7f, 0.5f);
        toggleRect.sizeDelta = new Vector2(300, 40);
        Image toggleImg = btnToggle.AddComponent<Image>();
        toggleImg.color = buttonNormalColor;
        Button toggleBtnComp = btnToggle.AddComponent<Button>();
        toggleBtnComp.onClick.AddListener(clickToggle);
        btnToggle.AddComponent<ButtonHoverEffect>().normalColor = buttonNormalColor;
        btnToggle.GetComponent<ButtonHoverEffect>().hoverColor = buttonHoverColor;

        // Giá trị text trên nút
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnToggle.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        valText = textObj.AddComponent<Text>();
        valText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valText.fontSize = 18;
        valText.color = accentColor;
        valText.alignment = TextAnchor.MiddleCenter;
    }

    private void UpdateVolumeUI()
    {
        if (volumeText == null) return;
        int blocks = currentVolume / 10;
        string bar = "";
        for (int i = 0; i < 10; i++)
        {
            if (i < blocks) bar += "█";
            else bar += "░";
        }
        volumeText.text = currentVolume + "% [" + bar + "]";
    }

    private void UpdateGraphicsUI()
    {
        if (graphicsText == null) return;
        string[] levels = { "MƯỢT MÀ (LOW)", "CÂN BẰNG (MEDIUM)", "TỐI ƯU (HIGH)" };
        graphicsText.text = levels[currentGraphics];
    }

    private void UpdateResolutionUI()
    {
        if (resolutionText == null) return;
        string[] resNames = { "1920 x 1080 (16:9)", "1280 x 720 (16:9)", "2560 x 1440 (16:9)" };
        resolutionText.text = resNames[currentResolution];
    }

    private void OnVolumeChange(int delta)
    {
        currentVolume = Mathf.Clamp(currentVolume + delta, 0, 100);
        AudioListener.volume = currentVolume / 100f;
        if (audioSource != null)
        {
            audioSource.volume = currentVolume / 100f;
        }
        UpdateVolumeUI();
    }

    private void OnGraphicsChange()
    {
        currentGraphics = (currentGraphics + 1) % 3;
        QualitySettings.SetQualityLevel(currentGraphics);
        UpdateGraphicsUI();
    }

    private void OnResolutionChange()
    {
        currentResolution = (currentResolution + 1) % 3;
        int width = 1920;
        int height = 1080;
        if (currentResolution == 1) { width = 1280; height = 720; }
        else if (currentResolution == 2) { width = 2560; height = 1440; }
        
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        UpdateResolutionUI();
    }

    private void ShowSettingsPanel()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    private void HideSettingsPanel()
    {
        PlayerPrefs.SetInt("PrefVolume", currentVolume);
        PlayerPrefs.SetInt("PrefGraphics", currentGraphics);
        PlayerPrefs.SetInt("PrefResolution", currentResolution);
        PlayerPrefs.Save();

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    private void OnAcceptDisclaimer()
    {
        if (isTransitioning) return;
        hasAcceptedDisclaimer = true;
        StartCoroutine(TransitionToMainMenuRoutine());
    }

    private void OnDeclineDisclaimer()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator TransitionToMainMenuRoutine()
    {
        isTransitioning = true;

        // 1. Fade out màn disclaimer sang đen
        float elapsedFade = 0f;
        float fadeOutDuration = 0.5f;
        while (elapsedFade < fadeOutDuration)
        {
            elapsedFade += Time.deltaTime;
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsedFade / fadeOutDuration));
            }
            yield return null;
        }

        // 2. Tắt disclaimer panel, hiện menu panel
        if (disclaimerPanel != null) disclaimerPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);

        // 3. Tắt nền Canvas chính (background panel image) để lộ cảnh 3D phía sau
        if (bgImage != null)
        {
            bgImage.enabled = false;
        }

        // 4. Fade out màn đen để hiện Menu trên cảnh 3D
        elapsedFade = 0f;
        while (elapsedFade < fadeOutDuration)
        {
            elapsedFade += Time.deltaTime;
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, elapsedFade / fadeOutDuration));
            }
            yield return null;
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        }

        isTransitioning = false;
    }

    private void CreateDisclaimerButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction clickAction)
    {
        GameObject btnObj = new GameObject(label + "Button");
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 60);
        btnRect.anchoredPosition = pos;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = buttonNormalColor;

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnImg;
        button.onClick.AddListener(clickAction);

        var hoverEffect = btnObj.AddComponent<ButtonHoverEffect>();
        hoverEffect.normalColor = buttonNormalColor;
        hoverEffect.hoverColor = buttonHoverColor;
        hoverEffect.accentColor = accentColor;

        UnityEngine.UI.Outline btnOutline = btnObj.AddComponent<UnityEngine.UI.Outline>();
        btnOutline.effectColor = new Color(1f, 1f, 1f, 0.2f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
    }

    private void CreateLeftAlignedMenuButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction clickAction)
    {
        GameObject btnObj = new GameObject(label + "Button");
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.1f, 0.5f);
        btnRect.anchorMax = new Vector2(0.1f, 0.5f);
        btnRect.pivot = new Vector2(0f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 55);
        btnRect.anchoredPosition = pos;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = buttonNormalColor;

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnImg;
        button.onClick.AddListener(clickAction);

        var hoverEffect = btnObj.AddComponent<ButtonHoverEffect>();
        hoverEffect.normalColor = buttonNormalColor;
        hoverEffect.hoverColor = buttonHoverColor;
        hoverEffect.accentColor = accentColor;

        UnityEngine.UI.Outline btnOutline = btnObj.AddComponent<UnityEngine.UI.Outline>();
        btnOutline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;

        // Thêm một vạch dọc trang trí màu accentColor ở cạnh trái của nút
        GameObject borderLine = new GameObject("LeftBorder");
        borderLine.transform.SetParent(btnObj.transform, false);
        RectTransform borderRect = borderLine.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 0.1f);
        borderRect.anchorMax = new Vector2(0f, 0.9f);
        borderRect.pivot = new Vector2(0f, 0.5f);
        borderRect.anchoredPosition = new Vector2(6f, 0f);
        borderRect.sizeDelta = new Vector2(4f, 0f); // Rộng 4px, co giãn theo chiều dọc
        Image borderImg = borderLine.AddComponent<Image>();
        borderImg.color = accentColor;
    }

    private void StartGame()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToSceneRoutine(introSceneName));
    }

    private void ExitGame()
    {
        if (isTransitioning) return;
        Debug.Log("Thoát Game!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator TransitionToSceneRoutine(string sceneName)
    {
        isTransitioning = true;
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            
            // Fade màn hình menu sang màu đen
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, progress);
            }
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }
}

/// <summary>
/// Hiệu ứng hover đổi màu text cho menu chính kiểu tối giản
/// </summary>
public class MenuTextHoverEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public Color normalColor;
    public Color hoverColor;
    public Text targetText;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (targetText != null) targetText.color = hoverColor;
        transform.localScale = originalScale * 1.05f; // Tăng nhẹ 5% để tăng tính phản hồi
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (targetText != null) targetText.color = normalColor;
        transform.localScale = originalScale;
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
    }
}

/// <summary>
/// Script bổ trợ tạo hiệu ứng hover mượt mà và trực quan cho nút bấm.
/// </summary>
public class ButtonHoverEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public Color normalColor;
    public Color hoverColor;
    public Color accentColor;

    private Image buttonImage;
    private UnityEngine.UI.Outline outline;
    private Vector3 originalScale;

    private void Awake()
    {
        buttonImage = GetComponent<Image>();
        outline = GetComponent<UnityEngine.UI.Outline>();
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (buttonImage != null) buttonImage.color = hoverColor;
        if (outline != null) outline.effectColor = accentColor;
        transform.localScale = originalScale * 1.05f; // Phóng to nhẹ 5%
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (buttonImage != null) buttonImage.color = normalColor;
        if (outline != null) outline.effectColor = new Color(1f, 1f, 1f, 0.2f);
        transform.localScale = originalScale;
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
    }
}

/// <summary>
/// Hiệu ứng nhấp nháy đèn neon (neon flicker) cho Text tiêu đề game
/// </summary>
public class NeonFlickerEffect : MonoBehaviour
{
    private Text targetText;
    private Color originalColor;
    public Color flickerColor = new Color(0.3f, 0.05f, 0.05f, 1f); // Màu tối khi đèn "tắt"
    public float flickerSpeed = 0.07f;
    
    private void Awake()
    {
        targetText = GetComponent<Text>();
        if (targetText != null)
        {
            originalColor = targetText.color;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            if (targetText == null) yield break;
            
            // Random nhấp nháy ngẫu nhiên mô phỏng đèn neon bị hỏng
            float rand = Random.value;
            if (rand < 0.05f)
            {
                // Tắt ngẫu nhiên trong khoảng ngắn
                targetText.color = flickerColor;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }
            else if (rand < 0.12f)
            {
                // Nhấp nháy nhanh liên tục
                targetText.color = flickerColor;
                yield return new WaitForSeconds(flickerSpeed);
                targetText.color = originalColor;
                yield return new WaitForSeconds(flickerSpeed);
                targetText.color = flickerColor;
                yield return new WaitForSeconds(flickerSpeed);
            }
            
            targetText.color = originalColor;
            
            // Pulse nhẹ (thay đổi alpha/độ sáng của màu gốc theo hình sin)
            float elapsed = 0f;
            float pulseDuration = Random.Range(1f, 3f);
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float pulse = 0.85f + Mathf.PingPong(Time.time * 2f, 0.15f);
                targetText.color = new Color(originalColor.r * pulse, originalColor.g * pulse, originalColor.b * pulse, originalColor.a);
                yield return null;
            }
        }
    }
}
