using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// PHẦN 1 — GAMEPLAY ĐỘT NHẬP VILLA (NGƯỜI CHƠI ĐIỀU KHIỂN), Đêm Ngày 2.
///
/// Chuỗi sự kiện (được StoryPhase2Manager kích hoạt sau khi MẬT PHỤC xong):
///   1. Đột nhập tầng trệt → spawn 3 chứng cứ (ma tuý đá / kim tiêm / bình boong)
///      để người chơi tương tác thu thập (phím E hoặc click chuột).
///   2. Ra cửa sau hướng sân vườn + nhấn [E]:
///        - Chưa đủ 3 chứng cứ  → chặn, thoại nội tâm "cửa khóa chặt từ bên ngoài".
///        - Đã đủ 3 chứng cứ    → phát hiện cửa sau bị khóa, mở lối vòng bên hông + bật GardenTrigger ngoài sân.
///   3. Vào hẳn trong GardenTrigger + nhấn [E] (gọi đồng đội) → khóa Player +
///      StoryPhase2Manager.CompletePhase2Gameplay() (điểm nối Cutscene Phần 2 &amp; 3).
/// </summary>
[DisallowMultipleComponent]
public class VillaInvestigation : MonoBehaviour
{
    public enum GameplayState
    {
        CollectingEvidence,
        ReadyToCallTeam
    }

