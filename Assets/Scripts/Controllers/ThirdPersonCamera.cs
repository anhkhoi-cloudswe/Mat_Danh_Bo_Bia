using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 0.0f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.55f, 0.15f);

    [Header("Mouse Orbit")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 40f; // Lowered to 40f to prevent camera from rising too high

    [Header("Cooking Focus")]
    [SerializeField] private float cookingDistance = 0.0f;
    [SerializeField] private Vector3 cookingTargetOffset = new Vector3(0f, 1.55f, 0.15f);
    [SerializeField] private float cameraBlendSpeed = 5f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float rotationSmoothSpeed = 14f;
    [SerializeField] private float targetYawSmoothSpeed = 12f;
    [SerializeField] private bool rotateTargetWithCamera = true;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private Vector3 currentTargetOffset;
    private Vector3 positionVelocity;
    private Quaternion currentRotation;
    private BoBiaMechanic boBiaMechanic;
    private PlayerMovement targetMovement;

    private bool isFocusing = false;
    private Vector3 focusTargetPos;
    private float focusTargetPitch;
    private float focusTransitionSpeed = 2f;

    public bool IsRotationLocked { get; set; } = false;

    public void StartFocus(Vector3 targetPosition, float targetPitch = 12f, float speed = 2f)
    {
        isFocusing = true;
        focusTargetPos = targetPosition;
        focusTargetPitch = targetPitch;
        focusTransitionSpeed = speed;
        IsRotationLocked = true;
    }

    public void StopFocus()
    {
        isFocusing = false;
        IsRotationLocked = false;
    }

    public void ResetOrientation()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pitch = 10f;
        }
        else
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }
        currentRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void OnEnable()
    {
        ResetOrientation();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentDistance = distance;
        currentTargetOffset = targetOffset;
        boBiaMechanic = Object.FindAnyObjectByType<BoBiaMechanic>();
        targetMovement = target != null ? target.GetComponent<PlayerMovement>() : null;

        // Kh?i t?o yaw t? hu?ng xoay c?a target (nhân v?t), không ph?i camera transform.
        // Ði?u này d?m b?o camera luôn nhìn cùng hu?ng v?i nhân v?t khi b?t d?u,
        // b?t k? th? t? Script Execution Order v?i StoryPhase1/2Manager.
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pitch = 10f; // Góc nhìn xu?ng nh? khi b?t d?u
        }
        else
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }
        currentRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Update()
    {
        if (target == null) return;

        // Toggle Cursor Lock when pressing LeftAlt
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (IsRotationLocked) return;
        if (Cursor.lockState != CursorLockMode.Locked) return; // Freeze camera rotation if cursor is unlocked

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity * 0.02f;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * 0.02f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (isFocusing)
        {
            Vector3 direction = focusTargetPos - target.position;
            direction.y = 0f;
            if (direction.magnitude > 0.1f)
            {
                float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, focusTransitionSpeed * 50f * Time.deltaTime);
                pitch = Mathf.MoveTowards(pitch, focusTargetPitch, focusTransitionSpeed * 30f * Time.deltaTime);
            }
        }

        bool targetIsMoving = targetMovement != null && targetMovement.CurrentSpeed > 0.1f;
        if (rotateTargetWithCamera && !targetIsMoving)
        {
            float targetYawBlend = 1f - Mathf.Exp(-targetYawSmoothSpeed * Time.deltaTime);
            Quaternion desiredTargetRotation = Quaternion.Euler(0f, yaw, 0f);
            target.rotation = Quaternion.Slerp(target.rotation, desiredTargetRotation, targetYawBlend);
        }

        bool isCooking = false; // Gi? nguyên camera góc nhìn th? nh?t bình thu?ng
        float targetDistance = isCooking ? cookingDistance : distance;
        Vector3 targetOffsetValue = isCooking ? cookingTargetOffset : targetOffset;

        float cameraBlend = 1f - Mathf.Exp(-cameraBlendSpeed * Time.deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, cameraBlend);
        currentTargetOffset = Vector3.Lerp(currentTargetOffset, targetOffsetValue, cameraBlend);

        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = target.position + currentTargetOffset;
        Vector3 desiredPosition = targetPosition - (desiredRotation * Vector3.forward * currentDistance);

        // Perform camera collision check to prevent clipping through walls
        Vector3 collisionDir = desiredPosition - targetPosition;
        float collisionCheckDist = collisionDir.magnitude;
        if (collisionCheckDist > 0.01f)
        {
            collisionDir.Normalize();
            // SphereCast with 0.2f radius to account for near clip plane
            RaycastHit[] hits = Physics.SphereCastAll(targetPosition, 0.2f, collisionDir, collisionCheckDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float closestDistance = collisionCheckDist;

            foreach (var hit in hits)
            {
                // Ignore the player character, cart, and anything containing "Player" or "Xe_Bo_Bia"
                if (hit.transform == target || hit.transform.IsChildOf(target))
                    continue;
                if (hit.transform.name.Contains("Xe_Bo_Bia") || hit.transform.name.Contains("Player"))
                    continue;

                Transform p = hit.transform.parent;
                bool shouldIgnore = false;
                while (p != null)
                {
                    if (p == target || p.name.Contains("Xe_Bo_Bia") || p.name.Contains("Player"))
                    {
                        shouldIgnore = true;
                        break;
                    }
                    p = p.parent;
                }
                if (shouldIgnore) continue;

                // Adjust to hit distance minus safety margin
                if (hit.distance < closestDistance)
                {
                    closestDistance = Mathf.Max(0.15f, hit.distance - 0.05f);
                }
            }
            desiredPosition = targetPosition + collisionDir * closestDistance;
        }

        if (currentDistance <= 0.1f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
        }
        currentRotation = Quaternion.Slerp(
            currentRotation,
            desiredRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));
        transform.rotation = currentRotation;
    }
}
