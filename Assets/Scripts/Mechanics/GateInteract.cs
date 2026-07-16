using UnityEngine;

public class GateInteract : MonoBehaviour
{
    [Header("Cấu hình khóa cửa")]
    [Tooltip("Nếu true, người chơi sẽ không thể tương tác để mở cửa này.")]
    [SerializeField] private bool isLocked = false;

    private bool playerInRange = false;
    private Transform playerTransform;
    private bool isOpen = false;
    private bool isOpening = false;
    private Transform pivotLeft;
    private Transform pivotRight;
    private float currentAngle = 0f;
    private float targetAngle = 90f;
    private float openSpeed = 60f; // degrees per second

    void Start()
    {
        // Tự động khóa nếu đây là cửa Villa (Gate_Interactive) để tránh người chơi mở tự do ở Phase 1/2
        if (gameObject.name == "Gate_Interactive")
        {
            isLocked = true;
        }

        // Force domain reload to refresh MCP
        pivotLeft = transform.Find("pivot left");
        pivotRight = transform.Find("pivot right");
    }

    void Update()
    {
        if (isLocked) return;

        bool isFacingGate = CheckIfFacingGate();

        if (playerInRange && isFacingGate && !isOpening && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;
            isOpening = true;
            targetAngle = isOpen ? 90f : 0f;
        }

        if (isOpening && pivotLeft != null && pivotRight != null)
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, openSpeed * Time.deltaTime);
            pivotLeft.localRotation = Quaternion.Euler(0, -currentAngle, 0);
            pivotRight.localRotation = Quaternion.Euler(0, currentAngle, 0);

            if (currentAngle == targetAngle)
            {
                isOpening = false;
            }
        }
    }

    /// <summary>Mở cổng theo luồng cốt truyện, bỏ qua yêu cầu người chơi phải đứng đúng góc nhìn.</summary>
    public void OpenForStory()
    {
        EnsurePivots();
        isLocked = false;
        isOpen = true;
        isOpening = true;
        targetAngle = 90f;
    }

    /// <summary>Đặt nhanh góc cổng cho cutscene mà vẫn giữ trạng thái khóa tương tác.</summary>
    public void SetVisualAngle(float angle)
    {
        EnsurePivots();
        currentAngle = Mathf.Clamp(angle, 0f, 90f);
        targetAngle = currentAngle;
        isOpen = currentAngle > 0.01f;
        isOpening = false;

        if (pivotLeft != null) pivotLeft.localRotation = Quaternion.Euler(0, -currentAngle, 0);
        if (pivotRight != null) pivotRight.localRotation = Quaternion.Euler(0, currentAngle, 0);
    }

    private void EnsurePivots()
    {
        if (pivotLeft == null) pivotLeft = transform.Find("pivot left");
        if (pivotRight == null) pivotRight = transform.Find("pivot right");
    }

    private bool CheckIfFacingGate()
    {
        if (playerTransform == null) return false;

        // Calculate direction from player to gate (ignoring Y axis height difference)
        Vector3 directionToGate = (transform.position - playerTransform.position);
        directionToGate.y = 0;
        directionToGate.Normalize();

        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        // Dot product: 1 means same direction, 0 means perpendicular, -1 means opposite direction
        float dot = Vector3.Dot(playerForward, directionToGate);
        
        // 0.4f is roughly a 66-degree field of view on either side (total 132 degrees)
        return dot > 0.4f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLocked) return;

        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerTransform = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isLocked) return;

        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerTransform = null;
        }
    }

    private void OnGUI()
    {
        if (isLocked) return;

        bool isFacingGate = CheckIfFacingGate();

        if (playerInRange && isFacingGate && !isOpening)
        {
            string msg = isOpen ? "Press E to close" : "Press E to open";
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            // Draw a subtle shadow for readability
            GUI.Label(new Rect(Screen.width / 2f - 100 + 2, Screen.height / 2f + 50 + 2, 200, 50), msg, style);
            
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(Screen.width / 2f - 100, Screen.height / 2f + 50, 200, 50), msg, style);
        }
    }
}

