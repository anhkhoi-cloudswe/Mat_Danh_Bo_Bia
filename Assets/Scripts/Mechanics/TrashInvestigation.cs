using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cơ chế "LỤC SOÁT ĐỐNG RÁC" tại Điểm 2 — phần kết của PHASE 1 ("Anh Bò Bía").
///
/// Luồng gameplay (được <see cref="StoryPhase1Manager"/> kích hoạt ở Trạng thái 5 qua
/// <see cref="BeginInvestigation"/>):
///   1. WaitingForPlayer – Player lại gần bãi rác → hiện chữ "Nhấn [E] để lục soát đống rác".
///   2. Digging          – Player BẤM GIỮ E → thanh progress chạy 0→100% trong <see cref="digDuration"/>s,
///                         khóa tạm di chuyển. Buông E giữa chừng → progress tụt về 0, mở khóa.
///   3. EvidenceSpawned  – Đủ 100% → ẩn progress, spawn đồng thời 2 vật chứng (kim tiêm + bịch ma túy)
///                         kèm AddForce văng nhẹ ra ngoài + đèn phát sáng. Player bấm E vào từng món để thu thập.
///   4. Completed        – Thu đủ 2 món → thoại nội tâm chốt hạ + đánh dấu Phase 1 hoàn thành
///                         (<see cref="StoryPhase1Manager.MarkPhase1Complete"/>) + mở vùng chuyển tiếp Phase 2.
///
/// LƯU Ý KỸ THUẬT: progress bar dùng World-Space canvas gắn trước camera + material ZTest=Always,
/// vì Screen-Space Overlay canvas tạo lúc runtime KHÔNG render trong setup URP của project này
/// (giống <see cref="DialogueScreenUI"/> / <see cref="ClueNotificationManager"/>).
/// </summary>
[DisallowMultipleComponent]
public class TrashInvestigation : MonoBehaviour
{
    private enum Stage
    {
        Dormant,          // Chưa được kích hoạt (trước Trạng thái 5)
        WaitingForPlayer, // Chờ Player tới gần & giữ E
        Digging,          // Đang giữ E, progress tăng dần
        EvidenceSpawned,  // Đã spawn 2 vật chứng, chờ thu thập
        Completed         // Đã thu đủ — Phase 1 xong
    }

    // ===================================================================
    // INSPECTOR
    // ===================================================================

    [Header("=== Prefab vật chứng (tự gán qua Editor) ===")]
    [Tooltip("syringe.prefab — kim tiêm còn dính máu.")]
    [SerializeField] private GameObject syringePrefab;
    [Tooltip("bichmaithuy.prefab — bịch ma túy đá.")]
    [SerializeField] private GameObject drugBagPrefab;

    [Header("=== Mốc bãi rác ===")]
    [Tooltip("Tâm đống rác để đo khoảng cách Player & làm gốc spawn vật chứng (thường là BaiRac_ThungRac).")]
    [SerializeField] private Transform trashAnchor;
    [Tooltip("Điểm spawn vật chứng. Để trống sẽ dùng trashAnchor.")]
    [SerializeField] private Transform spawnPoint;

    [Header("=== Tham số tương tác ===")]
    [Tooltip("Khoảng cách (m) Player phải đứng để lục soát được đống rác.")]
    [SerializeField] private float interactRange = 3.5f;
    [Tooltip("Thời gian (giây) giữ E để hoàn tất lục soát.")]
    [SerializeField] private float digDuration = 3f;
    [Tooltip("Khoảng cách (m) để nhặt từng vật chứng đã spawn.")]
    [SerializeField] private float evidencePickupRange = 2.2f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    [Header("=== Vật lý khi văng vật chứng ===")]
    [Tooltip("Độ cao (m) trên gốc spawn để thả vật chứng.")]
    [SerializeField] private float spawnHeight = 0.9f;
    [Tooltip("Vận tốc bay lên khi văng (m/s).")]
    [SerializeField] private float upPop = 2.6f;
    [Tooltip("Vận tốc văng ngang ra ngoài đống rác (m/s).")]
    [SerializeField] private float horizontalPop = 1.6f;

