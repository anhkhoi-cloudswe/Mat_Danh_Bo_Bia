using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton Manager quản lý toàn bộ logic điều tra trong game Anh Bò Bía.
/// Chịu trách nhiệm:
///   - Lưu trữ danh sách bằng chứng đã thu thập.
///   - Tính toán và cập nhật Evidence Score.
///   - Quản lý chỉ số Độ Nghi Ngờ (Suspicion Level).
/// </summary>
public class CaseManager : MonoBehaviour
{
    // ===================================================================
    // SINGLETON
    // ===================================================================

    /// <summary>Instance duy nhất của CaseManager trong scene.</summary>
    public static CaseManager Instance { get; private set; }

    // ===================================================================
    // EVENTS - Các hệ thống khác (UI, Audio...) lắng nghe thay đổi
    // ===================================================================

    /// <summary>Phát sinh khi một bằng chứng mới được thêm vào. Tham số: EvidenceData vừa thêm.</summary>
    public event Action<EvidenceData> OnEvidenceCollected;

    /// <summary>Phát sinh khi thu thập thêm manh mối từ NPC (DAY).</summary>
    public event Action OnNpcClueCollected;

    /// <summary>Phát sinh khi Evidence Score thay đổi. Tham số: giá trị Score mới.</summary>
    public event Action<int> OnScoreChanged;

    /// <summary>Phát sinh khi Suspicion Level thay đổi. Tham số: giá trị Suspicion mới (0-100).</summary>
    public event Action<float> OnSuspicionChanged;

