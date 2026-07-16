using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý quy trình cuốn bánh Bò Bía theo từng bước tuần tự:
///   Bước 0: Bánh tráng (BanhTrang)
///   Bước 1: Kẹo mạch nha (KeoMachNha)
///   Bước 2: Dừa (Dua)
///   Bước 3: Mè (Me)
///
/// Điều kiện kích hoạt:
///   1. Pha hiện tại là DAY (GameTimeManager.CurrentPhase == DAY).
///   2. Người chơi đứng trong vùng proximity của quầy hàng.
///   3. Có ít nhất một khách đang đợi (CustomerManager.HasCustomerWaiting).
///   4. Người chơi nhấn phím tương tác (mặc định: E).
///
/// Tách biệt rõ ràng:
///   - BoBiaMechanic  : logic cuốn bánh + điều kiện kích hoạt.
///   - CustomerManager: quản lý hàng đợi khách (không kết hợp ở đây).
///   - GameTimeManager: quản lý pha DAY/NIGHT.
/// </summary>
public class BoBiaMechanic : MonoBehaviour
{
    // ===================================================================
    // ENUMS
    // ===================================================================

    /// <summary>Các bước trong quy trình lắp ráp và cuốn bánh Bò Bía ngọt.</summary>
    public enum CookingStep
    {
        None       = 0,
        AddPastry  = 1,
        AddPastry_Applying = 2,
        AddCandy   = 3,
        AddCandy_Applying = 4,
        AddCoconut = 5,
        AddCoconut_Applying = 6,
        AddSesame  = 7,
        AddSesame_Applying = 8,
        Rolling    = 9,
        Completed  = 10
    }

    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Phát sinh khi một bước cuốn bánh hoàn tất. Tham số: bước vừa xong.</summary>
    public event Action<CookingStep> OnStepCompleted;

    /// <summary>Phát sinh khi cuốn bánh hoàn tất. Tham số: tổng WaitTime.</summary>
    public event Action<float> OnRollingCompleted;

    /// <summary>Phát sinh khi quy trình reset về đầu.</summary>
    public event Action OnRollingReset;
    public event Action<float> OnRollingProgressChanged;
    public event Action<bool> OnRollingProgressVisibleChanged;

    /// <summary>
    /// Phát sinh khi trạng thái "người chơi có thể tương tác" thay đổi.
    /// Tham số: true = có thể tương tác, false = không thể.
    /// Dùng cho UI hiển thị prompt "[E] Cuốn bánh".
    /// </summary>
    public event Action<bool> OnInteractableChanged;

    // ===================================================================
    // INSPECTOR FIELDS
    // ===================================================================

    [Header("Thời Gian Thực Hiện Mỗi Bước (giây)")]
    [Tooltip("Thời gian cần để hoàn thành bước Bánh Tráng.")]
    [SerializeField] private float banhTrangDuration = 1.5f;

    [Tooltip("Thời gian cần để hoàn thành bước Kẹo Mạch Nha.")]
    [SerializeField] private float keoMachNhaDuration = 1.2f;

    [Tooltip("Thời gian cần để hoàn thành bước Dừa.")]
    [SerializeField] private float duaDuration = 1.0f;

    [Tooltip("Thời gian cần để hoàn thành bước Mè.")]
    [SerializeField] private float meDuration = 0.8f;

    [Header("Progress UI")]
    [Range(0.5f, 10f)]
    [SerializeField] private float totalRollingDuration = 3f;

    [Header("Thưởng WaitTime Khi Hoàn Thành Bước")]
    [Tooltip("Thời gian (giây) cộng thêm khi hoàn thành bước Bánh Tráng.")]
    [SerializeField] private float waitTimePerBanhTrang = 5f;

    [Tooltip("Thời gian (giây) cộng thêm khi hoàn thành bước Kẹo Mạch Nha.")]
    [SerializeField] private float waitTimePerKeoMachNha = 8f;

    [Tooltip("Thời gian (giây) cộng thêm khi hoàn thành bước Dừa.")]
    [SerializeField] private float waitTimePerDua = 10f;

    [Tooltip("Thời gian (giây) cộng thêm khi hoàn thành bước Mè.")]
    [SerializeField] private float waitTimePerMe = 15f;

    [Header("Điều Kiện Kích Hoạt Bán Hàng")]
    [Tooltip("Transform trung tâm quầy hàng. Dùng để kiểm tra khoảng cách người chơi.")]
    [SerializeField] private Transform stallCenter;

    [Tooltip("Khoảng cách tối đa (m) người chơi phải đứng gần quầy để tương tác.")]
    [Range(0.5f, 10f)]
    [SerializeField] private float interactRadius = 2.5f;

    [Tooltip("Transform của người chơi. Tự động tìm theo tag 'Player' nếu để null.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Phím tương tác để bắt đầu cuốn bánh.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Thưởng Điểm Số (Score)")]
    [Tooltip("Số điểm thưởng cộng vào CaseManager khi phục vụ khách hàng thành công.")]
    [SerializeField] private int scorePerCustomer = 15;

    [Header("Audio Feedback")]
    [Tooltip("Âm thanh phát khi hoàn thành cuốn bánh xong.")]
    [SerializeField] private AudioClip completionAudioClip;

    [Header("Camera Focus")]
    [Tooltip("Vị trí và góc nhìn camera khi bắt đầu cuốn bánh.")]
    [SerializeField] private Transform cookingCameraPosition;

    [Header("Tham Chiếu")]
    [Tooltip("CustomerManager cung cấp thông tin khách đang đợi.")]
    [SerializeField] private CustomerManager customerManager;

    // ===================================================================
    // PREFAB NGUYÊN LIỆU TRUNG GIAN (Process_Making_BoBia)
    // ===================================================================

    [Header("Prefab Quy Trình Làm Bò Bía")]
    [Tooltip("Prefab hiển thị sau khi chọn Xấp Bánh Bò Bía.")]
    [SerializeField] private GameObject prefab_LatBanhBoBia;

    [Tooltip("Prefab hiển thị sau khi chọn Thanh kẹo mạch nha (chồng lên lát bánh).")]
    [SerializeField] private GameObject prefab_ThanhKeoMachNha;

    [Tooltip("Prefab hiển thị sau khi chọn Hũ dừa nạo.")]
    [SerializeField] private GameObject prefab_DuaNao;

    [Tooltip("Prefab thay thế Dừa Nạo sau khi chọn Hũ mè đen.")]
    [SerializeField] private GameObject prefab_DuaNaoVoiMeDen;

    [Tooltip("Prefab cuối cùng — Bò Bía Hoàn chỉnh (cầm trên tay người chơi).")]
    [SerializeField] private GameObject prefab_BoBiaHoanChinh;

    [Header("Vị Trí Hiển Thị")]
    [Tooltip("Điểm hiển thị các prefab trung gian (trên mặt thớt xe). Tự tìm theo Xe_Bo_Bia_Model nếu để null.")]
    [SerializeField] private Transform ingredientDisplayPoint;

    [Tooltip("Điểm cầm bò bía trên tay người chơi (góc nhìn FPS). Nên là child của Camera.")]
    [SerializeField] private Transform firstPersonHandPoint;

