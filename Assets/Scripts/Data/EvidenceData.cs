using UnityEngine;

/// <summary>
/// ScriptableObject lưu trữ thông tin của một bằng chứng (Evidence) trong game.
/// Tạo asset mới: Right-click > Create > AnhBoBia > EvidenceData
/// </summary>
[CreateAssetMenu(fileName = "NewEvidence", menuName = "AnhBoBia/EvidenceData", order = 0)]
public class EvidenceData : ScriptableObject
{
    // ===================================================================
    // FIELDS - Cấu hình trên Inspector
    // ===================================================================

    [Header("Thông Tin Cơ Bản")]

    [Tooltip("ID định danh duy nhất cho bằng chứng này. Ví dụ: 'EVD_001'")]
    [SerializeField] private string evidenceID = "EVD_000";

    [Tooltip("Tên hiển thị của bằng chứng trên UI")]
    [SerializeField] private string evidenceName = "Bằng Chứng Mới";

    [Tooltip("Mô tả nội dung / ý nghĩa của bằng chứng")]
    [TextArea(3, 6)]
    [SerializeField] private string content = "Nội dung bằng chứng...";

    [Header("Giá Trị & Độ Quan Trọng")]

    [Tooltip("Điểm số của bằng chứng này (Evidence Score) khi được thu thập")]
    [Range(1, 100)]
    [SerializeField] private int scoreValue = 10;

    [Tooltip("Mức độ quan trọng: 1 = Thấp, 2 = Trung bình, 3 = Cao")]
    [Range(1, 3)]
    [SerializeField] private int importanceLevel = 1;

    [Tooltip("Icon đại diện hiển thị trên UI (có thể để null)")]
    [SerializeField] private Sprite evidenceIcon;

    [Header("Hiệu Ứng Khi Thu Thập")]

    [Tooltip("Mức tăng Độ Nghi Ngờ khi người chơi lấy bằng chứng này")]
    [Range(0f, 50f)]
    [SerializeField] private float suspicionIncrease = 5f;

    // ===================================================================
    // PROPERTIES - Truy cập read-only từ bên ngoài
    // ===================================================================

    /// <summary>ID định danh duy nhất của bằng chứng.</summary>
    public string EvidenceID => evidenceID;

    /// <summary>Tên hiển thị của bằng chứng.</summary>
    public string EvidenceName => evidenceName;

    /// <summary>Nội dung mô tả chi tiết của bằng chứng.</summary>
    public string Content => content;

    /// <summary>Điểm số cộng vào Evidence Score khi thu thập.</summary>
    public int ScoreValue => scoreValue;

    /// <summary>Mức độ quan trọng của bằng chứng (1-3).</summary>
    public int ImportanceLevel => importanceLevel;

    /// <summary>Icon đại diện của bằng chứng.</summary>
    public Sprite EvidenceIcon => evidenceIcon;

    /// <summary>Mức tăng Suspicion khi bằng chứng này được thu thập.</summary>
    public float SuspicionIncrease => suspicionIncrease;

    // ===================================================================
    // METHODS
    // ===================================================================

    /// <summary>
    /// Kiểm tra bằng chứng này có phải loại quan trọng (High Importance) không.
    /// </summary>
    /// <returns>True nếu ImportanceLevel >= 3.</returns>
    public bool IsHighImportance()
    {
        return importanceLevel >= 3;
    }

    /// <summary>
    /// Trả về chuỗi mô tả ngắn gọn để debug.
    /// </summary>
    public override string ToString()
    {
        return $"[Evidence] ID: {evidenceID} | Name: {evidenceName} | Score: {scoreValue} | Importance: {importanceLevel}";
    }
}