    /// <summary>Phát sinh khi Suspicion chạm ngưỡng tối đa (game over / alert).</summary>
    public event Action OnSuspicionMaxReached;

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("Cấu Hình Độ Nghi Ngờ (Suspicion)")]

    [Tooltip("Ngưỡng Suspicion tối đa. Khi đạt giá trị này sẽ kích hoạt OnSuspicionMaxReached.")]
    [Range(1f, 100f)]
    [SerializeField] private float maxSuspicion = 100f;

    [Tooltip("Tốc độ giảm Suspicion tự nhiên theo thời gian (đơn vị/giây). 0 = không giảm.")]
    [Range(0f, 10f)]
    [SerializeField] private float suspicionDecayRate = 1f;

    [Header("Debug")]
    [Tooltip("Bật log chi tiết ra Console khi debug.")]
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    /// <summary>Danh sách các bằng chứng đã thu thập (chỉ đọc từ ngoài qua property).</summary>
    private List<EvidenceData> collectedEvidences = new List<EvidenceData>();

    /// <summary>Số lượng manh mối thu thập từ NPC hội thoại (pha DAY).</summary>
    private int npcClueCount = 0;

    /// <summary>Tổng điểm bằng chứng hiện tại.</summary>
    private int evidenceScore = 0;

    /// <summary>Mức độ nghi ngờ hiện tại (0 → maxSuspicion).</summary>
    private float suspicionLevel = 0f;

    // ===================================================================
    // PROPERTIES
    // ===================================================================

    /// <summary>Danh sách bằng chứng đã thu thập (read-only).</summary>
    public IReadOnlyList<EvidenceData> CollectedEvidences => collectedEvidences.AsReadOnly();

    /// <summary>Số lượng manh mối thu thập từ NPC hội thoại (pha DAY).</summary>
    public int NpcClueCount => npcClueCount;

    /// <summary>Tổng số lượng bằng chứng/manh mối đã thu thập (cả DAY và NIGHT).</summary>
    public int TotalEvidenceCount => collectedEvidences.Count + npcClueCount;

    /// <summary>Tổng Evidence Score hiện tại.</summary>
    public int EvidenceScore => evidenceScore;

    /// <summary>Mức Suspicion hiện tại (0 → maxSuspicion).</summary>
    public float SuspicionLevel => suspicionLevel;

    /// <summary>Suspicion được chuẩn hóa về [0, 1] để dùng cho UI ProgressBar.</summary>
    public float SuspicionNormalized => suspicionLevel / maxSuspicion;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        // --- Thiết lập Singleton ---
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CaseManager] Phát hiện instance trùng lặp. Đang huỷ component bản sao...");
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

    private void Update()
    {
        // Giảm Suspicion tự nhiên theo thời gian nếu decay rate > 0
        if (suspicionDecayRate > 0f && suspicionLevel > 0f)
        {
            ApplySuspicionChange(-suspicionDecayRate * Time.deltaTime);
        }
    }

    // ===================================================================
    // PUBLIC METHODS - Evidence
    // ===================================================================

    /// <summary>
    /// Thêm một bằng chứng vào danh sách đã thu thập.
    /// Tự động cập nhật Evidence Score và Suspicion Level.
    /// </summary>
    /// <param name="evidence">EvidenceData cần thêm. Không được null.</param>
    /// <returns>True nếu thêm thành công, False nếu đã thu thập rồi.</returns>
    public bool CollectEvidence(EvidenceData evidence)
    {
        if (evidence == null)
        {
            Debug.LogError("[CaseManager] CollectEvidence: evidence không được null!");
            return false;
        }

        // Kiểm tra trùng lặp bằng EvidenceID
        if (HasEvidence(evidence.EvidenceID))
        {
            Log($"Bằng chứng '{evidence.EvidenceName}' đã được thu thập trước đó.");
            return false;
        }

        // Thêm vào danh sách
        collectedEvidences.Add(evidence);

        // Cộng điểm
        AddScore(evidence.ScoreValue);

        // Tăng suspicion
        ApplySuspicionChange(evidence.SuspicionIncrease);

        Log($"Đã thu thập bằng chứng: {evidence.EvidenceName} | +{evidence.ScoreValue} điểm | +{evidence.SuspicionIncrease} nghi ngờ");

        // Phát sự kiện để UI và các hệ thống khác phản hồi
        OnEvidenceCollected?.Invoke(evidence);
        return true;
    }

    /// <summary>
    /// Kiểm tra xem bằng chứng theo ID đã được thu thập chưa.
    /// </summary>
    /// <param name="evidenceID">ID cần kiểm tra.</param>
    /// <returns>True nếu đã có trong danh sách.</returns>
    public bool HasEvidence(string evidenceID)
    {
        return collectedEvidences.Exists(e => e.EvidenceID == evidenceID);
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu về bằng chứng và reset về trạng thái ban đầu.
    /// Dùng khi bắt đầu màn chơi mới.
    /// </summary>
    public void ResetCase()
    {
        collectedEvidences.Clear();
        npcClueCount = 0;
        SetScore(0);
        SetSuspicion(0f);
        Log("Đã reset toàn bộ dữ liệu Case.");
    }

    /// <summary>
    /// Ghi nhận đã thu thập 1 manh mối từ NPC hội thoại.
    /// </summary>
    public void AddNpcClue()
    {
        npcClueCount++;
        OnNpcClueCollected?.Invoke();
    }

    // ===================================================================
    // PUBLIC METHODS - Suspicion Logic
    // ===================================================================

    /// <summary>
    /// Đặt lại tốc độ giảm Suspicion tự nhiên theo thời gian.
    /// Được gọi bởi GameTimeManager khi chuyển pha DAY / NIGHT.
    /// </summary>
    /// <param name="rate">Giá trị decay mới (đơn vị/giây). 0 = tắt decay.</param>
    public void SetDecayRate(float rate)
    {
        suspicionDecayRate = Mathf.Max(0f, rate);
        Log($"Suspicion decay rate đã đặt thành: {suspicionDecayRate}/s");
    }

    /// <summary>
    /// Thay đổi mức Suspicion theo một giá trị delta (có thể âm để giảm).
    /// </summary>
    /// <param name="delta">Giá trị thay đổi (+tăng / -giảm).</param>
    public void ApplySuspicionChange(float delta)
    {
        float newSuspicion = Mathf.Clamp(suspicionLevel + delta, 0f, maxSuspicion);

        if (Mathf.Approximately(newSuspicion, suspicionLevel)) return; // Không thay đổi thì bỏ qua

        suspicionLevel = newSuspicion;
        OnSuspicionChanged?.Invoke(suspicionLevel);

        // Kiểm tra ngưỡng tối đa
        if (suspicionLevel >= maxSuspicion)
        {
            Log("⚠️ Suspicion đã đạt mức tối đa! Kích hoạt cảnh báo.");
            OnSuspicionMaxReached?.Invoke();
        }
    }

    // ===================================================================
    // PRIVATE HELPERS
    // ===================================================================

    /// <summary>Cộng thêm điểm vào Evidence Score và phát sự kiện.</summary>
    public void AddScore(int amount)
    {
        SetScore(evidenceScore + amount);
    }

    /// <summary>Đặt Evidence Score về giá trị mới và phát sự kiện.</summary>
    private void SetScore(int newScore)
    {
        evidenceScore = Mathf.Max(0, newScore);
        OnScoreChanged?.Invoke(evidenceScore);
    }

    /// <summary>Đặt Suspicion về giá trị mới và phát sự kiện.</summary>
    private void SetSuspicion(float value)
    {
        suspicionLevel = Mathf.Clamp(value, 0f, maxSuspicion);
        OnSuspicionChanged?.Invoke(suspicionLevel);
    }

    /// <summary>Ghi log có điều kiện (chỉ khi enableDebugLog = true).</summary>
    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[CaseManager] {message}");
    }
}
