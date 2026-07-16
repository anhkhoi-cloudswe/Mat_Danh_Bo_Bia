using UnityEngine;

/// <summary>
/// Quản lý dữ liệu hội thoại và điểm bằng chứng liên quan của NPC.
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    /// <summary>
    /// Struct chứa thông tin chi tiết về cuộc hội thoại và điểm thưởng bằng chứng.
    /// </summary>
    [System.Serializable]
    public struct DialogueInfo
    {
        [TextArea(2, 5)]
        [Tooltip("Nội dung lời thoại khi NPC vừa đến (đặt hàng).")]
        public string welcomeText;

        [Tooltip("Soundtrack phát cùng lời chào/đặt hàng.")]
        public MainGameplayVoiceKey welcomeVoice;

        [TextArea(2, 5)]
        [Tooltip("Nội dung lời thoại về bằng chứng sau khi bán xong.")]
        public string evidenceText;

        [Tooltip("Soundtrack phát cùng lời thoại sau khi nhận bánh.")]
        public MainGameplayVoiceKey evidenceVoice;

        [Tooltip("Số điểm bằng chứng nhận được sau khi hội thoại.")]
        public int evidencePoints;
    }

    [Header("Cấu Hình Hội Thoại")]
    [SerializeField]
    [Tooltip("Dữ liệu hội thoại hiện tại của NPC.")]
    private DialogueInfo dialogueData;

    /// <summary>
    /// Getter để truy xuất dữ liệu hội thoại từ các script điều khiển khác.
    /// </summary>
    public DialogueInfo DialogueData
    {
        get { return dialogueData; }
        set { dialogueData = value; }
    }
}
