using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 0414

/// <summary>
/// Procedural Walker tối ưu cho Rigify model từ Blender.
///
/// BA CHẾ ĐỘ (walkerMode):
///   PlayerMode   : Dùng CartController.CurrentSpeed làm nguồn tốc độ.
///   NPCMode      : Tự tính tốc độ từ vị trí thực tế, dùng cho NPC.
///   StaticMode   : Không walking, chỉ dùng khi cuốn bánh. Dùng cho NPC đứng đợi.
///
/// NHỮNG LỖI ĐÃ SỬA (v2):
///   ✓ BỎ hết logic Breathing (nhịp thở) và Idle Sway → tránh biến dạng lồng ngực, đầu, cổ
///   ✓ ApplyBodyMotion: GIỮ NGUYÊN localPosition gốc → tránh nhân vật bay lên trời
///   ✓ ApplyBodyMotion: Chỉ xoay nhẹ trục Z/X khi đi bộ (walkBlend > 0)
///   ✓ ApplyArmMotion: CHỈ CỘNG delta rotation vào originLocalRot → tránh tay bị gãy ngược
///   ✓ Idle (Speed=0): Toàn bộ model giữ tư thế gốc tự nhiên, không áp bất kỳ lực math nào
///
/// GẮN VÀO NPC: Chỉ cần gắn script, đặt Mode = NPCMode, bật autoDiscoverArms.
/// </summary>
public class SimpleProceduralWalker : MonoBehaviour
{
    // ===================================================================
    // ENUM
    // ===================================================================

    public enum WalkerMode
    {
        PlayerMode,  // Lấy tốc độ từ CartController
        NPCMode,     // Tự tính tốc độ từ transform.position
        StaticMode   // Chỉ Breathing, không walking
    }

    public enum WalkerState
    {
        OnFoot,
        Riding
    }

    // ===================================================================
    // INSPECTOR — Chung
    // ===================================================================

    [Header("=== CHẾ ĐỘ HOẠT ĐỘNG ===")]
    [Tooltip("PlayerMode = dùng CartController. NPCMode = tự tính speed. StaticMode = chỉ breathing.")]
    [SerializeField] private WalkerMode walkerMode = WalkerMode.NPCMode;

    [Header("=== TRẠNG THÁI DI CHUYỂN ===")]
    [SerializeField] private WalkerState walkerState = WalkerState.OnFoot;

    public WalkerState CurrentState
    {
        get => walkerState;
        set => walkerState = value;
    }

    [Header("Tham Chiếu")]

    [Tooltip("[Rolling Mode] BoBiaMechanic component quản lý trạng thái cuốn bánh.")]
    [SerializeField] private BoBiaMechanic boBiaMechanic;

    [Tooltip("Transform gốc skeleton (Hips / Root). Để null = dùng transform này.")]
    [SerializeField] private Transform bodyRoot;

    [Header("Ngưỡng & Blend")]
    [Range(0f, 2f)]
    [SerializeField] private float walkThreshold = 0.05f;

