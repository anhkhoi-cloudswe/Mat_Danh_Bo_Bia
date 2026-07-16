using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Đạo diễn chuỗi sự kiện PHASE 2 — Ngày 2 (Sáng + Tối).
///
/// Buổi Sáng (Morning_Day2):
///   1. Bà Nga   — mua bò bía + thì thầm về kim tiêm đầu ngõ.
///   2. Shipper   — mua bò bía + kể chuyện Villa đặt đồ ăn mở tiệc.
///   3. Mê Liu    — mua bò bía + monologue nội tâm (mùi khai amoniac) + nói vu vơ tiệc tùng.
///   Sau Mê Liu   → chuyển cảnh sang Buổi Tối (Evening_Day2).
///
/// Buổi Tối (Evening_Day2): Placeholder — sẽ triển khai ở giai đoạn tiếp theo.
/// </summary>
[DisallowMultipleComponent]
public class StoryPhase2Manager : MonoBehaviour
{
    // ===================================================================
    // SINGLETON
    // ===================================================================

    public static StoryPhase2Manager Instance { get; private set; }

    // ===================================================================
    // ENUM
    // ===================================================================

    public enum Phase2TimeOfDay
    {
        Morning_Day2,
        Evening_Day2
    }

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("=== THỜI GIAN TRONG NGÀY ===")]
    [SerializeField] private Phase2TimeOfDay currentTime = Phase2TimeOfDay.Morning_Day2;
    public Phase2TimeOfDay CurrentTime => currentTime;

    [Header("=== Morning Day 2 — NPC Prefabs ===")]
    [Tooltip("Prefab Bà Nga (Nganpc) — khách thứ 1.")]
    [SerializeField] private GameObject baNgaPrefab;
    [Tooltip("Prefab Shipper — khách thứ 2.")]
    [SerializeField] private GameObject shipperPrefab;
    [Tooltip("Prefab Mê Liu (Miu Le) — khách thứ 3.")]
    [SerializeField] private GameObject meLiuPrefab;

    [Header("=== Tham chiếu ===")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera storyCamera;
    [SerializeField] private string playerTag = "Player";

    [Header("=== Player Spawn Theo Phase ===")]
    [Tooltip("Vị trí Player khi fade sang Sáng Ngày 2, cạnh quầy bò bía.")]
    [SerializeField] private Transform morningDay2PlayerSpawn;
    [Tooltip("Vị trí Player khi fade sang Tối Ngày 2, hướng về Villa/vùng mật phục.")]
    [SerializeField] private Transform eveningDay2PlayerSpawn;
    [Tooltip("Tọa độ đầu hẻm dùng cho Spawn Tối Ngày 2. Player sẽ nhìn về Hiding Zone.")]
    [SerializeField] private Vector3 eveningDay2SpawnPos = new Vector3(-10.5f, 0.06f, 7.5f);

    [Header("=== Mê Liu đi ra từ Villa (buổi sáng) ===")]
    [Tooltip("Vị trí cửa Villa nơi Mê Liu bước ra để đi mua bò bía và quay về sau khi mua. " +
             "Mặc định (-5,0.15,13.5) = sân trước Villa, nằm ở phía Đông tường chặn hẻm nên không xuyên tường.")]
    [SerializeField] private Vector3 meLiuVillaDoorPos = new Vector3(-5f, 0.15f, 13.5f);

    [Header("=== Suspicion ===")]
    [Tooltip("Mức tăng Suspicion khi phục vụ Mê Liu (phát hiện manh mối).")]
    [SerializeField] private float meLiuSuspicionIncrease = 15f;

    [Header("=== Vùng Mật Phục (Hiding Zone) — Buổi Tối ===")]
    [Tooltip("Vị trí vòng sáng xanh mật phục trong hành lang hẹp giữa tường và hông Villa (vùng khoanh đỏ).")]
    [SerializeField] private Vector3 hidingZonePos = new Vector3(8.9f, 0.06f, 9.7f);
    [Tooltip("Bán kính vùng mật phục.")]
    [SerializeField] private float hidingZoneRadius = 0.65f;
    [Tooltip("Thời gian mật phục trước khi cho phép đột nhập (khi chưa gắn cutscene ngoài).")]
    [SerializeField] private float surveillanceDuration = 3f;
    [Tooltip("Bật khi đã gắn cutscene riêng: chờ cutscene gọi CompleteSurveillance() thay vì timer.")]
    [SerializeField] private bool waitForExternalCutscene = false;

    [Header("=== Nghi phạm tại Villa — Buổi Tối ===")]
    [Tooltip("Vị trí Mê Liu tại bàn ghế sân sau, đồng bộ từ MiddleCutScene.")]
    [SerializeField] private Vector3 miuLeVillaPos = new Vector3(-1.03f, 0.02f, 30.41f);
    [SerializeField] private Vector3 miuLeVillaEuler = new Vector3(0f, 268.28f, 0f);
    [Tooltip("Vị trí Huy Sẹo tại bàn ghế sân sau, đồng bộ từ MiddleCutScene.")]
    [SerializeField] private Vector3 huySeoVillaCarPos = new Vector3(-1.94f, 0.01f, 31.21f);
    [SerializeField] private Vector3 huySeoVillaEuler = new Vector3(0f, 179.58f, 0f);

    [Header("=== Monologue Thời Lượng ===")]
    [SerializeField] private float meLiuRedMonologueDuration = 10f;
    [SerializeField] private float postBaNgaMonologueDuration = 6f;
    [SerializeField] private float postShipperMonologueDuration = 7f;
    [SerializeField] private float preFadeMonologueDuration = 7f;

    [Header("=== Hook Kết Thúc PHẦN 1 (Cutscene Phần 2 & 3) ===")]
    [Tooltip("Phát NGAY khi người chơi gọi đồng đội xong trong GardenTrigger. " +
             "GẮN kích hoạt Cutscene Phần 2 & 3 (Timeline / animation độc lập của nhóm) vào đây.")]
    public UnityEvent onPhase2GameplayCompleted;

    [Header("=== Debug ===")]
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // RUNTIME STATE
    // ===================================================================

    private int boBiaSold = 0;
    private bool transitioning = false;
    private bool meLiuRedMonologueShown = false;
    public bool IsMeLiuDialogueRunning { get; private set; }
    private CanvasGroup transitionFadeGroup;
    private PlayerMovement playerMovement;
    private GameObject alleyBarrier;
    private const string VillaGardenBarrierName = "VillaGarden_DynamicBarrier";

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.OnCustomerArrived -= HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerServed -= HandleCustomerServed;
        }
    }

    // ===================================================================
    // PUBLIC API — Entry Point
    // ===================================================================

    /// <summary>
    /// Được gọi sau khi Phase 1 hoàn thành (và transition fade xong).
    /// Khởi tạo buổi sáng Ngày 2 với 3 khách hàng tuần tự.
    /// </summary>
    public void StartPhase2Morning()
    {
        Log("=== Bắt đầu Phase 2 — Buổi Sáng Ngày 2 ===");

        currentTime = Phase2TimeOfDay.Morning_Day2;
        boBiaSold = 0;
        transitioning = false;
        meLiuRedMonologueShown = false;

        ResolveReferences();

        // Sáng Ngày 2 luôn bắt đầu cạnh quầy, không giữ vị trí ở bãi rác cuối Phase 1.
        WarpPlayerTo(morningDay2PlayerSpawn);

        // --- Cleanup Phase 1 leftovers ---
        CleanupPhase1Leftovers();

        // --- Thiết lập ánh sáng buổi sáng ---
        DayNightCycle dayNight = FindFirstObjectByType<DayNightCycle>();
        if (dayNight != null)
        {
            dayNight.autoProgress = false;
            dayNight.timeOfDay = 12f; // 12h trưa — sáng hẳn
        }

        // --- Tạm dừng chuyển pha tự động ---
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.autoTransitionPaused = true;
        }

        // --- Kích hoạt cơ chế bán hàng ---
        if (BoBiaMechanic.Instance != null)
        {
            BoBiaMechanic.Instance.enabled = true;
            BoBiaMechanic.Instance.ForceReset();
        }

        // --- Thiết lập CustomerManager với 3 NPC tuần tự ---
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
            CustomerManager.Instance.enabled = true;

            // Đăng ký event
            CustomerManager.Instance.OnCustomerArrived -= HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerArrived += HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerServed -= HandleCustomerServed;
            CustomerManager.Instance.OnCustomerServed += HandleCustomerServed;

            SetupMorningDay2NPCsList();
            ConfigureMeLiuVillaOrigin();
            CustomerManager.Instance.SpawnIntervalMin = 3f;
            CustomerManager.Instance.SpawnIntervalMax = 3f;
            CustomerManager.Instance.StartSpawning();
        }

        // --- Ẩn NPC buổi tối Phase 1 ---
        HideEveningPhase1NPCs();

        // --- Tắt đèn đường buổi sáng ---
        SetStreetLightsState(false);

        // --- Chặn hẻm buổi sáng: người chơi chưa được vào hẻm khi đang bán hàng ---
        SetAlleyBarrierActive(true);

        // --- Mở khóa di chuyển người chơi ---
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        ShowMonologue("Sáng nay tiếp tục bán hàng thôi. Phải vừa bán vừa thu thập thêm thông tin về căn Villa đáng ngờ kia...", 6f, MainGameplayVoiceKey.PlayerDay2MorningIntro);

        Log("Phase 2 Morning setup hoàn tất. Đang chờ khách hàng...");
    }

    // ===================================================================
    // DEV CHEAT TOOLS — chỉ dùng để TEST NHANH trong Editor (Play Mode)
    // ===================================================================

    /// <summary>
    /// [DEV CHEAT] Bắt đầu lại từ đầu BUỔI SÁNG NGÀY 2 (3 khách Bà Nga → Shipper → Mê Liu),
    /// không cần chơi lại Phase 1. Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_StartMorningDay2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Bắt đầu lại Buổi Sáng Ngày 2.");
        StopAllCoroutines();
        StartPhase2Morning();
    }

    /// <summary>
    /// [DEV CHEAT] Nhảy thẳng tới trước khi phục vụ Miu Lê ở sáng Ngày 2 (bỏ qua bà Nga và Shipper).
    /// Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_SkipToMeLiu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Nhảy thẳng tới Miu Lê.");
        StopAllCoroutines();
        
        currentTime = Phase2TimeOfDay.Morning_Day2;
        boBiaSold = 2; // Đã bán được 2 cái (Bà Nga & Shipper), bánh tiếp theo (thứ 3) sẽ là Miu Lê
        transitioning = false;
        meLiuRedMonologueShown = false;

        ResolveReferences();
        WarpPlayerTo(morningDay2PlayerSpawn);
        CleanupPhase1Leftovers();

        DayNightCycle dayNight = FindFirstObjectByType<DayNightCycle>();
        if (dayNight != null)
        {
            dayNight.autoProgress = false;
            dayNight.timeOfDay = 12f;
        }

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.autoTransitionPaused = true;
        }

        if (BoBiaMechanic.Instance != null)
        {
            BoBiaMechanic.Instance.enabled = true;
            BoBiaMechanic.Instance.ForceReset();
        }

        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
            CustomerManager.Instance.enabled = true;

            CustomerManager.Instance.OnCustomerArrived -= HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerArrived += HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerServed -= HandleCustomerServed;
            CustomerManager.Instance.OnCustomerServed += HandleCustomerServed;

            // Thiết lập hàng đợi bắt đầu thẳng từ Miu Lê (mục thứ 3)
            var morningPrefabs = new System.Collections.Generic.List<GameObject>();
            if (meLiuPrefab != null) morningPrefabs.Add(meLiuPrefab);

            if (morningPrefabs.Count > 0)
            {
                CustomerManager.Instance.CustomerPrefabs = morningPrefabs.ToArray();
            }
            ConfigureMeLiuVillaOrigin();
            CustomerManager.Instance.SpawnIntervalMin = 1f;
            CustomerManager.Instance.SpawnIntervalMax = 1f;
            CustomerManager.Instance.StartSpawning();
        }

        HideEveningPhase1NPCs();
        SetStreetLightsState(false);
        SetAlleyBarrierActive(true);

        ShowMonologue("Sáng nay tiếp tục bán hàng thôi. Phải vừa bán vừa thu thập thêm thông tin về căn Villa đáng ngờ kia...", 6f, MainGameplayVoiceKey.PlayerDay2MorningIntro);
    }

    /// <summary>
    /// [DEV CHEAT] Kích hoạt ngay Red Monologue "mùi khai amoniac" của Mê Liu để test riêng phân đoạn này.
    /// Tự reset cờ hiển thị nên bấm được nhiều lần. Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_TriggerMeLiuRedMonologue()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Test Red Monologue (mùi khai Mê Liu).");
        ResolveReferences();
        meLiuRedMonologueShown = false; // reset để test lại được nhiều lần
        TriggerMeLiuRedMonologue();
    }

    /// <summary>
    /// [DEV CHEAT] Nhảy thẳng tới BUỔI TỐI NGÀY 2: dừng khách buổi sáng, fade đen, gọi
    /// <see cref="SetupEveningDay2"/> (bật đèn / đổi trời tối) rồi fade lại. Bỏ qua 3 khách + cutscene
    /// chuyển cảnh. Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_SkipToEveningDay2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Nhảy đến Buổi Tối Ngày 2.");
        StopAllCoroutines();
        StartCoroutine(CheatSkipToEveningDay2Routine());
    }

    private IEnumerator CheatSkipToEveningDay2Routine()
    {
        ResolveReferences();
        transitioning = true;

        // 1. Khóa player trong lúc snap môi trường
        if (playerMovement != null) playerMovement.IsMovementLocked = true;

        // 2. Dừng & xóa sạch khách buổi sáng còn lại
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
        }

        // 3. Fade đen
        yield return StartCoroutine(FadeTransition(1f, 1f));
        yield return new WaitForSeconds(0.5f);

        // 5. Dựng môi trường buổi tối Ngày 2
        currentTime = Phase2TimeOfDay.Evening_Day2;
        SetupEveningDay2();

        // 6. Mở khóa player & fade lại
        if (playerMovement != null) playerMovement.IsMovementLocked = false;
        yield return StartCoroutine(FadeTransition(0f, 1f));

        Log("[CHEAT] Đã snap sang Buổi Tối Ngày 2.");
    }

    /// <summary>
    /// [DEV CHEAT] Dịch chuyển player ngay trước cửa Villa ở buổi tối để đỡ phải đi bộ.
    /// Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_WarpToVilla()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Dịch chuyển Player đến cửa Villa.");
        ResolveReferences();
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = new Vector3(-5.0f, 0.05f, 13.0f);
            player.rotation = Quaternion.identity;
            if (cc != null) cc.enabled = true;
        }

        // Bỏ qua mật phục: bắt đầu luôn điều tra Villa để test thu thập chứng cứ.
        StartVillaInvestigation();
    }

    /// <summary>
    /// [DEV CHEAT] Dựng vùng MẬT PHỤC và đặt người chơi ngay cạnh để đi bộ vào test
    /// toàn bộ luồng mật phục → mở quyền đột nhập Villa. Chỉ chạy khi đang ở chế độ Play.
    /// Tự động chuyển bối cảnh sang tối ngày 2 trước khi đặt player vào.
    /// </summary>
    public void Cheat_TestSurveillance()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Test Mật Phục.");
        StopAllCoroutines();
        StartCoroutine(CheatTestSurveillanceRoutine());
    }

    private IEnumerator CheatTestSurveillanceRoutine()
    {
        ResolveReferences();
        transitioning = true;

        if (playerMovement != null) playerMovement.IsMovementLocked = true;

        // 1. Dừng & xóa sạch khách buổi sáng
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
        }

        // 2. Fade đen nhanh
        yield return StartCoroutine(FadeTransition(1f, 0.5f));
        yield return new WaitForSeconds(0.2f);

        // 3. Đổi sang Evening_Day2 và Setup Evening Day 2
        currentTime = Phase2TimeOfDay.Evening_Day2;
        SetupEveningDay2();

        // 4. Đặt người chơi cách vùng ~2.5m để đi bộ vào kích hoạt trigger.
        if (player != null)
        {
            Vector3 spawn = hidingZonePos + new Vector3(0f, 0f, -2.5f);
            spawn.y = player.position.y;

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = spawn;
            player.rotation = Quaternion.LookRotation(hidingZonePos - spawn);
            if (cc != null) cc.enabled = true;
        }

        // 5. Mở khóa player & fade in lại
        if (playerMovement != null) playerMovement.IsMovementLocked = false;
        yield return StartCoroutine(FadeTransition(0f, 0.5f));

        Log("[CHEAT] Đã snap sang Buổi Tối Ngày 2 và dịch chuyển Player cạnh Hiding Zone.");
    }

    /// <summary>
    /// [DEV CHEAT] Thu thập ngay lập tức cả 3 chứng cứ tại Villa.
    /// Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_CollectAllVillaEvidence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Thu thập toàn bộ bằng chứng Villa.");
        VillaInvestigation investigation = FindFirstObjectByType<VillaInvestigation>();
        if (investigation != null)
        {
            // Tìm tất cả các item trong scene và thu thập chúng
            VillaEvidenceItem[] items = FindObjectsByType<VillaEvidenceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (items.Length > 0)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        item.SendMessage("Collect", SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
            
            // Đảm bảo kích hoạt hoàn thành trong manager
            investigation.Cheat_ForceCompleteCollection();
        }
    }

    /// <summary>
    /// [DEV CHEAT] Hoàn tất ngay PHẦN 1: ép thu thập đủ chứng cứ rồi gọi
    /// <see cref="CompletePhase2Gameplay"/> (điểm nối Cutscene Phần 2 & 3).
    /// Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_FinishPhase1Gameplay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase2][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Hoàn tất PHẦN 1 và gọi CompletePhase2Gameplay().");

        // 1. Ép thu thập đủ 3 chứng cứ.
        Cheat_CollectAllVillaEvidence();

        // 2. Nhảy thẳng tới điểm nối kết thúc PHẦN 1.
        CompletePhase2Gameplay();
    }

    // ===================================================================
    // HOOK KẾT THÚC PHẦN 1 — điểm nối sang Cutscene Phần 2 & 3
    // ===================================================================

    /// <summary>
    /// KẾT THÚC PHẦN 1 (gọi đồng đội báo cáo). Khóa hoàn toàn di chuyển + camera của Player,
    /// rồi phát hook <see cref="onPhase2GameplayCompleted"/> để nhóm gắn Cutscene Phần 2 & 3.
    /// </summary>
    public void CompletePhase2Gameplay()
    {
        ResolveReferences();

        // Khóa hoàn toàn di chuyển người chơi.
        if (player != null)
        {
            if (playerMovement == null)
                playerMovement = player.GetComponent<PlayerMovement>();
            if (playerMovement != null)
                playerMovement.IsMovementLocked = true;
        }

        // Khóa hoàn toàn camera.
        ThirdPersonCamera cam = FindFirstObjectByType<ThirdPersonCamera>();
        if (cam != null)
            cam.IsRotationLocked = true;

        Debug.Log("=== PLAYBACK CUTSCENE NOW ===");

        // HOOK: nhóm gắn kích hoạt Cutscene Phần 2 & 3 (Timeline/animation) vào sự kiện này.
        if (onPhase2GameplayCompleted != null)
            onPhase2GameplayCompleted.Invoke();

        // Tự động chuyển tiếp sang MiddleCutScene
        StartCoroutine(TransitionToMiddleCutsceneRoutine());
    }

    private IEnumerator TransitionToMiddleCutsceneRoutine()
    {
        Log("Bắt đầu chuyển tiếp sang MiddleCutScene...");
        float voiceRemaining = MainGameplayVoiceover.Instance.RemainingTime;
        if (voiceRemaining > 0f)
            yield return new WaitForSeconds(voiceRemaining);

        // 1. Fade out to black
        yield return StartCoroutine(FadeTransition(1f, 1.5f));

        // 2. Màn hình đen 1.5 giây
        yield return new WaitForSeconds(1.5f);

        // 3. Load scene MiddleCutScene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MiddleCutScene");
    }

    // ===================================================================
    // CUSTOMER EVENT HANDLERS
    // ===================================================================

    private void HandleCustomerArrived(CustomerManager.Customer customer)
    {
        if (currentTime != Phase2TimeOfDay.Morning_Day2) return;
        if (customer == null || customer.npcInstance == null) return;

        NPCDialogue npcDialogue = customer.npcInstance.GetComponent<NPCDialogue>();
        if (npcDialogue == null)
        {
            npcDialogue = customer.npcInstance.AddComponent<NPCDialogue>();
        }

        string nameLower = customer.npcInstance.name.ToLower();
        Log($"Customer arrived: '{customer.npcInstance.name}' (nameLower='{nameLower}')");

        if (nameLower.Contains("nganpc"))
        {
            // === KHÁCH 1: BÀ NGA ===
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Gớm, chú em hôm nay mở hàng sớm thế. Cho bà 2 cái nhiều dừa nhé, thằng cháu ở nhà nó thích ăn lắm.",
                welcomeVoice = MainGameplayVoiceKey.NgaDay2Welcome,
                evidenceText = "Chả vội sao được chú! Đêm qua lúc đi tập thể dục sớm, bà đi ngang qua cái thùng rác đầu ngõ thấy có mấy thằng xăm trổ đứng lén lút vứt cái gì nhìn như kim tiêm ấy. Cái hẻm này dạo này loạn quá, chú mày bán hàng ở đây cũng phải cẩn thận đấy nhé!",
                evidenceVoice = MainGameplayVoiceKey.NgaDay2Receive,
                evidencePoints = 10
            };
            customer.orderQuantity = 2; // Bà Nga đặt 2 cái
            Log("Đã gán hội thoại cho Bà Nga (Khách 1). Số lượng bánh: 2.");
        }
        else if (nameLower.Contains("shipper"))
        {
            // === KHÁCH 2: SHIPPER ===
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Anh giai ơi! Làm gấp cho em 3 cái đầy đủ topping với! Đang chạy deadline giao cuốc hàng nổ liên tục, đói lả cả người rồi!",
                welcomeVoice = MainGameplayVoiceKey.ShipperDay2Welcome,
                evidenceText = "Dạ bên cái căn Villa to đùng cuối góc chữ U kia kìa anh. Từ hôm qua tới giờ tụi nó đặt toàn đồ ăn xa xỉ với mấy thùng nước ngọt, loa đài giao tới liên tục. Nghe bảo tối nay tụi nó lại bao trọn gói mở tiệc tiếp đấy. Thôi em đi giao tiếp đây, cảm ơn anh giai!",
                evidenceVoice = MainGameplayVoiceKey.ShipperDay2Receive,
                evidencePoints = 10
            };
            customer.orderQuantity = 3; // Shipper đặt 3 cái
            Log("Đã gán hội thoại cho Shipper (Khách 2). Số lượng bánh: 3.");
        }
        else if (nameLower.Contains("miu") || nameLower.Contains("meliu") || nameLower.Contains("me liu"))
        {
            // === KHÁCH 3: MÊ LIU ===
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Này chủ quán, làm cho tôi 4 cái bánh. Ít ngọt thôi nhé, cho nhiều mè vào cho thơm.",
                welcomeVoice = MainGameplayVoiceKey.MeLiuDay2Welcome,
                evidenceText = "À, đêm qua thức chiến game với mấy đứa bạn ấy mà. Mua mấy cái bánh này về cho tụi nó lót dạ, lấy sức tối nay trong Villa lại tiếp tục đại tiệc tùng tiếp, tha hồ mà quẩy banh nóc!",
                evidenceVoice = MainGameplayVoiceKey.MeLiuDay2Receive,
                evidencePoints = 15
            };
            customer.orderQuantity = 4; // Mê Liu đặt 4 cái
            Log("Đã gán hội thoại cho Mê Liu (Khách 3). Số lượng bánh: 4.");
        }
        else
        {
            // Fallback cho NPC không nhận diện được
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Cho tôi một cuốn bò bía nha!",
                evidenceText = "Bò bía ngon lắm, cảm ơn!",
                evidencePoints = 0
            };
            customer.orderQuantity = 1; // Fallback: 1 cái
            Log($"NPC không nhận diện — gán hội thoại mặc định cho '{customer.npcInstance.name}'.");
        }
    }

    private void HandleCustomerServed(CustomerManager.Customer customer)
    {
        if (currentTime != Phase2TimeOfDay.Morning_Day2) return;

        boBiaSold++;
        Log($"Đã phục vụ '{customer.displayName}'. Tổng boBiaSold: {boBiaSold}/3");

        string nameLower = customer.npcInstance != null ? customer.npcInstance.name.ToLower() : customer.displayName.ToLower();

        if (nameLower.Contains("nganpc") || nameLower.Contains("bà nga"))
        {
            // === SAU BÀ NGA ===
            StartCoroutine(PostBaNgaMonologue(customer.npcInstance));
        }
        else if (nameLower.Contains("shipper"))
        {
            // === SAU SHIPPER ===
            StartCoroutine(PostShipperMonologue(customer.npcInstance));
        }
        else if (nameLower.Contains("miu") || nameLower.Contains("meliu") || nameLower.Contains("mê liu"))
        {
            // === SAU MÊ LIU ===
            if (!transitioning)
            {
                transitioning = true;
                if (CustomerManager.Instance != null)
                {
                    CustomerManager.Instance.StopSpawning(false);
                }

                // Tăng suspicion
                if (CaseManager.Instance != null)
                {
                    CaseManager.Instance.ApplySuspicionChange(meLiuSuspicionIncrease);
                    Log($"Đã tăng Suspicion +{meLiuSuspicionIncrease} sau khi phục vụ Mê Liu.");
                }

                StartCoroutine(MorningDay2ToEveningSequence(customer.npcInstance));
            }
        }
    }

    // ===================================================================
    // POST-CUSTOMER MONOLOGUES
    // ===================================================================

    private IEnumerator PostBaNgaMonologue(GameObject customerNpc)
    {
        // 1. Chờ bà Nga nói xong hết hội thoại (đợi cho trạng thái chuyển khỏi TalkingEvidence)
        while (CustomerManager.Instance != null && 
               CustomerManager.Instance.CurrentCustomerState == CustomerManager.CustomerState.TalkingEvidence)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Chờ thêm 1.0 giây để bà Nga bắt đầu quay đi
        yield return new WaitForSeconds(1.0f);

        ShowMonologue(
            "Kim tiêm sao? Đúng như những gì mình thu thập được đêm qua. " +
            "Căn Villa kia chắc chắn có vấn đề.",
            postBaNgaMonologueDuration,
            MainGameplayVoiceKey.PlayerAfterNgaDay2);

        Log("Đã hiển thị monologue sau Bà Nga.");
    }

    private IEnumerator PostShipperMonologue(GameObject customerNpc)
    {
        // 1. Chờ Shipper nói xong hết hội thoại (đợi cho trạng thái chuyển khỏi TalkingEvidence)
        while (CustomerManager.Instance != null && 
               CustomerManager.Instance.CurrentCustomerState == CustomerManager.CustomerState.TalkingEvidence)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Chờ thêm 1.0 giây để Shipper bắt đầu quay đi
        yield return new WaitForSeconds(1.0f);

        ShowMonologue(
            "Lại đặt thêm loa đài và nhu yếu phẩm... " +
            "Tần suất dày đặc thế này chứng tỏ tụi nó sắp tổ chức một vụ bay lắc quy mô lớn hơn rồi.",
            postShipperMonologueDuration,
            MainGameplayVoiceKey.PlayerAfterShipperDay2);

        Log("Đã hiển thị monologue sau Shipper.");
    }

    // ===================================================================
    // MÊ LIU — RED MONOLOGUE (MÙI KHAI)
    // ===================================================================

    public bool IsMeLiuRedMonologueRunningOnly { get; private set; }
    public bool IsMeLiuQuestionRunningOnly { get; private set; }

    /// <summary>
    /// Gọi khi NPC Mê Liu tới quầy. Hiển thị monologue đỏ cảnh báo về mùi khai (chỉ độc thoại).
    /// </summary>
    public void TriggerMeLiuRedMonologueOnly()
    {
        if (meLiuRedMonologueShown) return;
        meLiuRedMonologueShown = true;

        StartCoroutine(MeLiuRedMonologueOnlySequence());
    }

    private IEnumerator MeLiuRedMonologueOnlySequence()
    {
        IsMeLiuRedMonologueRunningOnly = true;
        Log("Bắt đầu Red Monologue (Chỉ độc thoại) — mùi khai Mê Liu.");

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        string redText =
            "<color=#FF4444><b>(Khoảng cách đủ gần... Khoan đã! Mùi gì lạ thế này? " +
            "Người cô ta sực lên một mùi khai cực kỳ nồng, rất giống mùi amoniac hóa chất! " +
            "Đây chính là mùi phát ra từ các lò nấu hoặc các đối tượng vừa sử dụng ma túy đá thâu đêm! " +
            "Manh mối cốt lõi đây rồi!)</b></color>";

        float redVoiceDuration = MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerDetectMeLiu);
        float redDuration = Mathf.Max(meLiuRedMonologueDuration, redVoiceDuration);

        DialogueScreenUI ui = FindFirstObjectByType<DialogueScreenUI>();
        if (ui != null)
        {
            ui.ForceShow(redText, redDuration);
        }
        else
        {
            Debug.Log($"[Story] Red Monologue: {redText}");
        }

        yield return new WaitForSeconds(redDuration);

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        IsMeLiuRedMonologueRunningOnly = false;
        Log("Độc thoại đỏ hoàn tất.");
    }

    /// <summary>
    /// Phát câu hỏi của người chơi sau khi làm bánh xong cho Mê Liu.
    /// </summary>
    public void TriggerMeLiuQuestion()
    {
        StartCoroutine(MeLiuQuestionSequence());
    }

    private IEnumerator MeLiuQuestionSequence()
    {
        IsMeLiuQuestionRunningOnly = true;
        Log("Phát câu hỏi hỏi Mê Liu.");

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        yield return ShowLine("anhbobia",
            "Bánh của chị xong rồi đây ạ. Trông chị có vẻ mệt mỏi thế, đêm qua mất ngủ ạ?",
            4f,
            MainGameplayVoiceKey.PlayerAskMeLiu);

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        IsMeLiuQuestionRunningOnly = false;
        Log("Phát câu hỏi hoàn tất.");
    }

    public void TriggerMeLiuRedMonologue()
    {
        if (meLiuRedMonologueShown) return;
        meLiuRedMonologueShown = true;

        StartCoroutine(MeLiuRedMonologueSequence());
    }

    private IEnumerator MeLiuRedMonologueSequence()
    {
        IsMeLiuDialogueRunning = true;
        Log("Bắt đầu Red Monologue — mùi khai Mê Liu.");

        // Khóa di chuyển người chơi trong lúc đọc monologue
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        // Hiển thị monologue đỏ (dùng DialogueScreenUI với màu đỏ cảnh báo)
        string redText =
            "<color=#FF4444><b>(Khoảng cách đủ gần... Khoan đã! Mùi gì lạ thế này? " +
            "Người cô ta sực lên một mùi khai cực kỳ nồng, rất giống mùi amoniac hóa chất! " +
            "Đây chính là mùi phát ra từ các lò nấu hoặc các đối tượng vừa sử dụng ma túy đá thâu đêm! " +
            "Manh mối cốt lõi đây rồi!)</b></color>";

        float redVoiceDuration = MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerDetectMeLiu);
        float redDuration = Mathf.Max(meLiuRedMonologueDuration, redVoiceDuration);

        DialogueScreenUI ui = FindFirstObjectByType<DialogueScreenUI>();
        if (ui != null)
        {
            ui.ForceShow(redText, redDuration);
        }
        else
        {
            Debug.Log($"[Story] Red Monologue: {redText}");
        }

        yield return new WaitForSeconds(redDuration);

        // Hiển thị câu trả lời của người chơi
        yield return ShowLine("anhbobia",
            "Bánh của chị xong rồi đây ạ. Trông chị có vẻ mệt mỏi thế, đêm qua mất ngủ ạ?",
            4f,
            MainGameplayVoiceKey.PlayerAskMeLiu);

        // Mở khóa di chuyển
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        IsMeLiuDialogueRunning = false;
        Log("Red Monologue hoàn tất.");
    }

    // ===================================================================
    // TRANSITION: MORNING DAY 2 → EVENING DAY 2
    // ===================================================================

    private IEnumerator MorningDay2ToEveningSequence(GameObject customerNpc)
    {
        Log("Bắt đầu chuyển cảnh: Sáng Ngày 2 → Tối Ngày 2.");

        // 1. Khóa di chuyển
        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        // 2. Chờ Mê Liu nói xong hết hội thoại (đợi cho trạng thái chuyển khỏi TalkingEvidence)
        while (CustomerManager.Instance != null && 
               CustomerManager.Instance.CurrentCustomerState == CustomerManager.CustomerState.TalkingEvidence)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Chờ thêm 1 giây để thấy Miu Le bắt đầu quay lưng đi khỏi
        yield return new WaitForSeconds(1.0f);

        // 3. Hiển thị monologue trước khi chuyển cảnh
        string preMonologue =
            "Manh mối đã quá rõ ràng rồi. Đêm nay mình phải tìm cách tiếp cận " +
            "căn Villa đó bằng mọi giá...";
        float preVoiceDuration = MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerAfterMeLiu);
        float preDuration = Mathf.Max(preFadeMonologueDuration, preVoiceDuration);
        ShowMonologue(preMonologue, preDuration);
        yield return new WaitForSeconds(preDuration);

        // 4. Fade out
        yield return StartCoroutine(FadeTransition(1f, 1.5f));

        // 5. Màn hình đen 3 giây
        yield return new WaitForSeconds(3f);

        // 6. Chuyển environment sang Evening_Day2
        currentTime = Phase2TimeOfDay.Evening_Day2;
        SetupEveningDay2();

        // 7. Mở khóa di chuyển
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        // 8. Fade in
        yield return StartCoroutine(FadeTransition(0f, 1.5f));

        Log("Chuyển cảnh sang Tối Ngày 2 hoàn tất.");
    }

    // ===================================================================
    // EVENING DAY 2 SETUP (PLACEHOLDER)
    // ===================================================================

    private void SetupEveningDay2()
    {
        // Đảm bảo dọn dẹp các tàn dư của Phase 1 (kể cả khi dùng cheat nhảy thẳng tới đây)
        CleanupPhase1Leftovers();

        Log("Thiết lập môi trường Tối Ngày 2.");

        // Tắt cơ chế bán hàng
        if (BoBiaMechanic.Instance != null)
        {
            BoBiaMechanic.Instance.ForceReset();
            BoBiaMechanic.Instance.enabled = false;
        }
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
            CustomerManager.Instance.RouteMeLiuFromVilla = false;
            CustomerManager.Instance.enabled = false;
        }

        // Chuyển ánh sáng sang buổi tối
        DayNightCycle dayNight = FindFirstObjectByType<DayNightCycle>();
        if (dayNight != null)
        {
            dayNight.autoProgress = false;
            dayNight.timeOfDay = 20f; // 8h tối
        }

        // Bật đèn đường
        SetStreetLightsState(true);

        // Mở hẻm buổi tối: người chơi được tự do đi vào hẻm để tiếp cận Villa / vùng mật phục.
        SetAlleyBarrierActive(false);

        // Tối Ngày 2 bắt đầu đúng đầu hẻm và quay sẵn về vùng nấp.
        ConfigureEveningDay2Spawn();
        WarpPlayerTo(eveningDay2PlayerSpawn);

        // Hiển thị monologue buổi tối — dẫn người chơi tới vùng mật phục
        ShowMonologue(
            "Trời đã tối, tiệc trong Villa chắc bắt đầu rồi. " +
            "Mình phải tới chỗ khuất đối diện cổng — vòng sáng xanh kia — để nấp quan sát trước đã.",
            7f,
            MainGameplayVoiceKey.PlayerNight2Intro);

        // Di chuyển MiuLe_Villa và HuySeo_Villa về vị trí sân sau ngay từ đầu buổi tối để tránh lộ diện ở hẻm
        GameObject miuLeVilla = GameObject.Find("MiuLe_Villa");
        if (miuLeVilla != null)
        {
            miuLeVilla.transform.SetPositionAndRotation(miuLeVillaPos, Quaternion.Euler(miuLeVillaEuler));
            Log("Đã dời MiuLe_Villa về sân sau.");
        }
        GameObject huySeoVilla = GameObject.Find("HuySeo_Villa");
        if (huySeoVilla != null)
        {
            huySeoVilla.transform.SetPositionAndRotation(huySeoVillaCarPos, Quaternion.Euler(huySeoVillaEuler));
            Log("Đã dời HuySeo_Villa về sân sau.");
        }

        // Đảm bảo có VillaInvestigation nhưng CHƯA bắt đầu — chờ mật phục xong mới đột nhập.
        EnsureVillaInvestigation();
        CreateVillaGardenDynamicBarrier();

        // Tạo vùng MẬT PHỤC. Đột nhập Villa (spawn chứng cứ) chỉ mở sau khi mật phục hoàn tất.
        CreateHidingZone();

        Log("Evening Day 2 setup hoàn tất. Đã tạo vùng mật phục, chờ người chơi tiếp cận.");
    }

    // ===================================================================
    // MẬT PHỤC (HIDING ZONE) — gate đột nhập Villa
    // ===================================================================

    /// <summary>Đảm bảo có 1 VillaInvestigation trong scene (chưa bắt đầu điều tra).</summary>
    private VillaInvestigation EnsureVillaInvestigation()
    {
        VillaInvestigation villaInvest = FindFirstObjectByType<VillaInvestigation>();
        if (villaInvest == null)
        {
            GameObject viGo = GameObject.Find("_VillaInvestigation");
            if (viGo == null) viGo = new GameObject("_VillaInvestigation");
            villaInvest = viGo.AddComponent<VillaInvestigation>();
        }
        return villaInvest;
    }

    private void CreateVillaGardenDynamicBarrier()
    {
        GameObject existing = GameObject.Find(VillaGardenBarrierName);
        if (existing != null) Destroy(existing);

        GameObject barrier = new GameObject(VillaGardenBarrierName);
        BoxCollider collider = barrier.AddComponent<BoxCollider>();
        collider.isTrigger = false;

        GameObject villa = GameObject.Find("Vietnamese_Townhouse");
        GameObject lawn = GameObject.Find("Ground_Map01");
        Bounds villaBounds;
        Bounds lawnBounds;

        if (TryGetWorldBounds(villa, out villaBounds) && TryGetWorldBounds(lawn, out lawnBounds))
        {
            Vector3 normal = lawnBounds.center - villaBounds.center;
            normal.y = 0f;
            if (normal.sqrMagnitude > 0.001f)
            {
                normal.Normalize();
                // Dịch chuyển barrier lùi sâu về phía sân vườn thêm 1.2m để không chặn lối đi ngay ngoài cửa sau
                Vector3 offset = normal * 1.2f;
                barrier.transform.position = villaBounds.center + Vector3.Scale(normal, villaBounds.extents) + offset;
                barrier.transform.position = new Vector3(barrier.transform.position.x, 2.5f, barrier.transform.position.z);

                bool spansZ = Mathf.Abs(normal.x) >= Mathf.Abs(normal.z);
                collider.size = spansZ
                    ? new Vector3(0.35f, 5f, Mathf.Max(villaBounds.size.z, lawnBounds.size.z))
                    : new Vector3(Mathf.Max(villaBounds.size.x, villaBounds.size.x) + 15f, 5f, 0.35f);
                return;
            }
        }

        // Vị trí mặc định lùi về Z = 28.6f (sau vòng sáng xanh) và kéo dài X = 25f để chắn hết lối
        barrier.transform.position = new Vector3(-5.5f, 2.5f, 28.6f);
        collider.size = new Vector3(25f, 5f, 0.35f);
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    /// <summary>
    /// Tạo (hoặc dùng lại) vùng mật phục. Nếu đã đặt sẵn HidingZone trong scene
    /// (để gắn cutscene), sẽ dùng cái đó; nếu chưa thì tạo procedural cùng vòng sáng xanh.
    /// </summary>
    private void CreateHidingZone()
    {
        HidingZone hz = FindFirstObjectByType<HidingZone>();

        if (hz == null)
        {
            GameObject hzGo = new GameObject("_HidingZone");

            SphereCollider sc = hzGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = hidingZoneRadius;

            // Vòng tròn ánh sáng xanh đánh dấu vùng mật phục (placeholder — có thể thay marker riêng).
            GameObject glow = new GameObject("HidingZoneGlow");
            glow.transform.SetParent(hzGo.transform, false);
            glow.transform.localPosition = Vector3.up * 0.2f;
            Light l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.3f, 1f, 0.6f);
            l.range = hidingZoneRadius * 3f;
            l.intensity = 2.5f;
            l.shadows = LightShadows.None;

            hz = hzGo.AddComponent<HidingZone>();
            // Luôn chờ cutscene Mê Liu điều khiển thời điểm hoàn tất (thay cho timer cố định).
            hz.Configure(playerTag, surveillanceDuration, true);

            Log($"Đã tạo vùng Mật Phục procedural tại {hidingZonePos}.");
        }
        else
        {
            hz.ResetZone();
            Log("Dùng lại HidingZone có sẵn trong scene.");
        }

        // Giữ trigger runtime trong đúng nhóm Hierarchy và luôn snap về đầu hẻm.
        GameObject waypointRoot = GameObject.Find("=== WAYPOINTS & TRIGGERS ===");
        if (waypointRoot != null)
            hz.transform.SetParent(waypointRoot.transform, true);
        hz.transform.position = hidingZonePos;

        // Người chơi phải bước sát vào chấm xanh mới kích hoạt, không chạm từ xa.
        SphereCollider hzCollider = hz.GetComponent<SphereCollider>();
        if (hzCollider != null)
        {
            hzCollider.isTrigger = true;
            hzCollider.radius = hidingZoneRadius;
        }

        Light markerLight = hz.GetComponentInChildren<Light>();
        if (markerLight != null)
            markerLight.range = hidingZoneRadius * 3f;

        // Nối callback:
        //   - Bước vào vùng → diễn cutscene Mê Liu (cửa hé mở, dáo dác, vào Villa, khóa xích cổng).
        //   - Mật phục xong → trả quyền điều khiển + bắt đầu đột nhập Villa.
        hz.OnSurveillanceStartedAction = StartMeLiuSurveillanceCutscene;
        hz.OnSurveillanceCompletedAction = OnSurveillanceComplete;
    }

    // ===================================================================
    // CUTSCENE MẬT PHỤC — Mê Liu xuất hiện ở cổng Villa
    // ===================================================================

    /// <summary>
    /// Hook chạy khi người chơi vừa bước vào vùng mật phục (đã bị khóa di chuyển + ngồi thụp +
    /// khóa camera hướng về cổng). Diễn hoạt cảnh Mê Liu rồi gọi CompleteSurveillance().
    /// </summary>
    private void StartMeLiuSurveillanceCutscene()
    {
        StartCoroutine(MeLiuSurveillanceRoutine());
    }

    private IEnumerator MeLiuSurveillanceRoutine()
    {
        Log("Cutscene mật phục: cửa Villa hé mở, Mê Liu xuất hiện.");

        GateInteract villaGate = GameObject.Find("Gate_Interactive")?.GetComponent<GateInteract>();
        if (villaGate != null) villaGate.SetVisualAngle(20f);

        Vector3 doorPos = meLiuVillaDoorPos;
        Vector3 outwardDirection = hidingZonePos - doorPos;
        outwardDirection.y = 0f;
        if (outwardDirection.sqrMagnitude < 0.001f) outwardDirection = Vector3.left;
        outwardDirection.Normalize();
        Vector3 stepOutPos = doorPos + outwardDirection * 1.2f;
        Vector3 walkInPos = doorPos - outwardDirection * 3.5f;

        yield return new WaitForSeconds(1f);
        ShowMonologue("Khoan đã... cửa Villa hé mở kìa!", 2.5f,
            MainGameplayVoiceKey.PlayerNight2Surveillance);
        yield return new WaitForSeconds(1.2f);

        GameObject meLiu = null;
        if (meLiuPrefab != null)
        {
            meLiu = Instantiate(meLiuPrefab, doorPos, Quaternion.Euler(0f, 180f, 0f));
            meLiu.name = "MeLiu_Surveillance";
            StripNpcGameplay(meLiu);

            // Prefab có chân thấp hơn root khoảng 0.1m; snap root lên đúng cao độ nền
            // ngay khi spawn để không xuất hiện dưới mặt đất ở khung hình đầu tiên.
            float feetOffset = GetCharacterFeetOffset(meLiu.transform);
            meLiu.transform.position = GetGroundedCharacterPosition(
                meLiu.transform, doorPos, feetOffset);
        }

        // 1. Bước ra khỏi cửa
        if (meLiu != null)
            yield return MoveTransform(meLiu.transform, stepOutPos, 1.5f, true);

        // 2. Dáo dác nhìn quanh
        if (meLiu != null)
            yield return LookAround(meLiu.transform, 2.5f);

        ShowMonologue("Là ả Mê Liu ban sáng... ả ta đang dáo dác canh chừng xung quanh.", 3f);
        yield return new WaitForSeconds(0.8f);

        // 3. Quay trở vào trong Villa
        if (meLiu != null)
        {
            yield return MoveTransform(meLiu.transform, walkInPos, 2.2f, true);
            Destroy(meLiu);
        }

        // Mê Liu cần có thời gian đi sâu vào Villa trước khi người chơi được lẻn vào.
        yield return new WaitForSeconds(2.5f);
        if (villaGate != null) villaGate.SetVisualAngle(0f);
        Log("Mê Liu đã vào trong, cổng sắt khóa xích lại.");

        // Hoàn tất mật phục → HidingZone trả quyền điều khiển + gọi OnSurveillanceComplete.
        if (HidingZone.Instance != null)
            HidingZone.Instance.CompleteSurveillance();
        else
            OnSurveillanceComplete();
    }

    /// <summary>Di chuyển tuyến tính một Transform tới đích trong <paramref name="duration"/> giây.</summary>
    private IEnumerator MoveTransform(Transform t, Vector3 target, float duration, bool faceMovement)
    {
        if (t == null) yield break;

        float feetOffset = GetCharacterFeetOffset(t);
        Vector3 start = GetGroundedCharacterPosition(t, t.position, feetOffset);
        target = GetGroundedCharacterPosition(t, target, feetOffset);
        t.position = start;

        if (faceMovement)
        {
            Vector3 dir = target - start;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(dir);
        }

        float elapsed = 0f;
        while (elapsed < duration && t != null)
        {
            elapsed += Time.deltaTime;
            Vector3 next = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            t.position = GetGroundedCharacterPosition(t, next, feetOffset);
            yield return null;
        }
        if (t != null)
            t.position = GetGroundedCharacterPosition(t, target, feetOffset);
    }

    private static float GetCharacterFeetOffset(Transform character)
    {
        if (character == null) return 0.1f;

        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0.1f;

        float minY = renderers[0].bounds.min.y;
        for (int i = 1; i < renderers.Length; i++)
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);

        return Mathf.Max(0f, character.position.y - minY);
    }

    private static Vector3 GetGroundedCharacterPosition(
        Transform character, Vector3 desiredPosition, float feetOffset)
    {
        Vector3 origin = desiredPosition + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin, Vector3.down, 10f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float bestDelta = float.PositiveInfinity;
        float groundY = desiredPosition.y;
        bool foundGround = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < 0.45f) continue;
            if (character != null
                && (hit.transform == character || hit.transform.IsChildOf(character))) continue;

            // Chọn mặt nền gần cao độ đường đi nhất, tránh bắt nhầm sàn tầng trên.
            float delta = Mathf.Abs(hit.point.y - desiredPosition.y);
            if (delta > 1.5f || delta >= bestDelta) continue;

            bestDelta = delta;
            groundY = hit.point.y;
            foundGround = true;
        }

        if (foundGround)
            desiredPosition.y = groundY + feetOffset + 0.01f;

        return desiredPosition;
    }

    /// <summary>Hiệu ứng "dáo dác nhìn quanh" — lia đầu trái/phải quanh hướng hiện tại.</summary>
    private IEnumerator LookAround(Transform t, float duration)
    {
        if (t == null) yield break;

        float baseYaw = t.eulerAngles.y;
        float elapsed = 0f;
        while (elapsed < duration && t != null)
        {
            elapsed += Time.deltaTime;
            float yaw = baseYaw + Mathf.Sin(elapsed * 3f) * 55f;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return null;
        }
    }

    /// <summary>Gỡ các script gameplay khách hàng để Mê Liu chỉ đóng vai diễn trong cutscene.</summary>
    private void StripNpcGameplay(GameObject npc)
    {
        if (npc == null) return;
        NPCDialogue dlg = npc.GetComponent<NPCDialogue>();
        if (dlg != null) Destroy(dlg);

        Animator anim = npc.GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.Play("Miu_Le_Idle", 0, 0f);
        }
    }

    /// <summary>Được gọi khi người chơi mật phục xong → mở quyền đột nhập Villa.</summary>
    private void OnSurveillanceComplete()
    {
        Log("Mật phục hoàn tất — bắt đầu điều tra Villa.");
        StartVillaInvestigation();
    }

    private void StartVillaInvestigation()
    {
        VillaInvestigation villaInvest = EnsureVillaInvestigation();
        villaInvest.StartInvestigation();
        OrganizeVillaSuspects();
    }

    private void OrganizeVillaSuspects()
    {
        GameObject characterRoot = GameObject.Find("=== CHARACTERS & VEHICLES ===");
        Transform parent = characterRoot != null ? characterRoot.transform : null;

        GameObject miuLe = GameObject.Find("MiuLe_Villa");
        if (miuLe != null)
        {
            if (parent != null) miuLe.transform.SetParent(parent, true);
            miuLe.transform.SetPositionAndRotation(miuLeVillaPos, Quaternion.Euler(miuLeVillaEuler));
        }

        GameObject huySeo = GameObject.Find("HuySeo_Villa");
        if (huySeo != null)
        {
            if (parent != null) huySeo.transform.SetParent(parent, true);
            huySeo.transform.SetPositionAndRotation(huySeoVillaCarPos, Quaternion.Euler(huySeoVillaEuler));
        }

        Log($"Đã sắp xếp nghi phạm vào nhóm Characters: MiuLe={miuLeVillaPos}, HuySeo={huySeoVillaCarPos}.");
    }

    // ===================================================================
    // SETUP HELPERS
    // ===================================================================

    private void SetupMorningDay2NPCsList()
    {
        if (CustomerManager.Instance == null) return;

        var morningPrefabs = new System.Collections.Generic.List<GameObject>();
        if (baNgaPrefab != null) morningPrefabs.Add(baNgaPrefab);
        if (shipperPrefab != null) morningPrefabs.Add(shipperPrefab);
        if (meLiuPrefab != null) morningPrefabs.Add(meLiuPrefab);

        if (morningPrefabs.Count > 0)
        {
            CustomerManager.Instance.CustomerPrefabs = morningPrefabs.ToArray();
            Log($"SetupMorningDay2NPCsList: Set {morningPrefabs.Count} NPC prefabs: " +
                string.Join(", ", morningPrefabs.ConvertAll(p => p.name)));
        }
        else
        {
            Debug.LogWarning("[StoryPhase2] Danh sách NPC buổi sáng Ngày 2 trống! " +
                             "Hãy gán các prefab trong Inspector.");
        }
    }

    /// <summary>
    /// Cấu hình để Mê Liu (khách thứ 3) đi ra từ cửa Villa và quay về Villa sau khi mua,
    /// thay vì spawn ở đầu hẻm như các khách khác — hợp lý hơn với mạch truyện.
    /// </summary>
    private void ConfigureMeLiuVillaOrigin()
    {
        if (CustomerManager.Instance == null) return;

        Transform villaDoor = CustomerManager.Instance.VillaSpawnPoint;
        if (villaDoor == null)
        {
            GameObject doorGo = GameObject.Find("_MeLiuVillaDoor");
            if (doorGo == null) doorGo = new GameObject("_MeLiuVillaDoor");
            doorGo.transform.position = meLiuVillaDoorPos;
            villaDoor = doorGo.transform;
            CustomerManager.Instance.VillaSpawnPoint = villaDoor;
        }

        CustomerManager.Instance.RouteMeLiuFromVilla = true;
        Log($"Mê Liu sẽ đi ra từ Villa tại {villaDoor.position} và quay về sau khi mua.");
    }

    private void CleanupPhase1Leftovers()
    {
        Log("Dọn dẹp tàn dư Phase 1...");

        // Dọn vật thể clone của Phase 1
        var allGOs = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string n = go.name;
            if (n.Contains("batlua(Clone)") ||
                n.Contains("syringe(Clone)") ||
                n.Contains("bichmaithuy(Clone)") ||
                n.Contains("bichrac") && n.Contains("(Clone)") ||
                n.Contains("Nganpc") && n.Contains("(Clone)") ||
                n.Contains("shipper") && n.Contains("(Clone)") ||
                n.Contains("Miu Le") && n.Contains("(Clone)") ||
                n.Contains("MiuLe") && n.Contains("(Clone)"))
            {
                Destroy(go);
            }
        }

        // Ẩn NPC buổi tối Phase 1
        HideEveningPhase1NPCs();

        // Ẩn batlua gốc
        GameObject batLua = GameObject.Find("batlua");
        if (batLua != null) batLua.SetActive(false);

        // Tắt StoryPhase1Manager (nếu còn tồn tại)
        StoryPhase1Manager phase1 = FindFirstObjectByType<StoryPhase1Manager>();
        if (phase1 != null)
        {
            phase1.enabled = false;
            Log("Đã tắt StoryPhase1Manager.");
        }

        Log("Dọn dẹp Phase 1 hoàn tất.");
    }

    private void HideEveningPhase1NPCs()
    {
        // Ẩn anhbanhmi, anhxamminh, Huy_seo — NPC buổi tối Phase 1
        GameObject anhBanhMi = GameObject.Find("anhbanhmi");
        if (anhBanhMi != null) anhBanhMi.SetActive(false);

        GameObject anhXamMinh = GameObject.Find("anhxamminh");
        if (anhXamMinh != null) anhXamMinh.SetActive(false);

        // Tìm và ẩn tất cả instance của Huy_seo
        var allGOs = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            if (go != null && go.name.Contains("Huy_seo"))
            {
                go.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Bật/tắt bức tường tàng hình "MorningAlleyBarrier" (chỉ BoxCollider) chặn lối vào hẻm.
    /// Phase 2 tự kiểm soát: chặn buổi sáng (đang bán hàng), mở buổi tối (đi mật phục / vào Villa).
    /// Bức tường này do StoryPhase1Manager tạo; nếu chưa có thì tự dựng lại cùng kích thước/vị trí.
    /// </summary>
    private void SetAlleyBarrierActive(bool active)
    {
        if (alleyBarrier == null)
        {
            var allGOs = GameObject.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (go != null && go.name == "MorningAlleyBarrier")
                {
                    alleyBarrier = go;
                    break;
                }
            }
        }

        if (alleyBarrier != null)
        {
            alleyBarrier.SetActive(active);
            Log(active
                ? "Đã CHẶN hẻm (bật MorningAlleyBarrier) cho buổi sáng Ngày 2."
                : "Đã MỞ hẻm (tắt MorningAlleyBarrier) cho buổi tối Ngày 2.");
        }
        else if (active)
        {
            // Phòng trường hợp Phase 1 chưa từng tạo barrier — dựng lại tường tàng hình (chỉ BoxCollider).
            alleyBarrier = new GameObject("MorningAlleyBarrier");
            alleyBarrier.transform.position = new Vector3(-5.5f, 3.0f, -2.75f);
            alleyBarrier.transform.localScale = new Vector3(0.5f, 8.0f, 10.0f);
            BoxCollider box = alleyBarrier.AddComponent<BoxCollider>();
            box.isTrigger = false;
            Log("Đã tạo mới MorningAlleyBarrier để chặn hẻm buổi sáng Ngày 2.");
        }
    }

    private void SetStreetLightsState(bool isEvening)
    {
        var allGOs = GameObject.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string goName = go.name;

            // Xe bánh mì
            if (goName == "xebanhmi_fbx")
            {
                go.SetActive(isEvening);
            }

            // Đèn đường
            if (goName == "StreetLight" || goName.StartsWith("Street_Light_OldTown"))
            {
                Light lightComp = go.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.enabled = isEvening;
                }
            }

            // Bóng đèn phát quang
            if (goName == "Object_8")
            {
                Transform parent = go.transform.parent;
                if (parent != null && parent.name == "Lamp_2")
                {
                    Renderer bulbRend = go.GetComponent<Renderer>();
                    if (bulbRend != null)
                    {
                        foreach (var mat in bulbRend.materials)
                        {
                            if (mat == null) continue;
                            if (isEvening)
                            {
                                mat.EnableKeyword("_EMISSION");
                                if (mat.HasProperty("_EmissionColor"))
                                    mat.SetColor("_EmissionColor",
                                        new Color(1f, 0.85f, 0.6f) * 4.0f);
                            }
                            else
                            {
                                mat.DisableKeyword("_EMISSION");
                                if (mat.HasProperty("_EmissionColor"))
                                    mat.SetColor("_EmissionColor", Color.black);
                            }
                        }
                    }
                }
            }
        }
    }

    // ===================================================================
    // FADE TRANSITION
    // ===================================================================

    private IEnumerator FadeTransition(float targetAlpha, float duration)
    {
        CreateTransitionFadeOverlay();
        if (transitionFadeGroup == null) yield break;

        float startAlpha = transitionFadeGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionFadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        transitionFadeGroup.alpha = targetAlpha;
    }

    private void CreateTransitionFadeOverlay()
    {
        if (transitionFadeGroup != null) return;

        Camera cam = storyCamera != null ? storyCamera : Camera.main;
        if (cam == null) return;

        GameObject fadeCanvasObj = new GameObject("Phase2TransitionFadeCanvas");
        fadeCanvasObj.transform.SetParent(cam.transform, false);
        fadeCanvasObj.transform.localPosition = new Vector3(0f, 0f, 0.31f);
        fadeCanvasObj.transform.localRotation = Quaternion.identity;
        fadeCanvasObj.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);

        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;

        RectTransform canvasRect = fadeCanvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000f, 1000f);

        transitionFadeGroup = fadeCanvasObj.AddComponent<CanvasGroup>();
        transitionFadeGroup.alpha = 0f;
        transitionFadeGroup.interactable = false;
        transitionFadeGroup.blocksRaycasts = false;

        GameObject panel = new GameObject("FadePanel");
        panel.transform.SetParent(fadeCanvasObj.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(-500f, -500f);
        panelRect.offsetMax = new Vector2(500f, 500f);

        Material alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode",
            (int)UnityEngine.Rendering.CompareFunction.Always);

        Image img = panel.AddComponent<Image>();
        img.color = Color.black;
        img.material = alwaysOnTop;
    }

    // ===================================================================
    // DIALOGUE & MONOLOGUE HELPERS
    // ===================================================================

    private IEnumerator ShowLine(string speaker, string text, float dur)
    {
        DialogueScreenUI ui = FindFirstObjectByType<DialogueScreenUI>();
        if (ui != null)
            ui.ForceShow($"<b>{speaker}</b>\n{text}", dur);
        else
            Debug.Log($"[Story] {speaker}: {text}");
        yield return new WaitForSeconds(dur);
    }

    private IEnumerator ShowLine(string speaker, string text, float dur, MainGameplayVoiceKey voiceKey)
    {
        float actualDuration = Mathf.Max(dur, MainGameplayVoiceover.PlayLine(voiceKey));
        yield return ShowLine(speaker, text, actualDuration);
    }

    private void ShowMonologue(string text, float dur)
    {
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(text, dur);
        else
            Debug.Log($"[Nội tâm] {text}");
    }

    private void ShowMonologue(string text, float dur, MainGameplayVoiceKey voiceKey)
    {
        float actualDuration = Mathf.Max(dur, MainGameplayVoiceover.PlayLine(voiceKey));
        ShowMonologue(text, actualDuration);
    }

    // ===================================================================
    // REFERENCE RESOLUTION
    // ===================================================================

    private void ResolveReferences()
    {
        if (player == null)
        {
            var p = GameObject.FindWithTag(playerTag);
            if (p != null) player = p.transform;
        }
        if (player != null && playerMovement == null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        if (storyCamera == null)
        {
            storyCamera = Camera.main;
        }
        // Buổi sáng bắt đầu cạnh xe bò bía. Buổi tối dùng anchor đầu hẻm riêng.
        GameObject cartSpawn = GameObject.Find("Player_Interaction_Spot");
        if (cartSpawn != null)
        {
            morningDay2PlayerSpawn = cartSpawn.transform;
        }
    }

    private void ConfigureEveningDay2Spawn()
    {
        if (eveningDay2PlayerSpawn == null)
        {
            GameObject spawnGo = GameObject.Find("_PlayerSpawn_EveningDay2");
            if (spawnGo == null) spawnGo = new GameObject("_PlayerSpawn_EveningDay2");

            GameObject waypointRoot = GameObject.Find("=== WAYPOINTS & TRIGGERS ===");
            if (waypointRoot != null)
                spawnGo.transform.SetParent(waypointRoot.transform, true);
            eveningDay2PlayerSpawn = spawnGo.transform;
        }

        eveningDay2PlayerSpawn.position = eveningDay2SpawnPos;
        Vector3 lookDirection = hidingZonePos - eveningDay2SpawnPos;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
            eveningDay2PlayerSpawn.rotation = Quaternion.LookRotation(lookDirection);
    }

    private void WarpPlayerTo(Transform spawn)
    {
        if (player == null || spawn == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(spawn.position, spawn.rotation);
        if (controller != null) controller.enabled = true;

        ThirdPersonCamera tpCam = Camera.main?.GetComponent<ThirdPersonCamera>();
        if (tpCam != null)
        {
            tpCam.ResetOrientation();
        }

        Log($"Warp Player tới '{spawn.name}' tại {spawn.position}.");
    }

    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[StoryPhase2] {message}");
    }
}
