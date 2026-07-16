using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Xử lý tương tác thu thập cho từng vật chứng riêng lẻ trong Villa.
/// </summary>
[DisallowMultipleComponent]
public class VillaEvidenceItem : MonoBehaviour
{
    // ===================================================================
    // FIELDS
    // ===================================================================

    [Header("=== Cấu hình Bằng chứng ===")]
    [SerializeField] private EvidenceData evidenceData;
    [SerializeField] private float interactRange = 2.0f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Kích thước tối thiểu của trigger theo world-space để Player dễ đi vào.")]
    [SerializeField] private Vector3 triggerWorldSize = new Vector3(1.6f, 2f, 1.6f);

    [Header("=== Hiệu ứng di chuyển ===")]
    [SerializeField] private float rotateSpeed = 60f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobFrequency = 2.0f;

    // ===================================================================
    // PRIVATE STATE
    // ===================================================================

    private Transform player;
    private float baseY;
    private float timeSeed;
    private VillaEvidenceUI ui;
    [SerializeField] private VillaInvestigation manager;
    private bool canCollect;
    private bool currentPlayerInside;
    private bool isCollected = false;

    public bool CanCollect => canCollect;
    public bool CurrentPlayerInside => currentPlayerInside;
    public bool IsCollected => isCollected;
    public VillaInvestigation Manager => manager;

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    public void Init(EvidenceData data, VillaInvestigation parentManager)
    {
        this.evidenceData = data;
        this.manager = parentManager;
        this.baseY = transform.position.y;
        this.timeSeed = Random.value * 10f;

        EnsurePlayer();
        EnsureUI();
        EnsureCollider();
    }

    // ===================================================================
    // UNITY LIFECYCLE
    // ===================================================================

    private void Awake()
    {
        baseY = transform.position.y;
        timeSeed = Random.value * 10f;
    }

    private void Start()
    {
        EnsurePlayer();
        EnsureUI();
        EnsureCollider();
    }

    /// <summary>
    /// Thu thập bằng CLICK CHUỘT TRÁI trực tiếp vào vật chứng.
    /// Unity gọi OnMouseDown khi click trúng Collider gắn trên chính GameObject này.
    /// </summary>
    private void OnMouseDownDisabled()
    {
        if (isCollected) return;

        EnsurePlayer();

        // Kiểm tra khoảng cách: chỉ cho thu thập khi đứng đủ gần (giống cơ chế phím E).
        if (player != null)
        {
            float dist = Vector3.Distance(player.position, transform.position);
            if (dist > interactRange)
            {
                ShowTooFarHint();
                return;
            }
        }

        Collect();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        canCollect = true;
        currentPlayerInside = true;
        EnsureUI();
        if (ui != null) ui.ShowPrompt(this, "Nhấn <color=#FFFF00>[E]</color> để thu thập chứng cứ");
        Debug.Log($"Player entered evidence trigger: {name}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        canCollect = false;
        currentPlayerInside = false;
        if (ui != null) ui.HidePrompt(this);
    }

    private void Update()
    {
        if (isCollected) return;

        // --- Hiệu ứng xoay + nhấp nhô nhịp nhàng ---
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin((Time.time + timeSeed) * bobFrequency) * bobAmplitude;
        transform.position = pos;

        // --- Check trigger + khoảng cách dự phòng với Player ---
        // CharacterController có thể bị teleport vào vùng khiến OnTriggerEnter không chạy.
        EnsurePlayer();
        bool nearByDistance = player != null
            && Vector3.Distance(player.position, transform.position) <= interactRange;
        canCollect = currentPlayerInside || nearByDistance;

        if (canCollect && !isCollected)
        {
            if (ui != null) ui.ShowPrompt(this, "Nhấn <color=#FFFF00>[E]</color> để thu thập chứng cứ");

            if (Input.GetKeyDown(interactKey))
            {
                Debug.Log($"Pressed E on evidence: {name}");
                Collect();
            }
        }
        else
        {
            if (ui != null)
            {
                ui.HidePrompt(this);
            }
        }
    }

    private void OnDestroy()
    {
        // Prompt là UI dùng chung cho toàn bộ tương tác trong Villa, không hủy theo từng vật chứng.
        if (ui != null) ui.HidePrompt(this);
    }

    // ===================================================================
    // HELPER METHODS
    // ===================================================================

    private static Transform cachedPlayer;

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null;
    }
    private void EnsurePlayer()
    {
        if (player != null) return;
        if (cachedPlayer != null)
        {
            player = cachedPlayer;
            return;
        }
        
        // Tìm player thông qua tag
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) p = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (p != null)
        {
            player = p.transform;
            cachedPlayer = player;
        }
    }

    private void EnsureUI()
    {
        if (ui != null) return;
        ui = VillaEvidenceUI.GetOrCreate();
    }

    /// <summary>
    /// Đảm bảo có Collider NGAY TRÊN GameObject này để OnMouseDown nhận được cú click chuột.
    /// Nếu vật thể chưa có collider thì tự thêm BoxCollider (isTrigger=true để không cản di chuyển/vật lý;
    /// cú click vẫn nhận vì Physics.queriesHitTriggers mặc định bật).
    /// </summary>
    private void EnsureCollider()
    {
        foreach (Collider existing in GetComponents<Collider>())
            existing.isTrigger = true;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        // Ép kích thước tối thiểu theo world-space; prefab evidence có scale rất nhỏ.
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds; // world-space
            box.center = transform.InverseTransformPoint(b.center);

            Vector3 lossy = transform.lossyScale;
            Vector3 worldSize = new Vector3(
                Mathf.Max(b.size.x, triggerWorldSize.x),
                Mathf.Max(b.size.y, triggerWorldSize.y),
                Mathf.Max(b.size.z, triggerWorldSize.z));
            box.size = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? worldSize.x : worldSize.x / Mathf.Abs(lossy.x),
                Mathf.Approximately(lossy.y, 0f) ? worldSize.y : worldSize.y / Mathf.Abs(lossy.y),
                Mathf.Approximately(lossy.z, 0f) ? worldSize.z : worldSize.z / Mathf.Abs(lossy.z));
        }
        else
        {
            Vector3 lossy = transform.lossyScale;
            box.size = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? triggerWorldSize.x : triggerWorldSize.x / Mathf.Abs(lossy.x),
                Mathf.Approximately(lossy.y, 0f) ? triggerWorldSize.y : triggerWorldSize.y / Mathf.Abs(lossy.y),
                Mathf.Approximately(lossy.z, 0f) ? triggerWorldSize.z : triggerWorldSize.z / Mathf.Abs(lossy.z));
        }

        EnsureKinematicRigidbody();
    }

    private void EnsureKinematicRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
    }

    /// <summary>Nhắc người chơi khi click chuột nhưng đứng quá xa vật chứng.</summary>
    private void ShowTooFarHint()
    {
        if (InternalMonologueManager.Instance != null)
        {
            InternalMonologueManager.Instance.Show("Cần lại gần hơn để thu thập.", 2.5f);
        }
        else if (ui != null && evidenceData != null)
        {
            ui.ShowPrompt(this, "Cần lại gần hơn để thu thập.");
        }
    }

    private void Collect()
    {
        if (isCollected) return;

        if (ui != null)
        {
            ui.HidePrompt(this);
        }

        // Đăng ký bằng chứng vào CaseManager
        if (evidenceData != null && CaseManager.Instance != null)
        {
            CaseManager.Instance.CollectEvidence(evidenceData);
        }

        // Hiện Toast thông báo
        if (evidenceData != null && ClueNotificationManager.Instance != null)
        {
            ClueNotificationManager.Instance.ShowNotification($"🔍 Đã thu thập: {evidenceData.EvidenceName}");
        }

        // Hiện monologue nội tâm
        if (evidenceData != null && InternalMonologueManager.Instance != null)
        {
            string monologue = $"Manh mối: {evidenceData.EvidenceName}. {evidenceData.Content}";
            InternalMonologueManager.Instance.Show(monologue, 4.5f);
        }

        if (manager == null) manager = Object.FindFirstObjectByType<VillaInvestigation>();
        if (manager != null) manager.AddEvidence();

        isCollected = true;
        canCollect = false;
        currentPlayerInside = false;
        Debug.Log($"Evidence collected: {name}");

        // Chỉ tắt item hiện tại; không tác động manager hoặc EvidenceItem khác.
        gameObject.SetActive(false);
    }
}

