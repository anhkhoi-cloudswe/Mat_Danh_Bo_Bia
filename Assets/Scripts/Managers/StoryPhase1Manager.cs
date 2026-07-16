using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Đạo diễn chuỗi sự kiện chính của PHASE 1 ("Anh Bò Bía") theo lộ trình trên bản đồ
/// edited-image.jpg. Toàn bộ kịch bản được điều khiển bằng một MÁY TRẠNG THÁI tuần tự
/// (State Machine) chạy trên coroutine, liên kết các trigger Animator với chuyển động
/// NavMeshAgent và camera điện ảnh.
///
/// Tóm tắt 5 trạng thái:
///   1. <see cref="Phase1State.Intro"/>            – Mở mắt tại Điểm 1, thoại nội tâm đói bụng.
///   2. <see cref="Phase1State.EncounterFlee"/>    – Bấm "Mua Bánh Mì" → huy_seo giật mình, rơi hột quẹt, bỏ chạy về Điểm 2.
///   3. <see cref="Phase1State.DisposeEvidence"/>  – Tại Điểm 2 huy_seo vứt bịch ma túy vào đống rác rồi chạy lên Điểm 3.
///   4. <see cref="Phase1State.DoorReturnDialogue"/> – Tại Điểm 3 huy_seo loay hoay mở cửa; Player nhặt hột quẹt rồi lên trả → đối thoại.
///   5. <see cref="Phase1State.TrashQuestActive"/> – huy_seo biến mất, mở khóa nhiệm vụ lục thùng rác tại Điểm 2.
///
/// LƯU Ý SETUP: Vì huy_seo chưa có sẵn trong scene, hãy kéo prefab Huy_seo vào scene
/// rồi gán vào trường <c>huySeo</c>, đồng thời đặt 3 Transform mốc Điểm 1/2/3 và các prefab.
/// Những tham chiếu cơ bản (Player, Camera, anhbanhmi, anhxamminh) sẽ được tự dò nếu để trống.
/// </summary>
[DisallowMultipleComponent]
public class StoryPhase1Manager : MonoBehaviour
{
    // ===================================================================
    // STATE MACHINE
    // ===================================================================

    public enum Phase1State
    {
        Intro,
        EncounterFlee,
        DisposeEvidence,
        DoorReturnDialogue,
        TrashQuestActive,
        Completed
    }

    public enum TimeOfDay
    {
        Morning_Day1,
        Evening_Phase1
    }

    [Header("=== THỜI GIAN TRONG NGÀY ===")]
    [Tooltip("Thời gian hiện tại trong game. Mặc định là Evening_Phase1 để chạy kịch bản.")]
    public TimeOfDay currentTime = TimeOfDay.Evening_Phase1;

    [Header("=== Morning NPCs Prefabs ===")]
    [SerializeField] private GameObject coNgaPrefab;
    [SerializeField] private GameObject anhShipperPrefab;
    [SerializeField] private GameObject coDongVienPrefab;

    [Header("=== TRẠNG THÁI HIỆN TẠI (chỉ đọc khi chạy) ===")]
    [SerializeField] private Phase1State currentState = Phase1State.Intro;
    public Phase1State CurrentState => currentState;

    // ===================================================================
    // INSPECTOR REFERENCES
    // ===================================================================

    [Header("=== Nhân vật chính & Camera ===")]
    [Tooltip("Người chơi (anhbobia). Để trống sẽ tự tìm theo tag.")]
    [SerializeField] private Transform player;
    [Tooltip("Camera dùng cho các khung quay điện ảnh. Để trống sẽ lấy Camera.main.")]
    [SerializeField] private Camera storyCamera;
    [Tooltip("Tag của người chơi để tự dò tham chiếu.")]
    [SerializeField] private string playerTag = "Player";

    [Header("=== Player Spawn Theo Phase ===")]
    [Tooltip("Vị trí Player khi bắt đầu buổi sáng bán hàng.")]
    [SerializeField] private Transform morningPlayerSpawn;
    [Tooltip("Vị trí Player sau fade sang Tối Ngày 1, tại đầu ngõ gần tiệm bánh mì.")]
    [SerializeField] private Transform eveningDay1PlayerSpawn;
    [Tooltip("Các script điều khiển Player sẽ bị TẮT trong lúc diễn hoạt cảnh (cutscene), bật lại khi xong.")]
    [SerializeField] private MonoBehaviour[] disableDuringCutscene;

    [Header("=== NPC tại tiệm bánh mì cột đèn ===")]
    [SerializeField] private GameObject anhXamMinh;
    [SerializeField] private GameObject anhBanhMi;

    [Header("=== huy_seo (kẻ tình nghi) ===")]
    [Tooltip("GameObject huy_seo trong scene (kéo từ prefab Huy_seo).")]
    [SerializeField] private GameObject huySeo;
    [Tooltip("Animator của huy_seo (states: FastRun, HuySeoOpening, HuySeoLeftTurn, Talking; triggers: toReact, toThrow).")]
    [SerializeField] private Animator huySeoAnimator;
    [Tooltip("NavMeshAgent của huy_seo (nếu thiếu/không có NavMesh sẽ tự fallback sang di chuyển thẳng).")]
    [SerializeField] private NavMeshAgent huySeoAgent;
    [Tooltip("Điểm gốc bàn tay huy_seo — vị trí spawn bịch ma túy khi vung tay.")]
    [SerializeField] private Transform huySeoHand;

    [Header("=== Lộ trình theo edited-image.jpg ===")]
    [Tooltip("Điểm 1 — đầu ngõ tiệm bánh mì: nơi huy_seo giật mình đánh rơi hột quẹt.")]
    [SerializeField] private Transform point1_Alley;
    [Tooltip("Điểm 2 — bãi rác / thùng rác: nơi phi tang bịch ma túy.")]
    [SerializeField] private Transform point2_TrashArea;
    [Tooltip("Điểm 3 — căn nhà: nơi huy_seo đứng mở cửa.")]
    [SerializeField] private Transform point3_House;
    [Tooltip("Đống rác để quăng bịch ma túy vào (AddForce hướng về đây). Để trống sẽ dùng Điểm 2.")]
    [SerializeField] private Transform trashPileTarget;
    [Tooltip("Vị trí ngõ rẽ (hẻm khoanh tròn) để kích hoạt cảnh ném ma túy ở Điểm 2. Để trống sẽ tự tìm.")]
    [SerializeField] private Transform alleyTriggerPoint;
    [Tooltip("Khoảng cách tối thiểu để kích hoạt cảnh ném ma túy.")]
    [SerializeField] private float triggerDistance = 3.5f;

    [Header("=== Prefab vật phẩm ===")]
    [Tooltip("batlua.prefab — hột quẹt / Zippo phát sáng.")]
    [SerializeField] private GameObject batLuaPrefab;
    [Tooltip("bichrac2.prefab — bịch ma túy trắng bị phi tang.")]
    [SerializeField] private GameObject bichRac2Prefab;

    [Header("=== Nhiệm vụ lục thùng rác (Điểm 2) ===")]
    [Tooltip("Collider/SpyZone tại bãi rác, sẽ được KÍCH HOẠT ở Trạng thái 5.")]
    [SerializeField] private GameObject trashInteractZone;
    [Tooltip("Mini-game bới rác (TrashInvestigation). Để trống sẽ chỉ kích hoạt trashInteractZone như cũ rồi hoàn thành ngay.")]
    [SerializeField] private TrashInvestigation trashInvestigation;