    [Header("Crosshair Interaction")]
    [Tooltip("CrosshairInteractionSystem gắn trên Player hoặc Camera.")]
    [SerializeField] private CrosshairInteractionSystem crosshairSystem;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool showProximityGizmo = true;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    /// <summary>Bước cuốn bánh hiện tại.</summary>
    private CookingStep currentStep = CookingStep.None;

    /// <summary>Tổng WaitTime tích lũy trong phiên bán hàng.</summary>
    private float totalWaitTime = 0f;

    /// <summary>Đang trong quá trình cuốn bánh không.</summary>
    private bool isRolling = false;

    /// <summary>Coroutine đang chạy để có thể dừng khi cần.</summary>
    private Coroutine activeRollingCoroutine;

    /// <summary>Coroutine điều khiển di chuyển camera.</summary>
    private Coroutine activeLerpCoroutine;

    /// <summary>Người chơi hiện đang đứng trong vùng proximity.</summary>
    private bool isPlayerNearby = false;

    /// <summary>Trạng thái "có thể tương tác" của lần check trước (dùng để phát event khi thay đổi).</summary>
    private bool wasInteractable = false;

    private PlayerMovement playerMovement;

    // Trực quan kéo chuột (Drag to roll)
    private Vector3 dragStartPosition;
    private float rollProgress = 0f;
    private float applyProgress = 0f;

    private List<GameObject> spawnedToppings = new List<GameObject>();

    // ===================================================================
    // MULTI-ROLL STATE — Số lượng bánh theo đơn hàng
    // ===================================================================

    /// <summary>Tổng số bánh cần cuốn cho đơn hàng hiện tại (lấy từ customer.orderQuantity).</summary>
    private int requiredRollCount = 1;

    /// <summary>Số bánh đã cuốn xong trong đơn hàng hiện tại.</summary>
    private int completedRollCount = 0;

    // ===================================================================
    // TRẠNG THÁI QUY TRÌNH CLICK-BASED MỚI
    // ===================================================================

    /// <summary>Prefab trung gian đang hiển thị trên mặt thớt (lát bánh + kẹo).</summary>
    private List<GameObject> spawnedProcessPrefabs = new List<GameObject>();

    /// <summary>Bò bía hoàn chỉnh đang cầm trên tay người chơi (FPS hand item).</summary>
    private GameObject boBiaHandItem = null;

    /// <summary>Đang cầm bò bía hoàn chỉnh và chờ nhấn E để giao NPC không.</summary>
    private bool isHoldingBoBia = false;

    // ===================================================================
    // PROPERTIES
    // ===================================================================

    /// <summary>Bước cuốn bánh hiện tại.</summary>
    public CookingStep CurrentStep => currentStep;

    /// <summary>Có đang cầm bò bía hoàn chỉnh không.</summary>
    public bool IsHoldingBoBia => isHoldingBoBia;

    /// <summary>Tiến trình di chuột trải nguyên liệu hiện tại.</summary>
    public float ApplyProgress => applyProgress;

    /// <summary>Tiến trình cuốn bánh bằng kéo chuột.</summary>
    public float RollProgress => rollProgress;

    /// <summary>Tổng WaitTime tích lũy được.</summary>
    public float TotalWaitTime => totalWaitTime;

    /// <summary>Đang trong tiến trình cuốn bánh hay không.</summary>
    public bool IsRolling => isRolling;

    /// <summary>Người chơi đứng đủ gần quầy không.</summary>
    public bool IsPlayerNearby => isPlayerNearby;

    /// <summary>Số bánh đã hoàn thành của đơn hàng hiện tại.</summary>
    public int CompletedRollCount => completedRollCount;

    /// <summary>Tổng số bánh yêu cầu của đơn hàng hiện tại.</summary>
    public int RequiredRollCount => requiredRollCount;