    // ===================================================================
    // SINGLETON
    // ===================================================================
    public static VillaInvestigation Instance { get; private set; }

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("=== Prefabs Vật chứng ===")]
    [SerializeField] private GameObject maithuyPrefab;
    [SerializeField] private GameObject syringePrefab;
    [SerializeField] private GameObject binhbongPrefab;

    [Header("=== Assets EvidenceData ===")]
    [SerializeField] private EvidenceData maithuyData;
    [SerializeField] private EvidenceData syringeData;
    [SerializeField] private EvidenceData binhbongData;

    [Header("=== Nghi phạm trong Villa ===")]
    [SerializeField] private GameObject miuLePrefab;
    [SerializeField] private GameObject huySeoPrefab;
    [SerializeField] private Vector3 miuLePos = new Vector3(-6.3f, 0.05f, 23.8f);
    [SerializeField] private Vector3 huySeoPos = new Vector3(-5.4f, 0.05f, 23.8f);

    [Header("=== Vị trí chứng cứ (tầng trệt) ===")]
    [SerializeField] private Vector3 maithuyPos = new Vector3(-7.1f, 0.89f, 22.8f);
    [SerializeField] private Vector3 syringePos = new Vector3(-7.3f, 0.89f, 22.5f);
    [SerializeField] private Vector3 binhbongPos = new Vector3(-4.9f, 0.485f, 24.3f);

    [Header("=== Cổng chính vào Villa ===")]
    [Tooltip("Vị trí cổng/cửa chính Villa. Chỉ mở tương tác sau khi Mê Liu đã vào trong.")]
    [SerializeField] private Vector3 frontDoorPos = new Vector3(-5f, 0.15f, 13.5f);
    [Tooltip("Điểm đặt người chơi ngay trong tầng trệt sau khi lẻn qua cổng chính.")]
    [SerializeField] private Vector3 villaEntryPos = new Vector3(-5.5f, 0.15f, 18f);
    [SerializeField] private float frontDoorInteractRange = 3f;

    [Header("=== Cửa sau ra sân vườn ===")]
    [Tooltip("Vị trí cửa sau biệt thự (hướng ra sân vườn). Người chơi tới đây nhấn [E] để mở.")]
    [SerializeField] private Vector3 backDoorPos = new Vector3(-5.5f, 1.0f, 25.5f);
    [Tooltip("Bán kính phát hiện người chơi đứng cạnh cửa sau.")]
    [SerializeField] private float backDoorInteractRange = 2.5f;

    [Header("=== GardenTrigger (vòng sáng xanh gọi đồng đội) ===")]
    [Tooltip("Vị trí vòng sáng xanh GardenTrigger ngoài bãi cỏ sân vườn sau biệt thự.")]
    [SerializeField] private Vector3 gardenTriggerPos = new Vector3(-3.0f, 0.05f, 27.3f);
    [Tooltip("Bán kính vòng GardenTrigger.")]
    [SerializeField] private float gardenTriggerRadius = 1.0f;

    [Header("=== Tương tác ===")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    // ===================================================================
    // RUNTIME STATE
    // ===================================================================

    private const int RequiredEvidence = 3;

    private int collectedCount = 0;
    private bool investigationStarted = false;
    private bool allCollected = false;
    private bool backDoorOpened = false;
    private bool phase1Completed = false;
    [SerializeField] private GameplayState currentState = GameplayState.CollectingEvidence;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private GameObject frontDoorObj;
    private GameObject backDoorObj;
    private BoxCollider backDoorBlocker;
    private GameObject backDoorHintObj;
    private GameObject gardenTriggerObj;

    public bool CanOpenBackDoor => allCollected;
    public bool IsBackDoorOpened => backDoorOpened;
    public int EvidenceCount => collectedCount;
    public GameplayState CurrentState => currentState;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    /// <summary>
    /// Bắt đầu gameplay đột nhập tầng trệt (gọi từ StoryPhase2Manager sau khi mật phục xong).
    /// </summary>
    public void StartInvestigation()
    {
        if (investigationStarted) return;
        investigationStarted = true;

        collectedCount = 0;
        allCollected = false;
        backDoorOpened = false;
        phase1Completed = false;
        SetState(GameplayState.CollectingEvidence);

        Debug.Log("[VillaInvestigation] Bắt đầu đột nhập & thu thập chứng cứ tầng trệt.");

        ResolveEditorReferences();
        CleanupSpawned();
        SpawnSuspects();
        SpawnEvidenceItems();
        CreateFrontDoor();
        CreateBackDoor();

        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Mê Liu đã vào trong. Tới cổng chính, nhấn E để lẻn vào tầng trệt và tìm đủ 3 chứng cứ.", 5f);
    }

    /// <summary>Được gọi bởi cổng chính sau cảnh Mê Liu đi vào Villa.</summary>
    public void EnterVilla(Transform player)
    {
        if (!investigationStarted || player == null) return;

        GateInteract gate = GameObject.Find("Gate_Interactive")?.GetComponent<GateInteract>();
        if (gate != null) gate.OpenForStory();

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.position = villaEntryPos;
        if (controller != null) controller.enabled = true;

        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Đã vào được tầng trệt. Thu thập đủ tinh thể ma túy đá, đồ thủy tinh và các chứng cứ còn lại.", 5f);
    }

    /// <summary>Được gọi bởi VillaEvidenceItem khi người chơi nhặt một bằng chứng.</summary>
    public void OnEvidenceCollected(VillaEvidenceItem item)
    {
        AddEvidence();
    }

    public void AddEvidence()
    {
        if (collectedCount >= RequiredEvidence) return;

        collectedCount = Mathf.Min(collectedCount + 1, RequiredEvidence);
        Debug.Log($"Collected evidence: {collectedCount}/{RequiredEvidence}");
        Debug.Log($"Evidence count: {collectedCount}/{RequiredEvidence}");

        if (collectedCount >= RequiredEvidence)
        {
            SetState(GameplayState.ReadyToCallTeam);
            CompleteCollection();
        }
        else if (InternalMonologueManager.Instance != null)
        {
            InternalMonologueManager.Instance.Show(
                $"Manh mối rất quan trọng! Vẫn còn {RequiredEvidence - collectedCount} thứ cần tìm quanh đây.", 3.5f);
        }
    }

    private void SetState(GameplayState newState)
    {
        currentState = newState;
        Debug.Log($"[VillaInvestigation] GameplayState = {currentState}");
    }

    /// <summary>[DEV CHEAT] Ép hoàn thành thu thập chứng cứ.</summary>
    public void Cheat_ForceCompleteCollection()
    {
        collectedCount = RequiredEvidence;
        SetState(GameplayState.ReadyToCallTeam);
        CompleteCollection();
    }

    /// <summary>
    /// Được gọi bởi <see cref="VillaBackDoor"/> khi người chơi nhấn [E] tại cửa sau.
    /// Chặn nếu chưa thu thập đủ 3 chứng cứ; nếu đủ thì mở cửa + bật GardenTrigger.
    /// </summary>
    public void TryOpenBackDoor()
    {
        if (!allCollected)
        {
            if (InternalMonologueManager.Instance != null)
                InternalMonologueManager.Instance.Show(
                    "Cửa sau đã bị khóa chặt từ bên ngoài rồi, mình phải tìm cách thu thập đủ chứng cứ đã.",
                    5f);
            return;
        }

        if (backDoorOpened)
            return;

        // Cửa sau luôn bị khóa theo kịch bản. Chỉ sau khi người chơi kiểm tra cửa
        // mới mở lối đi vòng bên hông và hiện GardenTrigger ở sân sau.
        backDoorOpened = true;
        // Giữ nguyên barrier để chặn người chơi vượt quá vòng xanh đi tới chỗ Mê Liu/bàn tiệc
        // GameObject barrier = GameObject.Find("VillaGarden_DynamicBarrier");
        // if (barrier != null) barrier.SetActive(false);

        CreateGardenTrigger();
        Debug.Log("[VillaInvestigation] Cửa sau bị khóa. Đã mở lối đi vòng bên hông Villa.");

        // Bấm [E] ở cửa sau (bị khóa) → phát #4 (PlayerNight2CallTeam) vì audio #4 chứa
        // câu chỉ dẫn người chơi tới "vòng sáng xanh" để gọi đồng đội.
        float backDoorDuration = Mathf.Max(6f,
            MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerNight2CallTeam));
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Đường này bị chặn rồi. Phải lẻn ra sân vườn bằng lối khác — đứng vào vòng sáng xanh — gọi đồng đội thôi!",
                backDoorDuration);
    }

    /// <summary>
    /// Được gọi bởi <see cref="GardenCallTrigger"/> khi người chơi đứng trong GardenTrigger và
    /// nhấn [E] (Hành động Gọi đồng đội báo cáo) — KẾT THÚC PHẦN 1.
    /// </summary>
    public void OnGardenCallTeammates()
    {
        if (phase1Completed) return;
        phase1Completed = true;

        Debug.Log("[VillaInvestigation] Người chơi gọi đồng đội báo cáo — KẾT THÚC PHẦN 1.");

        float voiceDuration = MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerNight2CallTeamConfirmed);
        float displayDuration = Mathf.Max(4f, voiceDuration);

        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Đồng đội ơi, vào hỗ trợ ngay! Toàn bộ chứng cứ đã sẵn sàng!", displayDuration);

        // Điểm nối (Hook) sang Cutscene Phần 2 & 3.
        if (StoryPhase2Manager.Instance != null)
        {
            StoryPhase2Manager.Instance.CompletePhase2Gameplay();
        }
        else
        {
            // Fallback an toàn nếu không có manager trong scene.
            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) pm.IsMovementLocked = true;

            ThirdPersonCamera cam = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (cam != null) cam.IsRotationLocked = true;

            Debug.Log("=== PLAYBACK CUTSCENE NOW ===");
        }
    }

    // ===================================================================
    // INTERNAL GAMEPLAY FLOW
    // ===================================================================

    private void SpawnEvidenceItems()
    {
        // 1. Bịch ma tuý đá (Side table)
        if (maithuyPrefab != null)
        {
            GameObject go = Instantiate(maithuyPrefab, maithuyPos, Quaternion.Euler(0f, 45f, 0f));
            go.name = "Evidence_Crystal";
            VillaEvidenceItem evItem = go.AddComponent<VillaEvidenceItem>();
            evItem.Init(maithuyData, this);
            AddGlow(go, new Color(0.5f, 1f, 0.45f)); // Đèn xanh độc
            spawnedItems.Add(go);
        }

        // 2. Kim tiêm (Side table)
        if (syringePrefab != null)
        {
            GameObject go = Instantiate(syringePrefab, syringePos, Quaternion.Euler(0f, -30f, 0f));
            go.name = "Evidence_Bag";
            VillaEvidenceItem evItem = go.AddComponent<VillaEvidenceItem>();
            evItem.Init(syringeData, this);
            AddGlow(go, new Color(0.55f, 0.85f, 1f)); // Đèn xanh lạnh
            spawnedItems.Add(go);
        }

        // 3. Bình boong / đồ thủy tinh (Coffee table)
        if (binhbongPrefab != null)
        {
            GameObject go = Instantiate(binhbongPrefab, binhbongPos, Quaternion.identity);
            go.name = "Evidence_GlassTool";
            VillaEvidenceItem evItem = go.AddComponent<VillaEvidenceItem>();
            evItem.Init(binhbongData, this);
            AddGlow(go, new Color(1f, 0.7f, 0.3f)); // Đèn cam ấm
            spawnedItems.Add(go);
        }
    }

    private void ResolveEditorReferences()
    {
#if UNITY_EDITOR
        if (maithuyPrefab == null)
            maithuyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/bichmaithuy.prefab");
        if (syringePrefab == null)
            syringePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/syringe.prefab");
        if (binhbongPrefab == null)
            binhbongPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/binhbong.prefab");
        if (maithuyData == null)
            maithuyData = AssetDatabase.LoadAssetAtPath<EvidenceData>("Assets/Prefabs/EVD_BichMaiThuy.asset");
        if (syringeData == null)
            syringeData = AssetDatabase.LoadAssetAtPath<EvidenceData>("Assets/Prefabs/EVD_Syringe.asset");
        if (binhbongData == null)
            binhbongData = AssetDatabase.LoadAssetAtPath<EvidenceData>("Assets/Prefabs/EVD_BinhBong.asset");
        if (miuLePrefab == null)
            miuLePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Miu Le.prefab");
        if (huySeoPrefab == null)
            huySeoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Huy_seo.prefab");
#endif
    }

    private void SpawnSuspects()
    {
        if (miuLePrefab != null)
        {
            GameObject miuLe = Instantiate(miuLePrefab, miuLePos, Quaternion.Euler(0f, 180f, 0f));
            miuLe.name = "MiuLe_Villa";
            spawnedItems.Add(miuLe);
        }

        if (huySeoPrefab != null)
        {
            GameObject huySeo = Instantiate(huySeoPrefab, huySeoPos, Quaternion.Euler(0f, 180f, 0f));
            huySeo.name = "HuySeo_Villa";
            spawnedItems.Add(huySeo);
        }
    }

    private void AddGlow(GameObject host, Color color)
    {
        GameObject lightGo = new GameObject("EvidenceGlow");
        lightGo.transform.SetParent(host.transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.15f;
        lightGo.transform.localScale = Vector3.one;

        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = 1.2f;
        l.intensity = 0.45f;
        l.shadows = LightShadows.None;
    }

    private void CompleteCollection()
    {
        if (allCollected) return;
        allCollected = true;

        // Đủ 3/3 mới hướng người chơi tới kiểm tra cửa sau. Cửa và barrier bên hông
        // vẫn giữ nguyên cho tới khi người chơi thật sự bấm [E] tại cửa sau.
        CreateBackDoorHintZone();

        Debug.Log("[VillaInvestigation] Thu thập đủ 3/3 chứng cứ. Chờ người chơi kiểm tra cửa sau.");

        // Thu đủ 3 chứng cứ → phát #3 (PlayerNight2EvidenceComplete).
        float evidenceMonologueDuration = Mathf.Max(6f,
            MainGameplayVoiceover.PlayLine(MainGameplayVoiceKey.PlayerNight2EvidenceComplete));
        if (InternalMonologueManager.Instance != null)
            InternalMonologueManager.Instance.Show(
                "Đã đủ 3 chứng cứ đắt giá ở tầng trệt rồi. " +
                "Giờ phải lẻn ra ngoài vườn sau để gọi đồng đội tới hỗ trợ thôi!", evidenceMonologueDuration);
    }

    private void CreateBackDoorHintZone()
    {
        if (backDoorHintObj != null) return;

        backDoorHintObj = new GameObject("VillaBackDoorHint_LivingRoomTable");
        backDoorHintObj.transform.position = binhbongPos;

        SphereCollider trigger = backDoorHintObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.2f;

        VillaBackDoorHintZone hint = backDoorHintObj.AddComponent<VillaBackDoorHintZone>();
        hint.Configure(this, 2.2f, playerTag);
    }

    private void CreateBackDoor()
    {
        if (backDoorObj != null) return;

        backDoorObj = new GameObject("VillaBackDoor");
        backDoorObj.transform.position = backDoorPos;

        backDoorBlocker = backDoorObj.AddComponent<BoxCollider>();
        backDoorBlocker.isTrigger = false;
        backDoorBlocker.center = new Vector3(0f, 0.75f, 0f);
        backDoorBlocker.size = new Vector3(7f, 3.5f, 0.35f);

        SphereCollider sc = backDoorObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = backDoorInteractRange;

        VillaBackDoor door = backDoorObj.AddComponent<VillaBackDoor>();
        door.Configure(this, backDoorInteractRange, interactKey, playerTag);

        // Đèn chỉ báo cửa sau (cam = chưa mở được).
        AddMarkerLight(backDoorObj, new Color(1f, 0.55f, 0.2f), 2.0f, 0.6f);

        Debug.Log($"[VillaInvestigation] Đã tạo cửa sau tại {backDoorPos}.");
    }

    private void CreateFrontDoor()
    {
        if (frontDoorObj != null) return;

        frontDoorObj = new GameObject("VillaFrontDoor");
        frontDoorObj.transform.position = frontDoorPos;

        SphereCollider trigger = frontDoorObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = frontDoorInteractRange;

        VillaFrontDoor door = frontDoorObj.AddComponent<VillaFrontDoor>();
        door.Configure(this, frontDoorInteractRange, interactKey, playerTag);
        AddMarkerLight(frontDoorObj, new Color(0.35f, 0.8f, 1f), 2.5f, 1f);

        Debug.Log($"[VillaInvestigation] Cổng chính Villa đã sẵn sàng tương tác tại {frontDoorPos}.");
    }

    private void CreateGardenTrigger()
    {
        if (gardenTriggerObj != null) return;

        gardenTriggerObj = new GameObject("GardenTrigger");
        gardenTriggerObj.transform.position = gardenTriggerPos;

        SphereCollider sc = gardenTriggerObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = gardenTriggerRadius;

        GardenCallTrigger gct = gardenTriggerObj.AddComponent<GardenCallTrigger>();
        gct.Configure(this, interactKey, playerTag);

        // Vòng tròn XANH NHÌN THẤY ĐƯỢC trên mặt đất đánh dấu điểm gọi đồng đội,
        // kèm point-light cho glow ban đêm.
        AddGroundRing(gardenTriggerObj, new Color(0.25f, 1f, 0.55f), gardenTriggerRadius);
        AddMarkerLight(gardenTriggerObj, new Color(0.3f, 1f, 0.6f), gardenTriggerRadius * 3f, 0.2f);

        Debug.Log($"[VillaInvestigation] Đã bật GardenTrigger (vòng sáng xanh) tại {gardenTriggerPos}.");
    }

    /// <summary>
    /// Tạo một VÒNG/ĐĨA XANH nhìn thấy được nằm phẳng trên mặt đất (unlit, bán trong suốt) làm
    /// chỉ báo "đứng vào đây" cho điểm gọi đồng đội. Dùng Cylinder dẹt để luôn phẳng & hiện rõ ban đêm.
    /// </summary>
    private void AddGroundRing(GameObject host, Color color, float radius)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "GreenRing";

        // Bỏ collider để không cản người chơi (vùng kích hoạt đã có SphereCollider riêng).
        Collider col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);

        disc.transform.SetParent(host.transform, false);
        disc.transform.localPosition = Vector3.up * 0.04f;
        // Cylinder mặc định đường kính 1, cao 2 → ép dẹt và phóng theo bán kính.
        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

        MeshRenderer mr = disc.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = color; c.a = 0.45f;
        mat.color = c;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void AddMarkerLight(GameObject host, Color color, float range, float heightOffset)
    {
        GameObject g = new GameObject("Glow");
        g.transform.SetParent(host.transform, false);
        g.transform.localPosition = Vector3.up * heightOffset;

        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 2.5f;
        l.shadows = LightShadows.None;
    }

    private void CleanupSpawned()
    {
        foreach (var item in spawnedItems)
            if (item != null) Destroy(item);
        spawnedItems.Clear();

        var clones = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in clones)
        {
            if (c == null) continue;
            if (c.name == "bichmaithuy_villa" || c.name == "syringe_villa" || c.name == "binhbong_villa"
                || c.name == "Evidence_Crystal" || c.name == "Evidence_GlassTool" || c.name == "Evidence_Bag"
                || c.name == "MiuLe_Villa" || c.name == "HuySeo_Villa"
                || c.name == "VillaFrontDoor" || c.name == "VillaBackDoor"
                || c.name == "VillaBackDoorHint_LivingRoomTable" || c.name == "GardenTrigger")
            {
                Destroy(c);
            }
        }

        frontDoorObj = null;
        backDoorObj = null;
        backDoorBlocker = null;
        backDoorHintObj = null;
        gardenTriggerObj = null;
    }
}

