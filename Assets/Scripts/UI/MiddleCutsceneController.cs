using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the middle cutscene sequence in Middle_CutScene.
/// Manages camera blending, character movements, dialogue subtitles, and scene transition.
/// </summary>
public class MiddleCutsceneController : MonoBehaviour
{
    [Header("Characters & Objects")]
    public GameObject mainCharacter;
    public GameObject mobileSoldier1;
    public GameObject mobileSoldier2;
    public GameObject mobileSoldier3;
    public GameObject mobileSoldierUnnumbered;
    public GameObject suspectMiuLe;
    public GameObject suspectHuySeo;

    [Header("Cinematic Cameras")]
    public Camera camBalcony;
    public Camera camAssault;
    public Camera camDialogue;
    public Camera camFinal;

    [Header("Settings")]
    public string nextSceneName = "Final_CutScene";
    public float fadeSpeed = 1.0f;
    public float soldierRunSpeed = 2.6f;


    [Header("Shot Blocking")]
    public Vector3 assaultCameraPosition = new Vector3(-2.0f, 1.8f, 30.7f);
    public Vector3 assaultCameraLookTarget = new Vector3(-6.0f, 1.2f, 24.0f);
    public Vector3 assaultCameraPanEndPosition = new Vector3(-1.5f, 1.8f, 30.7f);
    public Vector3 dialogueLeadMidPosition = new Vector3(-2.85f, 0.05f, 26.85f);
    public Vector3 dialogueLeadPosition = new Vector3(-1.8f, 0.05f, 28.5f);
    public Vector3 dialogueSupportPosition = new Vector3(-3.95f, 0.05f, 26.45f);
    public Vector3 dialogueCameraPosition = new Vector3(-4.656f, 0.948f, 29.058f);
    public float assaultPanDuration = 2.6f;
    public float badgeCloseupDistance = 1.15f;
    public float badgeCloseupHeight = 1.45f;
    public float playerRunSpeed = 3.0f;

    [Header("Audio")]
    public AudioClip sirenClip;
    private AudioSource sirenAudioSource;
    public AudioClip[] ttsAudioClips;
    private AudioSource ttsAudioSource;

    private DialogueScreenUI dialogueUI;
    private CanvasGroup fadeCanvasGroup;
    private bool isSkipping = false;
    private Coroutine cutsceneCoroutine;

    // Skip confirm window
    private float lastSkipPressTime = -999f;
    private const float SkipConfirmWindow = 3.0f;
    private CanvasGroup skipPromptCanvasGroup;
    private Text skipPromptText;
    private Coroutine skipPromptFadeCoroutine;

    private void Start()
    {
        // Force transition to Final_CutScene to ignore potential outdated Inspector values
        nextSceneName = "Final_CutScene";

        // Setup AudioSource for Siren
        sirenAudioSource = gameObject.AddComponent<AudioSource>();
        sirenAudioSource.playOnAwake = false;
        sirenAudioSource.loop = true;
        sirenAudioSource.volume = 0.5f;

        if (sirenClip == null)
        {
            sirenClip = Resources.Load<AudioClip>("Soundtrack/Police_Siren");
        }
        sirenAudioSource.clip = sirenClip;

        // Initialize TTS AudioSource
        ttsAudioSource = gameObject.AddComponent<AudioSource>();
        ttsAudioSource.playOnAwake = false;
        ttsAudioSource.loop = false;
        ttsAudioSource.volume = 1.0f;

        // Always populate TTS AudioClips in code if not already set in the inspector
        if (ttsAudioClips == null || ttsAudioClips.Length == 0)
        {
            var list = new List<AudioClip>();
            AudioClip narratorClip = Resources.Load<AudioClip>("Soundtrack/MiddleTTS/Middle_Narrator_1");
            if (narratorClip != null) list.Add(narratorClip);

            for (int i = 1; i <= 2; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>($"Soundtrack/MiddleTTS/Middle_{i}");
                if (clip != null) list.Add(clip);
            }
            ttsAudioClips = list.ToArray();
            Debug.Log($"[MiddleCutsceneController] Dynamically populated ttsAudioClips. Count: {ttsAudioClips.Length}");
        }
        else
        {
            Debug.Log($"[MiddleCutsceneController] Using inspector serialized ttsAudioClips. Count: {ttsAudioClips.Length}");
        }

        // Find Dialogue UI
        dialogueUI = FindAnyObjectByType<DialogueScreenUI>();
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueScreenUI");
            dialogueUI = go.AddComponent<DialogueScreenUI>();
            DontDestroyOnLoad(go);
        }

