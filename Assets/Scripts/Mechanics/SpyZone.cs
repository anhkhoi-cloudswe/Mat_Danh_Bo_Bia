using System.Collections;
using UnityEngine;

/// <summary>
/// Vùng thu thập bằng chứng bí mật (Spy Zone).
/// Khi người chơi bước vào vùng (Trigger Collider), bắt đầu đếm ngược thời gian.
/// Nếu người chơi ở lại đủ lâu, bằng chứng sẽ được thu thập tự động.
///
/// Yêu cầu setup:
///   - GameObject cần có Collider với "Is Trigger" = true.
///   - Người chơi cần có tag "Player".
///   - Kéo EvidenceData asset vào trường evidenceToCollect trên Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpyZone : MonoBehaviour
{
    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Phát sinh khi người chơi bước vào vùng. Tham số: thời gian đếm ngược cần thiết.</summary>
    public System.Action<float> OnPlayerEntered;

    /// <summary>Phát sinh mỗi frame trong khi đếm ngược. Tham số: thời gian còn lại.</summary>
    public System.Action<float> OnCountdownTick;

    /// <summary>Phát sinh khi người chơi rời đi trước khi hoàn tất.</summary>
    public System.Action OnPlayerExited;

    /// <summary>Phát sinh khi bằng chứng thu thập thành công.</summary>
    public System.Action<EvidenceData> OnEvidenceSuccessfullyCollected;

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("Bằng Chứng Cần Thu Thập")]
    [Tooltip("ScriptableObject chứa thông tin bằng chứng sẽ thu thập khi người chơi ở đủ lâu.")]
    [SerializeField] private EvidenceData evidenceToCollect;

    [Header("Thời Gian & Điều Kiện")]
    [Tooltip("Thời gian (giây) người chơi phải đứng trong vùng để thu thập bằng chứng.")]
    [Range(0.5f, 30f)]
    [SerializeField] private float requiredStayDuration = 3f;

    [Tooltip("Tag của đối tượng người chơi để nhận diện khi enter trigger.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Nếu true, người chơi chỉ có thể thu thập bằng chứng này 1 lần duy nhất.")]
    [SerializeField] private bool collectOnce = true;

    [Header("Hiệu Ứng Thị Giác")]
    [Tooltip("Hiệu ứng particle khi người chơi đang đứng trong vùng (có thể để null).")]
    [SerializeField] private ParticleSystem scanningEffect;

    [Tooltip("Hiệu ứng particle khi thu thập thành công (có thể để null).")]
    [SerializeField] private ParticleSystem successEffect;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    /// <summary>Đang đếm ngược thời gian không.</summary>
    private bool isCountingDown = false;

    /// <summary>Bằng chứng này đã được thu thập chưa.</summary>
    private bool hasBeenCollected = false;

    /// <summary>Coroutine đếm ngược hiện tại.</summary>
    private Coroutine countdownCoroutine;

    // ===================================================================
    // PROPERTIES
    // ===================================================================

    /// <summary>Thời gian yêu cầu đứng trong vùng để thu thập.</summary>
    public float RequiredStayDuration => requiredStayDuration;

    /// <summary>Bằng chứng này đã được thu thập chưa.</summary>
    public bool HasBeenCollected => hasBeenCollected;

    /// <summary>Đang đếm ngược.</summary>
    public bool IsCountingDown => isCountingDown;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        // Cảnh báo nếu chưa gán EvidenceData
        if (evidenceToCollect == null)
            Debug.LogWarning($"[SpyZone] '{gameObject.name}': Chưa gán EvidenceData! Vùng này sẽ không thu thập được bằng chứng.");
    }

    // ===================================================================
    // TRIGGER EVENTS - Nhận diện người chơi
    // ===================================================================

    /// <summary>
    /// Được gọi khi một Collider khác bước vào Trigger của SpyZone.
    /// Kiểm tra tag và bắt đầu đếm ngược nếu là người chơi.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidPlayer(other)) return;

        Log($"Người chơi bước vào SpyZone '{gameObject.name}'.");

        // Không cho phép thu thập nếu đã lấy rồi và collectOnce = true
        if (collectOnce && hasBeenCollected)
        {
            Log("Bằng chứng này đã được thu thập. Bỏ qua.");
            return;
        }

        // Bắt đầu đếm ngược
        StartCountdown();
        OnPlayerEntered?.Invoke(requiredStayDuration);
    }

    /// <summary>
    /// Được gọi khi một Collider khác rời khỏi Trigger của SpyZone.
    /// Hủy đếm ngược nếu người chơi rời đi sớm.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsValidPlayer(other)) return;

        Log($"Người chơi rời SpyZone '{gameObject.name}'. Hủy đếm ngược.");

        StopCountdown();
        OnPlayerExited?.Invoke();

        // Dừng hiệu ứng scanning
        if (scanningEffect != null && scanningEffect.isPlaying)
            scanningEffect.Stop();
    }

    // ===================================================================
    // COUNTDOWN LOGIC
    // ===================================================================

    /// <summary>Bắt đầu coroutine đếm ngược thời gian thu thập.</summary>
    private void StartCountdown()
    {
        if (isCountingDown) return; // Tránh chạy nhiều coroutine cùng lúc

        countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    /// <summary>Dừng coroutine đếm ngược đang chạy.</summary>
    private void StopCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        isCountingDown = false;
    }

    /// <summary>
    /// Coroutine đếm ngược từ requiredStayDuration về 0.
    /// Nếu hoàn thành → thu thập bằng chứng.
    /// Mỗi frame phát sự kiện OnCountdownTick để UI cập nhật thanh tiến trình.
    /// </summary>
    private IEnumerator CountdownCoroutine()
    {
        isCountingDown = true;
        float elapsed = 0f;

        // Bật hiệu ứng scanning
        if (scanningEffect != null)
            scanningEffect.Play();

        Log($"Bắt đầu đếm ngược: {requiredStayDuration}s...");

        while (elapsed < requiredStayDuration)
        {
            elapsed += Time.deltaTime;
            float remaining = requiredStayDuration - elapsed;

            // Phát tick để UI thanh progress cập nhật
            OnCountdownTick?.Invoke(remaining);

            yield return null; // Chờ frame tiếp theo
        }

        // Đếm ngược hoàn tất → thu thập bằng chứng
        isCountingDown = false;
        CollectEvidence();
    }

    // ===================================================================
    // EVIDENCE COLLECTION
    // ===================================================================

    /// <summary>
    /// Thực hiện thu thập bằng chứng:
    /// Gọi CaseManager.CollectEvidence(), phát hiệu ứng thành công,
    /// và vô hiệu hóa zone nếu collectOnce = true.
    /// </summary>
    private void CollectEvidence()
    {
        if (evidenceToCollect == null)
        {
            Debug.LogError($"[SpyZone] '{gameObject.name}': Không thể thu thập - EvidenceData chưa được gán!");
            return;
        }

        // Dừng scanning effect
        if (scanningEffect != null && scanningEffect.isPlaying)
            scanningEffect.Stop();

        // Bật success effect
        if (successEffect != null)
            successEffect.Play();

        // Gửi đến CaseManager để xử lý
        bool success = false;
        if (CaseManager.Instance != null)
        {
            success = CaseManager.Instance.CollectEvidence(evidenceToCollect);
        }
        else
        {
            Debug.LogError("[SpyZone] CaseManager.Instance là null! Đảm bảo có CaseManager trong scene.");
        }

        if (success)
        {
            hasBeenCollected = true;
            Log($"✅ Đã thu thập bằng chứng: {evidenceToCollect.EvidenceName}");
            OnEvidenceSuccessfullyCollected?.Invoke(evidenceToCollect);

            // Vô hiệu hóa collider nếu chỉ cho lấy 1 lần
            if (collectOnce)
            {
                GetComponent<Collider>().enabled = false;
                Log("SpyZone đã bị vô hiệu hóa (collectOnce = true).");
            }
        }
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    /// <summary>Kiểm tra xem Collider có phải là người chơi hợp lệ không.</summary>
    private bool IsValidPlayer(Collider other)
    {
        return other.CompareTag(playerTag);
    }

    /// <summary>Ghi log có điều kiện.</summary>
    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[SpyZone] {message}");
    }

    // ===================================================================
    // GIZMOS - Hiển thị vùng trong Editor
    // ===================================================================

    private void OnDrawGizmos()
    {
        Gizmos.color = hasBeenCollected
            ? new Color(0.5f, 0.5f, 0.5f, 0.3f)   // Xám nếu đã thu thập
            : new Color(0f, 1f, 0.5f, 0.3f);        // Xanh lá nếu chưa thu thập

        Gizmos.DrawCube(transform.position, transform.localScale);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f); // Viền vàng khi selected
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
