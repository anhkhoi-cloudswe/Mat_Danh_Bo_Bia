using UnityEngine;

/// <summary>
/// Triệt tiêu hoàn toàn Root Motion bằng cách báo cho Unity biết script tự xử lý di chuyển.
/// Giúp loại bỏ hoàn toàn lỗi giật giật (jitter) do tranh chấp vị trí giữa Animator và script di chuyển.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NpcRootMotionMuter : MonoBehaviour
{
    private void OnAnimatorMove()
    {
        // Phương thức này để trống. Unity sẽ giao toàn bộ quyền xử lý Root Motion cho script này.
        // Vì script không làm gì cả, Root Motion sẽ bị vô hiệu hóa 100% một cách an toàn và sạch sẽ.
    }
}
