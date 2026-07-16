using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance { get; private set; }

    [Serializable]
    public class Customer
    {
        public int customerId;
        public string displayName;
        public GameObject npcInstance;
        public int spawnPointIndex; // 0 for spawnPoint1, 1 for spawnPoint2
        public Transform customSpawnPoint; // Nếu set: spawn & quay về tại đây (vd Mê Liu đi ra từ Villa) thay vì đầu hẻm.
        /// <summary>Số lượng bánh khách đặt mua. Mặc định 1. Được StoryPhase2Manager gán khi khách đến.</summary>
        public int orderQuantity = 1;
    }

    public enum CustomerState
    {
        None,
        ArrivedWaitingToTalk, // Đang đợi bấm E để trò chuyện đặt hàng
        TalkingWelcome,       // Đang hiển thị thoại đặt hàng
        ReadyToRoll,          // Đang chờ cuốn bánh
        Rolling,              // Đang trong tiến trình cuốn bánh
        WaitingForEvidence,   // Cuốn xong, chờ bấm E để nghe manh mối
        TalkingEvidence,      // Đang hiển thị thoại manh mối
        Leaving               // Đang đi về
    }

    private CustomerState currentCustomerState = CustomerState.None;
    public CustomerState CurrentCustomerState => currentCustomerState;

    public event Action<Customer> OnCustomerArrived;
    public event Action<Customer> OnCustomerServed;
    public event Action<int> OnQueueChanged;

    /// <summary>Phát sinh khi NPC nói lời thoại. Tham số: (npcName, dialogueText, evidencePoints, displayDuration).</summary>
    public event Action<string, string, int, float> OnDialogueTriggered;

    /// <summary>Phát sinh khi lời thoại kết thúc hiển thị.</summary>
    public event Action OnDialogueEnded;

    [Header("Queue Points")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform approachPoint;
    [SerializeField] private Transform targetQueuePoint;

    [Header("Alley & Road Waypoints")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;
    [SerializeField] private Transform cornerPoint;
    [SerializeField] private Transform roadApproachPoint;

    [Header("Villa Origin (Story Phase 2 — Mê Liu)")]
    [Tooltip("Điểm cửa Villa. Nếu được gán, Mê Liu (Miu Le) sẽ đi ra từ đây và quay về đây sau khi mua, thay vì đầu hẻm.")]
    [SerializeField] private Transform villaSpawnPoint;
    [Tooltip("Bật để Mê Liu xuất phát từ Villa (StoryPhase2Manager tự bật trong buổi sáng Ngày 2).")]
    [SerializeField] private bool routeMeLiuFromVilla = false;
    public Transform VillaSpawnPoint { get => villaSpawnPoint; set => villaSpawnPoint = value; }
    public bool RouteMeLiuFromVilla { get => routeMeLiuFromVilla; set => routeMeLiuFromVilla = value; }

    [Header("Customer Spawn")]
    [SerializeField] private bool autoStartOnPlay = true;
    [Range(0f, 30f)][SerializeField] private float initialSpawnDelay = 1f;
    [Range(1f, 60f)][SerializeField] private float spawnIntervalMin = 8f;
    [Range(1f, 120f)][SerializeField] private float spawnIntervalMax = 15f;
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private GameObject[] customerPrefabs;
    [SerializeField] private string[] customerNames = { "Miu Le", "Chi Lan", "Em Minh" };

    [Header("Movement")]
    [Range(0.25f, 6f)][SerializeField] private float walkSpeed = 1.5f;
    [Range(1f, 20f)][SerializeField] private float turnSpeed = 10f;
    [Range(0.05f, 1f)][SerializeField] private float arrivalDistance = 0.35f;
    [Range(0.05f, 2f)][SerializeField] private float slowDownDistance = 0.9f;
    [SerializeField] private string walkStateName = "Walk";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string blendParameterName = "Blend";

    [Header("Dialogue")]
    [Tooltip("Thời gian (giây) hiển thị lời thoại NPC trên màn hình trước khi NPC rời đi.")]
    [Range(2f, 15f)]
    [SerializeField] private float dialogueDisplayDuration = 5f;

    [Header("Customer Interaction")]
    [Tooltip("Người chơi phải đứng trong khoảng này so với khách đang đợi mới được bấm E nói chuyện.")]
    [SerializeField] private float customerInteractionRange = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly Queue<Customer> customerQueue = new Queue<Customer>();
    private Coroutine spawnLoopCoroutine;
    private int nextCustomerId = 1;
    private int nextPrefabIndex = 0;
    private bool isActive;
    private bool isCooldownActive;
    private bool currentCustomerArrivedAtStall;

    public int QueueCount => customerQueue.Count;
    public bool HasCustomerWaiting => customerQueue.Count > 0 && currentCustomerArrivedAtStall;
    public Customer NextCustomer => customerQueue.Count > 0 ? customerQueue.Peek() : null;
    public GameObject[] CustomerPrefabs
    {
        get => customerPrefabs;
        set
        {
            customerPrefabs = value;
            nextPrefabIndex = 0;
        }
    }
    public float DialogueDisplayDuration => dialogueDisplayDuration;
    public float SpawnIntervalMin { get => spawnIntervalMin; set => spawnIntervalMin = value; }
    public float SpawnIntervalMax { get => spawnIntervalMax; set => spawnIntervalMax = value; }
    public bool IsPlayerNearCurrentCustomer
    {
        get
        {
            Customer customer = NextCustomer;
            if (customer == null || customer.npcInstance == null) return false;

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            return player != null && Vector3.Distance(player.transform.position, customer.npcInstance.transform.position) <= customerInteractionRange;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CustomerManager] Phát hiện instance trùng lặp. Đang huỷ component bản sao...");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        // Tự động tìm và gán các customerPrefabs từ Assets/Prefabs nếu mảng trống
        if (customerPrefabs == null || customerPrefabs.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            var list = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    string nameLower = prefab.name.ToLower();
                    if (nameLower != "huy_seo" && nameLower != "anhbanhmi" && nameLower != "batlua" && 
                        nameLower != "bichmaithuy" && !nameLower.Contains("bichrac") && 
                        !nameLower.Contains("cotden") && !nameLower.Contains("xebanhmi") && 
                        nameLower != "syringe" && nameLower != "binhbong" && nameLower != "thungrac")
                    {
                        list.Add(prefab);
                    }
                }
            }
            if (list.Count > 0)
            {
                customerPrefabs = list.ToArray();
                Debug.Log($"[CustomerManager] Tự động nạp {customerPrefabs.Length} NPC Prefab từ Assets/Prefabs: " + string.Join(", ", list.ConvertAll(p => p.name)));
            }
        }
#endif

        if (autoStartOnPlay)
        {
            StartSpawning();
        }
    }

    private void Update()
    {
        HandleInteractionInput();
    }

    private void HandleInteractionInput()
    {
        // Chỉ cho phép tương tác nếu có khách và khách đã đến quầy
        if (customerQueue.Count == 0 || !currentCustomerArrivedAtStall) return;

        // Không cho nói chuyện từ xa: phải đứng gần chính NPC đang chờ tại quầy.
        if (!IsPlayerNearCurrentCustomer) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentCustomerState == CustomerState.ArrivedWaitingToTalk)
            {
                StartCoroutine(ShowWelcomeDialogueRoutine());
            }
            else if (currentCustomerState == CustomerState.WaitingForEvidence)
            {
                StartCoroutine(ShowEvidenceDialogueRoutine());
            }
        }
    }

    public void SetCustomerState(CustomerState newState)
    {
        currentCustomerState = newState;
        Log($"Trạng thái khách hàng đổi thành: {newState}");
    }

    public void StartSpawning()
    {
        if (isActive) return;

        isActive = true;
        isCooldownActive = false;
        currentCustomerArrivedAtStall = false;
        nextPrefabIndex = 0;
        spawnLoopCoroutine = StartCoroutine(SpawnLoopWithCooldown());
        Log("Customer queue started.");
    }

    public void StopSpawning(bool clearQueue = false)
    {
        isActive = false;
        isCooldownActive = false;

        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
            spawnLoopCoroutine = null;
        }

        if (clearQueue)
        {
            ClearQueue();
        }
    }

    public Customer ServeNextCustomer()
    {
        if (customerQueue.Count == 0) return null;

        Customer served = customerQueue.Dequeue();
        currentCustomerArrivedAtStall = false;
        OnCustomerServed?.Invoke(served);
        OnQueueChanged?.Invoke(customerQueue.Count);

        if (served.npcInstance != null)
        {
            StartCoroutine(MoveAwayAndDestroyRoutine(served.npcInstance, served.spawnPointIndex, served.customSpawnPoint));
        }
        else
        {
            if (isActive && !isCooldownActive)
            {
                spawnLoopCoroutine = StartCoroutine(SpawnLoopWithCooldown());
            }
        }

        Log($"Served customer: {served.displayName}");
        return served;
    }

    public void ServeNextCustomerWithDialogue()
    {
        // Khi cuốn bánh xong, kích hoạt hội thoại ngay lập tức — không cần bấm E thêm lần nữa
        currentCustomerState = CustomerState.TalkingEvidence;
        Log($"Bánh đã bán xong. Tự động kích hoạt hội thoại cho {NextCustomer?.displayName}.");
        StartCoroutine(ShowEvidenceDialogueRoutine());
    }

    private IEnumerator ShowWelcomeDialogueRoutine()
    {
        currentCustomerState = CustomerState.TalkingWelcome;
        Customer customer = customerQueue.Peek();

        if (customer.npcInstance != null)
        {
            NPCDialogue npcDialogue = customer.npcInstance.GetComponent<NPCDialogue>();
            if (npcDialogue != null && !string.IsNullOrEmpty(npcDialogue.DialogueData.welcomeText))
            {
                float displayDuration = Mathf.Max(dialogueDisplayDuration,
                    MainGameplayVoiceover.PlayLine(npcDialogue.DialogueData.welcomeVoice));
                string bubbleText = $"<b>{customer.displayName}</b>\n{npcDialogue.DialogueData.welcomeText}";
                
                // Hiển thị bong bóng thoại trên đầu NPC
                DialogueBubble bubble = customer.npcInstance.GetComponentInChildren<DialogueBubble>(true);
                if (bubble != null)
                {
                    bubble.Show(bubbleText, displayDuration);
                }

                // Hiển thị panel hội thoại ở đáy màn hình
                OnDialogueTriggered?.Invoke(customer.displayName, npcDialogue.DialogueData.welcomeText, 0, displayDuration);

                yield return new WaitForSeconds(displayDuration);

                // --- CHÈN ĐỘC THOẠI MÙI KHAI MÊ LIU TẠI ĐÂY ---
                string customerName = customer.displayName != null ? customer.displayName.ToLower() : (customer.npcInstance != null ? customer.npcInstance.name.ToLower() : "");
                if (customerName.Contains("miu") || customerName.Contains("mê") || customerName.Contains("me") || customerName.Contains("meliu"))
                {
                    var sp2 = UnityEngine.Object.FindAnyObjectByType<StoryPhase2Manager>();
                    if (sp2 != null)
                    {
                        sp2.TriggerMeLiuRedMonologueOnly();
                        while (sp2.IsMeLiuRedMonologueRunningOnly)
                        {
                            yield return new WaitForSeconds(0.1f);
                        }
                    }
                }

                currentCustomerState = CustomerState.ReadyToRoll;
                OnDialogueEnded?.Invoke();
                yield break;
            }
        }

        yield return new WaitForSeconds(dialogueDisplayDuration);

        // Chờ hội thoại kết thúc, chuyển sang trạng thái sẵn sàng cuốn bánh
        currentCustomerState = CustomerState.ReadyToRoll;
        OnDialogueEnded?.Invoke();
    }

    private IEnumerator ShowEvidenceDialogueRoutine()
    {
        if (customerQueue.Count == 0) yield break;

        currentCustomerState = CustomerState.TalkingEvidence;
        Customer served = customerQueue.Dequeue();

        // Chèn câu hỏi của anh Bò bía sau khi cuốn bánh xong trước khi Mê Liu trả lời
        string servedName = served.displayName != null ? served.displayName.ToLower() : (served.npcInstance != null ? served.npcInstance.name.ToLower() : "");
        if (servedName.Contains("miu") || servedName.Contains("mê") || servedName.Contains("me") || servedName.Contains("meliu"))
        {
            var sp2 = UnityEngine.Object.FindAnyObjectByType<StoryPhase2Manager>();
            if (sp2 != null)
            {
                sp2.TriggerMeLiuQuestion();
                while (sp2.IsMeLiuQuestionRunningOnly)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        currentCustomerArrivedAtStall = false;
        OnCustomerServed?.Invoke(served);
        OnQueueChanged?.Invoke(customerQueue.Count);

        string dialogueText = "";
        int evidencePoints = 0;
        MainGameplayVoiceKey evidenceVoice = MainGameplayVoiceKey.None;

        if (served.npcInstance != null)
        {
            NPCDialogue npcDialogue = served.npcInstance.GetComponent<NPCDialogue>();
            if (npcDialogue != null)
            {
                dialogueText = npcDialogue.DialogueData.evidenceText;
                evidencePoints = npcDialogue.DialogueData.evidencePoints;
                evidenceVoice = npcDialogue.DialogueData.evidenceVoice;
            }
        }

        if (!string.IsNullOrEmpty(dialogueText))
        {
            float displayDuration = Mathf.Max(dialogueDisplayDuration, MainGameplayVoiceover.PlayLine(evidenceVoice));
            Log($"💬 {served.displayName}: \"{dialogueText}\" (+{evidencePoints} điểm bằng chứng)");
            OnDialogueTriggered?.Invoke(served.displayName, dialogueText, evidencePoints, displayDuration);

            // Kích hoạt Dialogue Bubble nổi trên đầu NPC
            if (served.npcInstance != null)
            {
                DialogueBubble bubble = served.npcInstance.GetComponentInChildren<DialogueBubble>(true);
                if (bubble != null)
                {
                    string bubbleText = $"<b>{served.displayName}</b>\n{dialogueText}";
                    bubble.Show(bubbleText, displayDuration);
                }
            }

            // Cộng điểm bằng chứng vào CaseManager
            if (evidencePoints > 0 && CaseManager.Instance != null)
            {
                CaseManager.Instance.AddScore(evidencePoints);
                Log($"Đã cộng {evidencePoints} điểm bằng chứng.");
            }

            yield return new WaitForSeconds(displayDuration);

            // Ẩn bóng hội thoại
            if (served.npcInstance != null)
            {
                DialogueBubble bubble = served.npcInstance.GetComponentInChildren<DialogueBubble>(true);
                if (bubble != null)
                {
                    bubble.Hide();
                }
            }

            OnDialogueEnded?.Invoke();

            // Chỉ ghi nhận vào "Hồ sơ điều tra" khi đây là MANH MỐI THẬT (evidencePoints > 0).
            // Các câu khen vu vơ ban sáng Ngày 1 (evidencePoints == 0) chỉ hiện bong bóng thoại,
            // TUYỆT ĐỐI không hiện toast hồ sơ và không tăng số manh mối NPC.
            if (evidencePoints > 0)
            {
                // Hiển thị Toast Notification ghi nhận manh mối vừa thu thập được.
                if (ClueNotificationManager.Instance != null)
                {
                    ClueNotificationManager.Instance.ShowNotification(dialogueText);
                }

                if (CaseManager.Instance != null)
                {
                    CaseManager.Instance.AddNpcClue();
                }
            }
        }

        // --- Sau khi lời thoại kết thúc, NPC quay đầu đi về ---
        if (served.npcInstance != null)
        {
            StartCoroutine(MoveAwayAndDestroyRoutine(served.npcInstance, served.spawnPointIndex, served.customSpawnPoint));
        }
        else
        {
            currentCustomerState = CustomerState.None;
            if (isActive && !isCooldownActive)
            {
                spawnLoopCoroutine = StartCoroutine(SpawnLoopWithCooldown());
            }
        }

        Log($"Served customer with dialogue: {served.displayName}");
    }

    public void ClearQueue()
    {
        StopAllCoroutines();

        foreach (Customer customer in customerQueue)
        {
            if (customer.npcInstance != null)
            {
                Destroy(customer.npcInstance);
            }
        }

        customerQueue.Clear();
        currentCustomerArrivedAtStall = false;
        isCooldownActive = false;
        nextPrefabIndex = 0;
        OnQueueChanged?.Invoke(0);
    }

    private IEnumerator SpawnLoopWithCooldown()
    {
        if (customerQueue.Count > 0) yield break;

        isCooldownActive = true;
        float cooldown = nextCustomerId == 1 ? initialSpawnDelay : UnityEngine.Random.Range(spawnIntervalMin, spawnIntervalMax);
        yield return new WaitForSeconds(cooldown);
        isCooldownActive = false;

        if (!isActive) yield break;
        SpawnSingleCustomer();
    }

    private void SpawnSingleCustomer()
    {
        GameObject prefab = GetCustomerPrefab();
        if (prefab == null || spawnPoint == null || targetQueuePoint == null)
        {
            Log("Missing customer prefab, spawn point, or target queue point.");
            return;
        }

        string displayName = prefab != null ? GetPrettyName(prefab.name) : $"Customer {nextCustomerId}";

        int spawnIdx = UnityEngine.Random.Range(0, 2);
        Transform selectedSpawn = spawnIdx == 0 ? spawnPoint1 : spawnPoint2;
        if (selectedSpawn == null) selectedSpawn = spawnPoint;

        // Mê Liu (Miu Le) đi ra từ Villa thay vì đầu hẻm khi câu chuyện Phase 2 yêu cầu.
        Transform customSpawn = null;
        if (routeMeLiuFromVilla && villaSpawnPoint != null && IsMeLiu(prefab.name))
        {
            customSpawn = villaSpawnPoint;
            selectedSpawn = villaSpawnPoint;
            Log("Mê Liu xuất phát từ Villa.");
        }

        // Miu Le model bind pose feet sink 0.142m below Y=0 due to FBX calibration.
        // Apply a spawn Y offset to compensate and keep her feet on the ground.
        Vector3 spawnPos = selectedSpawn.position;
        if (IsMeLiu(prefab.name))
        {
            spawnPos.y += 0.09f;
        }

        Customer newCustomer = new Customer
        {
            customerId = nextCustomerId++,
            displayName = displayName,
            npcInstance = Instantiate(prefab, spawnPos, selectedSpawn.rotation),
            spawnPointIndex = spawnIdx,
            customSpawnPoint = customSpawn
        };

        ParentToCharactersGroup(newCustomer.npcInstance);
        ConfigureNpcAnimator(newCustomer.npcInstance);
        customerQueue.Enqueue(newCustomer);

        if (newCustomer.npcInstance != null)
        {
            StartCoroutine(MoveToStallRoutine(newCustomer));
        }
    }

    // Cache nhóm "=== CHARACTERS & VEHICLES ===" để gom các NPC clone vào, tránh để lộ thiên ngoài Hierarchy.
    private Transform charactersGroupRoot;

    /// <summary>
    /// Gán parent của một NPC clone vào nhóm "=== CHARACTERS & VEHICLES ===" để Hierarchy luôn gọn gàng
    /// (giữ nguyên vị trí world). Nếu không tìm thấy nhóm thì bỏ qua an toàn, để NPC ở root như cũ.
    /// </summary>
    private void ParentToCharactersGroup(GameObject npc)
    {
        if (npc == null) return;

        if (charactersGroupRoot == null)
        {
            GameObject group = GameObject.Find("=== CHARACTERS & VEHICLES ===");
            if (group != null) charactersGroupRoot = group.transform;
        }

        if (charactersGroupRoot != null)
        {
            npc.transform.SetParent(charactersGroupRoot, true); // worldPositionStays = true
        }
    }

    private IEnumerator MoveToStallRoutine(Customer customer)
    {
        GameObject npc = customer.npcInstance;
        if (npc == null) yield break;

        Animator anim = npc.GetComponent<Animator>();
        SetAnimatorMoving(anim, true);

        // Cấu hình đường đi cho Mê Liu từ Villa: đi dọc theo hẻm phía Tây tránh xuyên nhà
        bool fromVilla = customer.customSpawnPoint != null;

        if (fromVilla)
        {
            // Đi qua các góc hẻm phía Tây (X = -10)
            yield return MoveNpcToPosition(npc, new Vector3(-10f, npc.transform.position.y, 11.3f));
            yield return MoveNpcToPosition(npc, new Vector3(-10f, npc.transform.position.y, -2.75f));
            if (approachPoint != null)
            {
                yield return MoveNpcToPosition(npc, approachPoint.position);
            }
        }
        else
        {
            if (cornerPoint != null)
            {
                yield return MoveNpcToPosition(npc, cornerPoint.position);
            }

            if (roadApproachPoint != null)
            {
                yield return MoveNpcToPosition(npc, roadApproachPoint.position);
            }

            if (approachPoint != null)
            {
                yield return MoveNpcToPosition(npc, approachPoint.position);
            }
        }

        yield return MoveNpcToPosition(npc, targetQueuePoint.position);

        if (npc == null) yield break;

        SetAnimatorMoving(anim, false);
        yield return RotateNpcTo(npc.transform, targetQueuePoint.rotation);

        currentCustomerArrivedAtStall = true;
        OnCustomerArrived?.Invoke(customer);
        OnQueueChanged?.Invoke(customerQueue.Count);
        Log($"Customer arrived: {customer.displayName}");

        // Khách chỉ chào sau khi người chơi tới gần và chủ động bấm E.
        currentCustomerState = CustomerState.ArrivedWaitingToTalk;
    }

    private IEnumerator MoveNpcToPosition(GameObject npc, Vector3 destination)
    {
        float timer = 0f;
        float timeout = 12f; // Tối đa 12 giây cho mỗi waypoint
        Vector3 lastPos = npc != null ? npc.transform.position : Vector3.zero;
        float stuckTimer = 0f;

        // Đảm bảo applyRootMotion tắt khi bắt đầu di chuyển
        Animator npcAnim = npc?.GetComponent<Animator>();
        if (npcAnim != null) npcAnim.applyRootMotion = false;

        while (npc != null && GetPlanarDistance(npc.transform.position, destination) > arrivalDistance)
        {
            // Bắt buộc applyRootMotion=false mỗi frame trong lúc di chuyển
            if (npcAnim != null && npcAnim.applyRootMotion)
                npcAnim.applyRootMotion = false;

            MoveNpcToward(npc.transform, destination, true);

            // Kiểm tra kẹt: nếu không di chuyển quá 0.02m trong 2 giây liên tiếp
            if (Vector3.Distance(npc.transform.position, lastPos) < 0.02f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = 0f;
                lastPos = npc.transform.position;
            }

            timer += Time.deltaTime;
            if (timer > timeout || stuckTimer > 2f)
            {
                Debug.LogWarning($"[CustomerManager] NPC {npc.name} bị kẹt hoặc quá thời gian di chuyển tới {destination}. Nhảy thẳng tới đích.");
                npc.transform.position = destination;
                break;
            }
            yield return null;
        }

        if (npc == null) yield break;
    }

    private IEnumerator MoveAwayAndDestroyRoutine(GameObject npc, int spawnPointIndex, Transform customReturnPoint = null)
    {
        currentCustomerState = CustomerState.None;

        if (isActive && !isCooldownActive)
        {
            spawnLoopCoroutine = StartCoroutine(SpawnLoopWithCooldown());
        }

        Animator anim = npc.GetComponent<Animator>();
        SetAnimatorMoving(anim, true);

        // Cấu hình đường đi quay về Villa cho Mê Liu: đi ngược lại qua hẻm phía Tây tránh xuyên nhà
        bool toVilla = customReturnPoint != null;

        if (toVilla)
        {
            if (approachPoint != null)
            {
                yield return MoveNpcToPosition(npc, approachPoint.position);
            }
            // Đi qua các góc hẻm phía Tây (X = -10)
            yield return MoveNpcToPosition(npc, new Vector3(-10f, npc.transform.position.y, -2.75f));
            yield return MoveNpcToPosition(npc, new Vector3(-10f, npc.transform.position.y, 11.3f));
        }
        else
        {
            if (approachPoint != null)
            {
                yield return MoveNpcToPosition(npc, approachPoint.position);
            }

            if (roadApproachPoint != null)
            {
                yield return MoveNpcToPosition(npc, roadApproachPoint.position);
            }

            if (cornerPoint != null)
            {
                yield return MoveNpcToPosition(npc, cornerPoint.position);
            }
        }

        Transform selectedSpawn = toVilla
            ? customReturnPoint
            : (spawnPointIndex == 0 ? spawnPoint1 : spawnPoint2);
        Vector3 targetDest = selectedSpawn != null ? selectedSpawn.position : (spawnPoint != null ? spawnPoint.position : npc.transform.position);

        float elapsed = 0f;
        float exitTimeout = 12f;
        while (npc != null && GetPlanarDistance(npc.transform.position, targetDest) > 0.4f)
        {
            MoveNpcToward(npc.transform, targetDest, false);
            elapsed += Time.deltaTime;
            if (elapsed > exitTimeout)
            {
                break;
            }
            yield return null;
        }

        if (npc != null)
        {
            Destroy(npc);
        }
    }

    private void MoveNpcToward(Transform npcTransform, Vector3 targetPosition, bool slowNearDestination)
    {
        // Chỉ tính hướng theo mặt phẳng XZ — bỏ qua Y để tránh NPC bị đẩy lên/xuống
        Vector3 direction = targetPosition - npcTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float speedMultiplier = 1f;
        if (slowNearDestination && slowDownDistance > arrivalDistance)
        {
            float distance = direction.magnitude;
            speedMultiplier = Mathf.Clamp01((distance - arrivalDistance) / (slowDownDistance - arrivalDistance));
            speedMultiplier = Mathf.Max(0.2f, speedMultiplier);
        }

        Vector3 step = direction.normalized * walkSpeed * speedMultiplier * Time.deltaTime;
        if (step.sqrMagnitude > direction.sqrMagnitude)
        {
            step = direction;
        }

        // Di chuyển chỉ theo XZ — giữ nguyên Y hiện tại để tránh animation root motion gây giật theo trục Y
        Vector3 newPos = npcTransform.position + step;
        newPos.y = npcTransform.position.y; // Khoá Y — script không cho phép animation điều khiển Y
        npcTransform.position = newPos;

        // Rotate mượt về phía đích
        npcTransform.rotation = Quaternion.Slerp(
            npcTransform.rotation,
            Quaternion.LookRotation(direction.normalized),
            Time.deltaTime * turnSpeed);

        // Đảm bảo Root Motion vẫn tắt sau mỗi frame (một số Animator state có thể tự bật lại)
        Animator anim = npcTransform.GetComponent<Animator>();
        if (anim != null && anim.applyRootMotion)
            anim.applyRootMotion = false;
    }

    private IEnumerator RotateNpcTo(Transform npcTransform, Quaternion targetRotation)
    {
        while (npcTransform != null && Quaternion.Angle(npcTransform.rotation, targetRotation) > 1f)
        {
            npcTransform.rotation = Quaternion.Slerp(npcTransform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            yield return null;
        }

        if (npcTransform != null)
        {
            npcTransform.rotation = targetRotation;
        }
    }

    private float GetPlanarDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private GameObject GetCustomerPrefab()
    {
        if (customerPrefabs != null && customerPrefabs.Length > 0)
        {
            GameObject prefab = customerPrefabs[nextPrefabIndex];
            Log($"Spawning prefab at index {nextPrefabIndex}: {(prefab != null ? prefab.name : "null")}");
            
            nextPrefabIndex = (nextPrefabIndex + 1) % customerPrefabs.Length;
            
            if (prefab != null)
            {
                return prefab;
            }
        }

        return customerPrefab;
    }

    private void ConfigureNpcAnimator(GameObject npc)
    {
        if (npc == null) return;

        Animator anim = npc.GetComponent<Animator>();
        if (anim != null)
        {
            // Luôn tắt Root Motion để script điều khiển vị trí, tránh animation làm giật NPC
            anim.applyRootMotion = false;
            // AnimatorUpdateMode.Fixed — đồng bộ Animator với FixedUpdate, tránh NPC bị "giật" khi deltaTime không đều
            anim.updateMode = AnimatorUpdateMode.Normal;
            SetAnimatorMoving(anim, false);
        }
    }

    private void SetAnimatorMoving(Animator anim, bool moving)
    {
        if (anim == null) return;

        // Luôn đảm bảo Root Motion tắt — một số Unity internal system có thể bật lại
        anim.applyRootMotion = false;

        if (!string.IsNullOrWhiteSpace(blendParameterName))
        {
            anim.SetFloat(blendParameterName, moving ? 0.5f : 0f);
        }

        anim.speed = 1f;
        string stateName = moving ? walkStateName : idleStateName;
        if (!string.IsNullOrWhiteSpace(stateName) && anim.HasState(0, Animator.StringToHash(stateName)))
        {
            // Dùng Play() thay vì CrossFade() — snap thẳng vào state không qua blend time.
            // CrossFade(0.15f) khi blend giữa 2 states có hasRootCurves=True sẽ gây giật
            // do root delta bị áp dụng trong suốt quá trình blend dù applyRootMotion=false.
            var currentInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool alreadyInState = currentInfo.IsName(stateName);
            if (!alreadyInState)
            {
                anim.Play(stateName, 0, 0f);
                // Flush Animator ngay lập tức để tránh pending update gây giật frame tiếp theo
                anim.Update(0f);
            }
            return;
        }

        // Fallback: freeze animation khi idle
        if (!moving) anim.speed = 0f;
    }

    private static bool IsMeLiu(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        string n = prefabName.ToLower();
        return n.Contains("miu") || n.Contains("meliu") || n.Contains("me liu");
    }

    public static string GetPrettyName(string prefabName)
    {
        return prefabName switch
        {
            "anhxamminh" => "Anh Xăm Mình",
            "Nganpc" => "Bà Nga",
            "Npc1" => "Cổ động viên",
            "Npc3" => "Bác Tư",
            "anh_aoxanh" => "Anh áo xanh",
            "shipper" => "Shipper",
            "Miu Le" => "Mê Liu",
            _ => prefabName
        };
    }

    private void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[CustomerManager] {message}");
        }
    }
}
