using UnityEngine;

/// <summary>
/// Script điều khiển camera góc nhìn thứ nhất (First-Person Camera).
/// Xoay camera theo chiều dọc (trục X) và xoay nhân vật theo chiều ngang (trục Y) bằng chuột.
/// </summary>
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Cấu Hình Camera")]
    [Tooltip("Độ nhạy của chuột khi xoay camera.")]
    [SerializeField] private float mouseSensitivity = 100f;

    [Tooltip("Transform của thân nhân vật để xoay ngang theo chuột.")]
    [SerializeField] private Transform playerBody;

    private float xRotation = 0f;

    private void Start()
    {
        // Khóa con trỏ chuột vào giữa màn hình và ẩn đi
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Đọc giá trị di chuyển chuột
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Tính toán góc xoay dọc (quay quanh trục X) và giới hạn góc nhìn từ -90 đến 90 độ
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Áp dụng xoay dọc cho camera
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Áp dụng xoay ngang cho thân nhân vật (quay quanh trục Y)
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }

    /// <summary>
    /// Đồng bộ góc xoay dọc từ bên ngoài (tránh camera bị giật ngược khi teleport).
    /// </summary>
    public void SetRotation(float xRot)
    {
        xRotation = Mathf.Clamp(xRot, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