    [Tooltip("Thời gian (giây) blend mượt IDLE ↔ WALKING.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float blendSpeed = 0.18f;

    // ===================================================================
    // INSPECTOR — AutomaticArmDown
    // ===================================================================

    [Header("=== AUTOMATIC ARM DOWN ===")]
    [Tooltip("Bật để tự động tìm và ép tay xuống dọc thân. Giải quyết T-Pose.")]
    [SerializeField] private bool autoDiscoverArms = true;

    [Tooltip("Keywords (viết thường) để tìm xương tay trong skeleton.\n" +
             "Script tìm bất kỳ xương nào có tên chứa một trong các từ này.\n" +
             "Hỗ trợ Rigify: 'UpperArm.L', 'UpperArm.R', 'UpperArm_L', v.v.")]
    [SerializeField] private string[] armKeywords = { "upperarm", "upper_arm", "arm", "forearm", "shoulder" };

    [Tooltip("Keywords để tìm xương bàn tay (không ép xuống, chỉ dùng cho fake walk).")]
    [SerializeField] private string[] handKeywords = { "hand", "wrist" };

    [Tooltip("Rotation đích (Local Euler) khi ép tay xuống — trục X.\n" +
             "Thông thường 60–80° để tay buông dọc thân.\n" +
             "Khi cartHandlePosition có, tay sẽ bị \"ép\" hướng tới handle thay vì xuống dọc.")]
    [Range(0f, 120f)]
    [SerializeField] private float armDownAngleX = 70f;

    [Tooltip("Tốc độ Lerp khi ép tay xuống lúc Start (giây để đạt đích).")]
    [Range(0f, 2f)]
    [SerializeField] private float armDownBlendTime = 0.3f;

    [Tooltip("Tay trái gắn thủ công (bỏ qua auto-discover cho bên trái).")]
    [SerializeField] private Transform leftUpperArm;

    [Tooltip("Tay phải gắn thủ công (bỏ qua auto-discover cho bên phải).")]
    [SerializeField] private Transform rightUpperArm;

    [Tooltip("Góc bù trừ thế giới (Euler) của tay trái để tự tinh chỉnh ngoài Inspector.")]
    [SerializeField] private Vector3 leftArmOffsetEuler = new Vector3(0f, 90f, 0f);

    [Tooltip("Góc bù trừ thế giới (Euler) của tay phải để tự tinh chỉnh ngoài Inspector.")]
    [SerializeField] private Vector3 rightArmOffsetEuler = new Vector3(0f, -90f, 0f);

    // ===================================================================
    // INSPECTOR — Cart Handle Grip (Bám Tay Vào Tay Cầm Xe)
    // ===================================================================

    [Header("=== CART HANDLE GRIP (Bám Vào Tay Cầm Xe) ===")]
    [Tooltip("Bật để tay bám vào handle/tay cầm của xe thay vì buông thõng.")]
    [SerializeField] private bool enableHandleGrip = false;

    [Tooltip("Transform của handle trái trên xe (nơi tay trái nắm lấy).")]
    [SerializeField] private Transform cartHandleLeft;

    [Tooltip("Transform của handle phải trên xe (nơi tay phải nắm lấy).")]
    [SerializeField] private Transform cartHandleRight;

    [Tooltip("Khi true, script sẽ tự động tìm handle từ CartController's children (nếu có).")]
    [SerializeField] private bool autoDiscoverHandles = true;

    [Tooltip("Ảnh hưởng của vị trí handle lên vị trí tay (0 = không bị ảnh hưởng, 1 = bám chặt).")]
    [Range(0f, 1f)]
    [SerializeField] private float handleGripStrength = 0.7f;

    [Tooltip("Độ lệch tối đa (độ) mà tay có thể vung nhưng vẫn giữ vị trí bám handle.")]
    [Range(0f, 30f)]
    [SerializeField] private float handleGripSwingLimitX = 15f;

    [Tooltip("Độ lệch tối đa (độ) theo trục Y (xoay cổ tay).")]
    [Range(0f, 20f)]
    [SerializeField] private float handleGripSwingLimitY = 8f;

    // ===================================================================
    // INSPECTOR — Rolling Hands (Cuốn Bánh)
    // ===================================================================

    [Header("=== ROLLING HANDS (Cuốn Bánh) ===")]
    [Tooltip("Bật để tay nhúc nhích, co duỗi khi đang cuốn bánh.")]
    [SerializeField] private bool enableRollingHands = true;

    [Tooltip("Keywords để tìm xương tay (wrist/hand) dùng cho rolling motion.\n" +
             "Mặc định: hand, wrist")]
    [SerializeField] private string[] rollingHandKeywords = { "hand", "wrist", "palm" };

    [Tooltip("Tầng số nhúc nhích tay khi cuốn bánh (Hz). Càng cao → nhúc nhích nhanh hơn.")]
    [Range(2f, 15f)]
    [SerializeField] private float rollingHandFrequency = 6f;

    [Tooltip("Biên độ xoay tay (độ). Tay co duỗi quanh trục Y (xoay cổ tay).")]
    [Range(5f, 45f)]
    [SerializeField] private float rollingHandAmplitude = 20f;

    [Tooltip("Giới hạn Y (độ) khi co tay (xoay cổ tay trong/ngoài).")]
    [Range(10f, 50f)]
    [SerializeField] private float rollingHandLimitY = 35f;

    [Tooltip("Giới hạn Z (độ) khi co tay (xoay cổ tay trái/phải).")]
    [Range(5f, 30f)]
    [SerializeField] private float rollingHandLimitZ = 15f;

    // ===================================================================
    // INSPECTOR — Fake Walking (tay vung)
    // ===================================================================

    [Header("=== FAKE WALKING — Vung Tay ===")]
    [Tooltip("Bật hiệu ứng vung tay khi NPC di chuyển.")]
    [SerializeField] private bool enableArmSwing = true;

    [Tooltip("Biên độ vung tay (độ). Tay vung tiến/lùi theo trục X.")]
    [Range(0f, 45f)]
    [SerializeField] private float armSwingAmplitude = 20f;

    [Tooltip("Tần số vung tay. Thường bằng tần số bob.")]
    [Range(0f, 20f)]
    [SerializeField] private float armSwingFrequency = 7f;

    // ===================================================================
    // INSPECTOR — Body Bob & Sway
    // ===================================================================

    [Header("Walking — Bob Y  (A·sin(B·t))")]
    [Range(0f, 0.15f)]
    [SerializeField] private float bobAmplitude = 0.04f;

    [Range(0f, 20f)]
    [SerializeField] private float bobFrequency = 7f;

    [Header("Walking — Sway Rotation")]
    [Range(0f, 10f)]
    [SerializeField] private float swayAmplitudeZ = 2f;

    [Range(0f, 20f)]
    [SerializeField] private float swayFrequencyZ = 3.5f;

    [Range(0f, 5f)]
    [SerializeField] private float swayAmplitudeX = 0.8f;

    [Header("Walking — Speed Scale")]
    [Range(0f, 1f)]
    [SerializeField] private float speedInfluence = 0.4f;

    [Range(1f, 10f)]
    [SerializeField] private float referenceSpeed = 3f;

    // ===================================================================
    // INSPECTOR — Idle / Breathing
    // ===================================================================



    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    // Body root baseline
    private Vector3 bodyRootOriginLocalPos;
    private Quaternion bodyRootOriginLocalRot;

    // Arm data
    private struct ArmData
    {
        public Transform bone;
        public Quaternion originLocalRot;   // Bind-pose gốc
        public Quaternion targetRestRot;    // Rotation khi tay buông thẳng
        public bool isLeft;
        public Transform handleTarget;     // Handle trên xe (nếu bám vào)
    }

    private List<ArmData> discoveredArms = new List<ArmData>();

    // Rolling hand data (xương Hand/Wrist để cuốn bánh)
    private struct RollingHandData
    {
        public Transform bone;
        public Quaternion originLocalRot;   // Bind-pose gốc
        public bool isLeft;
    }

    private List<RollingHandData> discoveredRollingHands = new List<RollingHandData>();

    // Blend & timer
    private float walkBlend = 0f;
    private float sineTimer = 0f;
    private float armDownT = 0f;   // [0,1] tiến trình ép tay xuống lúc Start

    // Mode override tracking
    private bool isInRollingMode = false;  // Đang cuốn bánh?

    private PlayerMovement cachedPlayerMovement;
    private bool hasCheckedPlayerMovement = false;

    // NPC speed
    private Vector3 lastPosition;
    private Vector3 lastRootPosition;
    private float npcSpeed = 0f;

    // Cache speed & speedScale to prevent double-updating root position tracking in LateUpdate
    private float currentComputedSpeed = 0f;
    private float currentComputedSpeedScale = 1f;

    private Vector3 smoothedBodyRootLocalEuler = Vector3.zero;

    // Offset ngẫu nhiên để các NPC không đồng bộ nhau
    private float phaseOffset;

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        // Fallback references
        if (bodyRoot == null)
            bodyRoot = transform;

        // CartController deprecated and removed.

        // Auto-find BoBiaMechanic
        if (boBiaMechanic == null)
            boBiaMechanic = GetComponentInParent<BoBiaMechanic>();

        // Lưu baseline body root
        bodyRootOriginLocalPos = bodyRoot.localPosition;
        bodyRootOriginLocalRot = bodyRoot.localRotation;

        // NPC speed tracking
        lastPosition = transform.position;
        lastRootPosition = transform.root.position;

        // Phase offset ngẫu nhiên để đám đông không đồng bộ
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);

        // Auto-discover handles deprecated.

        // Discover arms
        if (autoDiscoverArms)
            DiscoverAndRegisterArms();

        // Discover rolling hands (xương Hand/Wrist)
        if (enableRollingHands)
            DiscoverRollingHands();
    }

