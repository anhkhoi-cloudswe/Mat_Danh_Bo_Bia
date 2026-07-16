using UnityEngine;

/// <summary>
/// Gắn vào từng GameObject nguyên liệu trong scene (Hũ dừa nạo, Hũ mè đen, v.v.)
/// để đánh dấu chúng là đồ vật có thể tương tác bằng cách nhìn vào (crosshair) và click.
/// Script này tự động thêm collider nếu chưa có để raycast có thể hit được.
/// </summary>
public class IngredientInteractable : MonoBehaviour
{
    [Header("Thông Tin Hiển Thị")]
    [Tooltip("Tên hiển thị khi người chơi nhìn vào. Dùng đúng tên trong Hierarchy.")]
    [SerializeField] private string displayName;

    [Header("Thứ Tự Trong Quy Trình")]
    [Tooltip("Bước cuốn bánh nào mới cho phép click nguyên liệu này.")]
    [SerializeField] private BoBiaMechanic.CookingStep requiredStep = BoBiaMechanic.CookingStep.AddPastry;

    // ===================================================================
    // PROPERTIES
    // ===================================================================

    /// <summary>Tên hiển thị lên màn hình khi hover.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

    /// <summary>Bước cuốn bánh yêu cầu để có thể click.</summary>
    public BoBiaMechanic.CookingStep RequiredStep => requiredStep;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        EnsureCollider();
    }

    // ===================================================================
    // PUBLIC METHODS
    // ===================================================================

    /// <summary>
    /// Được gọi bởi CrosshairInteractionSystem khi người chơi click vào đồ vật này.
    /// Chuyển tiếp tới BoBiaMechanic để xử lý logic bước cuốn bánh.
    /// </summary>
    public void OnClicked()
    {
        if (BoBiaMechanic.Instance != null)
        {
            BoBiaMechanic.Instance.OnClickIngredient(this);
        }
    }

    // ===================================================================
    // PRIVATE HELPERS
    // ===================================================================

    /// <summary>
    /// Tự động thêm BoxCollider nếu đồ vật (và toàn bộ children) chưa có Collider nào.
    /// Cần thiết để Raycast có thể hit được đồ vật.
    /// </summary>
    private void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        // Tính bounds của tất cả Renderer để tạo Collider vừa khít ở dạng Local Space
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds localBounds = new Bounds();
            bool first = true;

            foreach (Renderer r in renderers)
            {
                Bounds worldBounds = r.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;

                // Chuyển 8 góc của world bounds sang local space của transform hiện tại
                Vector3[] corners = new Vector3[8];
                corners[0] = transform.InverseTransformPoint(new Vector3(min.x, min.y, min.z));
                corners[1] = transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z));
                corners[2] = transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z));
                corners[3] = transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z));
                corners[4] = transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z));
                corners[5] = transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z));
                corners[6] = transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z));
                corners[7] = transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z));

                foreach (Vector3 corner in corners)
                {
                    if (first)
                    {
                        localBounds = new Bounds(corner, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        localBounds.Encapsulate(corner);
                    }
                }
            }

            box.center = localBounds.center;
            box.size = localBounds.size;
        }
        else
        {
            box.center = Vector3.zero;
            box.size = new Vector3(0.15f, 0.15f, 0.15f);
        }

        box.isTrigger = true;
    }
}