    [Header("=== Tham số diễn hoạt ===")]
    [SerializeField] private float reactDuration = 0.5f;
    [SerializeField] private float runSpeed = 4.5f;
    [Tooltip("Khoảng cách (m) coi như huy_seo đã tới mốc.")]
    [SerializeField] private float arriveThreshold = 1.2f;
    [Tooltip("Tầm tương tác (m) của Player với huy_seo tại Điểm 3.")]
    [SerializeField] private float interactRange = 2.5f;
    [Tooltip("Lực quăng bịch ma túy vào đống rác.")]
    [SerializeField] private float throwForce = 7f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("=== Camera điện ảnh ===")]
    [SerializeField] private bool useCinematicCamera = true;
    [SerializeField] private float closeupDistance = 2.2f;
    [SerializeField] private float closeupHeight = 1.6f;
    [SerializeField] private float farDistance = 7f;
    [SerializeField] private float farHeight = 3.5f;
    [SerializeField] private float cameraMoveDuration = 0.8f;

    [Header("=== Debug ===")]
    [Tooltip("Phím tắt mô phỏng bấm nút 'Mua Bánh Mì' (chỉ để test).")]
    [SerializeField] private KeyCode debugBuyKey = KeyCode.B;
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // RUNTIME STATE
    // ===================================================================

    private GameObject spawnedBatLua;
    private bool hasPickedUpLighter;
    public bool HasPickedUpLighter => hasPickedUpLighter;
    private bool sequenceRunning;     // đang chạy một coroutine cutscene → khóa input
    private int boBiaSold = 0;
    private bool transitioning = false;
    private CustomerManager.Customer finalMorningCustomer;
    private bool waitingForFinalCustomerDialogue;
    private CanvasGroup transitionFadeGroup;
    private PlayerMovement playerMovement;
    private GameObject morningAlleyBarrier;

    // di chuyển fallback (khi không có NavMesh)
    private bool moving;
    private Transform moveTarget;

    // cache camera để khôi phục sau cutscene
    private bool cameraCached;
    private Transform camOriginalParent;
    private Vector3 camOriginalLocalPos;
    private Quaternion camOriginalLocalRot;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Start()
    {
        ResolveReferences();
        WarpPlayerTo(currentTime == TimeOfDay.Morning_Day1 ? morningPlayerSpawn : eveningDay1PlayerSpawn);

        SubscribeEvents();

        SetupNPCsByTime();

        if (currentTime == TimeOfDay.Morning_Day1)
        {
            ShowMonologue("Buổi sáng bắt đầu. Hãy đứng tại quầy và phục vụ các vị khách đến mua bò bía.", 6f, MainGameplayVoiceKey.NarratorDay1MorningIntro);
            return;
        }

        // --- TRẠNG THÁI 1: KHỞI ĐẦU (mở mắt tại Điểm 1) ---
        currentState = Phase1State.Intro;

        // NPC tại tiệm bánh mì đứng sẵn
        if (anhXamMinh != null) anhXamMinh.SetActive(true);
        if (anhBanhMi != null) anhBanhMi.SetActive(true);

        // huy_seo chưa xuất hiện cho tới khi Player bấm "Mua Bánh Mì"
        if (huySeo != null) huySeo.SetActive(false);

        ShowMonologue("Đói quá... Hình như đầu ngõ có tiệm bánh mì cột đèn ngon lắm.", 6f, MainGameplayVoiceKey.PlayerNight1Intro);
        Log("Phase 1 bắt đầu — Trạng thái 1 (Intro).");
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.OnCustomerArrived -= HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerArrived += HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerServed -= HandleCustomerServed;
            CustomerManager.Instance.OnCustomerServed += HandleCustomerServed;
            CustomerManager.Instance.OnDialogueEnded -= HandleCustomerDialogueEnded;
            CustomerManager.Instance.OnDialogueEnded += HandleCustomerDialogueEnded;
        }
    }

    private void UnsubscribeEvents()
    {
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.OnCustomerArrived -= HandleCustomerArrived;
            CustomerManager.Instance.OnCustomerServed -= HandleCustomerServed;
            CustomerManager.Instance.OnDialogueEnded -= HandleCustomerDialogueEnded;
        }
    }

    private void Update()
    {
        if (currentTime == TimeOfDay.Morning_Day1) return;

        ManualMoveFallback();

        // Tương tác trực tiếp bằng phím E/B tại chỗ anhBanhMi để kích hoạt cốt truyện buổi tối
        if (currentState == Phase1State.Intro && !sequenceRunning)
        {
            if (IsBanhMiInteractable())
            {
                if (Input.GetKeyDown(interactKey) || (debugBuyKey != KeyCode.None && Input.GetKeyDown(debugBuyKey)))
                {
                    OnBuyBanhMiPressed();
                }
            }
            else if (debugBuyKey != KeyCode.None && Input.GetKeyDown(debugBuyKey))
            {
                OnBuyBanhMiPressed();
            }
        }

        // TRẠNG THÁI 4: chờ Player nhặt hột quẹt + lên Điểm 3 + bấm E để trả đồ
        if (currentState == Phase1State.DoorReturnDialogue && !sequenceRunning)
        {
            // Giữ cho huy_seo luôn thực hiện hoạt ảnh loay hoay mở cửa trong lúc chờ người chơi đến gần
            if (huySeoAnimator != null && !huySeoAnimator.GetCurrentAnimatorStateInfo(0).IsName("HuySeoOpening") && !huySeoAnimator.IsInTransition(0))
            {
                PlayState("HuySeoOpening");
            }

            if (hasPickedUpLighter && PlayerNear(point3_House) && Input.GetKeyDown(interactKey))
            {
                StartCoroutine(DoorDialogueSequence());
            }
        }

        // NHẶT HỘT QUẸT BẰNG PHÍM E KHI Ở GẦN
        if (currentState == Phase1State.EncounterFlee && !hasPickedUpLighter && spawnedBatLua != null && player != null)
        {
            if (IsPlayerNearLighter() && Input.GetKeyDown(interactKey))
            {
                NotifyLighterPicked();
            }
        }
    }

    // ===================================================================
    // PUBLIC API — gắn vào nút UI / hệ thống tương tác
    // ===================================================================

    /// <summary>
    /// TRẠNG THÁI 2: Player bấm nút "Mua Bánh Mì" tại chỗ anhbanhmi.
    /// Gắn hàm này vào onClick của Button hoặc gọi từ vùng tương tác.
    /// </summary>
    public void OnBuyBanhMiPressed()
    {
        if (currentState != Phase1State.Intro || sequenceRunning)
        {
            Log("Bỏ qua 'Mua Bánh Mì' — không ở trạng thái Intro hoặc đang diễn cảnh.");
            return;
        }
        StartCoroutine(EncounterSequence());
    }

    /// <summary>
    /// Được gọi bởi <see cref="Phase1LighterPickup"/> khi Player nhặt hột quẹt phát sáng ở Điểm 1.
    /// </summary>
    public void NotifyLighterPicked()
    {
        if (hasPickedUpLighter) return;
        hasPickedUpLighter = true;

        if (spawnedBatLua != null)
        {
            foreach (var l in spawnedBatLua.GetComponentsInChildren<Light>(true)) l.enabled = false;
            Destroy(spawnedBatLua);
        }

        ShowMonologue("Đã nhặt được chiếc hột quẹt Zippo phát sáng — của gã vừa giật mình bỏ chạy. Mang lên trả xem sao...", 4.5f,
            MainGameplayVoiceKey.PlayerPickupLighter);
        Log("Player đã nhặt hột quẹt.");
    }

    /// <summary>
    /// Được gọi bởi <see cref="TrashInvestigation"/> khi Player đã thu thập đủ cả kim tiêm và
    /// bịch ma túy ở bãi rác (Điểm 2) — chốt hạ và đánh dấu hoàn thành PHASE 1.
    /// </summary>
    public void MarkPhase1Complete()
    {
        if (currentState == Phase1State.Completed) return;
        currentState = Phase1State.Completed;
        Log("✅ Phase 1 hoàn thành — đã thu thập đủ vật chứng tại bãi rác, sẵn sàng chuyển sang Phase 2.");
    }

    // ===================================================================
    // DEV CHEAT TOOLS — chỉ dùng để TEST NHANH trong Editor (Play Mode)
    // ===================================================================

    /// <summary>
    /// [DEV CHEAT] Nhảy thẳng tới phân đoạn BUỔI TỐI: ép thời gian sang Evening_Phase1, dọn dẹp
    /// khách + tàn dư buổi sáng, bật môi trường tối (đèn đường / xe bánh mì / tường tàng hình theo
    /// logic evening) rồi kích hoạt NGAY chuỗi Huy_Seo giật mình bỏ chạy (<see cref="EncounterSequence"/>).
    /// Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_SkipToEveningCutscene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase1][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Nhảy đến Buổi Tối — kích hoạt cảnh Huy_Seo bỏ chạy.");

        // Dừng mọi coroutine cốt truyện đang dở để snap về trạng thái sạch
        StopAllCoroutines();
        StartCoroutine(CheatSkipToEveningRoutine());
    }

    private IEnumerator CheatSkipToEveningRoutine()
    {
        ResolveReferences();

        // 1. Khóa player trong lúc snap môi trường để tránh đi lạc
        if (playerMovement != null) playerMovement.IsMovementLocked = true;

        // 2. Ép thời gian sang buổi tối Phase 1
        currentTime = TimeOfDay.Evening_Phase1;

        // 3. Dọn dẹp tàn dư buổi sáng (khách hàng + vật phẩm thừa)
        CleanupMorningArtifacts();

        // 4. Bật môi trường buổi tối: đèn đường / xe bánh mì / barrier theo logic evening
        SetupNPCsByTime(true);
        WarpPlayerTo(eveningDay1PlayerSpawn);

        // 5. Đặt lại máy trạng thái về Intro để EncounterSequence chạy đúng từ đầu
        currentState = Phase1State.Intro;
        transitioning = false;
        hasPickedUpLighter = false;
        if (spawnedBatLua != null) { Destroy(spawnedBatLua); spawnedBatLua = null; }

        // Ẩn Huy_Seo cho tới khi EncounterSequence tự bật hắn lên
        if (huySeo != null) huySeo.SetActive(false);

        // Khóa cờ sequence ngay để Update() không kích hoạt trùng trong frame chờ
        sequenceRunning = true;
        yield return null; // chờ 1 frame cho các SetActive ổn định

        // 6. Mở khóa player — EncounterSequence sẽ tự quản lý cutscene từ đây
        if (playerMovement != null) playerMovement.IsMovementLocked = false;

        // 7. Kích hoạt ngay chuỗi sự kiện Huy_Seo bỏ chạy
        StartCoroutine(EncounterSequence());
    }

    /// <summary>
    /// [DEV CHEAT] Nhảy thẳng tới đầu BUỔI TỐI NGÀY 1 (bắt đầu từ trạng thái Intro thèm ăn bánh mì, chưa rượt đuổi).
    /// </summary>
    public void Cheat_SkipToEveningIntro()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase1][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Nhảy đến đầu Buổi Tối Ngày 1 (chưa rượt đuổi).");
        StopAllCoroutines();
        StartCoroutine(CheatSkipToEveningIntroRoutine());
    }

    private IEnumerator CheatSkipToEveningIntroRoutine()
    {
        ResolveReferences();
        if (playerMovement != null) playerMovement.IsMovementLocked = true;

        // 1. Fade đen nhanh
        yield return StartCoroutine(FadeTransition(1f, 0.5f));
        yield return new WaitForSeconds(0.2f);

        // 2. Dọn dẹp khách sáng
        CleanupMorningArtifacts();

        // 3. Thiết lập môi trường tối
        currentTime = TimeOfDay.Evening_Phase1;
        SetupNPCsByTime(true);
        WarpPlayerTo(eveningDay1PlayerSpawn);

        // 4. Khởi tạo trạng thái tối ngày 1 (chưa đuổi theo Huy Seo)
        currentState = Phase1State.Intro;
        transitioning = false;
        hasPickedUpLighter = false;
        if (spawnedBatLua != null) { Destroy(spawnedBatLua); spawnedBatLua = null; }
        if (huySeo != null) huySeo.SetActive(false);

        // 5. Mở khóa di chuyển và hiện lời thoại dẫn dắt
        if (playerMovement != null) playerMovement.IsMovementLocked = false;
        ShowMonologue("Đói quá... Hình như đầu ngõ có tiệm bánh mì cột đèn ngon lắm.", 6f, MainGameplayVoiceKey.PlayerNight1Intro);

        // 6. Fade in lại
        yield return StartCoroutine(FadeTransition(0f, 0.5f));
        Log("[CHEAT] Đã nhảy sang đầu Buổi Tối Ngày 1.");
    }

    /// <summary>
    /// [DEV CHEAT] Nhảy thẳng tới phân đoạn LỤC SOÁT ĐỐNG RÁC: coi như Huy_Seo đã bỏ chạy & biến mất,
    /// đã phi tang xong (mở tường hẻm), rồi kích hoạt NGAY mini-game bới rác
    /// (<see cref="TrashInvestigation.BeginInvestigation"/>) để test cơ chế GIỮ phím E nhặt kim tiêm /
    /// bịch ma túy. Chỉ chạy được khi đang ở chế độ Play.
    /// </summary>
    public void Cheat_SkipToTrashInvestigation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryPhase1][CHEAT] Chỉ dùng được khi đang ở chế độ PLAY.");
            return;
        }

        Log("⏩ [CHEAT] Nhảy đến Bới Rác — kích hoạt mini-game lục soát đống rác.");

        StopAllCoroutines();
        StartCoroutine(CheatSkipToTrashRoutine());
    }

    private IEnumerator CheatSkipToTrashRoutine()
    {
        ResolveReferences();

        // 1. Khóa player trong lúc snap môi trường
        if (playerMovement != null) playerMovement.IsMovementLocked = true;

        // 2. Ép thời gian sang buổi tối Phase 1
        currentTime = TimeOfDay.Evening_Phase1;

        // 3. Dọn dẹp tàn dư buổi sáng
        CleanupMorningArtifacts();

        // 4. Bật môi trường buổi tối
        SetupNPCsByTime(true);

        // 5. Coi như Huy_Seo đã bỏ chạy & biến mất, đã phi tang xong → mở tường hẻm cho player vào
        if (huySeo != null) huySeo.SetActive(false);
        SetMorningBarrierActive(false);

        // 6. Dọn hột quẹt còn phát sáng (nếu có) & coi như player đã nhặt
        if (spawnedBatLua != null) { Destroy(spawnedBatLua); spawnedBatLua = null; }
        hasPickedUpLighter = true;

        // 7. Ép máy trạng thái nhảy thẳng tới TrashQuestActive
        currentState = Phase1State.TrashQuestActive;
        sequenceRunning = false;
        transitioning = false;

        // 8. Kích hoạt vùng tương tác bới rác tại Điểm 2
        if (trashInteractZone != null)
        {
            trashInteractZone.SetActive(true);
            var col = trashInteractZone.GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        yield return null; // chờ 1 frame cho các SetActive ổn định

        // 9. Mở khóa player để có thể lại gần đống rác & giữ E
        if (playerMovement != null) playerMovement.IsMovementLocked = false;

        // 10. Khởi động mini-game bới rác
        if (trashInvestigation == null)
            trashInvestigation = FindFirstObjectByType<TrashInvestigation>();

        if (trashInvestigation != null)
        {
            trashInvestigation.BeginInvestigation();
            ShowMonologue("(DEV) Đã nhảy tới khúc bới rác. Lại gần đống rác và GIỮ phím E để lục soát!", 5f);
        }
        else
        {
            Debug.LogWarning("[StoryPhase1][CHEAT] Không tìm thấy TrashInvestigation trong scene — không thể bắt đầu mini-game bới rác.");
        }
    }

    /// <summary>
    /// Dọn dẹp tàn dư buổi sáng trước khi nhảy cheat: bật lại các script điều khiển player (phòng khi
    /// đang kẹt cutscene), dừng & xóa hàng đợi khách, hủy các clone NPC khách còn sót lại trong scene.
    /// </summary>
    private void CleanupMorningArtifacts()
    {
        // Đảm bảo các script điều khiển player được bật lại (phòng khi đang dở một cutscene)
        SetCutscene(false);

        // Dừng spawn & xóa sạch hàng đợi khách buổi sáng
        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.StopSpawning(true);
        }

        // Hủy các clone NPC khách buổi sáng còn sót lại
        var allGOs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string n = go.name;
            if (!n.Contains("(Clone)")) continue;
            if (n.Contains("Nganpc") || n.Contains("shipper") || n.Contains("Npc1") ||
                n.Contains("Npc3") || n.Contains("Miu Le") || n.Contains("anh_aoxanh"))
            {
                Destroy(go);
            }
        }

        Log("CleanupMorningArtifacts: đã dừng spawn khách & dọn clone NPC buổi sáng.");
    }

    // ===================================================================
    // TRẠNG THÁI 2: CHẠM MẶT & BỎ CHẠY
    // ===================================================================

    private IEnumerator EncounterSequence()
    {
        sequenceRunning = true;
        currentState = Phase1State.EncounterFlee;
        Log("→ Trạng thái 2 (EncounterFlee).");
        SetCutscene(true);

        // Đối thoại cốt truyện mới khi tương tác mua bánh mì buổi tối
        yield return ShowLine("anhbanhmi", "Mua bánh mì đi tui bán rẻ lắm", 3.0f,
            MainGameplayVoiceKey.BanhMiNight1);

        // huy_seo xuất hiện ở xa hơn và đi bộ tới vị trí rơi hột quẹt (Điểm 1)
        if (huySeo != null)
        {
            huySeo.SetActive(true);
            // Spawn cách Điểm 1 khoảng 4m về phía sau
            Vector3 startPos = point1_Alley != null 
                ? point1_Alley.position + Vector3.back * 4.0f 
                : (huySeo.transform.position + Vector3.back * 4.0f);
            WarpHuySeo(startPos);
            
            // Đi bộ chậm tới Điểm 1
            PlayState("Walk");
            if (point1_Alley != null)
            {
                float originalRunSpeed = runSpeed;
                runSpeed = 1.5f; // Tốc độ đi bộ chậm
                StartMoveTo(point1_Alley);
                yield return WaitForArrival(point1_Alley, 6f);
                runSpeed = originalRunSpeed; // Khôi phục tốc độ chạy nhanh
            }
            StopMove();
        }

        // Khung đoạn số 1: zoom cận vào Huy Sẹo ngay khi hắn nhìn thấy Player ở Point1.
        // FocusCameraPOV chỉ xoay camera first-person nên không tạo cảm giác zoom rõ ràng.
        yield return FocusCamera(huySeo != null ? huySeo.transform : null,
            closeupDistance + 1.2f, closeupHeight);
        yield return new WaitForSeconds(0.8f);

        // Anim giật mình (0.5s)
        SafeSetTrigger("toReact");
        yield return new WaitForSeconds(reactDuration);

        // Sinh hột quẹt dưới chân + bật phát sáng
        SpawnLighter();
        yield return new WaitForSeconds(0.3f);

        // Chuyển FastRun, chạy dọc đường đỏ về Điểm 2 (bãi rác)
        PlayState("FastRun");
        StartMoveTo(point2_TrashArea);

        // Trả camera về gameplay để Player nhìn hắn bỏ chạy
        yield return RestoreCamera();

        // Giải phóng điều khiển của Player để tự do di chuyển và đi nhặt hột quẹt
        SetCutscene(false);
        sequenceRunning = false;

        // Hiện hướng dẫn ngay; Huy_seo vẫn TỰ chạy về Điểm 2 (bãi rác) trong lúc Player đi lại.
        // KHÔNG chặn chờ Huy chạy xong nữa — nếu chờ, Player tới đầu hẻm rồi vẫn phải đợi
        // Huy chạy hết đường mới zoom (cảm giác "một lúc sau mới kích hoạt").
        ShowMonologue("Hắn đang dừng ở bãi rác... Lại gần đầu hẻm để quan sát.", 4.5f, MainGameplayVoiceKey.PlayerNight1ObserveHuy);

        // KÍCH HOẠT NGAY khi Player vừa tới đầu hẻm.
        while (!IsPlayerNearAlleyCorner())
        {
            // Nếu Huy Sẹo đã tới bãi rác, dừng di chuyển và chuyển sang đứng im (Idle) thay vì chạy tại chỗ
            if (huySeo != null && point2_TrashArea != null && Reached(point2_TrashArea))
            {
                StopMove();
                PlayState("Idle");
            }
            yield return new WaitForSeconds(0.1f);
        }

        // Player đã tới điểm quan sát: nếu Huy còn đang chạy chưa tới bãi rác thì cho hắn
        // tới ngay Điểm 2 để khung zoom + cảnh phi tang khớp bối cảnh, rồi zoom tức thì.
        StopMove();
        if (huySeo != null && point2_TrashArea != null && !Reached(point2_TrashArea))
        {
            WarpHuySeo(point2_TrashArea.position);
        }
        PlayState("Idle"); // tư thế đứng chờ tại bãi rác (dùng Idle thay vì Walk)

        // Zoom Point2 ngay khi Huy Sẹo tới bãi rác. Trước đây zoom chỉ chạy sau
        // khi Player đã nhặt hột quẹt nên cảnh Point2 bị tưởng là bỏ qua.
        SetCutscene(true);
        yield return FocusCamera(huySeo != null ? huySeo.transform : null,
            closeupDistance + 1.2f, closeupHeight);
        yield return new WaitForSeconds(1.2f);
        yield return RestoreCamera();
        SetCutscene(false);

        // Huy Sẹo phải phi tang ngay khi tới Point2. Trước đây đoạn ném bịch đá bị
        // chặn bởi điều kiện Player nhặt hột quẹt + đi tới hẻm, khiến hắn đứng im ở thùng rác.
        yield return DisposeEvidenceSequence();
    }

    // ===================================================================
    // TRẠNG THÁI 3: PHI TANG VẬT CHỨNG
    // ===================================================================

    private IEnumerator DisposeEvidenceSequence()
    {
        sequenceRunning = true;
        currentState = Phase1State.DisposeEvidence;
        Log("→ Trạng thái 3 (DisposeEvidence).");
        SetCutscene(true);
        StopMove();

        // Khung đoạn số 2: camera lia/zoom từ xa
        yield return FocusCamera(huySeo != null ? huySeo.transform : null, farDistance, farHeight);

        // Anim vứt đồ
        SafeSetTrigger("toThrow");
        yield return new WaitForSeconds(0.4f); // canh tới frame vung tay

        // Spawn bịch ma túy tại tay & quăng vào đống rác bằng Rigidbody.AddForce
        ThrowDrugBag();
        yield return new WaitForSeconds(0.7f);

        // huy_seo đã phi tang xong bịch ma túy → MỞ rào cản hẻm cho người chơi tiến vào khám phá
        SetMorningBarrierActive(false);
        Log("huy_seo đã vứt rác xong — mở MorningAlleyBarrier, người chơi được vào hẻm.");

        yield return RestoreCamera();

        // Giải phóng điều khiển của Player để họ đi theo Huy_seo lên Điểm 3
        SetCutscene(false);
        sequenceRunning = false;

        // Tiếp tục chạy dọc bờ tường lên Điểm 3 (căn nhà)
        PlayState("FastRun");
        StartMoveTo(point3_House);
        yield return WaitForArrival(point3_House, 10f);

        // Nếu quá thời gian di chuyển mà vẫn chưa đến cửa nhà (bị kẹt), tự động warp để tiếp tục
        if (huySeo != null && point3_House != null && !Reached(point3_House))
        {
            WarpHuySeo(point3_House.position);
        }

        yield return ArriveAtDoorSequence();
    }

    // ===================================================================
    // TRẠNG THÁI 4 (phần 1): ĐỨNG MỞ CỬA — chờ Player tương tác
    // ===================================================================

    private IEnumerator ArriveAtDoorSequence()
    {
        currentState = Phase1State.DoorReturnDialogue;
        Log("→ Trạng thái 4 (DoorReturnDialogue) — chờ Player trả hột quẹt.");
        StopMove();

        // Warp exactly to the position and rotation of point3_House to prevent standing too far away
        if (huySeo != null && point3_House != null)
        {
            WarpHuySeo(point3_House.position);
            huySeo.transform.rotation = point3_House.rotation;
        }

        // Quay lưng vào tường, loay hoay mở cửa
        FaceAwayFromPlayer();
        PlayState("HuySeoOpening");

        // Mở khóa input — Update() sẽ xử lý phần bấm E
        sequenceRunning = false;
        yield break;
    }

    // ===================================================================
    // TRẠNG THÁI 4 (phần 2): ĐỐI THOẠI TRẢ ĐỒ
    // ===================================================================

    private IEnumerator DoorDialogueSequence()
    {
        sequenceRunning = true;
        Log("Bắt đầu đối thoại trả hột quẹt.");
        SetCutscene(true);

        // NavMeshAgent với updateRotation=true sẽ GHI ĐÈ transform.rotation mỗi frame, giữ Huy
        // quay theo hướng di chuyển cũ (mặt vào cổng) → nuốt mất lệnh quay mặt thủ công, khiến
        // Huy quay lưng vào Player. Tắt điều khiển xoay của agent trong lúc hội thoại.
        bool restoreAgentRotation = false;
        if (huySeoAgent != null && huySeoAgent.isActiveAndEnabled)
        {
            huySeoAgent.isStopped = true;
            restoreAgentRotation = huySeoAgent.updateRotation;
            huySeoAgent.updateRotation = false;
        }

        // Clip HuySeoLeftTurn có Root Motion. Nếu vừa để clip tự xoay vừa Slerp root,
        // Huy Sẹo sẽ bị xoay hai lần và cuối cùng quay lưng về phía Player.
        // Tạm khóa Root Motion trong lúc quay mặt + hội thoại để root luôn hướng đúng.
        bool restoreRootMotion = huySeoAnimator != null && huySeoAnimator.applyRootMotion;
        if (huySeoAnimator != null) huySeoAnimator.applyRootMotion = false;

        // Xoay người mượt mà (Slerp) quay lại nói chuyện trực diện.
        PlayState("Talking");
        yield return SlerpFacePlayer(0.8f);
        FacePlayerImmediately();

        // Hội thoại UI đan xen
        yield return ShowLine("anhbobia", "Hột quẹt của anh làm rơi ở đầu ngõ này.", 3.5f,
            MainGameplayVoiceKey.PlayerReturnLighter);
        yield return ShowLine("huy_seo", "Ơ, cái Zippo kỷ niệm của tôi! Cảm ơn anh nhiều nhé...", 3.5f,
            MainGameplayVoiceKey.HuyReturnLighter);
        yield return ShowLine("huy_seo", "Mà anh thấy tụi nó không?... Tụi nó núp trong bóng tối... Đừng cướp kẹo của tôi... Hehehe...", 5f,
            MainGameplayVoiceKey.HuyWarning);

        // Trả Root Motion về cấu hình ban đầu trước khi mở cửa bước vào nhà.
        if (huySeoAnimator != null) huySeoAnimator.applyRootMotion = restoreRootMotion;

        // huy_seo mở cửa bước vào nhà rồi ẩn đi (giữ agent.updateRotation=false để FaceAwayFromPlayer
        // không bị agent ghi đè).
        FaceAwayFromPlayer();
        PlayState("HuySeoOpening");
        yield return new WaitForSeconds(1.5f);
        if (huySeo != null) huySeo.SetActive(false);

        // Khôi phục điều khiển xoay của agent (Huy đã ẩn nên chỉ để dọn dẹp trạng thái).
        if (huySeoAgent != null) huySeoAgent.updateRotation = restoreAgentRotation;

        yield return RestoreCamera();
        SetCutscene(false);

        yield return TrashQuestSequence();
    }

    // ===================================================================
    // TRẠNG THÁI 5: KÍCH HOẠT NHIỆM VỤ LỤC THÙNG RÁC
    // ===================================================================

    private IEnumerator TrashQuestSequence()
    {
        currentState = Phase1State.TrashQuestActive;
        Log("→ Trạng thái 5 (TrashQuestActive).");
        yield return new WaitForSeconds(0.4f);

        ShowMonologue(
            "Tên này phê thuốc nặng rồi, nói năng điên khùng không tỉnh táo. Khoan đã... " +
            "nãy nhìn từ xa rõ ràng thấy hắn vứt cái bịch gì đó rất lén lút vào đống rác đầu ngõ. " +
            "Phải quay lại thùng rác kiểm tra xem hắn giấu cái gì mới được!",
            9f,
            MainGameplayVoiceKey.PlayerInspectTrash);

        // Mở khóa vùng tương tác bới rác tại Điểm 2
        if (trashInteractZone != null)
        {
            trashInteractZone.SetActive(true);
            var col = trashInteractZone.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            Log("Đã kích hoạt vùng lục thùng rác tại Điểm 2.");
        }
        else
        {
            Log("CHƯA gán trashInteractZone — nhớ gán vùng lục rác để hoàn thiện nhiệm vụ.");
        }

        // Khởi động mini-game bới rác: Player giữ E để lục soát → spawn kim tiêm + bịch ma túy
        // → thu thập đủ → TrashInvestigation gọi lại MarkPhase1Complete() để chốt hạ Phase 1.
        if (trashInvestigation != null)
        {
            trashInvestigation.BeginInvestigation();
            Log("Đã khởi động mini-game bới rác (TrashInvestigation). Chờ Player thu thập đủ vật chứng...");
        }
        else
        {
            // Không có mini-game → giữ hành vi cũ: coi như hoàn thành ngay.
            currentState = Phase1State.Completed;
            Log("Không gán TrashInvestigation — đánh dấu Phase 1 hoàn thành ngay.");
        }

        sequenceRunning = false;
    }

    // ===================================================================
    // SPAWN & VẬT LÝ
    // ===================================================================

    private void SpawnLighter()
    {
        Vector3 pos = point1_Alley != null
            ? point1_Alley.position
            : (huySeo != null ? huySeo.transform.position : transform.position);

        // Nâng cao vị trí spawn 0.15m để tránh hột quẹt bị lún dưới mặt đất
        pos.y += 0.15f;

        if (batLuaPrefab == null)
        {
            Log("CHƯA gán batLuaPrefab — không thể spawn hột quẹt.");
            return;
        }

        spawnedBatLua = Instantiate(batLuaPrefab, pos, Quaternion.identity);

        // Tạo nguồn sáng Point Light màu vàng ấm lung linh cho hột quẹt để nổi bật trong đêm tối (đã làm dịu bớt độ sáng)
        GameObject lightGo = new GameObject("LighterGlowLight");
        lightGo.transform.SetParent(spawnedBatLua.transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.1f;
        Light lightComp = lightGo.AddComponent<Light>();
        lightComp.type = LightType.Point;
        lightComp.color = new Color(1f, 0.6f, 0.1f); // Màu lửa vàng ấm
        lightComp.range = 2.5f; // Tầm sáng dịu nhẹ 2.5m
        lightComp.intensity = 1.8f; // Cường độ sáng dịu mắt
        lightComp.shadows = LightShadows.None;

        // Bật component phát sáng (Light / Particle) nếu có sẵn trong prefab và làm dịu độ sáng nếu quá chói
        foreach (var l in spawnedBatLua.GetComponentsInChildren<Light>(true))
        {
            l.enabled = true;
            if (l.intensity > 2f) l.intensity = 1.2f;
        }
        foreach (var ps in spawnedBatLua.GetComponentsInChildren<ParticleSystem>(true)) ps.Play();

        // Gắn vùng nhặt (trigger) với bán kính rộng hơn (2.5m) để dễ nhặt hơn
        var trigger = spawnedBatLua.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.5f;

        var pickup = spawnedBatLua.AddComponent<Phase1LighterPickup>();
        pickup.Init(this, playerTag);

        Log("Đã spawn hột quẹt phát sáng tại Điểm 1.");
    }

    private void ThrowDrugBag()
    {
        if (bichRac2Prefab == null)
        {
            Log("CHƯA gán bichRac2Prefab — không thể quăng bịch ma túy.");
            return;
        }

        Vector3 spawnPos = huySeoHand != null
            ? huySeoHand.position
            : (huySeo != null ? huySeo.transform.position + Vector3.up * 1.2f : transform.position);

        GameObject bag = Instantiate(bichRac2Prefab, spawnPos, Random.rotation);

        Rigidbody rb = bag.GetComponent<Rigidbody>();
        if (rb == null) rb = bag.AddComponent<Rigidbody>();

        Vector3 target = trashPileTarget != null
            ? trashPileTarget.position
            : (point2_TrashArea != null ? point2_TrashArea.position : spawnPos + transform.forward * 3f);

        Vector3 toTarget = target - spawnPos;
        Vector3 horizontal = new Vector3(toTarget.x, 0f, toTarget.z);
        Vector3 force = horizontal.normalized * throwForce + Vector3.up * throwForce * 0.6f;
        rb.AddForce(force, ForceMode.Impulse);

        Log("Đã quăng bịch ma túy vào đống rác.");
    }

    // ===================================================================
    // DI CHUYỂN (NavMeshAgent + fallback thẳng)
    // ===================================================================

    private void StartMoveTo(Transform dest)
    {
        moveTarget = dest;
        moving = dest != null;

        if (huySeoAgent != null)
        {
            bool usable = AgentUsable();
            huySeoAgent.enabled = usable;
            if (usable && dest != null && huySeoAgent.isActiveAndEnabled && huySeoAgent.isOnNavMesh)
            {
                huySeoAgent.speed = runSpeed;
                huySeoAgent.isStopped = false;
                huySeoAgent.SetDestination(dest.position);
            }
        }
    }

    private void StopMove()
    {
        moving = false;
        if (huySeoAgent != null && huySeoAgent.isActiveAndEnabled && huySeoAgent.isOnNavMesh)
        {
            huySeoAgent.isStopped = true;
            huySeoAgent.velocity = Vector3.zero;
        }
    }

    private IEnumerator WaitForArrival(Transform dest, float timeout)
    {
        float elapsed = 0f;
        while (!Reached(dest) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        StopMove();
    }

    /// <summary>Di chuyển thủ công khi không có NavMesh hợp lệ (agent tự lo nếu có).</summary>
    private void ManualMoveFallback()
    {
        if (!moving || huySeo == null || moveTarget == null) return;
        if (AgentUsable()) return; // NavMeshAgent đang điều khiển

        Vector3 cur = huySeo.transform.position;
        Vector3 dest = moveTarget.position;
        dest.y = cur.y;

        huySeo.transform.position = Vector3.MoveTowards(cur, dest, runSpeed * Time.deltaTime);

        Vector3 dir = dest - cur;
        if (dir.sqrMagnitude > 0.01f)
            huySeo.transform.rotation = Quaternion.Slerp(
                huySeo.transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    private bool Reached(Transform dest)
    {
        if (huySeo == null || dest == null) return true;
        Vector3 a = huySeo.transform.position; a.y = 0f;
        Vector3 b = dest.position; b.y = 0f;
        return Vector3.Distance(a, b) <= arriveThreshold;
    }

    private bool AgentUsable()
    {
        if (huySeoAgent == null || huySeo == null) return false;
        NavMeshHit hit;
        return NavMesh.SamplePosition(huySeo.transform.position, out hit, 1.0f, NavMesh.AllAreas);
    }

    private void WarpHuySeo(Vector3 pos)
    {
        if (huySeoAgent != null)
        {
            huySeoAgent.enabled = AgentUsable();
            if (huySeoAgent.isActiveAndEnabled && huySeoAgent.isOnNavMesh)
            {
                huySeoAgent.Warp(pos);
                return;
            }
        }
        if (huySeo != null) huySeo.transform.position = pos;
    }

    // ===================================================================
    // XOAY NGƯỜI
    // ===================================================================

    private IEnumerator SlerpFacePlayer(float dur)
    {
        if (huySeo == null || player == null) yield break;

        Quaternion start = huySeo.transform.rotation;
        Vector3 dir = player.position - huySeo.transform.position; dir.y = 0f;
        Quaternion end = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : start;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            huySeo.transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        huySeo.transform.rotation = end;
    }

    private void FacePlayerImmediately()
    {
        if (huySeo == null || player == null) return;

        Vector3 dir = player.position - huySeo.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            huySeo.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void FaceAwayFromPlayer()
    {
        if (huySeo == null) return;

        Vector3 dir;
        if (point3_House != null) dir = point3_House.forward;
        else if (player != null) { dir = huySeo.transform.position - player.position; dir.y = 0f; }
        else return;

        if (dir.sqrMagnitude > 0.01f)
            huySeo.transform.rotation = Quaternion.LookRotation(dir);
    }

    private bool PlayerNear(Transform anchor)
    {
        if (player == null) return false;
        Vector3 reference = anchor != null ? anchor.position : (huySeo != null ? huySeo.transform.position : transform.position);
        return Vector3.Distance(player.position, reference) <= interactRange;
    }

    // ===================================================================
    // CAMERA ĐIỆN ẢNH
    // ===================================================================

    private IEnumerator FocusCamera(Transform target, float distance, float height)
    {
        if (!useCinematicCamera || storyCamera == null || target == null) yield break;

        CacheCamera();
        storyCamera.transform.SetParent(null, true);

        Vector3 dir = storyCamera.transform.position - target.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -target.forward;
        dir.Normalize();

        Vector3 destPos = target.position + dir * distance + Vector3.up * height;
        Vector3 lookAt = target.position + Vector3.up * 1.2f;
        Quaternion destRot = Quaternion.LookRotation(lookAt - destPos);

        yield return LerpCameraWorld(destPos, destRot, cameraMoveDuration);
    }

    private IEnumerator FocusCameraPOV(Transform target)
    {
        if (!useCinematicCamera || storyCamera == null || target == null || player == null) yield break;

        CacheCamera();
        storyCamera.transform.SetParent(null, true);

        // Góc nhìn của player nhìn về phía target
        Vector3 eyePos = player.position + Vector3.up * 1.6f + player.forward * 0.15f;
        Vector3 lookAt = target.position + Vector3.up * 1.2f;
        Quaternion destRot = Quaternion.LookRotation(lookAt - eyePos);

        yield return LerpCameraWorld(eyePos, destRot, cameraMoveDuration);
    }

    private IEnumerator RestoreCamera()
    {
        if (!cameraCached) yield break;

        storyCamera.transform.SetParent(camOriginalParent, true);

        float t = 0f;
        Vector3 startPos = storyCamera.transform.localPosition;
        Quaternion startRot = storyCamera.transform.localRotation;
        while (t < cameraMoveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / cameraMoveDuration);
            storyCamera.transform.localPosition = Vector3.Lerp(startPos, camOriginalLocalPos, k);
            storyCamera.transform.localRotation = Quaternion.Slerp(startRot, camOriginalLocalRot, k);
            yield return null;
        }
        storyCamera.transform.localPosition = camOriginalLocalPos;
        storyCamera.transform.localRotation = camOriginalLocalRot;
        cameraCached = false;
    }

    private IEnumerator LerpCameraWorld(Vector3 pos, Quaternion rot, float dur)
    {
        float t = 0f;
        Vector3 startPos = storyCamera.transform.position;
        Quaternion startRot = storyCamera.transform.rotation;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            storyCamera.transform.position = Vector3.Lerp(startPos, pos, k);
            storyCamera.transform.rotation = Quaternion.Slerp(startRot, rot, k);
            yield return null;
        }
        storyCamera.transform.position = pos;
        storyCamera.transform.rotation = rot;
    }

    private void CacheCamera()
    {
        if (cameraCached) return;
        camOriginalParent = storyCamera.transform.parent;
        camOriginalLocalPos = storyCamera.transform.localPosition;
        camOriginalLocalRot = storyCamera.transform.localRotation;
        cameraCached = true;
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    private void SetCutscene(bool active)
    {
        if (disableDuringCutscene == null) return;
        foreach (var mb in disableDuringCutscene)
            if (mb != null) mb.enabled = !active;
    }

    private void SafeSetTrigger(string triggerName)
    {
        if (huySeoAnimator == null) { Log($"huySeoAnimator null — bỏ qua trigger '{triggerName}'."); return; }

        foreach (var p in huySeoAnimator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
            {
                huySeoAnimator.SetTrigger(triggerName);
                return;
            }
        }
        Debug.LogWarning($"[StoryPhase1] Animator của huy_seo thiếu trigger '{triggerName}'.");
    }

    private void PlayState(string stateName)
    {
        if (huySeoAnimator == null) return;
        if (huySeoAnimator.HasState(0, Animator.StringToHash(stateName)))
            huySeoAnimator.CrossFadeInFixedTime(stateName, 0.15f);
        else
            Debug.LogWarning($"[StoryPhase1] Animator của huy_seo thiếu state '{stateName}'.");
    }

    private IEnumerator ShowLine(string speaker, string text, float dur)
    {
        DialogueScreenUI ui = FindObjectOfType<DialogueScreenUI>();
        if (ui != null) ui.ForceShow($"<b>{speaker}</b>\n{text}", dur);
        else Debug.Log($"[Story] {speaker}: {text}");
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
        {
            // Fallback trực tiếp để đoạn chuyển sáng → tối vẫn hiện thoại nếu bootstrap
            // InternalMonologueManager chưa kịp tạo ở frame đầu.
            DialogueScreenUI ui = FindFirstObjectByType<DialogueScreenUI>();
            if (ui != null)
                ui.ForceShow($"<i><color=#AEB7BF>{text}</color></i>", dur);
            else
                Debug.Log($"[Nội tâm] {text}");
        }
    }

    private void ShowMonologue(string text, float dur, MainGameplayVoiceKey voiceKey)
    {
        float actualDuration = Mathf.Max(dur, MainGameplayVoiceover.PlayLine(voiceKey));
        ShowMonologue(text, actualDuration);
    }

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
        if (storyCamera == null) storyCamera = Camera.main;

        // Xe bò bía là spawn an toàn, nằm đúng phía của collider dành cho người chơi.
        // Luôn ưu tiên vị trí này thay vì các anchor nằm phía sau hàng rào/collider.
        GameObject cartSpawn = GameObject.Find("Player_Interaction_Spot");
        if (cartSpawn != null)
        {
            morningPlayerSpawn = cartSpawn.transform;
            eveningDay1PlayerSpawn = cartSpawn.transform;
        }

        if (huySeo != null)
        {
            if (huySeoAnimator == null) huySeoAnimator = huySeo.GetComponentInChildren<Animator>();
            if (huySeoAgent == null) huySeoAgent = huySeo.GetComponent<NavMeshAgent>();
            if (huySeoAnimator != null) huySeoAnimator.applyRootMotion = false;
        }
        if (anhBanhMi == null)
        {
            var g = GameObject.Find("anhbanhmi");
            if (g == null) g = GameObject.Find("Uncle_BanhMi");
            if (g != null) anhBanhMi = g;
        }
        if (anhXamMinh == null) { var g = GameObject.Find("anhxamminh"); if (g != null) anhXamMinh = g; }
        if (anhXamMinh != null)
        {
            Animator anim = anhXamMinh.GetComponent<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }
        if (alleyTriggerPoint == null)
        {
            GameObject go = GameObject.Find("Waypoint_Road_Corner");
            if (go != null) alleyTriggerPoint = go.transform;
            else if (point2_TrashArea != null)
            {
                // BaoScene chỉ dùng Point1/2/3. Khi không có waypoint góc hẻm riêng,
                // Point2 là mốc hợp lý để Player chứng kiến Huy Sẹo phi tang rồi
                // tiếp tục cho Huy chạy tới Point3.
                alleyTriggerPoint = point2_TrashArea;
                Log("Không có Waypoint_Road_Corner — dùng Point2_TrashArea làm mốc tiếp tục Point3.");
            }
        }
        // Ép khoảng cách kích hoạt cảnh ném ma túy rộng hơn (4.5m) để đảm bảo người chơi đi tới góc hẻm là kích hoạt mượt mà
        triggerDistance = 4.5f;
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

    public bool IsSequenceRunning => sequenceRunning;

    public bool IsPlayerNearBanhMi()
    {
        if (anhBanhMi == null || player == null) return false;
        return Vector3.Distance(player.position, anhBanhMi.transform.position) <= interactRange;
    }

    /// <summary>
    /// True khi Player có thể bấm E để "Mua bánh mì": đứng gần anhbanhmi VÀ không đứng gần
    /// anhxamminh hơn. Nhờ vậy phím E phân biệt rõ: gần anhbanhmi → mua (kích hoạt cốt truyện),
    /// gần anhxamminh → để <see cref="NpcTalkInteraction"/> xử lý nói chuyện.
    /// </summary>
    public bool IsBanhMiInteractable()
    {
        if (!IsPlayerNearBanhMi()) return false;
        if (anhXamMinh != null && anhBanhMi != null && player != null)
        {
            float dBanhMi = Vector3.Distance(player.position, anhBanhMi.transform.position);
            float dXam = Vector3.Distance(player.position, anhXamMinh.transform.position);
            if (dXam < dBanhMi) return false; // gần anhxamminh hơn → nhường cho nói chuyện
        }
        return true;
    }

    public bool IsPlayerNearHouse()
    {
        return PlayerNear(point3_House);
    }

    public bool IsPlayerNearAlleyCorner()
    {
        if (player == null || alleyTriggerPoint == null) return false;
        return Vector3.Distance(player.position, alleyTriggerPoint.position) <= triggerDistance;
    }

    public bool IsPlayerNearLighter()
    {
        if (player == null || spawnedBatLua == null) return false;
        return Vector3.Distance(player.position, spawnedBatLua.transform.position) <= 2.5f;
    }

    private void SetupNPCsByTime(bool isCheatJump = false)
    {
        if (isCheatJump)
            Log("SetupNPCsByTime: được gọi từ Dev Cheat — thiết lập nhanh môi trường, bỏ qua phần intro buổi sáng.");

        if (currentTime == TimeOfDay.Morning_Day1)
        {
            // Tạm dừng chuyển pha tự động của GameTimeManager để cốt truyện kiểm soát hoàn toàn
            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.autoTransitionPaused = true;
            }

            // Kích hoạt các script mua bán mặc định của buổi sáng
            if (BoBiaMechanic.Instance != null)
            {
                BoBiaMechanic.Instance.enabled = true;
                BoBiaMechanic.Instance.ForceReset();
            }
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.StopSpawning(true);
                CustomerManager.Instance.enabled = true;
                SetupMorningNPCsList();
                CustomerManager.Instance.StartSpawning();
            }

            // Giữ cho script này bật để quản lý sự kiện chuyển pha
            this.enabled = true;

            // Cập nhật ánh sáng/thời gian buổi sáng (12h trưa để sáng hẳn)
            DayNightCycle dayNight = FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
            {
                dayNight.autoProgress = false;
                dayNight.timeOfDay = 12f; // 12h trưa
            }

            // Ẩn các NPC khác của buổi tối
            if (anhXamMinh != null) anhXamMinh.SetActive(false);
            if (anhBanhMi != null) anhBanhMi.SetActive(false);

            // Ẩn Huy_Seo và hột quẹt
            if (huySeo != null)
            {
                huySeo.SetActive(false);
            }
            if (spawnedBatLua != null)
            {
                spawnedBatLua.SetActive(false);
            }

            // Dọn dẹp hột quẹt và vật chứng thừa
            var clones = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var clone in clones)
            {
                if (clone != null && (clone.name.Contains("batlua(Clone)") || clone.name.Contains("syringe(Clone)") || clone.name.Contains("bichmaithuy(Clone)")))
                {
                    Destroy(clone);
                }
            }

            GameObject batLuaObj = GameObject.Find("batlua");
            if (batLuaObj != null) batLuaObj.SetActive(false);

            // Ẩn các vật chứng khác (như trashInteractZone)
            if (trashInteractZone != null)
            {
                trashInteractZone.SetActive(false);
            }

            // Kích hoạt rào cản hẻm buổi sáng — chặn người chơi đi vào hẻm
            SetMorningBarrierActive(true);

            // Tắt đèn đường và ẩn xe bánh mì vào buổi sáng
            SetStreetObjectsState(false);

            Log("SetupNPCsByTime: Đã kích hoạt cơ chế bán hàng buổi sáng, giữ cốt truyện Phase 1 bật.");
        }
        else if (currentTime == TimeOfDay.Evening_Phase1)
        {
            // Tạm dừng chuyển pha tự động của GameTimeManager để cốt truyện kiểm soát hoàn toàn
            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.autoTransitionPaused = true;
            }

            // Tắt hoàn toàn script mua bán/hội thoại cũ của buổi sáng
            if (BoBiaMechanic.Instance != null)
            {
                BoBiaMechanic.Instance.ForceReset();
                BoBiaMechanic.Instance.enabled = false;
            }
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.StopSpawning(true);
                CustomerManager.Instance.enabled = false;
            }

            // Cập nhật ánh sáng/thời gian buổi tối
            DayNightCycle dayNight = FindFirstObjectByType<DayNightCycle>();
            if (dayNight != null)
            {
                dayNight.autoProgress = false;
                dayNight.timeOfDay = 19f; // 7h tối
            }

            // Kích hoạt các NPC buổi tối
            if (anhXamMinh != null) anhXamMinh.SetActive(true);
            if (anhBanhMi != null) anhBanhMi.SetActive(true);

            // Bật script cốt truyện
            this.enabled = true;

            // Ẩn Huy_Seo lúc đầu
            if (huySeo != null)
            {
                huySeo.SetActive(false);
            }

            // Kích hoạt đống rác của buổi tối nhưng tắt collider tương tác ban đầu
            if (trashInteractZone != null)
            {
                trashInteractZone.SetActive(true);
                var col = trashInteractZone.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            // Buổi tối VẪN chặn hẻm lúc đầu — chỉ mở sau khi huy_seo phi tang xong bịch ma túy
            // (xem DisposeEvidenceSequence). Trước đó người chơi không được vào hẻm.
            SetMorningBarrierActive(true);

            // Bật đèn đường và hiện xe bánh mì vào buổi tối
            SetStreetObjectsState(true);

            Log("SetupNPCsByTime: Đã tắt cơ chế bán hàng buổi sáng, kích hoạt cốt truyện Phase 1 buổi tối.");
        }
    }

    private void SetupMorningNPCsList()
    {
        if (CustomerManager.Instance == null) return;

        System.Collections.Generic.List<GameObject> morningPrefabs = new System.Collections.Generic.List<GameObject>();
        if (coNgaPrefab != null) morningPrefabs.Add(coNgaPrefab);
        if (anhShipperPrefab != null) morningPrefabs.Add(anhShipperPrefab);
        if (coDongVienPrefab != null) morningPrefabs.Add(coDongVienPrefab);

        if (morningPrefabs.Count > 0)
        {
            CustomerManager.Instance.CustomerPrefabs = morningPrefabs.ToArray();
            Log($"SetupMorningNPCsList: Set {morningPrefabs.Count} morning prefabs: " + string.Join(", ", morningPrefabs.ConvertAll(p => p.name)));
        }
        else
        {
            Debug.LogWarning("[StoryPhase1] Morning prefabs list is empty! Please assign them in StoryPhase1Manager inspector.");
        }
    }

    private void HandleCustomerArrived(CustomerManager.Customer customer)
    {
        if (currentTime != TimeOfDay.Morning_Day1) return;
        if (customer == null || customer.npcInstance == null) return;

        NPCDialogue npcDialogue = customer.npcInstance.GetComponent<NPCDialogue>();
        if (npcDialogue == null)
        {
            npcDialogue = customer.npcInstance.AddComponent<NPCDialogue>();
        }

        string nameLower = customer.npcInstance.name.ToLower();
        if (nameLower.Contains("nganpc"))
        {
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Bán cho cô một cuốn bò bía ngọt nha cháu! Lâu lắm mới thấy cháu bán lại.",
                welcomeVoice = MainGameplayVoiceKey.NgaDay1Welcome,
                evidenceText = "Bò bía ngọt lịm ngon lắm cháu, chúc cháu đắt hàng nhé!",
                evidenceVoice = MainGameplayVoiceKey.NgaDay1Receive,
                evidencePoints = 0
            };
        }
        else if (nameLower.Contains("shipper"))
        {
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Cho một cuốn bò bía lẹ nha anh ơi, em đang vội giao đơn hàng tiếp theo!",
                welcomeVoice = MainGameplayVoiceKey.ShipperDay1Welcome,
                evidenceText = "Bò bía ngon quá anh, ăn cái là tỉnh táo làm việc tiếp liền. Cảm ơn anh!",
                evidenceVoice = MainGameplayVoiceKey.ShipperDay1Receive,
                evidencePoints = 0
            };
        }
        else if (nameLower.Contains("npc1"))
        {
            npcDialogue.DialogueData = new NPCDialogue.DialogueInfo
            {
                welcomeText = "Cho em một cuốn bò bía nhiều dừa nhiều mè nha anh ơi!",
                welcomeVoice = MainGameplayVoiceKey.FanDay1Welcome,
                evidenceText = "Bò bía cuốn chắc tay ngọt bùi ngon xuất sắc luôn anh!",
                evidenceVoice = MainGameplayVoiceKey.FanDay1Receive,
                evidencePoints = 0
            };
        }
    }

    private void HandleCustomerServed(CustomerManager.Customer customer)
    {
        if (currentTime != TimeOfDay.Morning_Day1) return;

        boBiaSold++;
        Log($"Served {customer.displayName}. Total boBiaSold: {boBiaSold}/3");

        if (boBiaSold >= 3 && !transitioning)
        {
            // Sự kiện phục vụ phát ra trước khi lời cảm ơn của cổ động viên kết thúc.
            // Chỉ chuyển sang tối từ OnDialogueEnded để không cắt ngang thoại.
            finalMorningCustomer = customer;
            waitingForFinalCustomerDialogue = true;
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.StopSpawning(false);
            }
        }
    }

    private void HandleCustomerDialogueEnded()
    {
        if (currentTime != TimeOfDay.Morning_Day1 || !waitingForFinalCustomerDialogue || transitioning) return;

        waitingForFinalCustomerDialogue = false;
        transitioning = true;
        StartCoroutine(MorningToEveningSequence(finalMorningCustomer.npcInstance));
    }

    private IEnumerator MorningToEveningSequence(GameObject customerNpc)
    {
        // 1. Lock player movement immediately so they cannot run away
        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        // OnDialogueEnded bảo đảm lời cảm ơn của cổ động viên đã phát xong.
        // Dừng một nhịp rõ ràng trước khi nhân vật tự nói, tránh bị cảm giác chuyển cảnh ngay.
        yield return new WaitForSeconds(1.5f);

        // Sau đó mới bắt đầu thoại nội tâm của người chơi. Giữ tối thiểu 8 giây
        // để người chơi có thời gian đọc xong trước khi màn hình fade sang buổi tối.
        string monoText = "Sáng nay chắc bán như vậy thôi, tối mình sẽ đi điều tra tiếp.";
        float monoDuration = 8f;
        float voiceDuration = MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerDay1MorningEnd);
        monoDuration = Mathf.Max(monoDuration, voiceDuration);
        ShowMonologue(monoText, monoDuration);
        Log("Cổ động viên đã mua xong → đang phát thoại nội tâm trước khi chuyển sang tối.");
        yield return new WaitForSeconds(monoDuration);

        // 4. Thoại nội tâm kết thúc → fade đen, khựng đúng 3 giây, rồi sang Buổi Tối.
        yield return StartCoroutine(TransitionToEveningRoutine(customerNpc));
    }

    private IEnumerator TransitionToEveningRoutine(GameObject departingCustomerNpc)
    {
        // Step 1: Fade out (duration: 1.5 seconds)
        yield return StartCoroutine(FadeTransition(1f, 1.5f));

        // The served customer has already been removed from CustomerManager's queue.
        // SetupNPCsByTime() clears that manager's coroutines, so its walk-away routine
        // would otherwise be cancelled and leave the NPC stranded in the evening scene.
        // Remove it only after the screen is fully black to keep the transition seamless.
        if (departingCustomerNpc != null)
        {
            Destroy(departingCustomerNpc);
        }

        // Step 2: Screen remains black for 3 seconds
        yield return new WaitForSeconds(3f);

        // Step 3: Change state to Evening_Phase1 and run evening setup
        currentTime = TimeOfDay.Evening_Phase1;
        SetupNPCsByTime();

        // Tối Ngày 1 luôn bắt đầu đúng đầu ngõ, không tái dùng vị trí quầy từ đầu game.
        WarpPlayerTo(eveningDay1PlayerSpawn);

        // Initialize evening state
        currentState = Phase1State.Intro;

        // Ensure Huy_Seo is hidden before evening script starts it
        if (huySeo != null) huySeo.SetActive(false);

        // Unlock player movement for evening gameplay
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        // Step 4: Show the evening monologue "Đói quá..." at the beginning of evening phase 1
        ShowMonologue("Đói quá... Hình như đầu ngõ có tiệm bánh mì cột đèn ngon lắm.", 6f, MainGameplayVoiceKey.PlayerNight1Intro);

        // Fade in back to normal (duration: 1.5 seconds)
        yield return StartCoroutine(FadeTransition(0f, 1.5f));
        
        Log("Transition to Evening Phase 1 completed successfully.");
    }

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

        GameObject fadeCanvasObj = new GameObject("TransitionFadeCanvas");
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
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        Image img = panel.AddComponent<Image>();
        img.color = Color.black;
        img.material = alwaysOnTop;
    }

    // ===================================================================
    // RÀO CẢN HẺM BUỔI SÁNG & TRẠNG THÁI ĐÈN ĐƯỜNG
    // ===================================================================

    /// <summary>
    /// Bật/tắt BỨC TƯỜNG TÀNG HÌNH (Invisible Wall) chặn LỐI RẼ TRÁI vào hẻm (rác + nhà) buổi sáng.
    /// Vị trí: X=-5.5, Y=3, Z=-2.75 — kích thước: 0.5×8×10 (chắn ngang trục đường về phía tây,
    /// giữ cho trục đường lớn bên phải — nơi khách tới mua hàng — luôn đi lại tự do).
    /// Barrier CHỈ có BoxCollider (tường vật lý chặn di chuyển), KHÔNG có MeshRenderer/MeshFilter
    /// nên người chơi nhìn xuyên qua thấy toàn bộ bối cảnh/bầu trời sau hẻm, nhưng không bước qua được.
    /// </summary>
    private void SetMorningBarrierActive(bool active)
    {
        if (active)
        {
            if (morningAlleyBarrier == null)
            {
                // Tìm barrier cũ trong scene (nếu có từ lần chạy trước)
                var allGOs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var go in allGOs)
                {
                    if (go != null && go.name == "MorningAlleyBarrier")
                    {
                        morningAlleyBarrier = go;
                        break;
                    }
                }
            }

            if (morningAlleyBarrier == null)
            {
                // Tạo mới bức tường TÀNG HÌNH: GameObject rỗng chỉ gắn BoxCollider làm tường vật lý.
                // Không dùng CreatePrimitive nữa để tránh sinh ra MeshRenderer/MeshFilter (khối đen).
                morningAlleyBarrier = new GameObject("MorningAlleyBarrier");
                morningAlleyBarrier.transform.position = new Vector3(-5.5f, 3.0f, -2.75f);
                morningAlleyBarrier.transform.localScale = new Vector3(0.5f, 8.0f, 10.0f);

                BoxCollider box = morningAlleyBarrier.AddComponent<BoxCollider>();
                box.isTrigger = false; // tường vật lý đặc, chặn người chơi di chuyển qua

                Log("Đã tạo MorningAlleyBarrier TÀNG HÌNH (chỉ BoxCollider) tại (-5.5, 3, -2.75).");
            }
            else
            {
                // Barrier cũ có thể còn dính MeshRenderer/MeshFilter (khối đen) — lột sạch để tàng hình hoàn toàn.
                StripBarrierVisuals(morningAlleyBarrier);
            }

            morningAlleyBarrier.SetActive(true);
        }
        else
        {
            if (morningAlleyBarrier != null)
            {
                morningAlleyBarrier.SetActive(false);
                Log("Đã ẩn MorningAlleyBarrier.");
            }
        }
    }

    /// <summary>
    /// Lột bỏ toàn bộ thành phần hiển thị (MeshRenderer + MeshFilter) khỏi barrier để nó tàng hình
    /// hoàn toàn, đồng thời đảm bảo vẫn còn một Collider đặc làm tường vật lý chặn di chuyển.
    /// </summary>
    private void StripBarrierVisuals(GameObject barrier)
    {
        if (barrier == null) return;

        var rend = barrier.GetComponent<MeshRenderer>();
        if (rend != null) Destroy(rend);

        var filter = barrier.GetComponent<MeshFilter>();
        if (filter != null) Destroy(filter);

        // Giữ tường vật lý: nếu vì lý do gì mà mất collider thì tạo lại BoxCollider đặc.
        var col = barrier.GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = barrier.AddComponent<BoxCollider>();
            box.isTrigger = false;
        }
        else
        {
            col.isTrigger = false;
        }
    }

    /// <summary>
    /// Bật/tắt các đối tượng đường phố theo thời điểm trong ngày:
    /// • xebanhmi_fbx — chỉ xuất hiện buổi tối
    /// • Đèn đường (StreetLight, Street_Light_OldTown_*) — chỉ sáng buổi tối
    /// • Bóng đèn phát quang (Lamp_2 > Object_8 emission) — chỉ phát sáng buổi tối
    /// </summary>
    private void SetStreetObjectsState(bool isEvening)
    {
        var allGOs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var go in allGOs)
        {
            if (go == null) continue;
            string goName = go.name;

            // --- Xe bánh mì: ẩn buổi sáng, hiện buổi tối ---
            if (goName == "xebanhmi_fbx")
            {
                go.SetActive(isEvening);
                Log($"xebanhmi_fbx.SetActive({isEvening})");
            }

            // --- Đèn đường: tắt Light component buổi sáng, bật buổi tối ---
            if (goName == "StreetLight" || goName.StartsWith("Street_Light_OldTown"))
            {
                Light lightComp = go.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.enabled = isEvening;
                }
            }

            // --- Bóng đèn phát quang: bật/tắt emission trên Material ---
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
                                    mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 4.0f);
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

        // --- Đèn chiếu sáng khu vực nhà Huy Sẹo (Point 3) vào buổi tối ---
        GameObject gateLightGo = GameObject.Find("HouseGate_DialogueLight");
        if (isEvening)
        {
            if (gateLightGo == null)
            {
                gateLightGo = new GameObject("HouseGate_DialogueLight");
                // Đặt vị trí gần point3_House (-5.36, 12.69), nâng cao lên 2.5m để tỏa sáng từ trên xuống
                Vector3 lightPos = point3_House != null ? point3_House.position : new Vector3(-5.36f, 0f, 12.69f);
                lightPos.y += 2.5f;
                // Nhích nhẹ ra ngoài một chút để chiếu sáng mặt nhân vật tốt hơn
                lightPos.z -= 1.2f; 
                gateLightGo.transform.position = lightPos;

                Light l = gateLightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 8.0f;
                l.intensity = 2.5f;
                l.color = new Color(1f, 0.95f, 0.8f); // Màu vàng ấm tự nhiên như đèn hiên nhà
                l.shadows = LightShadows.Soft;
            }
            else
            {
                Light l = gateLightGo.GetComponent<Light>();
                if (l != null) l.enabled = true;
            }
        }
        else
        {
            if (gateLightGo != null)
            {
                Light l = gateLightGo.GetComponent<Light>();
                if (l != null) l.enabled = false;
            }
        }

        Log($"SetStreetObjectsState: isEvening={isEvening} — đèn đường, xe bánh mì, bóng đèn và đèn cổng đã cập nhật.");
    }

    /// <summary>
    /// True khi Phase 1 đã hoàn thành (thu thập đủ vật chứng tại bãi rác).
    /// </summary>
    public bool IsPhase1Completed => currentState == Phase1State.Completed;

    /// <summary>
    /// Chuyển cảnh từ Phase 1 sang Phase 2 Morning (Ngày 2 Buổi Sáng).
    /// Được gọi bởi TrashInvestigation sau khi hoàn tất thu thập vật chứng.
    /// </summary>
    public void TransitionToPhase2Morning()
    {
        if (currentState != Phase1State.Completed)
        {
            Log("TransitionToPhase2Morning bị bỏ qua — Phase 1 chưa hoàn thành.");
            return;
        }
        StartCoroutine(TransitionToPhase2Routine());
    }

    private IEnumerator TransitionToPhase2Routine()
    {
        Log("Bắt đầu chuyển cảnh: Phase 1 → Phase 2 Morning.");

        // 1. Khóa di chuyển
        if (playerMovement == null && player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true;
        }

        // 2. Chờ monologue cuối Phase 1 hiển thị xong (khoảng vài giây)
        yield return new WaitForSeconds(2f);

        // 3. Fade out
        yield return StartCoroutine(FadeTransition(1f, 1.5f));

        // 4. Màn hình đen 3 giây
        yield return new WaitForSeconds(3f);

        // 5. Khởi động Phase 2 Morning
        // Đảm bảo StoryPhase2Manager tồn tại trong scene
        StoryPhase2Manager phase2 = FindFirstObjectByType<StoryPhase2Manager>();
        if (phase2 == null)
        {
            GameObject phase2Go = new GameObject("StoryPhase2Manager");
            phase2 = phase2Go.AddComponent<StoryPhase2Manager>();
            Log("Đã tạo mới StoryPhase2Manager trong scene.");
        }

        phase2.StartPhase2Morning();

        // 6. Tắt bản thân (Phase 1 không còn cần thiết)
        this.enabled = false;

        // 7. Mở khóa di chuyển
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        // 8. Fade in
        yield return StartCoroutine(FadeTransition(0f, 1.5f));

        Log("Chuyển cảnh sang Phase 2 Morning hoàn tất.");
    }

    private void Log(string message)
    {
        if (enableDebugLog) Debug.Log($"[StoryPhase1] {message}");
    }
}

/// <summary>
/// Vùng nhặt hột quẹt phát sáng được gắn runtime lên bản sao batlua. Khi Player chạm vào,
/// báo về <see cref="StoryPhase1Manager"/> rằng đã nhặt được vật phẩm ở Điểm 1.
/// </summary>
public class Phase1LighterPickup : MonoBehaviour
{
    private StoryPhase1Manager manager;
    private string playerTag = "Player";

    public void Init(StoryPhase1Manager owner, string tag)
    {
        manager = owner;
        if (!string.IsNullOrEmpty(tag)) playerTag = tag;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Đã tắt tự động nhặt bằng cách chạm vật lý, người chơi cần nhấn phím E
    }
}