/// <summary>
/// Cổng chính Villa. Sau cảnh Mê Liu đi vào trong, người chơi đứng gần cổng và nhấn E
/// để lẻn vào tầng trệt. Dùng kiểm tra khoảng cách dự phòng để prompt vẫn hoạt động
/// nếu CharacterController không phát OnTriggerEnter.
/// </summary>
public class VillaFrontDoor : MonoBehaviour
{
    private VillaInvestigation manager;
    private float range = 3f;
    private KeyCode key = KeyCode.E;
    private string playerTag = "Player";
    private Transform player;
    private VillaEvidenceUI ui;
    private bool playerInside;
    private bool used;

    public void Configure(VillaInvestigation m, float r, KeyCode k, string tag)
    {
        manager = m;
        range = r;
        key = k;
        playerTag = tag;
    }

    private void Start()
    {
        EnsurePlayer();
        EnsureUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = false;
        if (ui != null) ui.HidePrompt(this);
    }

    private void Update()
    {
        if (used) return;
        EnsurePlayer();
        bool near = playerInside || (player != null && Vector3.Distance(player.position, transform.position) <= range);
        if (!near)
        {
            if (ui != null) ui.HidePrompt(this);
            return;
        }

        if (ui != null)
            ui.ShowPrompt(this, $"Nhấn <color=#FFFF00>[{key}]</color> để mở cửa và lẻn vào Villa");

        if (Input.GetKeyDown(key) && manager != null)
        {
            used = true;
            if (ui != null) ui.HidePrompt(this);
            manager.EnterVilla(player);
        }
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    private void EnsureUI()
    {
        if (ui != null) return;
        ui = VillaEvidenceUI.GetOrCreate();
    }

    private void OnDestroy()
    {
        if (ui != null) ui.HidePrompt(this);
    }
}

/// <summary>
/// Cửa sau biệt thự hướng ra sân vườn. Khi người chơi đứng gần và nhấn [E] sẽ báo về
/// <see cref="VillaInvestigation.TryOpenBackDoor"/> (chặn nếu chưa đủ chứng cứ).
/// </summary>
public class VillaBackDoor : MonoBehaviour
{
    private VillaInvestigation manager;
    private float range = 2.5f;
    private KeyCode key = KeyCode.E;
    private string playerTag = "Player";

