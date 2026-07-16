using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vùng MẬT PHỤC (Hiding Zone) — "vòng tròn ánh sáng xanh" ở góc khuất đối diện cổng Villa.
///
/// Luồng kịch bản (PHẦN 1 — Mật Phục & Báo Cáo):
///   1. Người chơi đi tới vòng sáng xanh này.
///   2. Bước vào vùng → khóa di chuyển + monologue quan sát.
///   3. [CUTSCENE Mê Liu/Huy Sẹo — THÊM SAU] phát qua event onSurveillanceStarted.
///   4. Mật phục xong → mở quyền đột nhập Villa (OnSurveillanceCompletedAction).
///
/// CÁCH GẮN CUTSCENE SAU NÀY (không cần sửa file này):
///   - Đặt sẵn 1 GameObject có HidingZone trong scene, kéo Timeline/animation vào
///     event "onSurveillanceStarted" trên Inspector.
///   - Bật "waitForExternalCutscene" = true, rồi khi cutscene chiếu xong gọi
///     HidingZone.Instance.CompleteSurveillance() để tiếp tục cho phép vào Villa.
///   - Nếu để waitForExternalCutscene = false: sau "surveillanceDuration" giây sẽ tự tiếp tục.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HidingZone : MonoBehaviour
{
    // ===================================================================
    // SINGLETON (tiện cho cutscene gọi CompleteSurveillance())
    // ===================================================================
    public static HidingZone Instance { get; private set; }

    // ===================================================================
    // INSPECTOR
    // ===================================================================

    [Header("=== Cấu hình ===")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Nếu TRUE: KHÔNG tự tiếp tục theo timer — chờ cutscene gọi CompleteSurveillance().")]
    [SerializeField] private bool waitForExternalCutscene = false;

    [Tooltip("Thời gian (giây) mật phục trước khi cho phép đột nhập (khi KHÔNG dùng cutscene ngoài).")]
    [SerializeField] private float surveillanceDuration = 3f;

    [Tooltip("Có khóa di chuyển người chơi trong lúc mật phục không.")]
    [SerializeField] private bool lockPlayerMovement = true;

    [Header("=== Sự kiện (Hook cho Cutscene — gắn sau) ===")]
    [Tooltip("Phát khi người chơi bước vào vùng mật phục. GẮN CUTSCENE Mê Liu/Huy Sẹo vào đây.")]
    public UnityEvent onSurveillanceStarted;

    [Tooltip("Phát khi mật phục hoàn tất, ngay trước khi mở quyền đột nhập Villa.")]
    public UnityEvent onSurveillanceCompleted;

    // ===================================================================
    // CALLBACK CODE (StoryPhase2Manager nối vào để bắt đầu điều tra Villa)
    // ===================================================================
    public System.Action OnSurveillanceStartedAction;
    public System.Action OnSurveillanceCompletedAction;

    // ===================================================================
    // RUNTIME STATE
    // ===================================================================
    private bool triggered = false;
    private bool completed = false;
    private PlayerMovement playerMovement;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        Instance = this;

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(playerTag)) return;

        triggered = true;
        Debug.Log("[HidingZone] Người chơi vào vùng mật phục.");
        StartCoroutine(SurveillanceRoutine(other));
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    /// <summary>Cấu hình từ code khi tạo procedural (StoryPhase2Manager dùng).</summary>
    public void Configure(string tag, float duration, bool waitExternal, bool lockMovement = true)
    {
        playerTag = tag;
        surveillanceDuration = duration;
        waitForExternalCutscene = waitExternal;
        lockPlayerMovement = lockMovement;
    }

    /// <summary>
    /// Gọi từ cutscene khi đã chiếu xong (Mê Liu vào trong, khóa xích cổng...).
    /// Mở khóa di chuyển và cho phép người chơi đột nhập Villa.
    /// </summary>
    public void CompleteSurveillance()
    {
        FinishSurveillance();
    }

    /// <summary>Đặt lại trạng thái để có thể kích hoạt lại (dùng khi chạy lại phase/cheat).</summary>
    public void ResetZone()
    {
        triggered = false;
        completed = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    // ===================================================================
    // INTERNAL FLOW
    // ===================================================================

    private IEnumerator SurveillanceRoutine(Collider playerCol)
    {
        // 1. Khóa di chuyển & Kích hoạt Crouch
        if (lockPlayerMovement)
        {
            playerMovement = playerCol.GetComponent<PlayerMovement>();
            if (playerMovement == null)
                playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
            
            if (playerMovement != null)
            {
                playerMovement.IsMovementLocked = true;
                playerMovement.IsCrouching = true;
            }

            // Khóa camera và hướng dọc theo lối cổng để thấy rõ Mê Liu mở cửa,
            // bước ra quan sát rồi quay vào Villa.
            ThirdPersonCamera cameraCtrl = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (cameraCtrl != null)
            {
                GameObject villaGate = GameObject.Find("Gate_Interactive");
                Vector3 focusPos = villaGate != null
                    ? villaGate.transform.position
                    : new Vector3(-5f, 0.5f, 13.5f);
                cameraCtrl.StartFocus(focusPos, 4f, 3f);
            }
        }

        // 2. Monologue quan sát
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Nấp ở đây quan sát đã... Xem có động tĩnh gì ở cổng Villa không.", 4f);

        // 3. HOOK CUTSCENE (Mê Liu mở cửa, ngoắc Huy Sẹo xách túi đen, khóa xích cổng) — THÊM SAU
        onSurveillanceStarted?.Invoke();
        OnSurveillanceStartedAction?.Invoke();

        // 4. Chờ hoàn tất: theo cutscene ngoài, hoặc theo timer mặc định
        if (waitForExternalCutscene)
        {
            while (!completed) yield return null; // cutscene sẽ gọi CompleteSurveillance()
        }
        else
        {
            yield return new WaitForSeconds(surveillanceDuration);
            FinishSurveillance();
        }
    }

    private void FinishSurveillance()
    {
        if (completed) return;
        completed = true;

        // Mở khóa di chuyển & Tắt Crouch
        if (lockPlayerMovement && playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
            playerMovement.IsCrouching = false;
        }

        // Unlock camera
        ThirdPersonCamera cameraCtrl = Object.FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraCtrl != null)
        {
            cameraCtrl.StopFocus();
        }

        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Mê Liu vừa vào trong, cổng lại khóa xích bên trong rồi. " +
                "Mình phải lẻn vào theo lối khác — vào thu thập bằng chứng thôi!", 5f);

        onSurveillanceCompleted?.Invoke();
        OnSurveillanceCompletedAction?.Invoke();

        Debug.Log("[HidingZone] Mật phục hoàn tất — mở quyền đột nhập Villa.");

        // Vô hiệu hóa collider để không kích hoạt lại
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // ===================================================================
    // GIZMO — hiển thị vùng mật phục màu xanh trong Editor
    // ===================================================================

    private void OnDrawGizmos()
    {
        Gizmos.color = completed
            ? new Color(0.5f, 0.5f, 0.5f, 0.25f)
            : new Color(0f, 1f, 0.5f, 0.3f);

        float r = 1f;
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null) r = sc.radius;
        Gizmos.DrawSphere(transform.position, r);
    }
}