    [Header("=== Tham chiếu (tự gán qua Editor) ===")]
    [Tooltip("Script di chuyển của Player — sẽ bị khóa tạm trong lúc lục soát.")]
    [SerializeField] private PlayerMovement playerMovement;
    [Tooltip("Đạo diễn cốt truyện để báo hoàn thành Phase 1.")]
    [SerializeField] private StoryPhase1Manager storyManager;
    [Tooltip("Vùng/cửa chuyển tiếp sang Phase 2 — sẽ được SetActive(true) khi thu đủ vật chứng (có thể để trống).")]
    [SerializeField] private GameObject phase2TransitionZone;

    [Header("=== Tích hợp hệ thống điều tra (tùy chọn) ===")]
    [Tooltip("EvidenceData của kim tiêm — nếu gán sẽ cộng điểm qua CaseManager.")]
    [SerializeField] private EvidenceData syringeEvidence;
    [Tooltip("EvidenceData của bịch ma túy — nếu gán sẽ cộng điểm qua CaseManager.")]
    [SerializeField] private EvidenceData drugBagEvidence;

    [Header("=== Thoại nội tâm chốt hạ Phase 1 ===")]
    [TextArea(3, 6)]
    [SerializeField] private string finalMonologue =
        "Và cả bịch ma túy đá này nữa... Đúng như mình nghĩ, tên Huy Sẹo này có dính líu đến đường dây ma túy này. Căn nhà Villa gã vừa chạy vào... Tối ngày mai mình phải tìm cách đột nhập xem sao.";
    [SerializeField] private float finalMonologueDuration = 9f;
    [SerializeField] private MainGameplayVoiceKey spawnVoice = MainGameplayVoiceKey.PlayerAfterSearchSpawns;
    [SerializeField] private MainGameplayVoiceKey collectFirstVoice = MainGameplayVoiceKey.PlayerCollectFirstEvidence;
    [SerializeField] private MainGameplayVoiceKey finalVoice = MainGameplayVoiceKey.PlayerCompleteTrashPhase1;

    [Header("=== Debug ===")]
    [SerializeField] private bool enableDebugLog = true;

    // ===================================================================
    // RUNTIME STATE
    // ===================================================================

    private Stage stage = Stage.Dormant;
    private Transform player;
    private float digProgress;            // 0..1
    private TrashInvestigationUI ui;

    private GameObject spawnedSyringe;
    private GameObject spawnedDrugBag;
    private bool syringeCollected;
    private bool drugBagCollected;

    private const string PromptDig = "Nhấn <color=#FFFF00>[E]</color> (giữ) để lục soát đống rác";
    private const string PromptCollect = "Nhấn <color=#FFFF00>[E]</color> để thu thập vật chứng";

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (stage == Stage.Dormant || stage == Stage.Completed) return;

        EnsurePlayer();
        bool inRange = IsPlayerInRange();

        // An toàn: chỉ khóa di chuyển khi ĐANG ĐÀO. Mọi trạng thái khác luôn mở khóa
        // → tránh kẹt đứng hình nếu vì lý do nào đó khóa không được nhả đúng lúc.
        if (stage != Stage.Digging) SetMovementLocked(false);

