using UnityEngine;

/// <summary>
/// Script điều khiển nhân vật di chuyển độc lập bằng bàn phím (W, A, S, D).
/// Hỗ trợ đi bộ bình thường, đè SHIFT để chạy nhanh, khóa chân trên mặt đất
/// và đồng bộ hóa mượt mà 3 trạng thái (Idle - Walk - Run) với Animator Blend Tree.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Cấu Hình Tốc Độ")]
    [Tooltip("Tốc độ khi đi bộ bình thường (m/s).")]
    [SerializeField] private float walkSpeed = 3f;

    [Tooltip("Tốc độ khi đè thêm phím SHIFT để chạy nhanh (m/s).")]
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("Độ lớn lực trọng lực áp dụng.")]
    [SerializeField] private float gravity = 9.81f;

    [Header("Movement Facing")]
    [Tooltip("How quickly the character turns to face the movement direction.")]
    [SerializeField] private float turnSmoothSpeed = 14f;

    [Header("Hòa Trộn Hoạt Ảnh (Animation)")]
    [Tooltip("Tốc độ mượt mà khi chuyển đổi giữa Đứng im -> Đi -> Chạy.")]
    [SerializeField] private float animationBlendSpeed = 8f;

    [Header("Visual Correction")]
    [Tooltip("Xương Hông (Hips) hoặc Armature của mô hình để nâng lên khi đứng im.")]
    [SerializeField] private Transform targetVisualBone;

    [Tooltip("Khoảng bù trừ chiều cao (Y offset) khi đứng im.")]
    [SerializeField] private float verticalVisualOffset = 0f;

    private CharacterController characterController;
    private Animator animator;
    private Vector3 moveDirection = Vector3.zero;
    private float currentAnimVelocity = 0f;
    private float currentSpeed = 0f;
    private float verticalVelocity = 0f;
    private float lastGroundedY = 0f;
    private bool isGroundedAndStationary = false;

    private float originalHeight;
    private Vector3 originalCenter;
    private Vector3 originalVisualScale = Vector3.one;
    private bool isCrouching = false;
    private Camera mainCamera;

    public float CurrentSpeed => currentSpeed;
    public bool IsMovementLocked { get; set; } = false;
    public bool IsCrouching
    {
        get => isCrouching;
        set => isCrouching = value;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (characterController != null)
        {
            originalHeight = characterController.height;
            originalCenter = characterController.center;
        }

        if (targetVisualBone == null)
        {
            // Ép ưu tiên tìm lưới hiển thị char1 hoặc tên Mesh trước để nâng da thịt lên
            targetVisualBone = transform.Find("char1");
            if (targetVisualBone == null) targetVisualBone = transform.Find("Mesh");
            if (targetVisualBone == null) targetVisualBone = transform.Find("Armature");
        }

        if (targetVisualBone != null)
        {
            originalVisualScale = targetVisualBone.localScale;
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        // 1. Đọc tín hiệu nút bấm WASD
        float horizontal = IsMovementLocked ? 0f : Input.GetAxisRaw("Horizontal");
        float vertical = IsMovementLocked ? 0f : Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        float targetAnimValue = 0f; // Mặc định đứng im = 0 (Idle)
        float currentMoveSpeed = walkSpeed;

        // 2. Kiểm tra xem người chơi có đang di chuyển không
        if (inputDir.magnitude > 0.1f)
        {
            // Kiểm tra combo đè nút Shift
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentMoveSpeed = runSpeed;
                targetAnimValue = 1f; // Ngưỡng chạy nhanh trong Blend Tree
            }
            else
            {
                currentMoveSpeed = walkSpeed;
                targetAnimValue = 0.5f; // Ngưỡng đi bộ trong Blend Tree
            }

            Vector3 targetDirection = inputDir;

            // Tính toán hướng di chuyển tương đối dựa theo Camera
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            if (mainCamera != null)
            {
                Vector3 camForward = mainCamera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = mainCamera.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                targetDirection = camForward * vertical + camRight * horizontal;
            }

            if (targetDirection.magnitude > 0.1f)
            {
                targetDirection.Normalize();
                moveDirection = targetDirection * currentMoveSpeed;

                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
                float turnBlend = 1f - Mathf.Exp(-turnSmoothSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnBlend);
            }
        }
        else
        {
            moveDirection = Vector3.zero;
            targetAnimValue = 0f; // Đứng im hoàn toàn
        }

        // 3. Xử lý Trọng lực & Di chuyển vật lý bằng Character Controller
        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded)
            {
                // Khi đứng trên mặt đất, giữ một lực hút nhẹ để tránh bị trôi hoặc nảy.
                // Nếu di chuyển (inputDir.magnitude > 0.1f), dùng lực bám sàn trung bình (-0.5f).
                // Nếu đứng im hoàn toàn, dùng lực bám sàn tối thiểu (-0.05f) để tránh xung đột với khớp Hông (Hips pivot).
                if (inputDir.magnitude > 0.1f)
                {
                    verticalVelocity = -0.5f;
                }
                else
                {
                    // Lực hút tối ưu khi đứng im để giữ isGrounded ổn định mà không gây nén lò xo xương
                    verticalVelocity = -0.1f;
                }
            }
            else
            {
                // Tích lũy trọng lực khi rơi tự do (không bị reset mỗi frame)
                verticalVelocity -= gravity * Time.deltaTime;
            }

            // Tách biệt hoàn toàn chuyển động ngang và dọc để tránh lỗi reset vận tốc Y
            Vector3 finalMove = moveDirection;
            finalMove.y = verticalVelocity;

            characterController.Move(finalMove * Time.deltaTime);

            // Keep gravity/ground check clean without direct transform.position updates that trigger jitter
        }
        else
        {
            // Fallback nếu không có CharacterController
            transform.Translate(moveDirection * Time.deltaTime, Space.World);
        }

        // 4. Cập nhật mượt mà trạng thái chuyển động vào Blend Tree
        currentSpeed = inputDir.magnitude > 0.1f ? currentMoveSpeed : 0f;
        UpdateAnimator(targetAnimValue);

        // Crouch interpolation
        UpdateCrouchState();
    }

    private void UpdateCrouchState()
    {
        float targetHeight = isCrouching ? originalHeight * 0.6f : originalHeight;
        Vector3 targetCenter = isCrouching 
            ? new Vector3(originalCenter.x, originalCenter.y * 0.6f, originalCenter.z) 
            : originalCenter;
        Vector3 targetScale = isCrouching 
            ? new Vector3(originalVisualScale.x, originalVisualScale.y * 0.6f, originalVisualScale.z) 
            : originalVisualScale;

        float transitionSpeed = 8f; // control crouching transition speed

        if (characterController != null)
        {
            characterController.height = Mathf.MoveTowards(characterController.height, targetHeight, transitionSpeed * Time.deltaTime);
            characterController.center = Vector3.MoveTowards(characterController.center, targetCenter, transitionSpeed * 0.5f * Time.deltaTime);
        }

        if (targetVisualBone != null)
        {
            targetVisualBone.localScale = Vector3.MoveTowards(targetVisualBone.localScale, targetScale, transitionSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimator(float targetValue)
    {
        if (animator == null) return;

        // Nội suy mượt mà giá trị để nhân vật chuyển từ đi sang chạy không bị giật
        currentAnimVelocity = Mathf.MoveTowards(currentAnimVelocity, targetValue, animationBlendSpeed * Time.deltaTime);

        // Bắn giá trị vào tham số "Blend"
        animator.SetFloat("Blend", currentAnimVelocity);
    }

    private void LateUpdate()
    {
        // Khi nhân vật đứng im trên mặt đất
        if (characterController != null && characterController.enabled && characterController.isGrounded && currentSpeed <= 0.1f)
        {
            if (!isGroundedAndStationary)
            {
                lastGroundedY = transform.position.y;
                isGroundedAndStationary = true;
            }

            Vector3 currentPos = transform.position;
            float targetY = lastGroundedY;

            RaycastHit hit;
            if (Physics.Raycast(currentPos + Vector3.up * 0.5f, Vector3.down, out hit, 1.0f))
            {
                targetY = hit.point.y;
            }

            // Tính toán cao độ mong muốn (Mặt sàn + Khoảng bù lún)
            float desiredY = targetY + verticalVisualOffset;

            // Tính độ lệch Delta Y cần dịch chuyển
            float deltaY = desiredY - currentPos.y;

            // Nếu có độ lệch, ép CharacterController tự dịch chuyển chính nó lên trên
            if (Mathf.Abs(deltaY) > 0.001f)
            {
                characterController.Move(new Vector3(0f, deltaY, 0f));
            }
        }
        else
        {
            isGroundedAndStationary = false;
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