/// <summary>
/// UI world-space hiển thị prompt tương tác trước camera cho VillaEvidenceItem.
/// </summary>
public class VillaEvidenceUI : MonoBehaviour
{
    private const float CanvasWidth = 800f;
    private const float CanvasHeight = 200f;
    private const float CanvasScale = 0.00062f;
    private const float DistanceFromCamera = 0.5f;

    private Camera attachedCamera;
    private Text promptText;
    private Object promptOwner;
    private Material alwaysOnTop;
    private Font legacyFont;

    public static VillaEvidenceUI Instance { get; private set; }

    /// <summary>Một prompt duy nhất cho toàn bộ vật chứng/cửa trong Villa để không chồng chữ.</summary>
    public static VillaEvidenceUI GetOrCreate()
    {
        if (Instance != null) return Instance;

        VillaEvidenceUI existing = FindFirstObjectByType<VillaEvidenceUI>();
        if (existing != null) return existing;

        GameObject uiObj = new GameObject("VillaInteractionPrompt");
        return uiObj.AddComponent<VillaEvidenceUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Gắn canvas vào trước camera chính giống TrashInvestigationUI
        if (attachedCamera == null || !attachedCamera.isActiveAndEnabled)
        {
            attachedCamera = Camera.main;
            if (attachedCamera != null)
            {
                transform.SetParent(attachedCamera.transform, false);
                transform.localPosition = new Vector3(0f, -0.05f, DistanceFromCamera);
                transform.localRotation = Quaternion.identity;
                transform.localScale = new Vector3(CanvasScale, CanvasScale, CanvasScale);
            }
        }
    }

    public void ShowPrompt(Object owner, string text)
    {
        if (promptText == null || owner == null) return;
        promptOwner = owner;
        promptText.text = text;
        if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
    }

    public void HidePrompt(Object owner)
    {
        if (owner == null || promptOwner != owner) return;
        if (promptText != null && promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(false);
        promptOwner = null;
    }

    private void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 525;

        RectTransform canvasRect = GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        alwaysOnTop = new Material(Shader.Find("UI/Default"));
        alwaysOnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(transform, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(760f, 90f);

        promptText = textObj.AddComponent<Text>();
        promptText.material = alwaysOnTop;
        promptText.font = legacyFont;
        promptText.fontSize = 40;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.supportRichText = true;
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline o = textObj.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(2f, -2f);
        o.useGraphicAlpha = true;

        textObj.SetActive(false);
    }
}