        switch (stage)
        {
            case Stage.WaitingForPlayer:
                if (inRange)
                {
                    if (Input.GetKey(interactKey))
                    {
                        BeginDigging();
                    }
                    else
                    {
                        ui.ShowPrompt(PromptDig);
                    }
                }
                else
                {
                    ui.HidePrompt();
                }
                break;

            case Stage.Digging:
                if (!inRange || !Input.GetKey(interactKey))
                {
                    // Buông E hoặc đi ra khỏi vùng → tụt progress về 0, mở khóa di chuyển
                    ResetDigging();
                }
                else
                {
                    ContinueDigging(Time.deltaTime);
                }
                break;

            case Stage.EvidenceSpawned:
                HandleEvidenceCollection();
                break;
        }
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    /// <summary>
    /// Kích hoạt nhiệm vụ bới rác. Được <see cref="StoryPhase1Manager"/> gọi ở Trạng thái 5.
    /// </summary>
    public void BeginInvestigation()
    {
        if (stage != Stage.Dormant)
        {
            Log("BeginInvestigation bị bỏ qua — nhiệm vụ đã được kích hoạt trước đó.");
            return;
        }

        ResolveReferences();
        EnsureUI();
        EnsurePlayer();

        stage = Stage.WaitingForPlayer;
        Log("Đã kích hoạt nhiệm vụ lục soát đống rác. Chờ Player tới gần & giữ E.");
    }

    // ===================================================================
    // DIGGING
    // ===================================================================

    private void BeginDigging()
    {
        stage = Stage.Digging;
        digProgress = 0f;
        ui.HidePrompt();
        ui.ShowProgress();
        ui.SetProgress(0f);
        SetMovementLocked(true);
        Log("Bắt đầu lục soát — đang giữ E.");
    }

    private void ContinueDigging(float dt)
    {
        SetMovementLocked(true);
        digProgress += dt / Mathf.Max(0.01f, digDuration);
        ui.SetProgress(digProgress);

        if (digProgress >= 1f)
        {
            CompleteDigging();
        }
    }

    private void ResetDigging()
    {
        digProgress = 0f;
        ui.HideProgress();
        SetMovementLocked(false);
        stage = Stage.WaitingForPlayer;
    }

    private void CompleteDigging()
    {
        digProgress = 1f;
        ui.HideProgress();
        ui.HidePrompt();
        SetMovementLocked(false);

        SpawnEvidence();
        stage = Stage.EvidenceSpawned;

        // Báo rõ cho người chơi là đã lòi ra vật chứng + hiện ngay prompt thu thập
        if (ClueNotificationManager.Instance != null)
            ClueNotificationManager.Instance.ShowNotification("Có gì đó lấp ló trong đống rác... Lại gần nhặt lên xem!");
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show("Có vài thứ vừa lộ ra trong đống rác — nhặt hết lên xem là gì!", 4f);
        if (spawnVoice != MainGameplayVoiceKey.None)
            MainGameplayVoiceover.PlayLine(spawnVoice);
        ui.ShowPrompt(PromptCollect);

        Log("Lục soát hoàn tất — đã spawn 2 vật chứng.");
    }

    // ===================================================================
    // SPAWN EVIDENCE
    // ===================================================================

    private void SpawnEvidence()
    {
        Vector3 basePos = (spawnPoint != null ? spawnPoint.position
                          : (trashAnchor != null ? trashAnchor.position : transform.position))
                          + Vector3.up * spawnHeight;

        // Kim tiêm — lệch sang một bên, văng nhẹ, đèn xanh lạnh
        spawnedSyringe = SpawnOne(syringePrefab, "syringe",
                                  basePos + new Vector3(0.45f, 0f, 0.2f), new Vector3(1f, 0f, 0.4f),
                                  new Color(0.55f, 0.85f, 1f), true);
        // Bịch ma túy — lệch sang phía kia, đèn xanh độc
        spawnedDrugBag = SpawnOne(drugBagPrefab, "bichmaithuy",
                                  basePos + new Vector3(-0.45f, 0f, -0.2f), new Vector3(-1f, 0f, -0.4f),
                                  new Color(0.5f, 1f, 0.45f), false);
    }