    /// <summary>
    /// Kiểm tra tổng hợp tất cả điều kiện để bắt đầu cuốn bánh:
    ///   1. Pha DAY.
    ///   2. Người chơi ở gần.
    ///   3. Có khách đang chờ.
    ///   4. Chưa đang cuốn bánh.
    /// </summary>
    public bool CanStartRolling =>
        isPlayerNearby &&
        HasCustomerWaiting() &&
        CustomerManager.Instance != null &&
        CustomerManager.Instance.CurrentCustomerState == CustomerManager.CustomerState.ReadyToRoll &&
        !isRolling &&
        currentStep != CookingStep.Completed;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    public static BoBiaMechanic Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BoBiaMechanic] Phát hiện bản sao thứ hai! Đang cập nhật tham chiếu mới.");
        }
        Instance = this;

        // Tự tìm playerTransform theo tag nếu chưa gán
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
            else
                Debug.LogWarning("[BoBiaMechanic] Không tìm thấy GameObject có tag 'Player'.");
        }

        if (playerTransform != null)
        {
            playerMovement = playerTransform.GetComponent<PlayerMovement>();
        }

        // Dùng chính transform này làm stallCenter nếu chưa gán
        if (stallCenter == null)
            stallCenter = transform;
    }

    private void Update()
    {
        // 1. Cập nhật trạng thái proximity mỗi frame
        UpdateProximity();

        // 2. Cập nhật và phát event "interactable" nếu có thay đổi
        NotifyInteractableChange();

        // 3. Xử lý Input tùy thuộc vào đang bán hay nấu
        if (isRolling)
        {
            HandleCookingInput();
        }
        else
        {
            HandleInteractInput();
        }
    }



    // ===================================================================
    // PUBLIC METHODS
    // ===================================================================

    public void StartRolling()
    {
        if (!CanStartRolling)
        {
            LogCannotStartReason();
            return;
        }

        // Teleport người chơi đến vị trí X xanh — ngay trước mặt xe bò bía
        if (playerTransform != null)
        {
            // Dùng transform của BoBiaMechanic (Xe_Bo_Bia_Root) làm gốc tính toán,
            // KHÔNG dùng stallCenter (Player_Interaction_Spot) vì nó đã bị offset sẵn.
            Vector3 cartPos = transform.position; // Xe_Bo_Bia_Root world pos: (~0.25, 0.05, ~-0.675)
            // Đặt player 0.12m về phía Nam (Z âm hơn) và 0.22m sang Tây (X âm hơn) từ tâm xe.
            // Kết quả: (~0.03, 0.05, ~-0.80) — sát quầy hơn để khớp góc nhìn thớt cuốn bánh.
            Vector3 warpPos = new Vector3(cartPos.x - 0.22f, 0.05f, cartPos.z - 0.12f);
            Quaternion warpRot = Quaternion.Euler(0f, 90f, 0f); // nhìn về phía +X (vào xe)

            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerTransform.position = warpPos;
            playerTransform.rotation = warpRot;
            if (cc != null) cc.enabled = true;

            // Đồng bộ camera về đúng hướng nhìn vào xe
            var tpCamWarp = Camera.main?.GetComponent<ThirdPersonCamera>();
            if (tpCamWarp != null) tpCamWarp.ResetOrientation();

            Log($"Warped player to cooking position: {warpPos}");
        }

        // Tự động xoay camera chính hướng thẳng vào khu vực thớt nguyên liệu
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 lookTarget = GetProcessDisplayPosition();
            Vector3 direction = (lookTarget - mainCam.transform.position).normalized;
            mainCam.transform.rotation = Quaternion.LookRotation(direction);
            
            // Nếu có ThirdPersonCamera, đồng bộ hướng xoay để tránh camera bị giật ngược lại
            var tpCamFocus = mainCam.GetComponent<ThirdPersonCamera>();
            if (tpCamFocus != null)
            {
                tpCamFocus.ResetOrientation();
            }

            // Nếu có FirstPersonCamera, đồng bộ góc xoay xRotation để tránh camera bị giật ngược lại
            var fpCam = mainCam.GetComponent<FirstPersonCamera>();
            if (fpCam != null)
            {
                var euler = mainCam.transform.localRotation.eulerAngles;
                float xRot = euler.x;
                if (xRot > 180f) xRot -= 360f;
                
                fpCam.SetRotation(xRot);
            }
        }

        // Tạm thời tắt collider của Xe_Bo_Bia_Root để raycast không bị cản
        var xeRoot = GameObject.Find("Xe_Bo_Bia_Root");
        if (xeRoot != null)
        {
            var col = xeRoot.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Log("Tắt collider Xe_Bo_Bia_Root để hỗ trợ raycast.");
            }
        }

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = true; // Khóa di chuyển nhân vật để đứng im làm bánh
        }

        // KHÔNG khóa camera rotation — người chơi cần xoay để nhìn vào nguyên liệu
        ThirdPersonCamera tpCam = Camera.main?.GetComponent<ThirdPersonCamera>();
        if (tpCam != null)
        {
            tpCam.IsRotationLocked = false;
        }

        // GIỮ cursor locked để camera vẫn xoay được theo chuột (FPS style)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Kích hoạt hệ thống crosshair interaction
        if (crosshairSystem != null)
            crosshairSystem.Enable();
        else
        {
            // Tự tìm nếu chưa gán
            crosshairSystem = UnityEngine.Object.FindAnyObjectByType<CrosshairInteractionSystem>();
            crosshairSystem?.Enable();
        }

        activeRollingCoroutine = StartCoroutine(RollingCoroutine());
    }

    /// <summary>
    /// Reset toàn bộ quy trình về bước đầu tiên.
    /// Dùng khi khách mới đến hoặc khi bắt đầu lại.
    /// </summary>
    public void ForceReset()
    {
        if (activeRollingCoroutine != null)
        {
            StopCoroutine(activeRollingCoroutine);
            activeRollingCoroutine = null;
        }

        if (activeLerpCoroutine != null)
        {
            StopCoroutine(activeLerpCoroutine);
            activeLerpCoroutine = null;
        }

        bool wasRolling = isRolling;
        currentStep = CookingStep.None;
        rollProgress = 0f;
        applyProgress = 0f;
        totalWaitTime = 0f;
        isRolling = false;
        isHoldingBoBia = false;

        // Reset bộ đếm số lượng bánh theo đơn hàng
        requiredRollCount = 1;
        completedRollCount = 0;

        OnRollingProgressChanged?.Invoke(0f);
        OnRollingProgressVisibleChanged?.Invoke(false);

        // Khôi phục lại các topping đã sinh ra trên thớt (cũ)
        ClearSpawnedToppings();

        // Xóa các prefab trung gian quy trình làm bò bía mới
        ClearProcessPrefabs();

        // Bật lại collider Xe_Bo_Bia_Root sau khi hoàn thành/reset
        var xeRoot = GameObject.Find("Xe_Bo_Bia_Root");
        if (xeRoot != null)
        {
            var col = xeRoot.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
                Log("Đã bật lại collider Xe_Bo_Bia_Root.");
            }
        }

        // Xóa bò bía trên tay người chơi nếu còn
        if (boBiaHandItem != null)
        {
            Destroy(boBiaHandItem);
            boBiaHandItem = null;
        }

        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        // Tắt crosshair interaction
        crosshairSystem?.Disable();

        // Mở khóa xoay camera và đặt lại hướng nhìn
        ThirdPersonCamera tpCam = Camera.main?.GetComponent<ThirdPersonCamera>();
        if (tpCam != null)
        {
            tpCam.IsRotationLocked = false;
            tpCam.ResetOrientation();
        }

        // Khóa con trỏ chuột về trạng thái chơi bình thường
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Log("Đã reset quy trình cuốn bánh.");
        OnRollingReset?.Invoke();
    }

    // ===================================================================
    // TOPPING SPAWNING METHODS
    // ===================================================================

    private Vector3 GetPlatePosition()
    {
        GameObject model = GameObject.Find("Xe_Bo_Bia_Model");
        if (model != null)
        {
            // Đặt trực tiếp lên mặt thớt gỗ (khúc yên xe) của Xe_Bo_Bia_Model
            return model.transform.position + Vector3.up * 0.39f;
        }
        return stallCenter.position + stallCenter.forward * 0.4f + stallCenter.up * 0.82f;
    }

    private void ClearSpawnedToppings()
    {
        foreach (var topping in spawnedToppings)
        {
            if (topping != null)
            {
                Destroy(topping);
            }
        }
        spawnedToppings.Clear();
    }

    public void OnClickAddPastry()
    {
        if (currentStep != CookingStep.AddPastry) return;
        applyProgress = 0f;
        currentStep = CookingStep.AddPastry_Applying;
        OnRollingProgressChanged?.Invoke(0f);
    }

    public void OnClickAddCandy()
    {
        if (currentStep != CookingStep.AddCandy) return;
        applyProgress = 0f;
        currentStep = CookingStep.AddCandy_Applying;
        OnRollingProgressChanged?.Invoke(0f);
    }

    public void OnClickAddCoconut()
    {
        if (currentStep != CookingStep.AddCoconut) return;
        applyProgress = 0f;
        currentStep = CookingStep.AddCoconut_Applying;
        OnRollingProgressChanged?.Invoke(0f);
    }

    public void OnClickAddSesame()
    {
        if (currentStep != CookingStep.AddSesame) return;
        applyProgress = 0f;
        currentStep = CookingStep.AddSesame_Applying;
        OnRollingProgressChanged?.Invoke(0f);
    }

    // ===================================================================
    // CLICK-BASED INGREDIENT INTERACTION (Cơ Chế Mới)
    // ===================================================================

    /// <summary>
    /// Được gọi bởi IngredientInteractable khi người chơi click vào nguyên liệu.
    /// Kiểm tra đúng bước → spawn prefab trung gian → chuyển sang bước tiếp theo.
    /// </summary>
    public void OnClickIngredient(IngredientInteractable ingredient)
    {
        if (ingredient == null) return;
        if (currentStep != ingredient.RequiredStep)
        {
            Log($"Click sai thứ tự: cần {currentStep} nhưng bấm vào {ingredient.DisplayName} (yêu cầu {ingredient.RequiredStep})");
            return;
        }

        Log($"[Bước {currentStep}] Người chơi đã chọn: {ingredient.DisplayName}");

        switch (currentStep)
        {
            case CookingStep.AddPastry:
                // Bước 1: Xấp Bánh Bò Bía → spawn Lát Bánh
                SpawnProcessPrefab(prefab_LatBanhBoBia, Vector3.zero);
                AdvanceStep(CookingStep.AddCandy, waitTimePerBanhTrang);
                break;

            case CookingStep.AddCandy:
                // Bước 2: Thanh kẹo mạch nha → chồng Thanh Kẹo lên Lát Bánh
                SpawnProcessPrefab(prefab_ThanhKeoMachNha, Vector3.up * 0.02f);
                AdvanceStep(CookingStep.AddCoconut, waitTimePerKeoMachNha);
                break;

            case CookingStep.AddCoconut:
                // Bước 3: Hũ dừa nạo → spawn Dừa Nạo chồng lên
                SpawnProcessPrefab(prefab_DuaNao, Vector3.up * 0.04f);
                AdvanceStep(CookingStep.AddSesame, waitTimePerDua);
                break;

            case CookingStep.AddSesame:
                // Bước 4: Hũ mè đen → giữ nguyên dừa nạo và rắc mè đen lên trên
                if (spawnedProcessPrefabs.Count > 0)
                {
                    SprinkleBlackSesame(spawnedProcessPrefabs[spawnedProcessPrefabs.Count - 1]);
                }
                AdvanceStep(CookingStep.Rolling, waitTimePerMe);
                // Sau khi đủ 4 nguyên liệu → chuyển sang bước cuộn
                StartCoroutine(FinalizeBoBia());
                break;
        }
    }

    /// <summary>
    /// Coroutine hoàn tất việc làm bò bía sau khi đủ 4 nguyên liệu.
    /// Xóa prefab trung gian → spawn Bò Bía Hoàn chỉnh trên thớt → kiểm tra số lượng đơn.
    /// Nếu chưa đủ số lượng: hiển thị thông báo và để người chơi cuốn tiếp.
    /// Nếu đủ: hiển thị bánh trên tay người chơi → giao cho NPC.
    /// </summary>
    private System.Collections.IEnumerator FinalizeBoBia()
    {
        // 1. Chờ 1 giây để người chơi nhìn thấy nguyên liệu cuối cùng (mè đen) được thêm vào
        yield return new WaitForSeconds(1.0f);

        // 2. Xóa các nguyên liệu trung gian trên thớt
        ClearProcessPrefabs();

        // 3. Tạo bánh Bò Bía Hoàn chỉnh ngay trên thớt
        GameObject boBiaOnBoard = null;
        if (prefab_BoBiaHoanChinh != null)
        {
            boBiaOnBoard = Instantiate(prefab_BoBiaHoanChinh, GetProcessDisplayPosition(), Quaternion.Euler(0f, 90f, 0f));
            boBiaOnBoard.transform.localScale = prefab_BoBiaHoanChinh.transform.localScale;

            // Tắt collider để tránh va chạm vật lý kỳ lạ trên thớt
            foreach (var col in boBiaOnBoard.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        // 4. Chờ 1.5 giây để người chơi chiêm ngưỡng bánh bò bía đã cuốn hoàn chỉnh trên thớt
        yield return new WaitForSeconds(1.5f);

        // 5. Xóa bánh trên thớt
        if (boBiaOnBoard != null)
        {
            Destroy(boBiaOnBoard);
        }

        // Phát âm thanh hoàn thành một cái bánh
        if (completionAudioClip != null)
        {
            var src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.PlayOneShot(completionAudioClip);
        }

        // 6. Tăng bộ đếm số bánh đã cuốn xong
        completedRollCount++;
        Log($"Hoàn thành bánh {completedRollCount}/{requiredRollCount}.");

        // 7. Kiểm tra còn cần cuốn thêm không
        if (completedRollCount < requiredRollCount)
        {
            // --- Chưa đủ: cần cuốn thêm ---
            // QUAN TRỌNG: Dừng RollingCoroutine hiện tại để tránh chạy song song và gây bug đếm bánh khi bắt đầu vòng mới
            if (activeRollingCoroutine != null)
            {
                StopCoroutine(activeRollingCoroutine);
                activeRollingCoroutine = null;
            }

            // Hiển thị thông báo tiến trình cho người chơi
            DialogueScreenUI progressUI = UnityEngine.Object.FindAnyObjectByType<DialogueScreenUI>();
            if (progressUI != null)
            {
                string progressMsg = $"Đã cuốn <b>{completedRollCount}/{requiredRollCount}</b> cái. Nhấn <b>[E]</b> để cuốn tiếp!";
                progressUI.ForceShow(progressMsg, 3f);
            }
            else
            {
                Log($"[Tiến trình] Đã cuốn {completedRollCount}/{requiredRollCount} cái. Tiếp tục cuốn!");
            }

            // Reset về trạng thái chờ cuốn tiếp (nhấn E)
            currentStep = CookingStep.None;
            isRolling = false;

            // Bật lại collider Xe_Bo_Bia_Root
            var xeRootNext = GameObject.Find("Xe_Bo_Bia_Root");
            if (xeRootNext != null)
            {
                var colNext = xeRootNext.GetComponent<Collider>();
                if (colNext != null) colNext.enabled = true;
            }

            // Mở khóa di chuyển để người chơi có thể tương tác tiếp
            if (playerMovement != null)
            {
                playerMovement.IsMovementLocked = false;
            }

            // Tắt crosshair vì đã xong một vòng cuốn
            crosshairSystem?.Disable();

            // Khóa con trỏ chuột về trạng thái chơi bình thường
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Đặt lại trạng thái CustomerManager về ReadyToRoll để cho phép bấm E cuốn tiếp
            if (CustomerManager.Instance != null)
            {
                CustomerManager.Instance.SetCustomerState(CustomerManager.CustomerState.ReadyToRoll);
            }

            Log($"Cần cuốn thêm {requiredRollCount - completedRollCount} cái nữa. Chờ người chơi nhấn E.");
            yield break; // Dừng coroutine này; lần nhấn E tiếp theo sẽ gọi StartRolling() lại
        }

        // --- Đã đủ số lượng ---
        currentStep = CookingStep.Completed;

        // 8. THOÁT KHỎI chế độ cuốn bánh
        isRolling = false;

        // Bật lại collider Xe_Bo_Bia_Root
        var xeRoot = GameObject.Find("Xe_Bo_Bia_Root");
        if (xeRoot != null)
        {
            var col = xeRoot.GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        // Mở khóa di chuyển của nhân vật
        if (playerMovement != null)
        {
            playerMovement.IsMovementLocked = false;
        }

        // Mở khóa xoay camera của người chơi
        ThirdPersonCamera tpCam = Camera.main?.GetComponent<ThirdPersonCamera>();
        if (tpCam != null)
        {
            tpCam.IsRotationLocked = false;
        }

        // Tắt hệ thống tương tác bằng tâm ngắm (không cần chỉ nguyên liệu nữa)
        if (crosshairSystem != null)
        {
            crosshairSystem.Disable();
        }

        // Khóa con trỏ chuột về trạng thái chơi bình thường (TPS look)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 9. TỰ ĐỘNG GIAO BÁNH: gắn bánh vào tay NPC → kích hoạt hội thoại ngay luôn
        //    ServeBoBiaToNPC() xử lý đủ: AttachBoBiaToNpcHand + ServeCurrentCustomer + ForceReset
        Log($"Đã cuốn đủ {requiredRollCount} cái. Giao bánh cho khách!");
        ServeBoBiaToNPC();
    }

    // ===================================================================
    // PREFAB SPAWN HELPERS
    // ===================================================================

    /// <summary>Lấy vị trí đặt prefab trung gian (trên mặt thớt xe).</summary>
    private Vector3 GetProcessDisplayPosition()
    {
        if (ingredientDisplayPoint != null)
            return ingredientDisplayPoint.position;

        // Vị trí mặt thớt gỗ cuốn bánh bò bía trên xe (ngay trước mặt người chơi).
        // Xe_Bo_Bia_Root có vị trí world: (0.25, 0.05, -0.675).
        // Mặt thớt ở trên quầy xe có vị trí: X = 0.22, Y = 0.46, Z = -0.75.
        return new Vector3(0.22f, 0.46f, -0.75f);
    }

    /// <summary>Spawn một prefab trung gian tại vị trí thớt + offset chồng lên.</summary>
    private void SpawnProcessPrefab(GameObject prefab, Vector3 stackOffset)
    {
        if (prefab == null)
        {
            Log($"[SpawnProcessPrefab] Prefab chưa được gán trong Inspector!");
            return;
        }

        Vector3 spawnPos = GetProcessDisplayPosition() + stackOffset;

        // Quay prefab theo hướng stallCenter
        Quaternion spawnRot = stallCenter != null ? stallCenter.rotation : Quaternion.identity;

        GameObject spawned = Instantiate(prefab, spawnPos, spawnRot);
        spawned.name = prefab.name + "_Process";

        // Tắt Collider trên prefab process để không ảnh hưởng raycast
        foreach (Collider col in spawned.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        spawnedProcessPrefabs.Add(spawned);
        Log($"Đã spawn: {spawned.name} tại {spawnPos}");
    }

    /// <summary>Xóa prefab trung gian cuối cùng trong danh sách (dùng khi thay thế).</summary>
    private void RemoveLastProcessPrefab()
    {
        if (spawnedProcessPrefabs.Count == 0) return;
        int last = spawnedProcessPrefabs.Count - 1;
        if (spawnedProcessPrefabs[last] != null)
            Destroy(spawnedProcessPrefabs[last]);
        spawnedProcessPrefabs.RemoveAt(last);
    }

    /// <summary>Xóa tất cả prefab trung gian trong scene.</summary>
    private void ClearProcessPrefabs()
    {
        foreach (var p in spawnedProcessPrefabs)
        {
            if (p != null) Destroy(p);
        }
        spawnedProcessPrefabs.Clear();
    }

    /// <summary>Rắc hạt mè đen (các khối cầu nhỏ màu đen) lên bề mặt dừa nạo.</summary>
    private void SprinkleBlackSesame(GameObject parentObj)
    {
        if (parentObj == null) return;

        // Tạo một Material màu đen dùng chung cho các hạt mè (Tương thích URP / Standard)
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        Shader targetShader = urpShader != null ? urpShader : Shader.Find("Standard");
        
        Material blackMaterial = new Material(targetShader);
        if (blackMaterial != null)
        {
            Color blackColor = new Color(0.05f, 0.05f, 0.05f); // Đen đậm hơn
            if (blackMaterial.HasProperty("_BaseColor"))
                blackMaterial.SetColor("_BaseColor", blackColor);
            else if (blackMaterial.HasProperty("_Color"))
                blackMaterial.SetColor("_Color", blackColor);

            if (blackMaterial.HasProperty("_Smoothness"))
                blackMaterial.SetFloat("_Smoothness", 0.2f);
            else if (blackMaterial.HasProperty("_Glossiness"))
                blackMaterial.SetFloat("_Glossiness", 0.2f);
        }

        // Rắc khoảng 35-50 hạt mè đen
        int seedCount = UnityEngine.Random.Range(35, 50);
        for (int i = 0; i < seedCount; i++)
        {
            GameObject sesame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sesame.name = "SesameSeed";

            // Xóa collider ngay lập tức để tránh ảnh hưởng vật lý
            var col = sesame.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Gán material màu đen
            var renderer = sesame.GetComponent<Renderer>();
            if (renderer != null && blackMaterial != null)
            {
                renderer.sharedMaterial = blackMaterial;
            }

            // Gán làm con của dừa nạo để khi xóa dừa nạo thì mè cũng biến mất
            sesame.transform.SetParent(parentObj.transform);

            // Sinh vị trí ngẫu nhiên trên bề mặt dừa nạo (tính theo hệ tọa độ local của dừa nạo)
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(0f, 0.0085f); // Khoảng cách từ tâm dừa nạo (Dừa_Nạo extents ~0.0096)
            float localX = Mathf.Cos(angle) * radius;
            float localZ = Mathf.Sin(angle) * radius;
            float localY = UnityEngine.Random.Range(0.0004f, 0.0012f); // Độ cao ngẫu nhiên trên lớp dừa

            sesame.transform.localPosition = new Vector3(localX, localY, localZ);

            // Kích thước hạt mè (rất nhỏ). Vì parent scale ~4.44, cần chia cho localScale để giữ kích thước world cố định.
            float parentScale = parentObj.transform.localScale.x;
            float scaleFactor = 1f / (parentScale > 0 ? parentScale : 1f);
            // Thu nhỏ hạt mè đi 40% so với trước (0.0025, 0.0012, 0.0018)
            sesame.transform.localScale = new Vector3(0.0025f * scaleFactor, 0.0012f * scaleFactor, 0.0018f * scaleFactor);

            // Xoay ngẫu nhiên
            sesame.transform.localRotation = Quaternion.Euler(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f)
            );
        }
    }

    /// <summary>
    /// Spawn Bò Bía Hoàn chỉnh trên tay người chơi (FPS hand item).
    /// Gắn vào firstPersonHandPoint (child của Camera) hoặc fallback về trước mặt camera.
    /// </summary>
    private void SpawnBoBiaHandItem()
    {
        if (prefab_BoBiaHoanChinh == null)
        {
            Log("[SpawnBoBiaHandItem] Prefab Bò Bía Hoàn chỉnh chưa được gán!");
            return;
        }

        // Xóa hand item cũ nếu còn
        if (boBiaHandItem != null)
            Destroy(boBiaHandItem);

        // Xác định parent: firstPersonHandPoint hoặc fallback Camera
        Transform parent = firstPersonHandPoint;
        if (parent == null && Camera.main != null)
            parent = Camera.main.transform;

        if (parent != null)
        {
            boBiaHandItem = Instantiate(prefab_BoBiaHoanChinh, parent);
            boBiaHandItem.transform.localPosition = new Vector3(0.2f, -0.25f, 0.5f);
            boBiaHandItem.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            boBiaHandItem.transform.localScale = prefab_BoBiaHoanChinh.transform.localScale * 0.8f;
        }
        else
        {
            // Fallback: spawn trước mặt nếu không tìm được parent
            boBiaHandItem = Instantiate(prefab_BoBiaHoanChinh, transform.position + transform.forward * 0.5f, Quaternion.identity);
        }

        // Tắt collider trên hand item để không block raycast
        foreach (Collider col in boBiaHandItem.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        Log($"Đã spawn Bò Bía Hoàn chỉnh trên tay người chơi.");
    }
    // ===================================================================
    // CAMERA TRANSITION METHODS
    // ===================================================================

    public void TransitionToCookingView(Camera mainCamera, Transform cookingCamPosition)
    {
        // Giữ nguyên góc nhìn camera của người chơi, chỉ hiện chuột để tương tác Point-and-Click
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator RestoreCameraPerspective(Camera cam, float duration)
    {
        // Không tịnh tiến camera, chỉ ẩn khóa con trỏ chuột về mặc định
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return null;
    }

    // ===================================================================
    // PRIVATE — Proximity, Input & Cooking Step Machine
    // ===================================================================

    private void UpdateProximity()
    {
        if (playerTransform == null || stallCenter == null)
        {
            isPlayerNearby = false;
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, stallCenter.position);
        isPlayerNearby = distance <= interactRadius;
    }

    private void NotifyInteractableChange()
    {
        bool currentlyInteractable = CanStartRolling;

        if (currentlyInteractable != wasInteractable)
        {
            wasInteractable = currentlyInteractable;
            OnInteractableChanged?.Invoke(currentlyInteractable);
            Log(currentlyInteractable ? "✅ Có thể tương tác: hiện prompt [E] Cuốn bánh." : "❌ Không thể tương tác.");
        }
    }

    private void HandleInteractInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Log($"Phím [{interactKey}] được nhấn.");

            if (isHoldingBoBia)
            {
                // Người chơi đang cầm Bò Bía hoàn chỉnh → nhấn E để giao cho NPC
                // Chỉ cho phép giao nếu đứng gần NPC của khách hàng hiện tại (<= 2.5m)
                var cm = customerManager != null ? customerManager : CustomerManager.Instance;
                if (cm != null && cm.NextCustomer != null && cm.NextCustomer.npcInstance != null && playerTransform != null)
                {
                    float distanceToNpc = Vector3.Distance(playerTransform.position, cm.NextCustomer.npcInstance.transform.position);
                    if (distanceToNpc <= 2.5f)
                    {
                        ServeBoBiaToNPC();
                    }
                    else
                    {
                        Log($"NPC ở quá xa ({distanceToNpc:F2}m). Hãy lại gần NPC để giao bánh.");
                    }
                }
                else
                {
                    ServeBoBiaToNPC();
                }
            }
            else
            {
                StartRolling();
            }
        }
    }

    /// <summary>
    /// Giao Bò Bía cho NPC: destroy hand item, attach vào tay NPC theo số lượng đơn hàng, kích hoạt dialogue.
    /// Logic phân phối bánh lên 2 tay NPC theo số lượng đơn:
    ///   - 1 cái  : tay phải 1 cái
    ///   - 2 cái  : tay phải 1, tay trái 1 (Bà Nga)
    ///   - 3 cái  : tay phải 2 (chồng nhau), tay trái 1 (Shipper)
    ///   - 4 cái  : tay phải 2, tay trái 2 (Mê Liu)
    /// </summary>
    private void ServeBoBiaToNPC()
    {
        isHoldingBoBia = false;

        // Xóa bò bía khỏi tay người chơi
        if (boBiaHandItem != null)
        {
            Destroy(boBiaHandItem);
            boBiaHandItem = null;
        }

        // Gắn bò bía vào tay NPC theo số lượng đơn hàng
        var cm = customerManager != null ? customerManager : CustomerManager.Instance;
        if (cm != null && cm.NextCustomer != null && cm.NextCustomer.npcInstance != null)
        {
            AttachMultipleBoBiaToNpcHands(cm.NextCustomer.npcInstance, requiredRollCount);
        }

        isRolling = false;
        ServeCurrentCustomer();
        OnRollingCompleted?.Invoke(totalWaitTime);
        ForceReset();
        Log("✅ Đã giao Bò Bía cho NPC và phục vụ khách.");
    }

    private void HandleCookingInput()
    {
        if (currentStep == CookingStep.AddPastry_Applying ||
            currentStep == CookingStep.AddCandy_Applying ||
            currentStep == CookingStep.AddCoconut_Applying ||
            currentStep == CookingStep.AddSesame_Applying)
        {
            // Tích lũy di chuyển chuột qua lại (X và Y) để mô phỏng rải nguyên liệu
            float mouseMove = Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"));
            if (mouseMove > 0.01f)
            {
                applyProgress += mouseMove * 0.05f; // Tăng dần tiến trình lắc chuột
                applyProgress = Mathf.Clamp01(applyProgress);
                OnRollingProgressChanged?.Invoke(applyProgress);

                if (applyProgress >= 1.0f)
                {
                    // Đạt 100% -> Spawn topping tương ứng và nhảy sang bước chọn kế tiếp
                    SpawnToppingForCurrentState();
                }
            }
        }
        else if (currentStep == CookingStep.Rolling)
        {
            // Click Mè đen đã tự động gọi FinalizeBoBia() để hoàn thành và cuốn bánh.
            // Không xử lý kéo chuột ở đây nữa để tránh kích hoạt trùng lặp FinalizeBoBia().
        }
    }

    private void SpawnToppingForCurrentState()
    {
        if (currentStep == CookingStep.AddPastry_Applying)
        {
            Vector3 pos = GetPlatePosition();
            GameObject pastry = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pastry.name = "Topping_Pastry";
            Destroy(pastry.GetComponent<Collider>());
            pastry.transform.position = pos;
            pastry.transform.rotation = stallCenter.rotation;
            pastry.transform.localScale = new Vector3(0.28f, 0.001f, 0.28f);

            Renderer ren = pastry.GetComponent<Renderer>();
            if (ren != null)
            {
                ren.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ren.material.color = new Color(0.95f, 0.95f, 0.9f, 0.8f);
            }
            spawnedToppings.Add(pastry);

            AdvanceStep(CookingStep.AddCandy, waitTimePerBanhTrang);
        }
        else if (currentStep == CookingStep.AddCandy_Applying)
        {
            Vector3 basePos = GetPlatePosition();
            for (int i = 0; i < 2; i++)
            {
                GameObject candy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                candy.name = "Topping_Candy_" + i;
                Destroy(candy.GetComponent<Collider>());

                float offset = (i == 0) ? -0.04f : 0.04f;
                candy.transform.position = basePos + stallCenter.right * offset + stallCenter.up * 0.002f;
                candy.transform.rotation = stallCenter.rotation * Quaternion.Euler(0f, 90f, 0f);
                candy.transform.localScale = new Vector3(0.015f, 0.08f, 0.015f);

                Renderer ren = candy.GetComponent<Renderer>();
                if (ren != null)
                {
                    ren.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    ren.material.color = new Color(0.85f, 0.55f, 0.1f, 1f);
                }
                spawnedToppings.Add(candy);
            }

            AdvanceStep(CookingStep.AddCoconut, waitTimePerKeoMachNha);
        }
        else if (currentStep == CookingStep.AddCoconut_Applying)
        {
            Vector3 basePos = GetPlatePosition();
            for (int i = 0; i < 8; i++)
            {
                GameObject coconut = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                coconut.name = "Topping_Coconut_" + i;
                Destroy(coconut.GetComponent<Collider>());

                float angle = UnityEngine.Random.Range(0f, 360f);
                float dist = UnityEngine.Random.Range(0f, 0.1f);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * stallCenter.forward * dist;

                coconut.transform.position = basePos + offset + stallCenter.up * 0.004f;
                coconut.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 90f);
                coconut.transform.localScale = new Vector3(0.004f, 0.03f, 0.004f);

                Renderer ren = coconut.GetComponent<Renderer>();
                if (ren != null)
                {
                    ren.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    ren.material.color = new Color(0.98f, 0.98f, 0.98f, 1f);
                }
                spawnedToppings.Add(coconut);
            }

            AdvanceStep(CookingStep.AddSesame, waitTimePerDua);
        }
        else if (currentStep == CookingStep.AddSesame_Applying)
        {
            Vector3 basePos = GetPlatePosition();
            for (int i = 0; i < 15; i++)
            {
                GameObject sesame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sesame.name = "Topping_Sesame_" + i;
                Destroy(sesame.GetComponent<Collider>());

                float angle = UnityEngine.Random.Range(0f, 360f);
                float dist = UnityEngine.Random.Range(0f, 0.11f);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * stallCenter.forward * dist;

                sesame.transform.position = basePos + offset + stallCenter.up * 0.006f;
                sesame.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), 0f);
                sesame.transform.localScale = new Vector3(0.005f, 0.003f, 0.005f);

                Renderer ren = sesame.GetComponent<Renderer>();
                if (ren != null)
                {
                    ren.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    ren.material.color = new Color(0.35f, 0.2f, 0.1f, 1f);
                }
                spawnedToppings.Add(sesame);
            }

            AdvanceStep(CookingStep.Rolling, waitTimePerMe);
        }
    }

    private void AdvanceStep(CookingStep nextStep, float waitTimeReward)
    {
        totalWaitTime += waitTimeReward;
        CookingStep completedStep = currentStep;
        currentStep = nextStep;
        applyProgress = 0f; // Reset tiến trình cho bước di chuột tiếp theo

        // Reset thanh hiển thị tiến trình của UI về 0 trước khi người chơi bắt đầu di chuột
        if (nextStep == CookingStep.AddCandy) OnRollingProgressChanged?.Invoke(0.0f);
        else if (nextStep == CookingStep.AddCoconut) OnRollingProgressChanged?.Invoke(0.0f);
        else if (nextStep == CookingStep.AddSesame) OnRollingProgressChanged?.Invoke(0.0f);
        else if (nextStep == CookingStep.Rolling) OnRollingProgressChanged?.Invoke(0.0f);

        OnStepCompleted?.Invoke(completedStep);
        Log($"Hoàn thành bước {completedStep} -> chuyển sang {nextStep}. +{waitTimeReward}s");
    }

    private void TriggerCompletionAudio()
    {
        if (completionAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(completionAudioClip, transform.position);
        }
        else
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Play();
            }
        }
    }

    // ===================================================================
    // PRIVATE — Condition Helpers
    // ===================================================================

    private bool IsPhaseDAY()
    {
        if (GameTimeManager.Instance == null) return true;
        return GameTimeManager.Instance.CurrentPhase == GameTimeManager.GamePhase.DAY;
    }

    private bool HasCustomerWaiting()
    {
        var manager = customerManager != null ? customerManager : CustomerManager.Instance;
        if (manager == null) return true;
        return manager.HasCustomerWaiting;
    }

    private void LogCannotStartReason()
    {
        if (!isPlayerNearby)
            Log("Không thể cuốn bánh: Người chơi đứng quá xa quầy hàng.");
        else if (!HasCustomerWaiting())
            Log("Không thể cuốn bánh: Không có khách đang đợi.");
        else if (isRolling)
            Log("Không thể cuốn bánh: Đang trong quy trình cuốn bánh.");
        else if (currentStep == CookingStep.Completed)
            Log("Không thể cuốn bánh: Quy trình đã hoàn thành. Gọi ForceReset() trước.");
    }

    // ===================================================================
    // COROUTINE — Quy trình chính
    // ===================================================================

    private IEnumerator RollingCoroutine()
    {
        isRolling = true;
        isHoldingBoBia = false;

        // Đọc số lượng bánh cần cuốn từ đơn hàng của khách hiện tại
        // (Chỉ reset khi bắt đầu đơn mới — completedRollCount == 0)
        if (completedRollCount == 0)
        {
            requiredRollCount = 1; // mặc định
            var customer = CustomerManager.Instance?.NextCustomer;
            if (customer != null && customer.orderQuantity > 0)
            {
                requiredRollCount = customer.orderQuantity;
            }
            Log($"Bắt đầu đơn hàng mới: cần cuốn {requiredRollCount} cái bánh.");
        }
        else
        {
            Log($"Tiếp tục cuốn bánh {completedRollCount + 1}/{requiredRollCount}.");
        }

        if (CustomerManager.Instance != null)
        {
            CustomerManager.Instance.SetCustomerState(CustomerManager.CustomerState.Rolling);
        }

        OnInteractableChanged?.Invoke(false);
        OnRollingProgressChanged?.Invoke(0f);
        OnRollingProgressVisibleChanged?.Invoke(false); // Không hiện progress bar cũ

        // Khởi đầu bước đầu tiên: chờ người chơi click Xấp Bánh Bò Bía
        currentStep = CookingStep.AddPastry;
        rollProgress = 0f;
        applyProgress = 0f;

        // Chờ cho đến khi người chơi hoàn tất tất cả bước click
        // (FinalizeBoBia() coroutine sẽ set currentStep = Completed hoặc yield break nếu chưa đủ)
        while (currentStep != CookingStep.Completed)
        {
            yield return null;
        }

        Log($"Quy trình làm Bò Bía click-based hoàn tất.");
        OnRollingProgressVisibleChanged?.Invoke(false);
    }

    private IEnumerator WaitForMeLiuRedMonologue()
    {
        if (StoryPhase2Manager.Instance != null)
        {
            StoryPhase2Manager.Instance.TriggerMeLiuRedMonologue();
            yield return null;
            while (StoryPhase2Manager.Instance != null && StoryPhase2Manager.Instance.IsMeLiuDialogueRunning)
                yield return null;
        }
    }

    // ===================================================================
    // DYNAMIC PROP SPAWNING & ATTACHMENT
    // ===================================================================

    /// <summary>
    /// Gắn nhiều bánh Bò Bía lên 2 tay NPC theo số lượng đơn hàng.
    /// Quy tắc phân phối:
    ///   - 1 cái : tay phải 1 cái
    ///   - 2 cái : tay phải 1, tay trái 1 (Bà Nga — 2 tay mỗi tay 1 cái)
    ///   - 3 cái : tay phải 2 (chồng nhẹ), tay trái 1 (Shipper — một tay thêm cái)
    ///   - 4 cái : tay phải 2, tay trái 2 (Mê Liu — mỗi tay 2 cái)
    /// </summary>
    public void AttachMultipleBoBiaToNpcHands(GameObject npcObject, int quantity)
    {
        if (npcObject == null || prefab_BoBiaHoanChinh == null) return;

        // Tìm tay phải và tay trái
        Transform rightHand = FindHandTransform(npcObject.transform, isRight: true);
        Transform leftHand = FindHandTransform(npcObject.transform, isRight: false);

        // Fallback: dùng root NPC nếu không tìm được tay
        if (rightHand == null) rightHand = npcObject.transform;
        if (leftHand == null) leftHand = npcObject.transform;

        // Phân phối bánh lên 2 tay
        int rightCount, leftCount;
        switch (quantity)
        {
            case 2:
                rightCount = 1; leftCount = 1;
                break;
            case 3:
                rightCount = 2; leftCount = 1;
                break;
            case 4:
                rightCount = 2; leftCount = 2;
                break;
            default: // 1 hoặc fallback
                rightCount = 1; leftCount = 0;
                break;
        }

        // Gắn bánh lên tay phải (chồng nhau theo chiều Y)
        for (int i = 0; i < rightCount; i++)
        {
            SpawnBoBiaOnHand(rightHand, i, isRight: true);
        }

        // Gắn bánh lên tay trái (chồng nhau theo chiều Y)
        for (int i = 0; i < leftCount; i++)
        {
            SpawnBoBiaOnHand(leftHand, i, isRight: false);
        }

        Log($"Đã gắn {rightCount} bánh lên tay phải và {leftCount} bánh lên tay trái của NPC {npcObject.name}.");
    }

    /// <summary>
    /// Spawn 1 cái Bò Bía Hoàn chỉnh gắn vào một bàn tay NPC.
    /// stackIndex: chỉ số chồng (0 = cái đầu tiên, 1 = cái thứ hai chồng lên trên)
    /// Scale được tính để bò bía luôn có kích thước world-space bằng với khi shipper cầm (rootScale=1.19),
    /// bất kể parent hand bone của từng character có lossyScale khác nhau (Nganpc=0.55, Npc1=1.0).
    /// </summary>
    private void SpawnBoBiaOnHand(Transform handTransform, int stackIndex, bool isRight)
    {
        if (prefab_BoBiaHoanChinh == null) return;

        GameObject boBiaProp = Instantiate(prefab_BoBiaHoanChinh, handTransform);
        boBiaProp.name = $"Rolled_BoBia_{(isRight ? "R" : "L")}_{stackIndex}";

        // Vị trí cơ bản trên bàn tay, cái thứ 2 chồng lên trên (offset Y theo stackIndex)
        float stackOffsetY = stackIndex * 0.025f;
        float sideOffset = isRight ? -0.015f : 0.015f;

        boBiaProp.transform.localPosition = new Vector3(sideOffset, 0.025f + stackOffsetY, 0.01f);
        boBiaProp.transform.localRotation = Quaternion.Euler(20f, 90f, 45f);

        // === FIX SCALE ===
        // Shipper có rootScale=1.19, nên khi gắn bò bía vào tay shipper:
        //   worldScale = prefab.lossyScale × 1.19
        // Mục tiêu: mọi NPC đều có worldScale bằng shipper.
        // Công thức: localScale = targetWorldScale / parentHandLossyScale
        //   = (prefab.lossyScale × 1.19) / handTransform.lossyScale
        const float shipperRootScale = 1.19f; // rootScale cố định của prefab shipper
        Vector3 prefabLossy = prefab_BoBiaHoanChinh.transform.lossyScale;
        Vector3 targetWorldScale = prefabLossy * shipperRootScale;
        Vector3 parentWorldScale = handTransform.lossyScale;
        Vector3 compensatedLocalScale = new Vector3(
            parentWorldScale.x != 0f ? targetWorldScale.x / parentWorldScale.x : targetWorldScale.x,
            parentWorldScale.y != 0f ? targetWorldScale.y / parentWorldScale.y : targetWorldScale.y,
            parentWorldScale.z != 0f ? targetWorldScale.z / parentWorldScale.z : targetWorldScale.z
        );
        boBiaProp.transform.localScale = compensatedLocalScale;

        // Vô hiệu hóa collider để không cản trở vật lý/camera của NPC
        foreach (Collider col in boBiaProp.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    /// <summary>
    /// Tương thích ngược với code cũ — gắn 1 bánh vào tay phải NPC.
    /// </summary>
    public void AttachBoBiaToNpcHand(GameObject npcObject)
    {
        AttachMultipleBoBiaToNpcHands(npcObject, 1);
    }

    /// <summary>
    /// Tìm xương bàn tay phải hoặc tay trái của NPC.
    /// </summary>
    private Transform FindHandTransform(Transform current, bool isRight)
    {
        string nameLower = current.name.ToLower();

        if (isRight)
        {
            if (nameLower.Contains("hand.r") || nameLower.Contains("wrist_r") || 
                nameLower.Contains("hand_r") || nameLower.Contains("righthand") ||
                nameLower.Contains("r_hand") || nameLower.Contains("handright"))
            {
                return current;
            }
        }
        else
        {
            if (nameLower.Contains("hand.l") || nameLower.Contains("wrist_l") || 
                nameLower.Contains("hand_l") || nameLower.Contains("lefthand") ||
                nameLower.Contains("l_hand") || nameLower.Contains("handleft"))
            {
                return current;
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindHandTransform(current.GetChild(i), isRight);
            if (found != null) return found;
        }

        return null;
    }

    // Tương thích ngược với phiên bản cũ (chỉ tìm tay phải)
    private Transform FindHandTransform(Transform current)
    {
        return FindHandTransform(current, isRight: true);
    }

    // ===================================================================
    // PRIVATE HELPERS
    // ===================================================================

    /// <summary>
    /// Phục vụ khách đầu hàng sau khi cuốn bánh xong.
    /// Gọi CustomerManager.ServeNextCustomerWithDialogue() để hiển thị lời thoại,
    /// cộng điểm bằng chứng, chờ hiển thị xong rồi mới cho NPC rời đi.
    /// </summary>
    private void ServeCurrentCustomer()
    {
        var manager = customerManager != null ? customerManager : CustomerManager.Instance;
        if (manager == null) return;

        // Gọi phiên bản có dialogue: NPC nói chuyện → cộng điểm → chờ → rồi mới đi
        manager.ServeNextCustomerWithDialogue();
        Log($"Đã kích hoạt phục vụ khách với lời thoại. Còn {manager.QueueCount} khách đợi.");

        if (CaseManager.Instance != null)
        {
            CaseManager.Instance.AddScore(scorePerCustomer);
        }
    }

    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log($"[BoBiaMechanic] {message}");
    }

    // ===================================================================
    // GIZMOS — Hiển thị vùng proximity trong Editor
    // ===================================================================

    private void OnDrawGizmos()
    {
        if (!showProximityGizmo || stallCenter == null) return;

        // Màu xanh lá = người chơi đủ gần / đỏ = quá xa
        Gizmos.color = isPlayerNearby
            ? new Color(0f, 1f, 0.3f, 0.25f)
            : new Color(1f, 0.3f, 0f, 0.15f);

        Gizmos.DrawSphere(stallCenter.position, interactRadius);

        Gizmos.color = isPlayerNearby ? Color.green : new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(stallCenter.position, interactRadius);
    }
}
