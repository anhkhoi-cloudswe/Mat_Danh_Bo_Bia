using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị bóng hội thoại (Dialogue Bubble) lơ lửng trên đầu NPC.
/// Hỗ trợ tự xoay về phía camera (Billboard) và hiệu ứng xuất hiện/biến mất mượt mà.
/// </summary>
public class DialogueBubble : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text textMesh;

    [Header("Transition Settings")]
    [Tooltip("Tốc độ chuyển cảnh của hiệu ứng phóng to/mờ dần (Scale/Fade).")]
    [SerializeField] private float transitionSpeed = 5f;

    private Transform mainCameraTransform;
    private float hideTime;
    private bool isShowing;
    private Vector3 initialScale;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<Text>();
        }

        // Lưu lại tỷ lệ scale ban đầu được cấu hình trong Prefab
        initialScale = transform.localScale;
        if (initialScale == Vector3.zero)
        {
            initialScale = new Vector3(0.005f, 0.005f, 0.005f);
        }

        // Khởi tạo ở trạng thái ẩn hoàn toàn (scale về 0 và mờ hẳn)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        transform.localScale = Vector3.zero;
        isShowing = false;
    }

    /// <summary>
    /// Hiển thị lời thoại của NPC với thời gian tự động ẩn.
    /// </summary>
    /// <param name="message">Nội dung thoại.</param>
    /// <param name="duration">Thời gian hiển thị (giây).</param>
    public void Show(string message, float duration)
    {
        if (textMesh != null)
        {
            textMesh.text = message;
        }

        isShowing = true;
        hideTime = Time.time + duration;
    }

    /// <summary>
    /// Ẩn bóng hội thoại ngay lập tức hoặc kích hoạt hiệu ứng thu nhỏ.
    /// </summary>
    public void Hide()
    {
        isShowing = false;
    }

    private void Update()
    {
        // Tự động đóng bóng hội thoại khi hết thời gian
        if (isShowing && Time.time >= hideTime)
        {
            Hide();
        }

        // Nội suy alpha mượt mà
        float targetAlpha = isShowing ? 1f : 0f;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, transitionSpeed * Time.deltaTime);
        }

        // Nội suy tỷ lệ scale mượt mà (Pop-in / Shrink-out)
        Vector3 targetScale = isShowing ? initialScale : Vector3.zero;
        float scaleSpeed = initialScale.x * transitionSpeed;
        transform.localScale = Vector3.MoveTowards(transform.localScale, targetScale, scaleSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        // Khi đang hiển thị hoặc chưa thu nhỏ về 0 hẳn, luôn xoay hướng về Camera chính
        if (isShowing || transform.localScale.sqrMagnitude > 0.000001f)
        {
            if (mainCameraTransform == null && Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }

            if (mainCameraTransform != null)
            {
                // Thực hiện billboard: Xoay hướng cùng chiều xoay của camera để không bị đảo ngược chữ
                transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                                 mainCameraTransform.rotation * Vector3.up);
            }
        }
    }
}
