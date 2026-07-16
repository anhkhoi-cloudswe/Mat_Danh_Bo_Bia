using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay hien ten ingredient va dau "x" ngay tam man hinh khi ray dang hover dung vao
/// mot nguyen lieu trong gameplay cuon bo bia.
/// Tu bootstrap neu scene chua co UI san.
/// </summary>
public class CrosshairLabelUI : MonoBehaviour
{
    private static CrosshairLabelUI _instance;
    public static CrosshairLabelUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CrosshairLabelUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    GameObject root = new GameObject("CrosshairLabelUI");
                    root.AddComponent<Canvas>();
                    root.AddComponent<CanvasScaler>();
                    root.AddComponent<GraphicRaycaster>();
                    _instance = root.AddComponent<CrosshairLabelUI>();
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private GameObject labelContainer;
    [SerializeField] private TextMeshProUGUI crosshairText;
    [SerializeField] private GameObject crosshairContainer;

    [Header("Theme")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.78f, 0.78f, 0.78f, 0.95f);
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color hintColor = new Color(0.4f, 0.9f, 1f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapOnLoad()
    {
        var dummy = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        
        // Force the theme colors to white/solid grey as requested by user
        activeColor = Color.white;
        crosshairColor = Color.white;
        inactiveColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        // Disable the black background image on the container
        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.enabled = false;
        }

        BuildIfNeeded();
        Hide();
    }

    public void SetPosition(Vector2 screenPosition)
    {
        BuildIfNeeded();

        // Find the parent Canvas (excluding ourselves, to avoid feedback loops)
        Canvas parentCanvas = null;
        Transform p = transform.parent;
        while (p != null)
        {
            Canvas c = p.GetComponent<Canvas>();
            if (c != null)
            {
                parentCanvas = c;
                break;
            }
            p = p.parent;
        }

        RectTransform canvasRect = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        RectTransform myRect = GetComponent<RectTransform>();
        
        if (canvasRect != null && myRect != null)
        {
            Vector2 localPoint;
            // ScreenSpaceOverlay Canvas uses null for camera in ScreenPointToLocalPointInRectangle
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out localPoint))
            {
                // Position the parent container (this GameObject) exactly at the screenPosition
                myRect.anchorMin = new Vector2(0.5f, 0.5f);
                myRect.anchorMax = new Vector2(0.5f, 0.5f);
                myRect.pivot = new Vector2(0.5f, 0.5f);
                myRect.anchoredPosition = localPoint;

                // Position the crosshair at the center of this container (aiming point)
                if (crosshairText != null)
                {
                    RectTransform crosshairRect = crosshairText.GetComponent<RectTransform>();
                    if (crosshairRect != null)
                    {
                        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
                        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
                        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
                        crosshairRect.anchoredPosition = Vector2.zero;
                    }
                }

                // Position the text label above the crosshair
                if (labelText != null)
                {
                    RectTransform labelRect = labelText.GetComponent<RectTransform>();
                    if (labelRect != null)
                    {
                        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                        labelRect.pivot = new Vector2(0.5f, 0.5f);
                        labelRect.anchoredPosition = new Vector2(0f, 60f); // 60 units above
                    }
                }
            }
        }
    }

    public void Show(string name, bool isInteractable, Vector2 screenPos)
    {
        SetPosition(screenPos);
        Show(name, isInteractable);
    }

    public void Show(string name, bool isInteractable)
    {
        BuildIfNeeded();

        if (labelContainer != null)
        {
            labelContainer.SetActive(true);
        }

        if (crosshairContainer != null)
        {
            crosshairContainer.SetActive(true);
        }

        if (labelText != null)
        {
            labelText.text = name;
            Color targetColor = isInteractable ? activeColor : inactiveColor;
            labelText.color = targetColor;
            labelText.faceColor = targetColor;
        }

        if (crosshairText != null)
        {
            crosshairText.text = "x";
            Color targetColor = isInteractable ? crosshairColor : inactiveColor;
            crosshairText.color = targetColor;
            crosshairText.faceColor = targetColor;
        }
    }

    public void Hide()
    {
        if (labelContainer != null)
        {
            labelContainer.SetActive(false);
        }

        if (crosshairContainer != null)
        {
            crosshairContainer.SetActive(false);
        }

        if (labelText != null)
        {
            labelText.text = string.Empty;
        }

        if (crosshairText != null)
        {
            crosshairText.text = string.Empty;
        }
    }

    public void ShowHint(string hintMessage)
    {
        BuildIfNeeded();

        if (labelContainer != null)
        {
            labelContainer.SetActive(true);
        }

        if (crosshairContainer != null)
        {
            crosshairContainer.SetActive(false);
        }

        if (labelText != null)
        {
            labelText.text = hintMessage;
            labelText.color = hintColor;
            labelText.faceColor = hintColor;
        }
    }

    private void BuildIfNeeded()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        if (canvas.gameObject == gameObject)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (labelContainer == null)
        {
            labelContainer = CreateTextObject("IngredientLabel", new Vector2(0f, 64f), new Vector2(700f, 90f), 48f, out labelText);
        }
        else if (labelText == null)
        {
            labelText = labelContainer.GetComponent<TextMeshProUGUI>();
        }

        if (crosshairContainer == null)
        {
            crosshairContainer = CreateTextObject("CrosshairX", new Vector2(0f, 8f), new Vector2(64f, 64f), 44f, out crosshairText);
        }
        else if (crosshairText == null)
        {
            crosshairText = crosshairContainer.GetComponent<TextMeshProUGUI>();
        }

        ConfigureText(labelText, 65f); // Increased from 48f
        ConfigureText(crosshairText, 60f); // Increased from 44f
    }

    private GameObject CreateTextObject(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, out TextMeshProUGUI text)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(transform, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text = obj.AddComponent<TextMeshProUGUI>();
        ConfigureText(text, fontSize);
        return obj;
    }

    private void ConfigureText(TextMeshProUGUI text, float fontSize)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        
        // Make outline thicker and solid black to stand out on bright backgrounds
        text.outlineWidth = 0.35f;
        text.outlineColor = Color.black;

        // Make the font bold so it's thicker and easier to see
        text.fontStyle = FontStyles.Bold;
        
        // Force text color and faceColor to white to clear any inspector override
        text.color = Color.white;
        text.faceColor = Color.white;
    }
}