    private void Start()
    {
    }

    private void Update()
    {
        sineTimer += Time.deltaTime;

        // Lấy tốc độ trước (chỉ gọi duy nhất một lần để tránh lỗi trùng lặp cập nhật toạ độ)
        float speed = GetCurrentSpeed();

        // Debug log ở đầu Update khi đi bộ
        if (walkerState == WalkerState.OnFoot)
        {
            Debug.Log("Walker dang chay o che do di bo. Toc do nhan duoc: " + speed);
        }

        // Kiểm tra chế độ cuốn bánh (ROLLING MODE)
        bool rollingNow = boBiaMechanic != null && boBiaMechanic.IsPlayerNearby && boBiaMechanic.IsRolling;
        isInRollingMode = rollingNow;

        // Nếu đang cuốn bánh, ép mode thành StaticMode (không chạy bộ)
        WalkerMode activeMode = isInRollingMode ? WalkerMode.StaticMode : walkerMode;

        // CartController deprecated.

        // Blend IDLE ↔ WALKING
        float targetBlend = (activeMode != WalkerMode.StaticMode && speed > walkThreshold) ? 1f : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, targetBlend, Time.deltaTime / blendSpeed);

        // Nếu tốc độ nhận được lớn hơn 0, ép biến walkBlend lên 1 ngay lập tức để kích hoạt bước chân tức thì
        if (walkerState == WalkerState.OnFoot && speed > 0f)
        {
            walkBlend = 1f;
        }