        // Auto-resolve character references if missing
        ResolveCharacterReferences();

        // Auto-resolve camera references if missing
        ResolveCameraReferences();

        // Disable PlayerMovement and CharacterController to prevent user controls and physics jitter
        DisableControls();

        // Setup Fade Overlay
        SetupFadeCanvas();

        // Setup Night Environment and Lights
        SetupNightEnvironment();

        // Create Skip prompt UI
        CreateSkipPromptUI();

        // Start Cutscene
        cutsceneCoroutine = StartCoroutine(CutsceneSequence());
    }

    private void Update()
    {
        // Skip with Space/Escape or Mouse Left Click
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)) && !isSkipping)
        {
            HandleSkipAttempt();
        }
    }

    private void ResolveCharacterReferences()
    {
        if (mainCharacter == null) mainCharacter = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (mobileSoldier1 == null) mobileSoldier1 = GameObject.Find("Mobile soldier (1)");
        if (mobileSoldier2 == null) mobileSoldier2 = GameObject.Find("Mobile soldier (2)");
        if (mobileSoldier3 == null) mobileSoldier3 = GameObject.Find("Mobile soldier (3)");
        if (mobileSoldierUnnumbered == null) mobileSoldierUnnumbered = GameObject.Find("Mobile soldier");
        if (suspectMiuLe == null) suspectMiuLe = GameObject.Find("Miu Le");
        if (suspectHuySeo == null) suspectHuySeo = GameObject.Find("Huy_seo");
    }

    private void ResolveCameraReferences()
    {
        if (camBalcony == null) camBalcony = GameObject.Find("Cam_Balcony")?.GetComponent<Camera>();
        if (camAssault == null) camAssault = GameObject.Find("Cam_Assault")?.GetComponent<Camera>();
        if (camDialogue == null) camDialogue = GameObject.Find("Cam_Dialogue")?.GetComponent<Camera>();
        if (camFinal == null) camFinal = GameObject.Find("Cam_Final")?.GetComponent<Camera>();
    }

    private void DisableControls()
    {
        if (mainCharacter != null)
        {
            var movement = mainCharacter.GetComponent("PlayerMovement") as MonoBehaviour;
            if (movement != null) movement.enabled = false;

            var cc = mainCharacter.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        // Disable Main Camera's ThirdPersonCamera script
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var tpc = mainCam.GetComponent("ThirdPersonCamera") as MonoBehaviour;
            if (tpc != null) tpc.enabled = false;
            mainCam.enabled = false;
        }

        // Disable gate interaction and force it open
        var gate = GameObject.Find("Gate_Interactive");
        if (gate != null)
        {
            var gateInteract = gate.GetComponent<GateInteract>();
            if (gateInteract != null) gateInteract.enabled = false;

            var pivotLeft = gate.transform.Find("pivot left");
            var pivotRight = gate.transform.Find("pivot right");
            if (pivotLeft != null) pivotLeft.localRotation = Quaternion.Euler(0, -90f, 0);
            if (pivotRight != null) pivotRight.localRotation = Quaternion.Euler(0, 90f, 0);
        }
    }

    private void SetupFadeCanvas()
    {
        var existingCanvas = FindAnyObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            var group = existingCanvas.GetComponentInChildren<CanvasGroup>();
            if (group != null && group.gameObject.name.Contains("Fade"))
            {
                fadeCanvasGroup = group;
                return;
            }
        }

        var fadeGo = new GameObject("CutsceneFadeOverlay");
        var canvas = fadeGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        fadeGo.AddComponent<CanvasScaler>();

        fadeCanvasGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f;

        var imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(fadeGo.transform, false);
        var rect = imgGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
    }

    private void SwitchActiveCamera(Camera activeCam)
    {
        if (camBalcony != null) camBalcony.enabled = (camBalcony == activeCam);
        if (camAssault != null) camAssault.enabled = (camAssault == activeCam);
        if (camDialogue != null) camDialogue.enabled = (camDialogue == activeCam);
        if (camFinal != null) camFinal.enabled = (camFinal == activeCam);
    }

