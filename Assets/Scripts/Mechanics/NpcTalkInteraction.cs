using System.Collections;
using UnityEngine;

/// <summary>
/// NPC tĩnh (vd anhxamminh đứng trước xe bánh mì) sẽ:
///   • TỰ ĐỘNG chào mời khi Player lại gần (qua bóng thoại <see cref="DialogueBubble"/> trên đầu) —
///     không cần bấm phím nên không tranh phím [E] với hành động "Mua bánh mì".
///   • Bấm [E] (khi đứng gần NPC này hơn <see cref="rivalNpc"/>) để nghe thêm một MANH MỐI
///     (lấy từ <see cref="NPCDialogue"/>.evidenceText), cộng điểm qua <see cref="CaseManager"/>.
///
/// Lời chào mặc định khuyến khích Player đi mua bánh mì (dẫn dắt vào cốt truyện Phase 1).
/// </summary>
[DisallowMultipleComponent]
public class NpcTalkInteraction : MonoBehaviour
{
    [Header("Tương tác")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Khoảng cách (m) để NPC tự chào mời và cho phép bấm E.")]
    [SerializeField] private float talkRange = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Lời thoại")]
    [TextArea(2, 4)]
    [Tooltip("Câu NPC tự nói khi Player lại gần.")]
    [SerializeField] private string greetingText =
        "Bánh mì cột đèn ở đây ngon nức tiếng luôn anh ơi! Anh thử một ổ đi, ăn là ghiền liền á!";

    [Header("Tham chiếu (tự dò nếu để trống)")]
    [SerializeField] private NPCDialogue npcDialogue;
    [SerializeField] private DialogueBubble bubble;
    [SerializeField] private Animator npcAnimator;
    [Tooltip("NPC tranh phím E ở gần (vd anhbanhmi). Chỉ cho bấm E nghe manh mối khi Player ở GẦN mình hơn NPC này.")]
    [SerializeField] private Transform rivalNpc;
    [SerializeField] private StoryPhase1Manager storyManager;

    [Header("Tùy chọn")]
    [SerializeField] private bool facePlayerWhenTalking = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private Transform player;
    private bool isTalking;
    private bool greetedThisVisit;
    private bool clueGiven;
    private Quaternion originalRotation;
    private DialogueScreenUI screenUiFallback;

    private void Awake()
    {
        if (npcDialogue == null) npcDialogue = GetComponent<NPCDialogue>();
        if (bubble == null) bubble = GetComponentInChildren<DialogueBubble>(true);
        if (npcAnimator == null) npcAnimator = GetComponent<Animator>();
        if (storyManager == null) storyManager = FindAnyObjectByType<StoryPhase1Manager>();
        originalRotation = transform.rotation;
    }

    private void Update()
    {
        EnsurePlayer();
        if (player == null || isTalking) return;

        float d = Vector3.Distance(player.position, transform.position);
        bool near = d <= talkRange;

        if (!near)
        {
            greetedThisVisit = false; // rời đi → lần tới lại chào
            return;
        }

        if (StoryBusy()) return;

        // 1) Tự động chào mời 1 lần mỗi lần Player tới gần (không cần phím)
        if (!greetedThisVisit)
        {
            greetedThisVisit = true;
            StartCoroutine(SpeakRoutine(greetingText, 4f, awardClue: false));
            return;
        }

        // 2) Bấm E để nghe thêm manh mối (chỉ khi gần anhxamminh hơn anhbanhmi → khỏi tranh phím "mua bánh mì")
        if (CloserThanRival(d) && Input.GetKeyDown(interactKey))
        {
            string clue = npcDialogue != null ? npcDialogue.DialogueData.evidenceText : "";
            if (!string.IsNullOrEmpty(clue))
                StartCoroutine(SpeakRoutine(clue, 5f, awardClue: true));
        }
    }

    private IEnumerator SpeakRoutine(string text, float duration, bool awardClue)
    {
        isTalking = true;

        if (!awardClue)
            duration = Mathf.Max(duration, MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.XamMinhNight1));

        if (facePlayerWhenTalking && player != null)
        {
            Vector3 dir = player.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        }

        ShowBubble(text, duration);

        if (awardClue && !clueGiven)
        {
            clueGiven = true;
            int points = npcDialogue != null ? npcDialogue.DialogueData.evidencePoints : 0;
            if (CaseManager.Instance != null)
            {
                if (points > 0) CaseManager.Instance.AddScore(points);
                CaseManager.Instance.AddNpcClue();
            }
            if (ClueNotificationManager.Instance != null)
                ClueNotificationManager.Instance.ShowNotification("Manh mối từ anh xăm mình:\n" + text);
            Log($"Đã thu thập manh mối từ NPC (+{points} điểm).");
        }

        yield return new WaitForSeconds(duration);

        if (facePlayerWhenTalking) transform.rotation = originalRotation;
        isTalking = false;
    }

    private void ShowBubble(string text, float duration)
    {
        if (bubble != null)
        {
            bubble.Show(text, duration);
        }

        // Luôn hiện thêm trên HUD. Bubble world-space có thể bị che bởi xe/NPC hoặc
        // quay khỏi camera trong frame đầu, khiến người chơi tưởng NPC không nói.
        if (screenUiFallback == null) screenUiFallback = FindAnyObjectByType<DialogueScreenUI>();
        if (screenUiFallback != null) screenUiFallback.ForceShow(text, duration);
    }

    private bool CloserThanRival(float myDist)
    {
        if (rivalNpc == null || player == null) return true;
        return myDist <= Vector3.Distance(player.position, rivalNpc.position);
    }

    private bool StoryBusy()
    {
        return storyManager != null && storyManager.IsSequenceRunning;
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.FindWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    private void Log(string message)
    {
        if (enableDebugLog) Debug.Log($"[NpcTalkInteraction:{name}] {message}");
    }
}
