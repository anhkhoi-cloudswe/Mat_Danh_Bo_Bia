using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FinalCutsceneController : MonoBehaviour
{
    [Header("Characters & Objects")]
    public GameObject mainCharacter;
    public GameObject bossOfficer;
    public GameObject policeCar;
    public GameObject[] mobileSoldiers;
    public GameObject suspectMiuLe;
    public GameObject suspectHuySeo;

    [Header("Audio")]
    public AudioClip endingSoundtrack;
    private AudioSource audioSource;
    private AudioSource sirenAudioSource;

    [Header("Flasher Settings")]
    public Light policeCarLightRed;
    public Light policeCarLightBlue;

    [Header("Cinematic Cameras")]
    public Camera camBalcony;
    public Camera camAssault;
    public Camera camBoss;
    public Camera camDialogue;
    public Camera camFinal;

    [Header("Text To Speech Audio")]
    [Tooltip("Danh sách các file âm thanh giọng đọc tương ứng với từng câu thoại trong Final CutScene.")]
    public AudioClip[] ttsAudioClips;
    private AudioSource ttsAudioSource;

    private Camera mainCamera;
    private DialogueScreenUI dialogueUI;
    private CanvasGroup fadeCanvasGroup;

    // UI elements for Credits
    private GameObject creditsCanvasObj;
    private CanvasGroup creditsCanvasGroup;
    private RectTransform creditsTextRect;
    private Button mainMenuButton;

    private bool isCutsceneRunning = false;
    private Vector3 mainStartPos;
    private Vector3 bossStartPos;
    private bool shouldLockProtagonist = false;
    private bool areCreditsActive = false;

    private void Update()
    {
        if (areCreditsActive)
        {
            if (Input.anyKeyDown)
            {
                areCreditsActive = false;
                SceneManager.LoadScene("Main_Menu");
            }
        }
    }

    private void Start()
    {
        // Khởi động AudioSource và load giọng đọc Final CutScene
        ttsAudioSource = gameObject.AddComponent<AudioSource>();
        ttsAudioSource.playOnAwake = false;
        ttsAudioSource.loop = false;
        ttsAudioSource.volume = 1.0f;

        if (ttsAudioClips == null || ttsAudioClips.Length == 0)
        {
            var list = new List<AudioClip>();
            for (int i = 1; i <= 7; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>($"Soundtrack/FinalTTS/final_{i}");
                if (clip != null) list.Add(clip);
            }
            ttsAudioClips = list.ToArray();
        }

        mainCamera = Camera.main;
        dialogueUI = FindAnyObjectByType<DialogueScreenUI>();
        if (dialogueUI == null)
        {
            var go = new GameObject("DialogueScreenUI");
            dialogueUI = go.AddComponent<DialogueScreenUI>();
            DontDestroyOnLoad(go);
        }

        // Try to auto-resolve references if not assigned
        if (mainCharacter == null) mainCharacter = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (bossOfficer == null) bossOfficer = GameObject.Find("Anh_Cong_An");
        if (policeCar == null) policeCar = GameObject.Find("XeCongAn");
        if (suspectMiuLe == null) suspectMiuLe = GameObject.Find("Miu Le");
        if (suspectHuySeo == null) suspectHuySeo = GameObject.Find("Huy_seo");

        if (mainCharacter != null) mainStartPos = mainCharacter.transform.position;
        if (bossOfficer != null) bossStartPos = bossOfficer.transform.position;

        // Disable CharacterController to prevent physics update jitter
        if (mainCharacter != null)
        {
            var cc = mainCharacter.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }
        if (bossOfficer != null)
        {
            var cc = bossOfficer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        if (mobileSoldiers == null || mobileSoldiers.Length == 0)
        {
            var s1 = GameObject.Find("Mobile soldier");
            var s2 = GameObject.Find("Mobile soldier (1)");
            var s3 = GameObject.Find("Mobile soldier (2)");
            var s4 = GameObject.Find("Mobile soldier (3)");
            var list = new List<GameObject>();
            if (s1 != null) list.Add(s1);
            if (s2 != null) list.Add(s2);
            if (s3 != null) list.Add(s3);
            if (s4 != null) list.Add(s4);
            mobileSoldiers = list.ToArray();
        }

        // Find camera references in scene by name
        if (camBalcony == null) camBalcony = GameObject.Find("Cam_Balcony")?.GetComponent<Camera>();
        if (camBalcony != null)
        {
            camBalcony.transform.position = new Vector3(-4.555f, 2.794f, 16.840f);
            camBalcony.transform.rotation = Quaternion.Euler(35.753f, 196.775f, 0f);
        }
        if (camAssault == null) camAssault = GameObject.Find("Cam_Assault")?.GetComponent<Camera>();
        if (camAssault != null)
        {
            camAssault.transform.position = new Vector3(1.5f, 2.5f, 15.0f);
            camAssault.transform.rotation = Quaternion.Euler(35f, 295f, 0f);
        }
        if (camBoss == null) camBoss = GameObject.Find("Cam_Boss")?.GetComponent<Camera>();
        if (camDialogue == null) camDialogue = GameObject.Find("Cam_Dialogue")?.GetComponent<Camera>();
        if (camFinal == null) camFinal = GameObject.Find("Cam_Final")?.GetComponent<Camera>();

        // Set up dark night-time lighting
        SetupNightLighting();

        // Dynamically align dialogue camera to show both characters
        AlignDialogueCamera();

        // Disable player controls if PlayerMovement or SimpleProceduralWalker is active
        if (mainCharacter != null)
        {
            var movement = mainCharacter.GetComponent<PlayerMovement>();
            if (movement != null) movement.enabled = false;

            var walker = mainCharacter.GetComponent<SimpleProceduralWalker>();
            if (walker != null) walker.enabled = false;

            var rb = mainCharacter.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
        if (bossOfficer != null)
        {
            var walker = bossOfficer.GetComponent<SimpleProceduralWalker>();
            if (walker != null) walker.enabled = false;

            var rb = bossOfficer.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        // Disable all ThirdPersonCamera and FirstPersonCamera components in the scene to prevent them from overriding player rotation
        var tpcs = FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None);
        foreach (var t in tpcs) t.enabled = false;
        var fpcs = FindObjectsByType<FirstPersonCamera>(FindObjectsSortMode.None);
        foreach (var f in fpcs) f.enabled = false;

        if (mainCamera != null)
        {
            mainCamera.enabled = false; // Disable main camera since we use multiple cams
        }

        // Disable gate interaction to hide "Press E to open" OnGUI text
        var gate = GameObject.Find("Gate_Interactive");
        if (gate != null)
        {
            var gateInteract = gate.GetComponent<GateInteract>();
            if (gateInteract != null) gateInteract.enabled = false;
        }

        // Expose AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        // Setup Police Siren AudioSource
        AudioClip sirenClip = Resources.Load<AudioClip>("Soundtrack/Police_Siren");
        if (sirenClip != null)
        {
            sirenAudioSource = gameObject.AddComponent<AudioSource>();
            sirenAudioSource.clip = sirenClip;
            sirenAudioSource.loop = true;
            sirenAudioSource.volume = 0.8f;
            sirenAudioSource.playOnAwake = false;
        }

        if (endingSoundtrack == null)
        {
            // Dynamically load the Cinematic ending music
            endingSoundtrack = Resources.Load<AudioClip>("Soundtrack/Cinematic");
        }
        audioSource.clip = endingSoundtrack;

        // Setup police flashing lights
        SetupPoliceLights();

        // Setup Fade Canvas Group
        SetupFadeCanvas();

        // Start Cutscene
        StartCoroutine(CutsceneSequence());
    }

    private void AlignDialogueCamera()
    {
        if (camDialogue != null && mainCharacter != null && bossOfficer != null)
        {
            Vector3 mainPos = mainCharacter.transform.position;
            Vector3 bossPos = bossOfficer.transform.position;
            Vector3 midpoint = (mainPos + bossPos) * 0.5f;

            // Position the camera higher and slightly further back (East)
            camDialogue.transform.position = midpoint + new Vector3(2.1f, 1.65f, 0f);
            
            // Look slightly lower (chest/waist level) to tilt the camera down, pushing characters up in frame
            Vector3 lookTarget = midpoint + Vector3.up * 0.8f;
            camDialogue.transform.LookAt(lookTarget);
        }
    }

    private void SetupNightLighting()
    {
        // Set sun (directional light) to night mode and rotate it below horizon
        var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun != null)
        {
            sun.intensity = 0.03f;
            sun.color = new Color(0.12f, 0.15f, 0.25f); // Moonlight blue
            sun.transform.rotation = Quaternion.Euler(-80f, 170f, 0f); // Face downwards below horizon
        }

        // Set ambient light to dark blue
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.015f, 0.015f, 0.03f);

        // Load and tweak skybox to render pitch black sky
        if (RenderSettings.skybox != null)
        {
            Material nightSky = Instantiate(RenderSettings.skybox);
            if (nightSky.HasProperty("_SkyTint")) nightSky.SetColor("_SkyTint", new Color(0.005f, 0.005f, 0.01f));
            if (nightSky.HasProperty("_GroundColor")) nightSky.SetColor("_GroundColor", Color.black);
            if (nightSky.HasProperty("_Exposure")) nightSky.SetFloat("_Exposure", 0.08f);
            RenderSettings.skybox = nightSky;
        }

        // Spawn warm street lights at runtime to illuminate the yard beautifully
        SpawnStreetLight(new Vector3(-5.0f, 3.5f, 15.0f)); // Near gate
        SpawnStreetLight(new Vector3(-1.5f, 3.5f, 15.0f)); // Near group
        SpawnStreetLight(new Vector3(2.0f, 3.5f, 15.0f));  // Near villa entrance
    }

    private void SpawnStreetLight(Vector3 pos)
    {
        GameObject lightGo = new GameObject("StreetLight_Runtime");
        lightGo.transform.position = pos;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.9f, 0.7f); // Warm yellow
        light.intensity = 3.0f;
        light.range = 15f;
        light.shadows = LightShadows.None;
    }

    private void SetupPoliceLights()
    {
        if (policeCar != null)
        {
            Vector3 parentScale = policeCar.transform.localScale;
            Vector3 redLocalPos = new Vector3(-0.5f / parentScale.x, 2.2f / parentScale.y, 0f);
            Vector3 blueLocalPos = new Vector3(0.5f / parentScale.x, 2.2f / parentScale.y, 0f);

            // Find existing red light or create if missing
            var redTrans = policeCar.transform.Find("PoliceLightRed");
            if (redTrans != null)
            {
                redTrans.localPosition = redLocalPos;
                policeCarLightRed = redTrans.GetComponent<Light>();
                if (policeCarLightRed != null)
                {
                    policeCarLightRed.range = 25f;
                    policeCarLightRed.intensity = 25f;
                    policeCarLightRed.shadows = LightShadows.None;
                }
            }
            else
            {
                var redGo = new GameObject("PoliceLightRed");
                redGo.transform.SetParent(policeCar.transform, false);
                redGo.transform.localPosition = redLocalPos;
                policeCarLightRed = redGo.AddComponent<Light>();
                policeCarLightRed.type = LightType.Point;
                policeCarLightRed.color = Color.red;
                policeCarLightRed.range = 25f;
                policeCarLightRed.intensity = 25f;
                policeCarLightRed.shadows = LightShadows.None;
            }

            // Find existing blue light or create if missing
            var blueTrans = policeCar.transform.Find("PoliceLightBlue");
            if (blueTrans != null)
            {
                blueTrans.localPosition = blueLocalPos;
                policeCarLightBlue = blueTrans.GetComponent<Light>();
                if (policeCarLightBlue != null)
                {
                    policeCarLightBlue.range = 25f;
                    policeCarLightBlue.intensity = 0f;
                    policeCarLightBlue.shadows = LightShadows.None;
                }
            }
            else
            {
                var blueGo = new GameObject("PoliceLightBlue");
                blueGo.transform.SetParent(policeCar.transform, false);
                blueGo.transform.localPosition = blueLocalPos;
                policeCarLightBlue = blueGo.AddComponent<Light>();
                policeCarLightBlue.type = LightType.Point;
                policeCarLightBlue.color = Color.blue;
                policeCarLightBlue.range = 25f;
                policeCarLightBlue.intensity = 0f;
                policeCarLightBlue.shadows = LightShadows.None;
            }

            StartCoroutine(FlashPoliceLightsRoutine());
        }
    }

    private IEnumerator FlashPoliceLightsRoutine()
    {
        while (true)
        {
            if (policeCarLightRed != null) policeCarLightRed.intensity = 25f;
            if (policeCarLightBlue != null) policeCarLightBlue.intensity = 0f;
            yield return new WaitForSeconds(0.25f);
            if (policeCarLightRed != null) policeCarLightRed.intensity = 0f;
            if (policeCarLightBlue != null) policeCarLightBlue.intensity = 25f;
            yield return new WaitForSeconds(0.25f);
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

        // Spawn a local fade overlay
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
        if (camBoss != null) camBoss.enabled = (camBoss == activeCam);
        if (camDialogue != null) camDialogue.enabled = (camDialogue == activeCam);
        if (camFinal != null) camFinal.enabled = (camFinal == activeCam);
    }

    private IEnumerator MoveAlongPath(GameObject obj, Vector3[] path, float speed, Vector3? finalLookTarget = null)
    {
        var anim = obj != null ? obj.GetComponent<Animator>() : null;
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.speed = 1f;
            try { anim.SetFloat("Blend", 0.5f); } catch {}
        }

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 target = path[i];
            target.y = obj.transform.position.y;
            
            while (Vector3.Distance(obj.transform.position, target) > 0.15f)
            {
                if (anim != null && anim.applyRootMotion)
                {
                    anim.applyRootMotion = false;
                }

                obj.transform.position = Vector3.MoveTowards(obj.transform.position, target, speed * Time.deltaTime);
                
                Vector3 dir = target - obj.transform.position;
                dir.y = 0;
                if (dir.magnitude > 0.05f)
                {
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
                }
                yield return null;
            }
            obj.transform.position = target;
        }

        if (finalLookTarget.HasValue)
        {
            Vector3 lookDir = finalLookTarget.Value - obj.transform.position;
            lookDir.y = 0;
            if (lookDir.magnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                float elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    obj.transform.rotation = Quaternion.Slerp(obj.transform.rotation, targetRot, 8f * Time.deltaTime);
                    yield return null;
                }
                obj.transform.rotation = targetRot;
            }
        }

        if (anim != null)
        {
            try { anim.SetFloat("Blend", 0f); } catch {}
            anim.speed = 1f;
        }
    }

    private IEnumerator CutsceneSequence()
    {
        isCutsceneRunning = true;

        // Start with Balcony Camera active (disabled black screen at start)
        SwitchActiveCamera(camBalcony);

        // Lock positions and make them face each other once at the start of the cutscene
        shouldLockProtagonist = true;
        if (mainCharacter != null) mainCharacter.transform.position = mainStartPos;
        if (bossOfficer != null) bossOfficer.transform.position = bossStartPos;

        if (mainCharacter != null && bossOfficer != null)
        {
            Vector3 lookAtBoss = bossStartPos;
            lookAtBoss.y = mainCharacter.transform.position.y;
            mainCharacter.transform.LookAt(lookAtBoss);

            Vector3 lookAtMain = mainStartPos;
            lookAtMain.y = bossOfficer.transform.position.y;
            bossOfficer.transform.LookAt(lookAtMain);
        }

        var mainAnimator = mainCharacter != null ? mainCharacter.GetComponent<Animator>() : null;
        if (mainAnimator != null) mainAnimator.applyRootMotion = false;
        var bossAnimator = bossOfficer != null ? bossOfficer.GetComponent<Animator>() : null;
        if (bossAnimator != null) bossAnimator.applyRootMotion = false;

        // Fade in from black
        float fade = 1f;
        while (fade > 0f)
        {
            fade -= Time.deltaTime;
            if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = fade;
            yield return null;
        }
        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 0f;

        // Play siren sound at start
        if (sirenAudioSource != null)
        {
            sirenAudioSource.Play();
        }

        // --- GÓC CAM 1: Ban công nhìn vào ---
        // Removed text subtitles as requested
        yield return new WaitForSeconds(4f);

        // --- GÓC CAM 2: Lực lượng đổ bộ ---
        // Cam trái nhìn qua, thấy lính đi vô từng người một
        SwitchActiveCamera(camAssault);

        // Fade police siren volume down to 0.15f over 2 seconds
        StartCoroutine(FadeSirenVolume(0.15f, 2.0f));

        PlayTTSAndShowSubtitle(0, "<color=#FFFF00>[Lực lượng cảnh sát cơ động bao vây, tiến hành khống chế các đối tượng]</color>", 4f);
        float audioLength = (ttsAudioClips != null && ttsAudioClips.Length > 0 && ttsAudioClips[0] != null) ? ttsAudioClips[0].length : 4.0f;
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow("<color=#FFFF00>[Lực lượng cảnh sát cơ động bao vây, tiến hành khống chế các đối tượng]</color>", audioLength + 1.0f);
        }

        // Waypoints for entering path (curved path as in image 1)
        Vector3[] enteringPath = new Vector3[] {
            new Vector3(-6.5f, 0.06f, 12.0f),
            new Vector3(-3.0f, 0.06f, 9.5f),
            new Vector3(0.5f, 0.06f, 10.5f),
            new Vector3(2.5f, 0.06f, 14.0f),
            new Vector3(2.0f, 0.06f, 18.0f),
            new Vector3(-1.5f, 0.06f, 20.0f)
        };

        // Wait exactly audioLength + 1.0f seconds
        yield return new WaitForSeconds(audioLength + 1.0f);

        // --- GÓC CAM HỘI THOẠI (Bộ trưởng và Anh Bò Bía) ---
        // Chuyển xuống lại gần nhìn đối thoại
        AlignDialogueCamera();
        SwitchActiveCamera(camDialogue);

        // Protagonist Dialogue - Segmented
        float d2 = PlayTTSAndShowSubtitle(1, "\"Báo cáo Đại tá! Toàn bộ chứng cứ cốt lõi đã thu thập đầy đủ.\"", 4.5f);
        yield return new WaitForSeconds(d2);

        float d3 = PlayTTSAndShowSubtitle(2, "\"Hai đối tượng chủ mưu đã bị bắt quả tang tại hiện trường!\"", 4.5f);
        yield return new WaitForSeconds(d3);

        // Boss Officer Dialogue - Segmented
        float d4 = PlayTTSAndShowSubtitle(3, "\"Tốt lắm Trung úy! Cậu đã hoàn thành xuất sắc nhiệm vụ nằm vùng đầy nguy hiểm này.\"", 5f);
        yield return new WaitForSeconds(d4);

        float d5 = PlayTTSAndShowSubtitle(4, "\"Chuyên án Hẻm 113 chính thức khép lại.\"", 3.5f);
        yield return new WaitForSeconds(d5);

        float d6 = PlayTTSAndShowSubtitle(5, "\"Cậu xứng đáng nhận huân chương chiến công!\"", 3.5f);
        yield return new WaitForSeconds(d6);

        if (dialogueUI != null) dialogueUI.ForceShow("", 0f);

        // --- GÓC CAM ÁP GIẢI ĐI RA (Cam 2/Cam Assault góc rộng hoặc chéo) ---
        // Quay lại cam rộng nhìn áp giải đi ra đứng ngay ngắn
        SwitchActiveCamera(camAssault);
        float startVoiceTime = Time.time;
        PlayTTSAndShowSubtitle(6, "<color=#FFFF00>[Lực lượng cơ động áp giải hai đối tượng chủ mưu Huy Sẹo và Miu Lê ra trình diện]</color>", 5f);
        float voiceLength = (ttsAudioClips != null && ttsAudioClips.Length > 6 && ttsAudioClips[6] != null) ? ttsAudioClips[6].length : 5.0f;

        Vector3 faceCenter = bossOfficer != null ? bossOfficer.transform.position : new Vector3(-5f, 0f, 15f);

        // Staggered walking: Miu Le goes first, then Huy_seo, then soldiers one by one.
        // Also paths are direct (straight line to destination), and characters rotate to face the officers immediately on arrival.
        if (suspectMiuLe != null)
        {
            Vector3[] miuLePath = new Vector3[] {
                new Vector3(-2.5f, 0.01f, 15.2f) // Red X position (front)
            };
            StartCoroutine(MoveAlongPath(suspectMiuLe, miuLePath, 3.2f, faceCenter));
        }

        yield return new WaitForSeconds(0.8f);

        if (suspectHuySeo != null)
        {
            Vector3[] huySeoPath = new Vector3[] {
                new Vector3(-2.5f, 0.01f, 16.2f) // Red X position (front-middle)
            };
            StartCoroutine(MoveAlongPath(suspectHuySeo, huySeoPath, 3.2f, faceCenter));
        }

        yield return new WaitForSeconds(3.5f);

        // Soldiers escorting them out to the grass side (Green Circles)
        // mobileSoldiers[0] (Mobile soldier) stays static at the gate.
        // mobileSoldiers[1] (Mobile soldier (1))
        if (mobileSoldiers.Length > 1 && mobileSoldiers[1] != null)
        {
            Vector3[] s1Path = new Vector3[] {
                new Vector3(-1.7f, 0.01f, 14.7f) // Green Circle outer spot 1 (behind Miu Le)
            };
            StartCoroutine(MoveAlongPath(mobileSoldiers[1], s1Path, 3.2f, faceCenter));
        }

        // mobileSoldiers[2] (Mobile soldier (2))
        if (mobileSoldiers.Length > 2 && mobileSoldiers[2] != null)
        {
            Vector3[] s2Path = new Vector3[] {
                new Vector3(-1.7f, 0.01f, 15.7f) // Green Circle outer spot 2 (behind Huy Seo)
            };
            StartCoroutine(MoveAlongPath(mobileSoldiers[2], s2Path, 3.2f, faceCenter));
        }

        // mobileSoldiers[3] (Mobile soldier (3))
        if (mobileSoldiers.Length > 3 && mobileSoldiers[3] != null)
        {
            Vector3[] s3Path = new Vector3[] {
                new Vector3(-1.7f, 0.01f, 16.7f) // Green Circle outer spot 3 (behind all)
            };
            StartCoroutine(MoveAlongPath(mobileSoldiers[3], s3Path, 3.2f, faceCenter));
        }

        // Calculate remaining wait to achieve exactly voiceLength + 2.0f seconds
        float elapsedSinceVoiceStart = Time.time - startVoiceTime;
        float targetTotalWait = voiceLength + 2.0f;
        float remainingWait = targetTotalWait - elapsedSinceVoiceStart;

        if (remainingWait > 0f)
        {
            yield return new WaitForSeconds(remainingWait);
        }
        else
        {
            yield return new WaitForSeconds(0.5f); // Fallback tiny wait
        }

        // Enforce final look targets directly
        if (suspectMiuLe != null)
        {
            Vector3 dir = faceCenter - suspectMiuLe.transform.position;
            dir.y = 0f;
            if (dir.magnitude > 0.05f) suspectMiuLe.transform.rotation = Quaternion.LookRotation(dir);
        }
        if (suspectHuySeo != null)
        {
            Vector3 dir = faceCenter - suspectHuySeo.transform.position;
            dir.y = 0f;
            if (dir.magnitude > 0.05f) suspectHuySeo.transform.rotation = Quaternion.LookRotation(dir);
        }
        foreach (var soldier in mobileSoldiers)
        {
            if (soldier != null)
            {
                Vector3 dir = faceCenter - soldier.transform.position;
                dir.y = 0f;
                if (dir.magnitude > 0.05f) soldier.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        // --- FINAL SHOT: Zoom out từ dưới lên trên nhìn từ cửa villa ra ---
        SwitchActiveCamera(camFinal);
        float elapsed = 0f;
        float duration = 4.0f;
        Vector3 startPos = camFinal.transform.position;
        Vector3 endPos = new Vector3(-5.07f, 4.2f, 17.8f); // Zoom out từ dưới lên trên, lùi sâu vào cửa
        Quaternion startRot = camFinal.transform.rotation;
        Quaternion endRot = Quaternion.LookRotation(new Vector3(-5.0f, 1.0f, 12.0f) - endPos);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            camFinal.transform.position = Vector3.Lerp(startPos, endPos, t);
            camFinal.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            
            // Fade out to black in the last 2 seconds
            if (t > 0.5f)
            {
                float fadeT = (t - 0.5f) * 2f;
                if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = fadeT;
            }
            yield return null;
        }

        if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = 1f;
        yield return new WaitForSeconds(1f);

        // Stop the siren sound when transitioning to credits
        if (sirenAudioSource != null)
        {
            sirenAudioSource.Stop();
        }

        // --- CREDITS & PLAY MUSIC ---
        PlayEndingCredits();
    }

    private void LateUpdate()
    {
        if (shouldLockProtagonist && mainCharacter != null && bossOfficer != null)
        {
            // Lock positions completely to eliminate physics and external movement conflicts
            mainCharacter.transform.position = mainStartPos;
            bossOfficer.transform.position = bossStartPos;

            // Make them face each other continuously (Y-axis only)
            Vector3 lookAtBoss = bossStartPos;
            lookAtBoss.y = mainCharacter.transform.position.y;
            mainCharacter.transform.LookAt(lookAtBoss);

            Vector3 lookAtMain = mainStartPos;
            lookAtMain.y = bossOfficer.transform.position.y;
            bossOfficer.transform.LookAt(lookAtMain);
        }
    }

    private void PlayEndingCredits()
    {
        // Play soundtrack
        if (audioSource != null && endingSoundtrack != null)
        {
            audioSource.Play();
        }

        // Setup Credits Canvas
        creditsCanvasObj = new GameObject("CreditsCanvas");
        var canvas = creditsCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        var scaler = creditsCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        creditsCanvasGroup = creditsCanvasObj.AddComponent<CanvasGroup>();
        creditsCanvasGroup.alpha = 0f;

        // Background
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(creditsCanvasObj.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = Color.black;

        // Scrolling credits container
        var textContainerGo = new GameObject("CreditsTextContainer");
        textContainerGo.transform.SetParent(creditsCanvasObj.transform, false);
        creditsTextRect = textContainerGo.AddComponent<RectTransform>();
        creditsTextRect.anchorMin = new Vector2(0.5f, 0f);
        creditsTextRect.anchorMax = new Vector2(0.5f, 0f);
        creditsTextRect.pivot = new Vector2(0.5f, 0f);
        creditsTextRect.sizeDelta = new Vector2(1200f, 1000f);
        creditsTextRect.anchoredPosition = new Vector2(0f, -800f); // Start below screen

        var text = textContainerGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.alignment = TextAnchor.UpperCenter;
        text.color = new Color(1f, 0.84f, 0f); // Gold color
        text.lineSpacing = 1.6f;
        text.text = @"CHUYÊN ÁN KẾT THÚC THÀNH CÔNG MỸ MÃN.

ĐƯỜNG DÂY MA TÚY TẠI HẺM 113 BỊ XÓA SỔ HOÀN TOÀN KHỎI ĐỊA BÀN.

CHIẾC XE BÁNH BÒ BÍA CỦA TRUNG ÚY LẠI LẶNG LẼ ĐƯỢC ĐẨY SANG MỘT CON HẺM KHÁC, BẮT ĐẦU MỘT VỎ BỌC MỚI…


THÀNH VIÊN THỰC HIỆN:
ANH KHÔI
GIA BẢO
BÁCH KHOA";

        // Shadows for premium look
        var shadow = textContainerGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        // Main Menu Button (Invisible at first, fades in later)
        var btnGo = new GameObject("MainMenuButton");
        btnGo.transform.SetParent(creditsCanvasObj.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.1f);
        btnRect.anchorMax = new Vector2(0.5f, 0.1f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300f, 60f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.12f, 0.16f, 0.85f); // Glassmorphism dark
        var outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.84f, 0f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        mainMenuButton = btnGo.AddComponent<Button>();
        
        var btnTextGo = new GameObject("ButtonText");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnTextRect = btnTextGo.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        var btnTxt = btnTextGo.AddComponent<Text>();
        btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnTxt.fontSize = 24;
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color = new Color(1f, 0.84f, 0f); // Gold
        btnTxt.text = "TRỞ VỀ MAIN MENU";

        mainMenuButton.onClick.AddListener(() => {
            SceneManager.LoadScene("Main_Menu");
        });

        // Hide button initially
        btnGo.SetActive(false);

        // Skip prompt text at the bottom
        var skipTxtGo = new GameObject("SkipPromptText");
        skipTxtGo.transform.SetParent(creditsCanvasObj.transform, false);
        var skipRect = skipTxtGo.AddComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(0.5f, 0.05f);
        skipRect.anchorMax = new Vector2(0.5f, 0.05f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);
        skipRect.sizeDelta = new Vector2(800f, 50f);

        var skipTxt = skipTxtGo.AddComponent<Text>();
        skipTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skipTxt.fontSize = 20;
        skipTxt.alignment = TextAnchor.MiddleCenter;
        skipTxt.color = new Color(1f, 1f, 1f, 0.6f); // semi-transparent white
        skipTxt.text = "[Nhấn phím bất kỳ để bỏ qua]";

        var skipOutline = skipTxtGo.AddComponent<Outline>();
        skipOutline.effectColor = Color.black;
        skipOutline.effectDistance = new Vector2(1f, -1f);

        // Mark credits as active for any-key skip handling in Update
        areCreditsActive = true;

        StartCoroutine(AnimateCreditsRoutine(btnGo));
    }

    private IEnumerator AnimateCreditsRoutine(GameObject buttonObj)
    {
        // Fade in credits screen
        float elapsed = 0f;
        while (creditsCanvasGroup.alpha < 1f)
        {
            creditsCanvasGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }

        // Scroll text up
        float scrollSpeed = 60f; // Pixels per second
        float targetY = 1100f;  // Target height to scroll up to
        while (creditsTextRect.anchoredPosition.y < targetY)
        {
            creditsTextRect.anchoredPosition += new Vector2(0f, scrollSpeed * Time.deltaTime);
            yield return null;
        }

        // Show Main Menu Button
        buttonObj.SetActive(true);
        var btnGroup = buttonObj.AddComponent<CanvasGroup>();
        btnGroup.alpha = 0f;
        while (btnGroup.alpha < 1f)
        {
            btnGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }
    }

    private IEnumerator FadeSirenVolume(float targetVolume, float duration)
    {
        if (sirenAudioSource == null) yield break;
        float startVolume = sirenAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sirenAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }
        sirenAudioSource.volume = targetVolume;
    }

    private float PlayTTSAndShowSubtitle(int index, string message, float fallbackDuration)
    {
        float duration = fallbackDuration;
        if (ttsAudioClips != null && index < ttsAudioClips.Length && ttsAudioClips[index] != null)
        {
            AudioClip clip = ttsAudioClips[index];
            duration = clip.length + 0.5f; // Đợi thêm 0.5s cho tự nhiên
            if (ttsAudioSource != null)
            {
                ttsAudioSource.clip = clip;
                ttsAudioSource.Play();
            }
        }
        if (dialogueUI != null)
        {
            dialogueUI.ForceShow(message, duration);
        }
        return duration;
    }
}