    private GameObject SpawnOne(GameObject prefab, string label, Vector3 pos, Vector3 baseDir, Color glow, bool isSyringe)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[TrashInvestigation] Chưa gán prefab '{label}' — không spawn được vật chứng này.");
            return null;
        }

        GameObject go = Instantiate(prefab, pos, Random.rotation);

        // Đảm bảo có Collider để vật chứng không rơi xuyên đất.
        // QUAN TRỌNG: radius của SphereCollider là LOCAL → phải chia cho lossyScale,
        // nếu không prefab scale lớn (vd bichmaithuy scale 100) sẽ tạo collider khổng lồ
        // gây nổ vật lý/đứng game.
        if (go.GetComponentInChildren<Collider>() == null)
        {
            AddScaledSphereCollider(go, 0.09f);
        }

        // Rigidbody + lực văng NHẸ ra ngoài đống rác (VelocityChange → ổn định bất kể mass).
        // ContinuousDynamic chống lọt xuyên sàn khi rơi.
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.35f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3 horiz = baseDir + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
        horiz.y = 0f;
        if (horiz.sqrMagnitude < 0.01f) horiz = Vector3.forward;
        horiz.Normalize();

        Vector3 velocity = horiz * horizontalPop + Vector3.up * upPop;
        rb.AddForce(velocity, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);

        AddGlow(go, glow);

        // Sau khi văng xong thì "đáp đất" + đóng băng để không lăn mất / không lún
        StartCoroutine(SettleRoutine(go, 1.2f, isSyringe));
        return go;
    }

    /// <summary>Sau <paramref name="delay"/> giây: dừng vật lý, đóng băng và snap vật chứng lên mặt đất gần nhất.</summary>
    private IEnumerator SettleRoutine(GameObject go, float delay, bool isSyringe)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Vector3 p = go.transform.position;

        // Mốc mặt đất: dùng cao độ đống rác (đáng tin hơn raycast vì tránh tự bắn trúng collider của chính nó).
        float groundY = trashAnchor != null ? trashAnchor.position.y : p.y;

        // CĂN THEO ĐÁY LƯỚI HIỂN THỊ thay vì pivot — cực kỳ quan trọng với prefab scale lớn
        // (bichmaithuy scale 100): pivot lệch xa mesh nên nếu đặt theo pivot sẽ chôn cái bịch
        // xuống dưới đất → không nhìn thấy. Ở đây tính khoảng cách pivot→đáy mesh rồi bù lại.
        float pivotToBottom = 0f;
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            pivotToBottom = p.y - b.min.y;
        }
        p.y = groundY + pivotToBottom + 0.12f; // đáy mesh nổi cách đất 0.12m → thấy rõ

        // Bố trí vị trí cố định an toàn ngoài đống rác để không bị lún hay khuất sau thùng rác
        if (trashAnchor != null)
        {
            if (isSyringe)
            {
                // Kim tiêm nằm ở góc trước bên phải đống rác
                p.x = trashAnchor.position.x + 0.7f;
                p.z = trashAnchor.position.z - 0.5f;
            }
            else
            {
                // Bịch ma túy nằm ngay sát bên cạnh kim tiêm (lệch một chút ra phía trước)
                p.x = trashAnchor.position.x + 0.5f;
                p.z = trashAnchor.position.z - 0.8f;
            }
        }

        go.transform.position = p;

        // Gắn hiệu ứng xoay + nhấp nhô để nhìn là biết nhặt được (juicy + dễ thấy)
        if (go.GetComponent<EvidenceBobber>() == null)
            go.AddComponent<EvidenceBobber>().Init(p.y);
    }

    /// <summary>Thêm SphereCollider với bán kính THẬT (world) cố định, đã bù trừ lossyScale của prefab.</summary>
    private void AddScaledSphereCollider(GameObject go, float worldRadius)
    {
        SphereCollider sc = go.AddComponent<SphereCollider>();
        Vector3 ls = go.transform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);
        sc.radius = worldRadius / scale;

        // Canh tâm collider về giữa lưới hiển thị (theo local space)
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            sc.center = go.transform.InverseTransformPoint(b.center);
        }
    }

    /// <summary>Tạo Point Light glow gắn vào vật chứng cho dễ thấy trong đêm (đủ sáng nhưng không cháy màn hình).</summary>
    private void AddGlow(GameObject host, Color color)
    {
        GameObject lightGo = new GameObject("EvidenceGlow");
        lightGo.transform.SetParent(host.transform, true); // worldPositionStays để không bị scale 100 bóp méo
        lightGo.transform.position = host.transform.position + Vector3.up * 0.2f;
        lightGo.transform.localScale = Vector3.one;

        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = 2f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
    }

    // ===================================================================
    // COLLECT EVIDENCE
    // ===================================================================

    private void HandleEvidenceCollection()
    {
        bool found = FindNearestEvidence(out _, out bool isSyringe);

        // Hiện prompt khi Player đứng trong khu vực bãi rác và còn vật chứng chưa nhặt.
        // (Dùng phạm vi quanh đống rác cho dễ tương tác, không bắt phải dí sát từng món bé xíu.)
        if (found && IsPlayerInRange())
        {
            string what = isSyringe ? "Kim tiêm" : "Bịch ma túy đá";
            ui.ShowPrompt($"Nhấn <color=#FFFF00>[E]</color> để nhặt {what}");
            if (Input.GetKeyDown(interactKey))
            {
                CollectEvidence(isSyringe);
            }
        }
        else
        {
            ui.HidePrompt();
        }
    }

    /// <summary>Tìm món vật chứng còn lại gần Player nhất.</summary>
    private bool FindNearestEvidence(out Transform nearest, out bool nearestIsSyringe)
    {
        nearest = null;
        nearestIsSyringe = false;
        float best = float.MaxValue;
        Vector3 from = player != null ? player.position : transform.position;

        if (!syringeCollected && spawnedSyringe != null)
        {
            float d = Vector3.Distance(from, spawnedSyringe.transform.position);
            if (d < best) { best = d; nearest = spawnedSyringe.transform; nearestIsSyringe = true; }
        }
        if (!drugBagCollected && spawnedDrugBag != null)
        {
            float d = Vector3.Distance(from, spawnedDrugBag.transform.position);
            if (d < best) { best = d; nearest = spawnedDrugBag.transform; nearestIsSyringe = false; }
        }
        return nearest != null;
    }

    private void CollectEvidence(bool isSyringe)
    {
        if (isSyringe)
        {
            syringeCollected = true;
            if (spawnedSyringe != null) Destroy(spawnedSyringe);
            RegisterEvidence(syringeEvidence, "🩸 Vật chứng: Kim tiêm còn dính máu");
        }
        else
        {
            drugBagCollected = true;
            if (spawnedDrugBag != null) Destroy(spawnedDrugBag);
            RegisterEvidence(drugBagEvidence, "💊 Vật chứng: Bịch ma túy đá");
        }

        ui.HidePrompt();

        if (syringeCollected && drugBagCollected)
        {
            CompleteInvestigation();
        }
        else if (InternalMonologueManager.Instance != null)
        {
            // Còn 1 món nữa — nhắc người chơi nhặt nốt
            InternalMonologueManager.Instance.Show(
                isSyringe ? "Một ống kim tiêm còn dính máu... Vẫn còn món nữa trong đống rác, nhặt nốt!"
                           : "Bịch bột trắng... ma túy đá! Còn một món nữa, nhặt cho đủ đã.", 3.5f);

            if (isSyringe && collectFirstVoice != MainGameplayVoiceKey.None)
            {
                MainGameplayVoiceover.PlayLine(collectFirstVoice);
            }
        }
    }

    private void RegisterEvidence(EvidenceData data, string toast)
    {
        if (data != null && CaseManager.Instance != null)
            CaseManager.Instance.CollectEvidence(data);

        if (ClueNotificationManager.Instance != null)
            ClueNotificationManager.Instance.ShowNotification(toast);

        Log($"Đã thu thập: {toast}");
    }

    // ===================================================================
    // COMPLETE PHASE 1
    // ===================================================================

    private void CompleteInvestigation()
    {
        stage = Stage.Completed;
        ui.HidePrompt();
        ui.HideProgress();
        SetMovementLocked(false);

        // Thoại nội tâm chốt hạ
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(finalMonologue, finalMonologueDuration);
        else
            Debug.Log($"[Nội tâm] {finalMonologue}");

        // Phát giọng nói cho monologue chốt hạ
        if (finalVoice != MainGameplayVoiceKey.None)
        {
            MainGameplayVoiceover.PlayLine(finalVoice);
        }

        // Mở vùng chuyển tiếp sang Phase 2
        if (phase2TransitionZone != null)
        {
            phase2TransitionZone.SetActive(true);
            Log("Đã kích hoạt vùng chuyển tiếp sang Phase 2.");
        }
        else
        {
            Log("Chưa gán phase2TransitionZone — bỏ qua bước mở vùng chuyển tiếp.");
        }

        // Báo cho đạo diễn cốt truyện
        if (storyManager != null)
        {
            storyManager.MarkPhase1Complete();

            // Chờ monologue cuối Phase 1 hiển thị xong rồi chuyển sang Phase 2
            StartCoroutine(TriggerPhase2TransitionAfterDelay());
        }

        Log("✅ Hoàn thành nhiệm vụ bới rác — kết thúc Phase 1.");
    }

    /// <summary>
    /// Chờ monologue cuối Phase 1 kết thúc rồi kích hoạt chuyển cảnh sang Phase 2 Morning.
    /// </summary>
    private IEnumerator TriggerPhase2TransitionAfterDelay()
    {
        // Chờ monologue chốt hạ hiển thị xong
        yield return new WaitForSeconds(finalMonologueDuration + 1f);

        // Kích hoạt chuyển cảnh sang Phase 2 Morning
        if (storyManager != null)
        {
            storyManager.TransitionToPhase2Morning();
            Log("Đã kích hoạt chuyển cảnh sang Phase 2 Morning.");
        }
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    private void SetMovementLocked(bool locked)
    {
        if (playerMovement != null) playerMovement.IsMovementLocked = locked;
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return false;
        Vector3 center = trashAnchor != null ? trashAnchor.position : transform.position;
        return Vector3.Distance(player.position, center) <= interactRange;
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        if (playerMovement != null) { player = playerMovement.transform; return; }
        GameObject p = GameObject.FindWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    private void EnsureUI()
    {
        if (ui != null) return;
        var go = new GameObject("TrashInvestigationUI");
        ui = go.AddComponent<TrashInvestigationUI>();
    }

    private void ResolveReferences()
    {
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (storyManager == null) storyManager = FindFirstObjectByType<StoryPhase1Manager>();
        if (trashAnchor == null && storyManager == null) trashAnchor = transform;
    }

    private void Log(string message)
    {
        if (enableDebugLog) Debug.Log($"[TrashInvestigation] {message}");
    }
}

/// <summary>
/// Gắn runtime lên vật chứng đã văng ra: xoay chậm + nhấp nhô lên xuống quanh độ cao gốc
/// để người chơi dễ nhận ra đây là vật phẩm nhặt được.
/// </summary>
public class EvidenceBobber : MonoBehaviour
{
    private float baseY;
    private float seed;

    public void Init(float groundY)
    {
        baseY = groundY;
        seed = Random.value * 10f;
    }

    private void Update()
    {
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin((Time.time + seed) * 2.2f) * 0.08f;
        transform.position = p;
    }
}

/// <summary>
/// UI world-space cho nhiệm vụ bới rác: một dòng prompt hướng dẫn + một thanh progress bar.
/// Gắn cố định trước mặt camera chính (giống <see cref="DialogueScreenUI"/>) với material
/// ZTest=Always để luôn vẽ đè lên geometry. Tự dựng hoàn toàn bằng code lúc runtime.
/// </summary>
public class TrashInvestigationUI : MonoBehaviour
{
    private const float CanvasWidth = 900f;
    private const float CanvasHeight = 420f;
    private const float CanvasScale = 0.00062f;
    private const float DistanceFromCamera = 0.5f;

    private Camera attachedCamera;
    private Text promptText;
    private GameObject progressGroup;
    private RectTransform fillRect;
    private Text progressLabel;

    private Material alwaysOnTop;
    private Font legacyFont;

    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        // Gắn canvas vào trước mặt camera chính (camera có thể xuất hiện muộn / bị đổi).
        if (attachedCamera == null || !attachedCamera.isActiveAndEnabled)
        {
            attachedCamera = Camera.main;
            if (attachedCamera != null)
            {
                transform.SetParent(attachedCamera.transform, false);
                transform.localPosition = new Vector3(0f, -0.02f, DistanceFromCamera);
                transform.localRotation = Quaternion.identity;
                transform.localScale = new Vector3(CanvasScale, CanvasScale, CanvasScale);
            }
        }
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    public void ShowPrompt(string text)
    {
        if (promptText == null) return;
        promptText.text = text;
        if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
    }

    public void HidePrompt()
    {
        if (promptText != null && promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(false);
    }

    public void ShowProgress()
    {
        if (progressGroup != null && !progressGroup.activeSelf)
            progressGroup.SetActive(true);
    }

    public void HideProgress()
    {
        if (progressGroup != null && progressGroup.activeSelf)
            progressGroup.SetActive(false);
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (fillRect != null) fillRect.anchorMax = new Vector2(t, 1f);
        if (progressLabel != null) progressLabel.text = $"Đang lục soát đống rác... {Mathf.RoundToInt(t * 100f)}%";
    }

    // ===================================================================
    // BUILD
    // ===================================================================

    private void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 520;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildPrompt();
        BuildProgressBar();
    }

    private void BuildPrompt()
    {
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(transform, false);
        RectTransform rt = promptObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(860f, 110f);

        promptText = promptObj.AddComponent<Text>();
        promptText.material = alwaysOnTop;
        promptText.font = legacyFont;
        promptText.fontSize = 44;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.supportRichText = true;
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline o = promptObj.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(2f, -2f);
        o.useGraphicAlpha = true;

        promptObj.SetActive(false);
    }

    private void BuildProgressBar()
    {
        progressGroup = new GameObject("ProgressGroup");
        progressGroup.transform.SetParent(transform, false);
        RectTransform groupRect = progressGroup.AddComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.5f);
        groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.anchoredPosition = new Vector2(0f, -40f);
        groupRect.sizeDelta = new Vector2(640f, 90f);

        // Nhãn phía trên thanh bar
        GameObject labelObj = new GameObject("ProgressLabel");
        labelObj.transform.SetParent(progressGroup.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 38f);
        labelRect.sizeDelta = new Vector2(640f, 44f);

        progressLabel = labelObj.AddComponent<Text>();
        progressLabel.material = alwaysOnTop;
        progressLabel.font = legacyFont;
        progressLabel.fontSize = 30;
        progressLabel.fontStyle = FontStyle.Bold;
        progressLabel.alignment = TextAnchor.MiddleCenter;
        progressLabel.color = new Color(1f, 0.84f, 0.27f, 1f);
        progressLabel.supportRichText = true;
        progressLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        progressLabel.verticalOverflow = VerticalWrapMode.Overflow;

        Outline labelOutline = labelObj.AddComponent<Outline>();
        labelOutline.effectColor = Color.black;
        labelOutline.effectDistance = new Vector2(1.5f, -1.5f);
        labelOutline.useGraphicAlpha = true;

        // Nền (track) tối của thanh bar
        GameObject bgObj = new GameObject("BarBackground");
        bgObj.transform.SetParent(progressGroup.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(0f, -8f);
        bgRect.sizeDelta = new Vector2(620f, 40f);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.material = alwaysOnTop;
        bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

        // Thanh fill (điều khiển bằng anchorMax.x = progress — không cần sprite)
        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.material = alwaysOnTop;
        fillImage.color = new Color(0.3f, 0.9f, 0.45f, 1f);

        progressGroup.SetActive(false);
    }
}