private void CreateSkipPromptUI()
    {
        GameObject canvasObj = new GameObject("SkipPromptCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        skipPromptCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        skipPromptCanvasGroup.alpha = 0f;

        GameObject panelObj = new GameObject("PromptPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.sizeDelta = new Vector2(420, 70);
        panelRect.anchoredPosition = new Vector2(-40f, 40f);

        var bgImage = panelObj.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

        var outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panelObj.transform, false);
        var txtRect = textObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(15f, 5f);
        txtRect.offsetMax = new Vector2(-15f, -5f);

        skipPromptText = textObj.AddComponent<Text>();
        skipPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skipPromptText.fontSize = 20;
        skipPromptText.alignment = TextAnchor.MiddleCenter;
        skipPromptText.color = Color.white;
        skipPromptText.text = "Nh\u1ea5p chu\u1ed9t ho\u1eb7c b\u1ea5m ESC/SPACE \u0111\u1ec3 b\u1ecf qua";

        var shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

private void HandleSkipAttempt()
    {
        bool hasSeenCutscene = PlayerPrefs.GetInt("HasSeenMiddleCutscene", 0) == 1;
        if (hasSeenCutscene)
        {
            StartCoroutine(SkipCutsceneRoutine());
            return;
        }

        float timeSinceLastPress = Time.time - lastSkipPressTime;
        if (timeSinceLastPress < SkipConfirmWindow)
        {
            HideSkipPrompt();
            StartCoroutine(SkipCutsceneRoutine());
        }
        else
        {
            lastSkipPressTime = Time.time;
            ShowSkipPrompt("X\u00e1c nh\u1eadn b\u1ecf qua? B\u1ea5m th\u00eam l\u1ea7n n\u1eefa...");
        }
    }

    private void ShowSkipPrompt(string message)
    {
        if (skipPromptText != null)
        {
            skipPromptText.text = message;
            skipPromptText.color = new Color(1f, 0.75f, 0.3f, 1f);
        }

        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
        }
        skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(1f, SkipConfirmWindow));
    }

    private void HideSkipPrompt()
    {
        if (skipPromptFadeCoroutine != null)
        {
            StopCoroutine(skipPromptFadeCoroutine);
        }
        skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(0f, 0f));
    }

    private IEnumerator FadeSkipPromptRoutine(float targetAlpha, float holdDuration)
    {
        float elapsed = 0f;
        float startAlpha = skipPromptCanvasGroup.alpha;

        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            skipPromptCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / 0.3f);
            yield return null;
        }
        skipPromptCanvasGroup.alpha = targetAlpha;

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration - 0.6f);

            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                skipPromptCanvasGroup.alpha = Mathf.Lerp(targetAlpha, 0f, elapsed / 0.3f);
                yield return null;
            }
            skipPromptCanvasGroup.alpha = 0f;
        }
    }

    private IEnumerator MoveObjectAlongPath(GameObject obj, Vector3[] waypoints, float speed, string animatorFloatParam = null, float animatorFloatValue = 0f)
    {
        if (obj == null || waypoints == null || waypoints.Length == 0)
        {
            yield break;
        }

        var animator = obj.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (!string.IsNullOrEmpty(animatorFloatParam))
            {
                animator.SetFloat(animatorFloatParam, animatorFloatValue);
            }
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 targetPos = waypoints[i];
            Vector3 startPos = obj.transform.position;
            targetPos.y = startPos.y;

            while (Vector3.Distance(obj.transform.position, targetPos) > 0.1f)
            {
                if (animator != null && animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                }

                obj.transform.position = Vector3.MoveTowards(obj.transform.position, targetPos, speed * Time.deltaTime);
                Vector3 direction = (targetPos - obj.transform.position).normalized;
                direction.y = 0f;
                if (direction.magnitude > 0.01f)
                {
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
                }
                yield return null;
            }

            obj.transform.position = targetPos;
        }

        if (animator != null && !string.IsNullOrEmpty(animatorFloatParam))
        {
            animator.SetFloat(animatorFloatParam, 0f);
        }
    }

    private IEnumerator MoveObject(GameObject obj, Vector3 targetPos, float speed, string animatorFloatParam = null, float animatorFloatValue = 0f)
    {
        var animator = obj != null ? obj.GetComponent<Animator>() : null;
        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (!string.IsNullOrEmpty(animatorFloatParam))
            {
                animator.SetFloat(animatorFloatParam, animatorFloatValue);
            }
        }

        Vector3 startPos = obj.transform.position;
        targetPos.y = startPos.y; // maintain current height

        while (Vector3.Distance(obj.transform.position, targetPos) > 0.1f)
        {
            if (animator != null && animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
            }

            obj.transform.position = Vector3.MoveTowards(obj.transform.position, targetPos, speed * Time.deltaTime);
            Vector3 direction = (targetPos - obj.transform.position).normalized;
            direction.y = 0;
            if (direction.magnitude > 0.01f)
            {
                obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
            }
            yield return null;
        }
        obj.transform.position = targetPos;

        if (animator != null && !string.IsNullOrEmpty(animatorFloatParam))
        {
            animator.SetFloat(animatorFloatParam, 0f); // Reset animation to idle
        }
    }

    private IEnumerator RotateToFace(GameObject obj, Vector3 targetFacePos, float duration)
    {
        Vector3 direction = (targetFacePos - obj.transform.position).normalized;
        direction.y = 0;
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Quaternion startRotation = obj.transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                obj.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / duration);
                yield return null;
            }
            obj.transform.rotation = targetRotation;
        }
    }

    private void SetAnimatorSpeed(GameObject obj, float speed)
    {
        if (obj == null) return;

        var animator = obj.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = speed;
        }
    }

    private void SetCameraPose(Camera cam, Vector3 position, Vector3 lookTarget)
    {
        if (cam == null) return;

        cam.transform.position = position;
        cam.transform.LookAt(lookTarget);
    }

    private IEnumerator PanCameraLookAt(Camera cam, Vector3 startPos, Vector3 endPos, Vector3 startLookTarget, Vector3 endLookTarget, float duration)
    {
        if (cam == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 camPos = Vector3.Lerp(startPos, endPos, t);
            Vector3 lookTarget = Vector3.Lerp(startLookTarget, endLookTarget, t);
            SetCameraPose(cam, camPos, lookTarget);
            yield return null;
        }

        SetCameraPose(cam, endPos, endLookTarget);
    }

    private IEnumerator HeadBobRoutine(Transform camTransform, float height, float bobSpeed, float bobAmount)
    {
        float timer = 0f;
        while (camTransform.parent != null && camTransform.parent.gameObject == mobileSoldier1)
        {
            timer += Time.deltaTime * bobSpeed;
            float bobY = Mathf.Sin(timer) * bobAmount;
            float bobX = Mathf.Cos(timer * 0.5f) * (bobAmount * 0.3f);

            camTransform.localPosition = new Vector3(
                bobX / mobileSoldier1.transform.localScale.x,
                (height + bobY) / mobileSoldier1.transform.localScale.y,
                0.5f / mobileSoldier1.transform.localScale.z
            );
            yield return null;
        }
    }