        // Nếu tốc độ nhận được bằng 0 (hoặc nhỏ hơn ngưỡng), ép biến walkBlend về 0 ngay lập tức để ngắt bước chân tức thì và tránh bị lún
        if (walkerState == WalkerState.OnFoot && speed <= walkThreshold)
        {
            walkBlend = 0f;
        }

        // Speed scale biên độ
        float speedScale = 1f + speedInfluence * Mathf.Clamp01(speed / referenceSpeed);

        // Tiến trình ép tay xuống (hoàn tất trong armDownBlendTime giây)
        if (armDownT < 1f && armDownBlendTime > 0f)
            armDownT = Mathf.MoveTowards(armDownT, 1f, Time.deltaTime / armDownBlendTime);
        else
            armDownT = 1f;

        // Lưu lại cache
        currentComputedSpeed = speed;
        currentComputedSpeedScale = speedScale;

        if (enableDebugLog)
        {
            string modeStr = isInRollingMode ? "ROLLING" : activeMode.ToString();
            Debug.Log($"[Walker:{name}] mode={modeStr} speed={speed:F2} blend={walkBlend:F2}");
        }
    }

    private void LateUpdate()
    {
        if ((currentComputedSpeed <= walkThreshold || walkBlend <= 0.01f) && !isInRollingMode)
        {
            smoothedBodyRootLocalEuler = Vector3.zero;
            return;
        }

        // Áp dụng các hiệu ứng xoay xương tại LateUpdate để chạy sau hệ thống Animator/nội bộ của Unity
        ApplyBodyMotion(currentComputedSpeedScale);

        // Nếu đang cuốn bánh, áp dụng rolling hands thay vì arm motion bình thường
        if (isInRollingMode)
            ApplyRollingHands();
        else
            ApplyArmMotion(currentComputedSpeedScale);
    }

    // ===================================================================
    // SPEED SOURCE
    // ===================================================================

    private float GetCurrentSpeed()
    {
        // Trạng thái OnFoot (Đi bộ tự do)
        // Tìm script PlayerMovement ở object cha hoặc cùng cấp (chỉ tìm 1 lần duy nhất)
        if (!hasCheckedPlayerMovement)
        {
            cachedPlayerMovement = GetComponentInParent<PlayerMovement>();
            hasCheckedPlayerMovement = true;
        }

        if (cachedPlayerMovement != null && cachedPlayerMovement.enabled)
        {
            return cachedPlayerMovement.CurrentSpeed; // Lấy trực tiếp tốc độ WASD tính toán chuẩn
        }

        // Nếu không thấy PlayerMovement (Ví dụ ở NPC Mode tự do), đo tốc độ dựa trên Rigidbody hoặc Root Transform
        Transform rootTransform = transform.root; // Lấy object cha ngoài cùng ngoài Hierarchy
        Vector3 delta = rootTransform.position - lastRootPosition;
        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastRootPosition = rootTransform.position;
        return speed;
    }

    // ===================================================================
    // CART HANDLE DISCOVERY
    // ===================================================================

    /// <summary>
    /// Tự động tìm handle từ CartController.
    /// Tìm kiếm các child object có tên chứa "handle" (case-insensitive).
    /// </summary>
    private void DiscoverCartHandles()
    {
    }

    /// <summary>Duyệt đệ quy tìm xương handle trong cây CartController.</summary>
    private void SearchHandlesRecursive(Transform root, ref Transform foundLeft, ref Transform foundRight)
    {
        if (root == null) return;

        string nameLower = root.name.ToLower();

        if (nameLower.Contains("handle"))
        {
            bool isLeft = (nameLower.Contains("left") || nameLower.Contains("l_") ||
                          nameLower.Contains(".l") || nameLower.Contains("_l")) &&
                         !(nameLower.Contains("right") || nameLower.Contains("r_") ||
                           nameLower.Contains(".r") || nameLower.Contains("_r"));

            if (isLeft && foundLeft == null)
                foundLeft = root;
            else if (!isLeft && foundRight == null)
                foundRight = root;
        }

        // Duyệt đệ quy
        for (int i = 0; i < root.childCount; i++)
            SearchHandlesRecursive(root.GetChild(i), ref foundLeft, ref foundRight);
    }

    // ===================================================================
    // ARM DISCOVERY — AutomaticArmDown
    // ===================================================================


    /// <summary>
    /// Duyệt toàn bộ skeleton của bodyRoot để tìm xương tay theo keyword.
    /// Ưu tiên leftUpperArm / rightUpperArm nếu đã gán thủ công.
    /// Tính toán targetRestRot (tay buông xuống dọc thân) từ armDownAngleX.
    /// 
    /// CẢI TIẾN RIGIFY:
    /// - Nhận diện đúng suffix `.L` / `.R` (ví dụ: UpperArm.L, UpperArm.R)
    /// - Hỗ trợ `.L_` và `_L` convention
    /// - Hỗ trợ `Left_` / `Right_` prefix
    /// </summary>
    private void DiscoverAndRegisterArms()
    {
        discoveredArms.Clear();

        // Nếu đã gán thủ công → dùng luôn, không auto-discover
        if (leftUpperArm != null)
            RegisterArm(leftUpperArm, isLeft: true);

        if (rightUpperArm != null)
            RegisterArm(rightUpperArm, isLeft: false);

        // Nếu chưa gán đủ → auto-discover
        if (leftUpperArm == null || rightUpperArm == null)
            SearchArmBones(bodyRoot, leftUpperArm == null, rightUpperArm == null);

        Log($"AutoDiscovery hoàn tất: {discoveredArms.Count} xương tay tìm thấy.");
    }

    /// <summary>
    /// Đệ quy toàn bộ cây xương, đăng ký các xương khớp keyword tay.
    /// Cải tiến: Xử lý chính xác Rigify convention (.L, .R, Left_, Right_, v.v.)
    /// </summary>
    private void SearchArmBones(Transform root, bool needLeft, bool needRight)
    {
        if (root == null) return;

        string nameLower = root.name.ToLower();

        // Kiểm tra có khớp keyword tay không
        if (MatchesKeywords(nameLower, armKeywords))
        {
            // Xác định TRÁI/PHẢI dựa trên Rigify convention
            bool isLeft = DetermineArmSideRigify(nameLower);

            // Tránh đăng ký trùng
            bool alreadyAdded = discoveredArms.Exists(a => a.bone == root);
            if (!alreadyAdded)
            {
                if (isLeft && needLeft)
                    RegisterArm(root, isLeft: true);
                else if (!isLeft && needRight)
                    RegisterArm(root, isLeft: false);
            }
        }

        for (int i = 0; i < root.childCount; i++)
            SearchArmBones(root.GetChild(i), needLeft, needRight);
    }

    /// <summary>
    /// Xác định xương tay là TRÁI hay PHẢI dựa trên Rigify naming convention.
    /// 
    /// RIGIFY FORMAT SUPPORT:
    /// - `.L` suffix  (ví dụ: UpperArm.L, Forearm.L, Hand.L)
    /// - `.R` suffix  (ví dụ: UpperArm.R, Forearm.R, Hand.R)
    /// - `Left_`/`Right_` prefix (ví dụ: Left_Shoulder, Right_Shoulder)
    /// - `_L`/`_R` suffix (ví dụ: Arm_L, Arm_R)
    /// - Legacy: chỉ chứa "l" hay "r" (fallback, nhưng ít chính xác)
    /// </summary>
    private bool DetermineArmSideRigify(string nameLower)
    {
        // Priority 1: Rigify `.L` / `.R` (most reliable)
        if (nameLower.EndsWith(".l"))
            return true;
        if (nameLower.EndsWith(".r"))
            return false;

        // Priority 2: `_L` / `_R` suffix
        if (nameLower.EndsWith("_l"))
            return true;
        if (nameLower.EndsWith("_r"))
            return false;

        // Priority 3: `left_` / `right_` prefix
        if (nameLower.StartsWith("left_"))
            return true;
        if (nameLower.StartsWith("right_"))
            return false;

        // Priority 4: Legacy: chứa "left" hoặc "right" (toàn bộ từ)
        if (nameLower.Contains("left"))
            return true;
        if (nameLower.Contains("right"))
            return false;

        // Fallback: Nếu không rõ, mặc định là LEFT
        // (và log warning để user check)
        Log($"⚠ Không xác định được trái/phải cho xương '{nameLower}', mặc định = TRÁI");
        return true;
    }

    /// <summary>Đăng ký một xương tay vào danh sách và tính targetRestRot.</summary>
    private void RegisterArm(Transform bone, bool isLeft)
    {
        if (isLeft)
        {
            if (leftUpperArm == null) leftUpperArm = bone;
        }
        else
        {
            if (rightUpperArm == null) rightUpperArm = bone;
        }

        // targetRestRot: xoay armDownAngleX độ quanh trục X local
        // Tay trái: góc dương, tay phải: âm (hoặc tuỳ convention của model)
        float angle = isLeft ? armDownAngleX : -armDownAngleX;
        Quaternion target = bone.localRotation * Quaternion.Euler(angle, 0f, 0f);

        // Tìm handle tương ứng
        Transform handle = isLeft ? cartHandleLeft : cartHandleRight;

        discoveredArms.Add(new ArmData
        {
            bone = bone,
            originLocalRot = bone.localRotation,
            targetRestRot = target,
            isLeft = isLeft,
            handleTarget = handle
        });

        Log($"  → Đăng ký xương {(isLeft ? "TRÁI" : "PHẢI")}: {bone.name} (handle: {handle?.name ?? "None"})");
    }

    // ===================================================================
    // ROLLING HANDS DISCOVERY
    // ===================================================================

    /// <summary>
    /// Duyệt skeleton tìm xương Hand/Wrist để dùng cho rolling motion.
    /// Hỗ trợ Rigify convention (.L, .R, _L, _R, Left_, Right_)
    /// </summary>
    private void DiscoverRollingHands()
    {
        discoveredRollingHands.Clear();
        SearchRollingBones(bodyRoot);
        Log($"DiscoverRollingHands: {discoveredRollingHands.Count} xương tay tìm thấy.");
    }

    /// <summary>Duyệt đệ quy tìm xương Hand/Wrist theo keywords.</summary>
    private void SearchRollingBones(Transform root)
    {
        if (root == null) return;

        string nameLower = root.name.ToLower();

        // Kiểm tra có khớp keyword rolling hand không
        if (MatchesKeywords(nameLower, rollingHandKeywords))
        {
            // Xác định trái/phải
            bool isLeft = DetermineArmSideRigify(nameLower);

            // Tránh đăng ký trùng
            bool alreadyAdded = discoveredRollingHands.Exists(h => h.bone == root);
            if (!alreadyAdded)
            {
                discoveredRollingHands.Add(new RollingHandData
                {
                    bone = root,
                    originLocalRot = root.localRotation,
                    isLeft = isLeft
                });

                Log($"  → Đăng ký rolling hand {(isLeft ? "TRÁI" : "PHẢI")}: {root.name}");
            }
        }

        // Duyệt đệ quy
        for (int i = 0; i < root.childCount; i++)
            SearchRollingBones(root.GetChild(i));
    }

    // ===================================================================
    // ROLLING HANDS MOTION — Co Duỗi Tay Khi Cuốn Bánh
    // ===================================================================

    /// <summary>
    /// Khi đang cuốn bánh, tay nhúc nhích, co duỗi liên tục ở phía trước ngực.
    /// Giả lập động tác rải dừa, trải bánh tráng, cuốn bánh.
    /// 
    /// Chiến lược:
    /// - Xoay tay (wrist/hand) quanh trục Y với tần số cao (6Hz) → nhúc nhích nhanh
    /// - Cộng thêm xoay trục Z để tay nhô/rút về phía ngực
    /// - Tay trái/phải vung cùng pha (không ngược như đi bộ)
    /// </summary>
    private void ApplyRollingHands()
    {
        if (discoveredRollingHands.Count == 0)
            return;

        for (int i = 0; i < discoveredRollingHands.Count; i++)
        {
            RollingHandData hand = discoveredRollingHands[i];
            if (hand.bone == null) continue;

            // Sine wave chính cho động tác nhúc nhích (Y-axis: xoay cổ tay)
            float rotY = rollingHandAmplitude
                       * Mathf.Sin(rollingHandFrequency * sineTimer * Mathf.PI * 2f)
                       * Mathf.Clamp01(rollingHandLimitY / 45f);

            // Sine wave phụ cho động tác co/duỗi (Z-axis: co vào/thout phía ngực)
            // Dùng frequency cao hơn để tạo chi tiết
            float rotZ = (rollingHandLimitZ * 0.5f)
                       * Mathf.Sin(rollingHandFrequency * sineTimer * Mathf.PI * 2f + Mathf.PI * 0.5f);

            // Tay trái và phải vung cùng pha (không ngược) khi cuốn bánh
            Quaternion rollingRot = Quaternion.Euler(0f, rotY, rotZ);

            // Áp dụng rotation gốc + rolling
            hand.bone.localRotation = hand.originLocalRot * rollingRot;
        }
    }

    // ===================================================================
    // ARM MOTION — Arm Down + Fake Walk Swing
    // ===================================================================

    /// <summary>
    /// Kết hợp các lớp rotation cho mỗi xương tay (UpperArm/Forearm).
    /// QUAN TRỌNG: Chỉ CỘNG thêm delta rotation vào originLocalRot, không ghi đè.
    /// 
    /// Chiến lược Rigify:
    ///   Lớp 1 (ArmDown) : Cộng delta rotation để tay xuống dọc thân (tránh T-Pose)
    ///   Lớp 2 (HandleGrip): Cộng thêm để tay hướng handle (nếu bật)
    ///   Lớp 3 (Swing): Cộng xoay X sine wave khi đi bộ, nhưng giới hạn biên độ
    /// 
    /// Tay trái/phải vung ngược pha để mô phỏng dáng đi thật (L:+π, R:-π).
    /// </summary>
    private void ApplyArmMotion(float speedScale)
    {
        if (discoveredArms.Count == 0) return;

        for (int i = 0; i < discoveredArms.Count; i++)
        {
            ArmData arm = discoveredArms[i];
            if (arm.bone == null) continue;

            // Bắt đầu từ rotation gốc
            Quaternion finalRot = arm.originLocalRot;

            // --- Lớp 1: ArmDown (Ép tay xuống dọc thân) ---
            float angle = arm.isLeft ? armDownAngleX : -armDownAngleX;
            Quaternion deltaDown = Quaternion.Euler(angle, 0f, 0f);

            // Cưỡng chế xoay xương bắp tay xuôi xuống một góc 70 độ để phá vỡ hoàn toàn tư thế T-Pose mặc định
            finalRot = arm.originLocalRot * deltaDown;

            // --- Lớp 2: HandleGrip (Deprecated) ---
            bool shouldGrip = false;

            // --- Lớp 3: Swing (chỉ khi enableArmSwing và đang đi bộ) ---
            if (enableArmSwing && walkBlend > 0f)
            {
                // Tay trái và phải vung ngược pha
                float phaseSide = arm.isLeft ? phaseOffset : phaseOffset + Mathf.PI;

                // Giới hạn biên độ nếu bám vào handle
                float maxSwingAngle = shouldGrip ? handleGripSwingLimitX : armSwingAmplitude;

                float swingAngle = maxSwingAngle * speedScale
                                 * Mathf.Sin(armSwingFrequency * sineTimer + phaseSide)
                                 * walkBlend;

                Quaternion deltaSwing = Quaternion.Euler(swingAngle, 0f, 0f);
                finalRot = finalRot * deltaSwing;
            }

            // Áp dụng final rotation
            arm.bone.localRotation = finalRot;
        }
    }

    // ===================================================================
    // BODY MOTION
    // ===================================================================

    /// <summary>
    /// Áp dụng slight sway rotation khi WALKING. Cưỡng chế cộng dồn vị trí dời Local (Bob Y) 
    /// để cả thân người nhấp nhô di chuyển sinh động theo nhịp phím bấm.
    /// </summary>
    private void ApplyBodyMotion(float speedScale)
    {
        if (walkBlend <= 0.01f)
            return;

        float t = sineTimer + phaseOffset;

        float speed01 = Mathf.Clamp01(currentComputedSpeed / Mathf.Max(referenceSpeed, 0.0001f));
        float followSharpness = Mathf.Lerp(4f, 12f, speed01);
        float followFactor = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);

        // Chỉ xoay nhẹ trục Z khi walking
        float swayZ = swayAmplitudeZ * speedScale
                    * Mathf.Sin(swayFrequencyZ * t + Mathf.PI * 0.5f)
                    * walkBlend;

        float swayX = swayAmplitudeX * speedScale
                    * Mathf.Sin(swayFrequencyZ * t + Mathf.PI * 0.25f)
                    * walkBlend;

        Vector3 targetLocalEuler = new Vector3(swayX, 0f, swayZ);

        smoothedBodyRootLocalEuler = Vector3.Lerp(smoothedBodyRootLocalEuler, targetLocalEuler, followFactor);

        // Chỉ áp dụng rotation sway
        bodyRoot.localRotation = bodyRootOriginLocalRot
                       * Quaternion.Euler(smoothedBodyRootLocalEuler);
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    /// <summary>Reset toàn bộ về trạng thái gốc. Gọi khi NPC despawn / teleport.</summary>
    public void ResetToOrigin()
    {
        if (bodyRoot == null) return;

        bodyRoot.localPosition = bodyRootOriginLocalPos;
        bodyRoot.localRotation = bodyRootOriginLocalRot;

        foreach (var arm in discoveredArms)
            if (arm.bone != null)
                arm.bone.localRotation = arm.originLocalRot;

        sineTimer = 0f;
        walkBlend = 0f;
        armDownT = 0f;
    }

    // SetCartController deprecated.

    /// <summary>Chuyển mode runtime (ví dụ: NPC đến quán → StaticMode).</summary>
    public void SetMode(WalkerMode newMode)
    {
        walkerMode = newMode;
        Log($"Mode đổi thành: {newMode}");
    }

    /// <summary>Chỉnh tần số bộ từ bên ngoài.</summary>
    public void SetWalkFrequency(float newBobFreq, float newSwayFreq)
    {
        bobFrequency = Mathf.Max(0f, newBobFreq);
        swayFrequencyZ = Mathf.Max(0f, newSwayFreq);
        armSwingFrequency = newBobFreq;
    }

    // ===================================================================
    // HELPERS
    // ===================================================================

    /// <summary>Kiểm tra tên xương có chứa keyword nào không (case-insensitive).</summary>
    private bool MatchesKeywords(string boneName, string[] keywords)
    {
        foreach (string kw in keywords)
            if (boneName.Contains(kw)) return true;
        return false;
    }

    private void Log(string msg)
    {
        if (enableDebugLog)
            Debug.Log($"[ProceduralWalker:{name}] {msg}");
    }

    // ===================================================================
    // GIZMOS
    // ===================================================================

    private void OnDrawGizmosSelected()
    {
        if (bodyRoot != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(bodyRoot.position, 0.08f);
        }

        // Vẽ xương tay đã discover
        foreach (var arm in discoveredArms)
        {
            if (arm.bone == null) continue;
            Gizmos.color = arm.isLeft ? new Color(0f, 1f, 0.4f, 0.7f) : new Color(1f, 0.3f, 0.3f, 0.7f);
            Gizmos.DrawWireSphere(arm.bone.position, 0.05f);
        }
    }
}
