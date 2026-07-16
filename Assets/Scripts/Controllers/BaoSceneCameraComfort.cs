using UnityEngine;

public class BaoSceneCameraComfort : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;

    private void Awake()
    {
        if (bodyRenderer == null)
        {
            GameObject body = GameObject.Find("char1");
            if (body != null)
            {
                bodyRenderer = body.GetComponent<SkinnedMeshRenderer>();
            }
        }
    }

    private void LateUpdate()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bool isRolling = BoBiaMechanic.Instance != null && BoBiaMechanic.Instance.IsRolling;
        bool shouldShowBody = !isRolling;

        if (bodyRenderer.enabled != shouldShowBody)
        {
            bodyRenderer.enabled = shouldShowBody;
        }
    }
}
