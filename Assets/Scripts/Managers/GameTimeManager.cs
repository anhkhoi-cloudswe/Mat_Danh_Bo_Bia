using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý vòng lặp thời gian DAY / NIGHT trong game Anh Bò Bía.
///
/// DAY   → Bán tại 22 Yên Phụ. CustomerManager kích hoạt spawn khách.
///          CartController LUÔN enabled — người chơi tự do di chuyển.
/// NIGHT → Giao hàng / Trinh sát đến Villa. CustomerManager dừng spawn.
///          Danh sách DeliveryMission được kích hoạt.
///
/// Tích hợp với:
///   - CustomerManager : start/stop spawn khách theo pha.
///   - CaseManager     : điều chỉnh Suspicion decay rate theo pha.
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    // ===================================================================
    // ENUM
    // ===================================================================

    /// <summary>Hai pha thời gian chính trong ngày.</summary>
    public enum GamePhase
    {
        DAY,    // Ban ngày — bán hàng cố định tại 22 Yên Phụ
        NIGHT   // Ban đêm — giao hàng & trinh sát đến Villa
    }

    // ===================================================================
    // INNER TYPE — Nhiệm vụ giao hàng
    // ===================================================================

    /// <summary>
    /// Đại diện cho một nhiệm vụ giao hàng đến Villa trong pha NIGHT.
    /// </summary>
    [Serializable]
    public class DeliveryMission
    {
        [Tooltip("Tên nhiệm vụ hiển thị trên UI.")]
        public string missionName = "Giao hàng";

        [Tooltip("Transform đích đến (vị trí Villa cần giao).")]
        public Transform destination;

        [Tooltip("Phần thưởng điểm khi hoàn thành nhiệm vụ này.")]
        public int rewardScore = 20;

        [Tooltip("Mức tăng Suspicion khi hoàn thành (trinh sát bị phát hiện).")]
        [Range(0f, 30f)]
        public float suspicionOnComplete = 10f;

        /// <summary>Nhiệm vụ đã hoàn thành chưa.</summary>
        [HideInInspector] public bool isCompleted = false;
    }

    // ===================================================================
    // SINGLETON
    // ===================================================================

    /// <summary>Instance duy nhất của GameTimeManager.</summary>
    public static GameTimeManager Instance { get; private set; }

    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>
    /// Phát sinh mỗi khi pha thay đổi.
    /// Tham số 1 (previous): Pha trước đó.
    /// Tham số 2 (current):  Pha hiện tại mới.
    /// </summary>
    public event Action<GamePhase, GamePhase> OnPhaseChanged;

    /// <summary>Phát sinh khi bắt đầu pha DAY. Dùng cho UI / Animation ban ngày.</summary>
    public event Action OnDayStarted;

    /// <summary>Phát sinh khi bắt đầu pha NIGHT. Dùng cho UI / Animation ban đêm.</summary>
    public event Action OnNightStarted;

    /// <summary>
    /// Phát sinh khi một DeliveryMission hoàn thành.
    /// Tham số: mission vừa hoàn thành.
    /// </summary>
    public event Action<DeliveryMission> OnMissionCompleted;

    /// <summary>Phát sinh khi tất cả nhiệm vụ đêm hoàn thành.</summary>
    public event Action OnAllMissionsCompleted;

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("Pha Khởi Đầu")]
    [Tooltip("Pha thời gian khi game bắt đầu.")]
    [SerializeField] private GamePhase startingPhase = GamePhase.DAY;

    [Header("Thời Lượng Mỗi Pha (giây thực)")]
    [Tooltip("Thời gian pha DAY kéo dài. 0 = không tự động chuyển sang NIGHT.")]
    [SerializeField] private float dayDuration = 120f;

    [Tooltip("Thời gian pha NIGHT kéo dài. 0 = không tự động chuyển sang DAY.")]
    [SerializeField] private float nightDuration = 180f;

    [Header("Tham Chiếu Scene")]
    [Tooltip("CustomerManager quản lý hàng đợi khách hàng ban ngày.")]
    [SerializeField] private CustomerManager customerManager;

    [Header("Nhiệm Vụ Giao Hàng (NIGHT)")]
    [Tooltip("Danh sách tất cả nhiệm vụ giao hàng sẽ kích hoạt khi vào pha NIGHT.")]
    [SerializeField] private List<DeliveryMission> deliveryMissions = new List<DeliveryMission>();

    [Header("Cấu Hình Suspicion Theo Pha")]
    [Tooltip("Tốc độ giảm Suspicion trong pha DAY (override CaseManager.suspicionDecayRate).")]
    [Range(0f, 10f)]
    [SerializeField] private float dayDecayRate = 2f;

    [Tooltip("Tốc độ giảm Suspicion trong pha NIGHT (chậm hơn vì nguy hiểm hơn).")]
    [Range(0f, 10f)]
    [SerializeField] private float nightDecayRate = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    /// <summary>Pha hiện tại.</summary>
    private GamePhase currentPhase;

    /// <summary>Thời gian đã trôi qua trong pha hiện tại (giây).</summary>
    private float phaseElapsed = 0f;

    /// <summary>Coroutine chuyển pha tự động đang chạy.</summary>
    private Coroutine autoTransitionCoroutine;

    /// <summary>Số nhiệm vụ đêm đã hoàn thành.</summary>
    private int completedMissionCount = 0;

    // ===================================================================
    // PROPERTIES
    // ===================================================================

    /// <summary>Tạm dừng chuyển pha tự động khi đang trong cốt truyện.</summary>
    public bool autoTransitionPaused = false;

    /// <summary>Pha thời gian hiện tại.</summary>
    public GamePhase CurrentPhase => currentPhase;

    /// <summary>Thời gian đã trôi qua trong pha hiện tại (giây).</summary>
    public float PhaseElapsed => phaseElapsed;

    /// <summary>Thời gian còn lại trong pha hiện tại (giây). -1 nếu pha không có giới hạn.</summary>
    public float PhaseTimeRemaining
    {
        get
        {
            float duration = currentPhase == GamePhase.DAY ? dayDuration : nightDuration;
            return duration > 0f ? Mathf.Max(0f, duration - phaseElapsed) : -1f;
        }
    }

    /// <summary>Tỉ lệ thời gian pha đã qua [0, 1] dùng cho UI ProgressBar.</summary>
    public float PhaseProgress
    {
        get
        {
            float duration = currentPhase == GamePhase.DAY ? dayDuration : nightDuration;
            return duration > 0f ? Mathf.Clamp01(phaseElapsed / duration) : 0f;
        }
    }

    /// <summary>Danh sách nhiệm vụ giao hàng (read-only).</summary>
    public IReadOnlyList<DeliveryMission> DeliveryMissions => deliveryMissions.AsReadOnly();

    /// <summary>Số nhiệm vụ đêm đã hoàn thành.</summary>
    public int CompletedMissionCount => completedMissionCount;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        // --- Singleton ---
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameTimeManager] Phát hiện instance trùng lặp. Đang huỷ component bản sao...");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Khởi tạo pha đầu tiên (không phát event chuyển pha, chỉ setup trạng thái)
        currentPhase = startingPhase;
        ApplyPhaseEffects(currentPhase);
        StartAutoTransition(currentPhase);

        Log($"GameTimeManager khởi động. Pha ban đầu: {currentPhase}");
    }

    private void Update()
    {
        if (autoTransitionPaused) return;

        // Đếm thời gian đã trải qua trong pha hiện tại
        phaseElapsed += Time.deltaTime;
    }

    // ===================================================================
    // PUBLIC METHODS — Chuyển Pha
    // ===================================================================

    /// <summary>
    /// Chuyển sang pha DAY.
    /// Khóa CartController và áp dụng decay rate ban ngày cho CaseManager.
    /// </summary>
    public void TransitionToDay()
    {
        if (currentPhase == GamePhase.DAY)
        {
            Log("Đã ở pha DAY. Bỏ qua yêu cầu chuyển.");
            return;
        }

        ChangePhase(GamePhase.DAY);
    }

    /// <summary>
    /// Chuyển sang pha NIGHT.
    /// Mở khóa CartController và kích hoạt danh sách nhiệm vụ giao hàng.
    /// </summary>
    public void TransitionToNight()
    {
        if (currentPhase == GamePhase.NIGHT)
        {
            Log("Đã ở pha NIGHT. Bỏ qua yêu cầu chuyển.");
            return;
        }

        ChangePhase(GamePhase.NIGHT);
    }

    /// <summary>
    /// Đánh dấu một nhiệm vụ giao hàng là hoàn thành theo tên.
    /// Cập nhật CaseManager (score + suspicion) và phát sự kiện.
    /// </summary>
    /// <param name="missionName">Tên nhiệm vụ cần đánh dấu hoàn thành.</param>
    /// <returns>True nếu tìm thấy và hoàn thành thành công.</returns>
    public bool CompleteMission(string missionName)
    {
        if (currentPhase != GamePhase.NIGHT)
        {
            Log("Chỉ có thể hoàn thành nhiệm vụ trong pha NIGHT!");
            return false;
        }

        DeliveryMission mission = deliveryMissions.Find(m => m.missionName == missionName);

        if (mission == null)
        {
            Debug.LogWarning($"[GameTimeManager] Không tìm thấy nhiệm vụ tên '{missionName}'.");
            return false;
        }

        if (mission.isCompleted)
        {
            Log($"Nhiệm vụ '{missionName}' đã hoàn thành trước đó.");
            return false;
        }

        // Đánh dấu hoàn thành
        mission.isCompleted = true;
        completedMissionCount++;

        // Cập nhật CaseManager
        if (CaseManager.Instance != null)
        {
            // Tăng điểm thưởng vào EvidenceScore thông qua một cơ chế thêm thẳng
            // (Gọi internal method hoặc dùng event tuỳ kiến trúc — ở đây dùng ApplySuspicionChange)
            CaseManager.Instance.ApplySuspicionChange(mission.suspicionOnComplete);
            Log($"Nhiệm vụ '{missionName}' hoàn thành! +{mission.rewardScore} điểm | +{mission.suspicionOnComplete} Suspicion.");
        }

        OnMissionCompleted?.Invoke(mission);

        // Kiểm tra hoàn thành tất cả
        if (completedMissionCount >= deliveryMissions.Count)
        {
            Log("✅ Tất cả nhiệm vụ đêm đã hoàn thành!");
            OnAllMissionsCompleted?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Reset toàn bộ nhiệm vụ đêm về trạng thái chưa hoàn thành.
    /// Thường gọi khi bắt đầu chu kỳ NIGHT mới.
    /// </summary>
    public void ResetMissions()
    {
        foreach (var mission in deliveryMissions)
            mission.isCompleted = false;

        completedMissionCount = 0;
        Log("Đã reset tất cả nhiệm vụ giao hàng.");
    }

    // ===================================================================
    // PRIVATE — Lõi chuyển pha
    // ===================================================================

    /// <summary>
    /// Thực hiện chuyển pha: lưu pha cũ, áp dụng hiệu ứng pha mới,
    /// khởi động timer tự động và phát sự kiện OnPhaseChanged.
    /// </summary>
    private void ChangePhase(GamePhase newPhase)
    {
        GamePhase previousPhase = currentPhase;
        currentPhase = newPhase;
        phaseElapsed = 0f;

        Log($"⏰ Chuyển pha: {previousPhase} → {newPhase}");

        // Áp dụng hiệu ứng của pha mới
        ApplyPhaseEffects(newPhase);

        // Khởi động lại timer tự động chuyển pha
        if (autoTransitionCoroutine != null)
            StopCoroutine(autoTransitionCoroutine);

        StartAutoTransition(newPhase);

        // Phát sự kiện chung
        OnPhaseChanged?.Invoke(previousPhase, newPhase);

        // Phát sự kiện cụ thể từng pha
        if (newPhase == GamePhase.DAY)
            OnDayStarted?.Invoke();
        else
            OnNightStarted?.Invoke();
    }

    /// <summary>
    /// Áp dụng tất cả hiệu ứng phụ khi vào một pha:
    ///   - Lock / Unlock CartController.
    ///   - Cập nhật Suspicion decay rate trong CaseManager.
    ///   - Reset / Kích hoạt nhiệm vụ giao hàng.
    /// </summary>
    private void ApplyPhaseEffects(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.DAY:
                ApplyDayEffects();
                break;

            case GamePhase.NIGHT:
                ApplyNightEffects();
                break;
        }
    }

    // ===================================================================
    // PRIVATE — Hiệu ứng DAY / NIGHT
    // ===================================================================

    /// <summary>
    /// Hiệu ứng DAY:
    ///   - Kích hoạt CustomerManager spawn khách hàng.
    ///   - Tăng tốc độ giảm Suspicion (ban ngày an toàn hơn).
    ///   - CartController KHÔNG bị khóa — người chơi tự do di chuyển.
    /// </summary>
    private void ApplyDayEffects()
    {
        // Bắt đầu spawn khách tại quán 22 Yên Phụ
        var manager = customerManager != null ? customerManager : CustomerManager.Instance;
        if (manager != null)
            manager.StartSpawning();
        else
            Log("CustomerManager (and Instance) is null.");

        // Suspicion giảm nhanh hơn ban ngày
        SetSuspicionDecayRate(dayDecayRate);

        Log($"[DAY] CustomerManager ACTIVE. Decay rate: {dayDecayRate}/s.");
    }

    /// <summary>
    /// Hiệu ứng NIGHT:
    ///   - Dừng CustomerManager (không spawn thêm khách ban đêm).
    ///   - Giảm tốc độ decay Suspicion (ban đêm nguy hiểm hơn).
    ///   - Reset và kích hoạt danh sách nhiệm vụ giao hàng.
    ///   - CartController KHÔNG bị khóa — người chơi tự do di chuyển.
    /// </summary>
    private void ApplyNightEffects()
    {
        // Dung spawn them khach, giu khach dang cho trong scene.
        var manager = customerManager != null ? customerManager : CustomerManager.Instance;
        if (manager != null)
            manager.StopSpawning(clearQueue: false);

        // Suspicion giảm chậm hơn ban đêm
        SetSuspicionDecayRate(nightDecayRate);

        // Reset nhiệm vụ cho chu kỳ đêm mới
        ResetMissions();

        Log($"[NIGHT] CustomerManager INACTIVE. Decay rate: {nightDecayRate}/s. Nhiệm vụ: {deliveryMissions.Count}.");
    }

    // ===================================================================
    // PRIVATE — CustomerManager Integration
    // ===================================================================
    // CartController KHÔNG còn bị khóa theo pha.
    // Người chơi được tự do di chuyển cả DAY lẫn NIGHT.
    // CustomerManager là hệ thống duy nhất bị start/stop theo pha.

    // ===================================================================
    // PRIVATE — CaseManager Integration
    // ===================================================================

    /// <summary>
    /// Ghi đè tốc độ giảm Suspicion trong CaseManager theo pha hiện tại.
    /// Sử dụng Reflection-free approach: CaseManager cần expose SetDecayRate().
    /// Nếu chưa có method đó, đăng ký vào Update của manager này để override.
    /// </summary>
    private void SetSuspicionDecayRate(float rate)
    {
        if (CaseManager.Instance != null)
        {
            CaseManager.Instance.SetDecayRate(rate);
        }
        else
        {
            Log("CaseManager.Instance là null. Không thể đặt decay rate.");
        }
    }

    // ===================================================================
    // PRIVATE — Auto Transition Timer
    // ===================================================================

    /// <summary>
    /// Bắt đầu Coroutine tự động chuyển pha sau thời gian cấu hình.
    /// Nếu duration = 0 → không tự chuyển, đợi gọi thủ công.
    /// </summary>
    private void StartAutoTransition(GamePhase phase)
    {
        float duration = phase == GamePhase.DAY ? dayDuration : nightDuration;

        if (duration <= 0f)
        {
            Log($"Auto-transition tắt cho pha {phase} (duration = 0).");
            return;
        }

        autoTransitionCoroutine = StartCoroutine(AutoTransitionCoroutine(phase, duration));
    }

    /// <summary>
    /// Coroutine đếm ngược thời gian rồi chuyển sang pha tiếp theo.
    /// DAY  → NIGHT
    /// NIGHT → DAY
    /// </summary>
    private IEnumerator AutoTransitionCoroutine(GamePhase phase, float duration)
    {
        Log($"Auto-transition: Sẽ chuyển sang {NextPhase(phase)} sau {duration}s...");
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!autoTransitionPaused)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }

        Log($"Hết thời gian pha {phase}. Tự động chuyển sang {NextPhase(phase)}.");
        ChangePhase(NextPhase(phase));
    }

    /// <summary>Trả về pha kế tiếp (DAY ↔ NIGHT).</summary>
    private static GamePhase NextPhase(GamePhase phase)
    {
        return phase == GamePhase.DAY ? GamePhase.NIGHT : GamePhase.DAY;
    }

    // ===================================================================
    // PRIVATE — Logging
    // ===================================================================

    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[GameTimeManager] {message}");
    }

    // ===================================================================
    // GIZMOS — Hiển thị điểm đến nhiệm vụ trong Editor
    // ===================================================================

    private void OnDrawGizmos()
    {
        if (deliveryMissions == null) return;

        foreach (var mission in deliveryMissions)
        {
            if (mission.destination == null) continue;

            // Màu vàng = chưa hoàn thành / xám = đã hoàn thành
            Gizmos.color = mission.isCompleted
                ? new Color(0.5f, 0.5f, 0.5f, 0.5f)
                : new Color(1f, 0.85f, 0f, 0.8f);

            Gizmos.DrawSphere(mission.destination.position, 0.5f);
            Gizmos.DrawLine(transform.position, mission.destination.position);
        }
    }
}
