using UnityEngine;

/// <summary>
/// Hệ thống tương tác bằng crosshair (tâm màn hình).
/// Raycast mỗi frame từ giữa camera về phía trước để phát hiện IngredientInteractable.
/// Khi hover: hiện tên nguyên liệu. Khi click chuột trái: gọi OnClicked().
/// 
/// Gắn vào: Player hoặc Camera GameObject.
/// Yêu cầu: CrosshairLabelUI phải tồn tại trong scene.
/// </summary>
public class CrosshairInteractionSystem : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Khoảng cách tối đa để nhận diện đồ vật (m).")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Layer mask cho raycast. Để All nếu không rõ.")]
    [SerializeField] private LayerMask detectionLayerMask = ~0; // All layers

    [Header("References")]
    [Tooltip("Camera dùng để raycast. Tự tìm Camera.main nếu để null.")]
    [SerializeField] private Camera targetCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebugRay = true;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    private IngredientInteractable currentHovered;
    private bool isMakingBoBia = false;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Start()
    {
        targetCamera = Camera.main;
    }

    private void Update()
    {
        // Liên tục cập nhật targetCamera để tránh camera chính bị đổi hoặc null
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
        {
            targetCamera = Camera.main;
        }

        // Chỉ hoạt động khi đang trong chế độ làm bò bía
        if (!isMakingBoBia)
        {
            if (currentHovered != null)
            {
                currentHovered = null;
                CrosshairLabelUI.Instance?.Hide();
            }
            return;
        }

        // Bảo đảm UI container hoạt động
        if (CrosshairLabelUI.Instance != null && !CrosshairLabelUI.Instance.gameObject.activeInHierarchy)
        {
            CrosshairLabelUI.Instance.gameObject.SetActive(true);
        }

        PerformCrosshairRaycast();
        HandleClickInput();
    }

    // ===================================================================
    // PUBLIC METHODS
    // ===================================================================

    /// <summary>Kích hoạt hệ thống crosshair interaction (gọi khi bắt đầu làm bò bía).</summary>
    public void Enable()
    {
        isMakingBoBia = true;
    }

    /// <summary>Tắt hệ thống crosshair interaction (gọi khi kết thúc làm bò bía).</summary>
    public void Disable()
    {
        isMakingBoBia = false;
        currentHovered = null;
        CrosshairLabelUI.Instance?.Hide();
    }

    // ===================================================================
    // PRIVATE METHODS
    // ===================================================================

    private void PerformCrosshairRaycast()
    {
        if (targetCamera == null) return;

        // Tạo ray: từ vị trí trỏ chuột nếu chuột tự do (khi cuốn bánh), hoặc từ tâm màn hình
        Ray ray;
        Vector2 screenPos;
        if (Cursor.lockState == CursorLockMode.None)
        {
            screenPos = Input.mousePosition;
            ray = targetCamera.ScreenPointToRay(screenPos);
        }
        else
        {
            screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        if (showDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * detectionRange, Color.cyan);

        // Sử dụng RaycastAll để bắn xuyên qua mọi vật cản (như xe bò bía hay collider của player)
        RaycastHit[] hits = Physics.RaycastAll(ray, detectionRange, detectionLayerMask, QueryTriggerInteraction.Collide);

        if (hits.Length > 0)
        {
            // Sắp xếp các hit theo khoảng cách từ gần đến xa
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

            foreach (var hit in hits)
            {
                // Bỏ qua các collider thuộc chính Player để không tự hit bản thân
                if (hit.collider.gameObject.CompareTag("Player") || hit.collider.transform.IsChildOf(transform))
                    continue;

                // Tìm IngredientInteractable trên GameObject hit (hoặc cha của nó)
                IngredientInteractable ingredient = hit.collider.GetComponentInParent<IngredientInteractable>();

                if (ingredient != null)
                {
                    // Kiểm tra xem đây có phải bước hiện tại không
                    bool isInteractable = BoBiaMechanic.Instance != null &&
                                          BoBiaMechanic.Instance.CurrentStep == ingredient.RequiredStep;

                    // Chỉ cập nhật UI nếu hover đổi sang object khác
                    if (currentHovered != ingredient)
                    {
                        currentHovered = ingredient;
                        Debug.Log($"[Crosshair] Hovered: {ingredient.DisplayName} (Step: {ingredient.RequiredStep}, Active: {isInteractable})");
                    }

                    // Cập nhật UI label mỗi frame với vị trí screenPos tương ứng
                    CrosshairLabelUI.Instance?.Show(ingredient.DisplayName, isInteractable, screenPos);
                    return;
                }
            }
        }

        // Không hit nguyên liệu nào → ẩn label
        if (currentHovered != null)
        {
            currentHovered = null;
            CrosshairLabelUI.Instance?.Hide();
        }
    }

    private void HandleClickInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (currentHovered == null) return;

        // Gọi OnClicked — logic xử lý bước nằm trong BoBiaMechanic
        currentHovered.OnClicked();
    }
}
