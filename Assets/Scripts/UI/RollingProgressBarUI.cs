using UnityEngine;
using UnityEngine.UI;

public class RollingProgressBarUI : MonoBehaviour
{
    [SerializeField] private BoBiaMechanic boBiaMechanic;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressSlider;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        Hide();
    }

    private void OnEnable()
    {
        if (boBiaMechanic == null)
        {
            boBiaMechanic = Object.FindAnyObjectByType<BoBiaMechanic>();
        }

        if (boBiaMechanic != null)
        {
            boBiaMechanic.OnRollingProgressChanged += HandleProgressChanged;
            boBiaMechanic.OnRollingProgressVisibleChanged += HandleVisibleChanged;
        }
    }

    private void OnDisable()
    {
        if (boBiaMechanic != null)
        {
            boBiaMechanic.OnRollingProgressChanged -= HandleProgressChanged;
            boBiaMechanic.OnRollingProgressVisibleChanged -= HandleVisibleChanged;
        }
    }

    private void HandleProgressChanged(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    private void HandleVisibleChanged(bool visible)
    {
        if (visible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Hide()
    {
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