    private Transform player;
    private VillaEvidenceUI ui;
    private bool playerInside = false;

    public void Configure(VillaInvestigation m, float r, KeyCode k, string tag)
    {
        manager = m;
        range = r;
        key = k;
        playerTag = tag;
    }

    private void Start()
    {
        EnsureUI();
        EnsurePlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
            if (ui != null) ui.HidePrompt(this);
        }
    }

    private void Update()
    {
        EnsurePlayer();

        bool near = playerInside;
        if (!near && player != null)
            near = Vector3.Distance(player.position, transform.position) <= range;

        bool canInteract = manager != null && manager.CanOpenBackDoor && !manager.IsBackDoorOpened;
        if (near && canInteract)
        {
            if (ui != null)
                ui.ShowPrompt(this, "Nhấn <color=#FFFF00>[E]</color> để kiểm tra cửa sau");

            if (Input.GetKeyDown(key))
                manager.TryOpenBackDoor();
        }
        else if (ui != null)
        {
            ui.HidePrompt(this);
        }
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        // Tìm player thật qua PlayerMovement để tránh nhầm với object khác cũng gắn tag "Player".
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    private void EnsureUI()
    {
        if (ui != null) return;
        ui = VillaEvidenceUI.GetOrCreate();
    }

    private void OnDestroy()
    {
        if (ui != null) ui.HidePrompt(this);
    }
}

/// <summary>
/// Điểm nhắc tại bàn phòng khách sau khi đã nhặt đủ 3/3 chứng cứ.
/// Chỉ dẫn người chơi quay tới cửa sau; không mở cửa từ xa.
/// </summary>
public class VillaBackDoorHintZone : MonoBehaviour
{
    private VillaInvestigation manager;
    private float range = 2.2f;
    private string playerTag = "Player";
    private Transform player;
    private VillaEvidenceUI ui;
    private bool playerInside;

