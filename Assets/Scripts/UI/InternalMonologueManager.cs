using UnityEngine;

/// <summary>
/// Quản lý hiển thị THOẠI NỘI TÂM (internal monologue) của nhân vật chính (anhbobia)
/// và các suy nghĩ dẫn dắt nhiệm vụ. Tận dụng lại panel world-space của
/// <see cref="DialogueScreenUI"/> (đã được chứng minh render ổn định trong setup URP
/// của project này) thay vì tạo Canvas overlay mới.
///
/// Thoại nội tâm được bo trong thẻ in nghiêng + màu xám nhạt để phân biệt với
/// hội thoại NPC thông thường.
///
/// Tự khởi tạo (bootstrap) khi vào Play mode — không cần kéo thả vào scene.
/// </summary>
public class InternalMonologueManager : MonoBehaviour
{
    /// <summary>Instance singleton (tự tạo lúc runtime).</summary>
    public static InternalMonologueManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("InternalMonologueManager");
        go.AddComponent<InternalMonologueManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Hiển thị một câu thoại nội tâm trong khoảng thời gian cho trước.
    /// </summary>
    /// <param name="text">Nội dung suy nghĩ.</param>
    /// <param name="duration">Thời gian hiển thị (giây).</param>
    public void Show(string text, float duration = 6f)
    {
        string formatted = $"<i><color=#AEB7BF>{text}</color></i>";

        DialogueScreenUI ui = FindObjectOfType<DialogueScreenUI>();
        if (ui != null)
        {
            ui.ForceShow(formatted, duration);
        }
        else
        {
            Debug.Log($"[Nội tâm] {text}");
        }
    }

    /// <summary>Bí danh của <see cref="Show"/> cho dễ đọc khi gọi từ kịch bản.</summary>
    public void ShowMonologue(string text, float duration = 6f) => Show(text, duration);
}