private IEnumerator CutsceneSequence()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;
        }

        if (sirenAudioSource != null && sirenClip != null)
        {
            sirenAudioSource.Play();
        }

        SetAnimatorSpeed(suspectMiuLe, 1f);
        SetAnimatorSpeed(suspectHuySeo, 1f);

        Transform originalBalconyParent = null;
        Vector3 originalBalconyPos = Vector3.zero;
        Quaternion originalBalconyRot = Quaternion.identity;

        if (camBalcony != null && mobileSoldier1 != null)
        {
            originalBalconyParent = camBalcony.transform.parent;
            originalBalconyPos = camBalcony.transform.position;
            originalBalconyRot = camBalcony.transform.rotation;

            camBalcony.transform.SetParent(mobileSoldier1.transform, false);
            camBalcony.transform.localPosition = new Vector3(
                0f,
                0.70f / mobileSoldier1.transform.localScale.y,
                0.5f / mobileSoldier1.transform.localScale.z
            );
            camBalcony.transform.localRotation = Quaternion.identity;

            StartCoroutine(HeadBobRoutine(camBalcony.transform, 0.70f, 12f, 0.025f));
        }

        SwitchActiveCamera(camBalcony);
        yield return new WaitForSeconds(0.5f);

        if (fadeCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime * fadeSpeed;
                fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 1.5f);
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
        }

        if (skipPromptCanvasGroup != null)
        {
            skipPromptText.text = "Nh\u1ea5p chu\u1ed9t ho\u1eb7c b\u1ea5m ESC/SPACE \u0111\u1ec3 b\u1ecf qua";
            skipPromptText.color = new Color(1f, 1f, 1f, 0.6f);
            skipPromptFadeCoroutine = StartCoroutine(FadeSkipPromptRoutine(1f, 4f));
        }

        if (mobileSoldier1 != null)
        {
            Vector3 redXPos = new Vector3(-1.70f, 0.01f, 26.50f);
            yield return StartCoroutine(MoveObject(mobileSoldier1, redXPos, soldierRunSpeed));
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        if (camBalcony != null)
        {
            camBalcony.transform.SetParent(originalBalconyParent);
            camBalcony.transform.position = originalBalconyPos;
            camBalcony.transform.rotation = originalBalconyRot;
        }

        Vector3 suspectCenter;
        if (suspectMiuLe != null && suspectHuySeo != null)
        {
            suspectCenter = (suspectMiuLe.transform.position + suspectHuySeo.transform.position) * 0.5f;
        }
        else
        {
            suspectCenter = new Vector3(-1.5f, 0.01f, 30.8f);
        }

        Vector3 mainStartPosition = mainCharacter != null ? mainCharacter.transform.position : dialogueLeadMidPosition;

        if (camAssault != null)
        {
            SetCameraPose(camAssault, assaultCameraPosition, assaultCameraLookTarget);
        }

        SwitchActiveCamera(camAssault);
        yield return new WaitForSeconds(0.45f);

        float dNarrator = PlayTTSAndShowSubtitle(0, "<color=#FFFF00>[L\u1ef1c l\u01b0\u1ee3ng \u0111\u1ed9t k\u00edch ti\u1ebfn h\u00e0nh kh\u00e9p ch\u1eb7t v\u00f2ng v\u00e2y]</color>", 3.0f);
        float narratorStartTime = Time.time;

        var perimeterMoves = new List<Coroutine>();
        if (mobileSoldier2 != null)
        {
            perimeterMoves.Add(StartCoroutine(MoveObject(mobileSoldier2, new Vector3(-1.70f, 0.01f, 25.50f), soldierRunSpeed)));
        }
        if (mobileSoldier3 != null)
        {
            perimeterMoves.Add(StartCoroutine(MoveObject(mobileSoldier3, new Vector3(-1.70f, 0.01f, 24.50f), soldierRunSpeed)));
        }
        foreach (var move in perimeterMoves)
        {
            if (move != null) yield return move;
        }

        var perimeterRotations = new List<Coroutine>();
        if (mobileSoldier1 != null) perimeterRotations.Add(StartCoroutine(RotateToFace(mobileSoldier1, suspectCenter, 0.5f)));
        if (mobileSoldier2 != null) perimeterRotations.Add(StartCoroutine(RotateToFace(mobileSoldier2, suspectCenter, 0.5f)));
        if (mobileSoldier3 != null) perimeterRotations.Add(StartCoroutine(RotateToFace(mobileSoldier3, suspectCenter, 0.5f)));
        foreach (var rot in perimeterRotations)
        {
            if (rot != null) yield return rot;
        }

        float elapsedNarrator = Time.time - narratorStartTime;
        if (elapsedNarrator < dNarrator)
        {
            yield return new WaitForSeconds(dNarrator - elapsedNarrator);
        }

        Coroutine assaultPan = null;
        if (camAssault != null)
        {
            assaultPan = StartCoroutine(PanCameraLookAt(
                camAssault,
                assaultCameraPosition,
                assaultCameraPanEndPosition,
                mainStartPosition + Vector3.up * 1.1f,
                dialogueLeadPosition + Vector3.up * 1.0f,
                assaultPanDuration));
        }

        var walkMoves = new List<Coroutine>();
        if (mainCharacter != null)
        {
            walkMoves.Add(StartCoroutine(MoveObjectAlongPath(
                mainCharacter,
                new[] { dialogueLeadMidPosition, dialogueLeadPosition },
                1.75f,
                "Blend",
                0.5f)));
        }
        if (mobileSoldierUnnumbered != null)
        {
            walkMoves.Add(StartCoroutine(MoveObject(mobileSoldierUnnumbered, dialogueSupportPosition, 1.35f)));
        }

        foreach (var move in walkMoves)
        {
            if (move != null) yield return move;
        }

        if (assaultPan != null)
        {
            yield return assaultPan;
        }

        var dialogueRotations = new List<Coroutine>();
        if (mainCharacter != null) dialogueRotations.Add(StartCoroutine(RotateToFace(mainCharacter, suspectCenter, 0.3f)));
        if (mobileSoldierUnnumbered != null) dialogueRotations.Add(StartCoroutine(RotateToFace(mobileSoldierUnnumbered, suspectCenter, 0.3f)));
        foreach (var rot in dialogueRotations)
        {
            if (rot != null) yield return rot;
        }

        if (camDialogue != null)
        {
            Vector3 playerPos = mainCharacter != null ? mainCharacter.transform.position : dialogueLeadPosition;
            Vector3 dialogLookTarget = (playerPos + suspectCenter) * 0.5f + Vector3.up * 0.95f;
            SetCameraPose(camDialogue, dialogueCameraPosition, dialogLookTarget);
            SwitchActiveCamera(camDialogue);
            yield return new WaitForSeconds(0.2f);
        }

        float d1 = PlayTTSAndShowSubtitle(1, "<b>Anh B\u00f2 B\u00eda:</b> \"B\u00e1nh b\u00f2 b\u00eda h\u1ebft su\u1ea5t r\u1ed3i. H\u00f4m nay, t\u00f4i ship l\u1ec7nh b\u1eaft gi\u1eef!\"", 4.5f);
        yield return new WaitForSeconds(d1);

        float d2 = PlayTTSAndShowSubtitle(2, "<b>Anh B\u00f2 B\u00eda:</b> \"Trung \u00fay Nguy\u1ec5n V\u0103n B\u1ea3o! Chuy\u00ean \u00e1n H\u1ebbm 113 k\u1ebft th\u00fac. T\u1ea5t c\u1ea3 \u0111\u1ee9ng im, hai tay gi\u01a1 l\u00ean \u0111\u1ea7u!\"", 5.5f);
        yield return new WaitForSeconds(d2);

        if (dialogueUI != null)
        {
            dialogueUI.ForceShow("", 0f);
        }

        if (camDialogue != null)
        {
            Vector3 playerPos = mainCharacter != null ? mainCharacter.transform.position : dialogueLeadPosition;
            Vector3 dialogLookTarget = (playerPos + suspectCenter) * 0.5f + Vector3.up * 0.95f;
            SetCameraPose(camDialogue, dialogueCameraPosition, dialogLookTarget);
            SwitchActiveCamera(camDialogue);
        }

        float zoomDuration = 3.0f;
        float elapsedZoom = 0f;
        Vector3 startZoomPos = camDialogue != null ? camDialogue.transform.position : Vector3.zero;
        Vector3 zoomDir = camDialogue != null ? -camDialogue.transform.forward : Vector3.zero;
        float startFOV = camDialogue != null ? camDialogue.fieldOfView : 60f;

        while (elapsedZoom < zoomDuration)
        {
            elapsedZoom += Time.deltaTime;
            float t = elapsedZoom / zoomDuration;

            if (camDialogue != null)
            {
                camDialogue.transform.position = startZoomPos + zoomDir * (t * 1.1f) + Vector3.up * (t * 0.45f);
                camDialogue.fieldOfView = Mathf.Lerp(startFOV, 82f, t);
            }

            if (t > 0.33f)
            {
                float fadeT = (t - 0.33f) * 1.5f;
                if (fadeCanvasGroup != null)
                {
                    fadeCanvasGroup.alpha = Mathf.Clamp01(fadeT);
                }
            }

            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
        }

        PlayerPrefs.SetInt("HasSeenMiddleCutscene", 1);
        PlayerPrefs.Save();

        float fadeVol = 0.5f;
        while (fadeVol > 0f)
        {
            fadeVol -= Time.deltaTime;
            if (sirenAudioSource != null) sirenAudioSource.volume = Mathf.Clamp01(fadeVol);
            yield return null;
        }

        if (sirenAudioSource != null)
        {
            sirenAudioSource.Stop();
        }

        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator SkipCutsceneRoutine()
    {
        isSkipping = true;
        PlayerPrefs.SetInt("HasSeenMiddleCutscene", 1);
        PlayerPrefs.Save();

        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
        }

        if (dialogueUI != null)
        {
            dialogueUI.ForceShow("", 0f);
        }

        if (sirenAudioSource != null)
        {
            sirenAudioSource.Stop();
        }

        if (ttsAudioSource != null && ttsAudioSource.isPlaying)
        {
            ttsAudioSource.Stop();
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            float elapsed = 0f;
            float startAlpha = fadeCanvasGroup.alpha;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime * 2f;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / 0.5f);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(nextSceneName);
    }

    private void SetupNightEnvironment()
    {
        // 1. Disable Directional Light
        var dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (dirLight != null)
        {
            dirLight.enabled = false;
        }

        // 2. Set Ambient Light and Skybox
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.01f, 0.01f, 0.03f, 1f);
        RenderSettings.skybox = null;

        // 3. Set all cameras to clear with Solid Color (black)
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam != null && cam.name.StartsWith("Cam_"))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
        }

        // 4. Create Gazebo Light
        GameObject gazeboLight = new GameObject("GazeboNightLight");
        var gLight = gazeboLight.AddComponent<Light>();
        gLight.type = LightType.Point;
        gLight.color = new Color(1.0f, 0.85f, 0.6f);
        gLight.intensity = 8.0f;
        gLight.range = 15f;
        gazeboLight.transform.position = new Vector3(-1.5f, 2.2f, 30.8f);

        // 5. Create Porch Lights (along the columns)
        Vector3[] porchPositions = new[] {
            new Vector3(-1.7f, 2.5f, 25.5f),
            new Vector3(-3.5f, 2.5f, 25.5f),
            new Vector3(-8.5f, 2.5f, 25.5f)
        };
        for (int i = 0; i < porchPositions.Length; i++)
        {
            GameObject porchLight = new GameObject($"PorchNightLight_{i}");
            var l = porchLight.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.9f, 0.8f);
            l.intensity = 5.0f;
            l.range = 10f;
            porchLight.transform.position = porchPositions[i];
        }

        // 6. Create Pool Light
        GameObject poolLight = new GameObject("PoolNightLight");
        var pLight = poolLight.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(0.2f, 0.7f, 1.0f);
        pLight.intensity = 8.0f;
        pLight.range = 18f;
        poolLight.transform.position = new Vector3(-6.0f, 1.0f, 25.0f);
    }

    private float PlayTTSAndShowSubtitle(int index, string message, float fallbackDuration)
    {
        float duration = fallbackDuration;
        if (ttsAudioClips != null && index < ttsAudioClips.Length && ttsAudioClips[index] != null)
        {
            AudioClip clip = ttsAudioClips[index];
            duration = clip.length + 0.5f; // Add 0.5s padding
            Debug.Log($"[MiddleCutsceneController] Playing TTS clip index {index}: '{clip.name}' (length: {clip.length}s, padding: 0.5s, total duration: {duration}s)");
            if (ttsAudioSource != null)
            {
                ttsAudioSource.clip = clip;
                ttsAudioSource.Play();
            }
        }
        else
        {
            Debug.LogWarning($"[MiddleCutsceneController] Cannot play TTS clip index {index}. ttsAudioClips null? {ttsAudioClips == null}, length: {(ttsAudioClips != null ? ttsAudioClips.Length : 0)}, clip null? {(ttsAudioClips != null && index < ttsAudioClips.Length ? (ttsAudioClips[index] == null) : true)}");
        }
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow(message, duration);
        }
        return duration;
    }
}