    public void Configure(VillaInvestigation m, float r, string tag)
    {
        manager = m;
        range = r;
        playerTag = tag;
    }

    private void Start()
    {
        EnsurePlayer();
        ui = VillaEvidenceUI.GetOrCreate();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = false;
        if (ui != null) ui.HidePrompt(this);
    }

    private void Update()
    {
        EnsurePlayer();
        bool near = playerInside || (player != null && Vector3.Distance(player.position, transform.position) <= range);
        bool shouldGuide = manager != null && manager.CanOpenBackDoor && !manager.IsBackDoorOpened;

        if (near && shouldGuide)
        {
            if (ui != null) ui.ShowPrompt(this, "Đã đủ 3 chứng cứ — hãy tới kiểm tra cửa sau");
        }
        else if (ui != null)
        {
            ui.HidePrompt(this);
        }
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    private void OnDestroy()
    {
        if (ui != null) ui.HidePrompt(this);
    }
}

/// <summary>
/// Vòng sáng xanh GardenTrigger ngoài sân vườn. Khi người chơi đứng hẳn bên trong và nhấn [E]
/// (Gọi đồng đội báo cáo) sẽ báo về <see cref="VillaInvestigation.OnGardenCallTeammates"/>.
/// </summary>
public class GardenCallTrigger : MonoBehaviour
{
    private VillaInvestigation manager;
    private KeyCode key = KeyCode.E;
    private string playerTag = "Player";

    private VillaEvidenceUI ui;
    private Transform player;
    private bool playerInside = false;
    private bool used = false;

    public void Configure(VillaInvestigation m, KeyCode k, string tag)
    {
        manager = m;
        key = k;
        playerTag = tag;
    }

    private void Start()
    {
        EnsureUI();
        EnsurePlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
            if (ui != null) ui.HidePrompt(this);
        }
    }

    private void Update()
    {
        if (used) return;

        EnsurePlayer();
        bool near = playerInside || (player != null && Vector3.Distance(player.position, transform.position) <= GetComponent<SphereCollider>().radius);

        if (near)
        {
            if (ui != null)
                ui.ShowPrompt(this, $"Nhấn <color=#FFFF00>[{key}]</color> để gọi đồng đội báo cáo");

            if (Input.GetKeyDown(key))
            {
                used = true;
                if (ui != null) ui.HidePrompt(this);
                if (manager != null) manager.OnGardenCallTeammates();
            }
        }
        else if (ui != null)
        {
            ui.HidePrompt(this);
        }
    }

    private void EnsureUI()
    {
        if (ui != null) return;
        ui = VillaEvidenceUI.GetOrCreate();
    }

    private void EnsurePlayer()
    {
        if (player != null) return;
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    private void OnDestroy()
    {
        if (ui != null) ui.HidePrompt(this);
    }
}
