using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages a day/night cycle:
/// - Rotates the directional light (sun) to simulate time passing.
/// - Street lights turn OFF during the day and ON at night.
/// - Certain objects (anhbanhmi, xebanhmi) only appear at night.
/// 
/// Attach this script to an empty GameObject in the scene.
/// It auto-discovers lights and night-only objects by name.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("=== Time Settings ===")]
    [Tooltip("Current time of day in hours (0-24). 6=sunrise, 12=noon, 18=sunset, 0=midnight")]
    [Range(0f, 24f)]
    public float timeOfDay = 6f; // Start at sunrise (day)

    [Tooltip("Speed multiplier for time passing. 1 = real-time, 60 = 1 min per second, etc.")]
    public float timeSpeed = 100f;

    [Tooltip("If true, time progresses automatically. If false, you can set timeOfDay manually.")]
    public bool autoProgress = true;

    [Header("=== Sun Settings ===")]
    [Tooltip("Reference to the Directional Light acting as the sun. Auto-found if left empty.")]
    public Light sunLight;

    [Tooltip("Sun color at noon (bright warm)")]
    public Color sunColorDay = new Color(1f, 0.95f, 0.85f);

    [Tooltip("Sun color at sunrise/sunset (orange)")]
    public Color sunColorSunset = new Color(1f, 0.55f, 0.2f);

    [Tooltip("Sun color at night (moonlight blue)")]
    public Color sunColorNight = new Color(0.35f, 0.4f, 0.55f);

    [Tooltip("Max sun intensity at noon")]
    public float sunIntensityDay = 2.2f;

    [Tooltip("Sun intensity at night (moonlight)")]
    public float sunIntensityNight = 0.15f;

    [Header("=== Ambient Settings ===")]
    public Color ambientColorDay = new Color(0.85f, 0.85f, 0.85f);
    public Color ambientColorNight = new Color(0.05f, 0.05f, 0.1f);

    [Header("=== Street Light Settings ===")]
    [Tooltip("Hour when street lights turn ON (e.g. 18 = 6 PM)")]
    public float lightsOnHour = 18f;

    [Tooltip("Hour when street lights turn OFF (e.g. 6 = 6 AM)")]
    public float lightsOffHour = 6f;

    [Tooltip("Fade duration in game-hours for lights to fade in/out")]
    public float lightFadeDuration = 0.5f;

    [Header("=== Night-Only Objects ===")]
    [Tooltip("Hour when night objects appear")]
    public float nightObjectsAppearHour = 18.5f;

    [Tooltip("Hour when night objects disappear")]
    public float nightObjectsDisappearHour = 5.5f;

    private class StreetLightCache
    {
        public Light light;
        public float maxIntensity;
        public List<Material> materials = new List<Material>();
    }
    private List<StreetLightCache> streetLightCaches = new List<StreetLightCache>();

    // Internal references (auto-discovered)
    private List<GameObject> nightOnlyObjects = new List<GameObject>();

    // Cached state
    private bool isNightTime = false;
    private bool lightsAreOn = false;
    private Material skyboxInstance;
    private float lastFadeFactor = -1f;
    private float lastDayFactor = -1f;


    void Start()
    {
        // Clone skybox material to avoid modifying the asset on disk at runtime
        if (RenderSettings.skybox != null)
        {
            skyboxInstance = Instantiate(RenderSettings.skybox);
            RenderSettings.skybox = skyboxInstance;
        }

        // Auto-find sun if not assigned
        if (sunLight == null)
        {
            Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var light in allLights)
            {
                if (light.type == LightType.Directional)
                {
                    sunLight = light;
                    break;
                }
            }
        }

        // Auto-discover street lights
        DiscoverStreetLights();

        // Auto-discover night-only objects
        DiscoverNightOnlyObjects();

        // Apply initial state
        UpdateCycle();

        Debug.Log($"[DayNightCycle] Initialized: {streetLightCaches.Count} street lights, {nightOnlyObjects.Count} night-only objects found.");
    }

    void Update()
    {
        if (autoProgress)
        {
            // Advance time (timeSpeed is in real-seconds per game-hour, so we multiply)
            timeOfDay += (timeSpeed * Time.deltaTime) / 3600f;

            // Wrap around 24 hours
            if (timeOfDay >= 24f)
                timeOfDay -= 24f;
        }

        UpdateCycle();
    }

    /// <summary>
    /// Updates all cycle elements based on current timeOfDay.
    /// </summary>
    void UpdateCycle()
    {
        UpdateSun();
        UpdateStreetLights();
        UpdateNightOnlyObjects();
        UpdateAmbient();
        UpdateSkybox();

        lastDayFactor = CalculateDayFactor();
    }

    /// <summary>
    /// Rotate and color the sun based on time of day.
    /// </summary>
    void UpdateSun()
    {
        if (sunLight == null) return;

        // Sun rotation: 0h = -90° (below horizon), 6h = 0° (sunrise), 12h = 90° (noon), 18h = 180° (sunset)
        float sunAngle = (timeOfDay / 24f) * 360f - 90f;
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

        // Calculate how "daytime" it is (0 = full night, 1 = full day)
        float dayFactor = CalculateDayFactor();

        // Intensity
        sunLight.intensity = Mathf.Lerp(sunIntensityNight, sunIntensityDay, dayFactor);

        // Color - blend between night, sunset, and day colors
        if (dayFactor > 0.5f)
        {
            // Day: blend between sunset and day
            float t = (dayFactor - 0.5f) * 2f;
            sunLight.color = Color.Lerp(sunColorSunset, sunColorDay, t);
        }
        else
        {
            // Night to sunset: blend between night and sunset
            float t = dayFactor * 2f;
            sunLight.color = Color.Lerp(sunColorNight, sunColorSunset, t);
        }
    }

    /// <summary>
    /// Calculate a 0-1 factor: 0 = deep night, 1 = full day
    /// Smooth transition around sunrise (5-7) and sunset (17-19)
    /// </summary>
    float CalculateDayFactor()
    {
        float hour = timeOfDay;

        // Sunrise transition: 5 to 7
        if (hour >= 5f && hour < 7f)
        {
            return Mathf.SmoothStep(0f, 1f, (hour - 5f) / 2f);
        }
        // Full day: 7 to 17
        else if (hour >= 7f && hour < 17f)
        {
            return 1f;
        }
        // Sunset transition: 17 to 19
        else if (hour >= 17f && hour < 19f)
        {
            return Mathf.SmoothStep(1f, 0f, (hour - 17f) / 2f);
        }
        // Night
        else
        {
            return 0f;
        }
    }

    /// <summary>
    /// Turn street lights on/off based on time.
    /// </summary>
    void UpdateStreetLights()
    {
        bool shouldBeOn = IsNightHours(lightsOnHour, lightsOffHour);

        // Calculate fade factor
        float fadeFactor = CalculateLightFade(shouldBeOn);

        if (Mathf.Abs(fadeFactor - lastFadeFactor) > 0.001f)
        {
            lastFadeFactor = fadeFactor;

            for (int i = 0; i < streetLightCaches.Count; i++)
            {
                var cache = streetLightCaches[i];
                if (cache.light == null) continue;

                if (fadeFactor <= 0f)
                {
                    cache.light.enabled = false;
                }
                else
                {
                    cache.light.enabled = true;
                    cache.light.intensity = cache.maxIntensity * fadeFactor;
                }

                // Cập nhật trực tiếp trên các material đã cache (không tạo rác, cực nhanh)
                for (int j = 0; j < cache.materials.Count; j++)
                {
                    var mat = cache.materials[j];
                    if (mat == null) continue;

                    if (fadeFactor <= 0f)
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.DisableKeyword("_EMISSION");
                    }
                    else
                    {
                        mat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.7f) * fadeFactor);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        lightsAreOn = shouldBeOn;
    }

    /// <summary>
    /// Show/hide night-only objects based on time.
    /// </summary>
    void UpdateNightOnlyObjects()
    {
        bool shouldBeVisible = IsNightHours(nightObjectsAppearHour, nightObjectsDisappearHour);

        if (shouldBeVisible != isNightTime)
        {
            foreach (var obj in nightOnlyObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(shouldBeVisible);
                }
            }
            isNightTime = shouldBeVisible;
        }
    }

    /// <summary>
    /// Update ambient lighting based on time.
    /// </summary>
    void UpdateAmbient()
    {
        float dayFactor = CalculateDayFactor();
        if (Mathf.Abs(dayFactor - lastDayFactor) > 0.001f)
        {
            RenderSettings.ambientLight = Color.Lerp(ambientColorNight, ambientColorDay, dayFactor);
        }
    }

    /// <summary>
    /// Update the skybox parameters dynamically for procedural skyboxes
    /// </summary>
    void UpdateSkybox()
    {
        Material skyMat = Application.isPlaying ? skyboxInstance : RenderSettings.skybox;
        if (skyMat != null)
        {
            float dayFactor = CalculateDayFactor();
            if (Mathf.Abs(dayFactor - lastDayFactor) > 0.001f)
            {
                // Interpolate sky parameters: Night (dark, low exposure) vs Day (sky blue, bright exposure)
                Color skyTint = Color.Lerp(new Color(0.1f, 0.13f, 0.28f), new Color(0.5f, 0.7f, 1f), dayFactor);
                Color groundColor = Color.Lerp(new Color(0.03f, 0.04f, 0.07f), new Color(0.36f, 0.3f, 0.25f), dayFactor);
                float exposure = Mathf.Lerp(0.3f, 1.0f, dayFactor);

                skyMat.SetColor("_SkyTint", skyTint);
                skyMat.SetColor("_GroundColor", groundColor);
                skyMat.SetFloat("_Exposure", exposure);
            }
        }
    }

    /// <summary>
    /// Check if current time is in the "night" window (wrapping around midnight).
    /// </summary>
    bool IsNightHours(float onHour, float offHour)
    {
        if (onHour > offHour)
        {
            // Night wraps around midnight (e.g., 18 to 6)
            return timeOfDay >= onHour || timeOfDay < offHour;
        }
        else
        {
            return timeOfDay >= onHour && timeOfDay < offHour;
        }
    }

    /// <summary>
    /// Smooth fade factor for lights.
    /// </summary>
    float CalculateLightFade(bool shouldBeOn)
    {
        if (lightFadeDuration <= 0f)
            return shouldBeOn ? 1f : 0f;

        float hourToOn = HourDistance(timeOfDay, lightsOnHour);
        float hourToOff = HourDistance(timeOfDay, lightsOffHour);

        if (shouldBeOn)
        {
            // Fading in after lightsOnHour
            if (hourToOn < lightFadeDuration)
                return Mathf.SmoothStep(0f, 1f, hourToOn / lightFadeDuration);
            return 1f;
        }
        else
        {
            // Fading out after lightsOffHour
            if (hourToOff < lightFadeDuration)
                return Mathf.SmoothStep(1f, 0f, hourToOff / lightFadeDuration);
            return 0f;
        }
    }

    /// <summary>
    /// Calculate shortest distance in hours from 'from' to 'to', going forward.
    /// </summary>
    float HourDistance(float from, float reference)
    {
        float dist = from - reference;
        if (dist < 0f) dist += 24f;
        return dist;
    }

    private GameObject FindObjectIncludingInactive(string name)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform t = FindRecursive(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform r = FindRecursive(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private void FindNightOnlyObjectsRecursive(Transform parent, string[] names)
    {
        foreach (string name in names)
        {
            if (parent.name == name)
            {
                if (!nightOnlyObjects.Contains(parent.gameObject))
                {
                    nightOnlyObjects.Add(parent.gameObject);
                }
            }
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            FindNightOnlyObjectsRecursive(parent.GetChild(i), names);
        }
    }

    /// <summary>
    /// Auto-discover all street lights in the scene by parent group name.
    /// </summary>
    void DiscoverStreetLights()
    {
        streetLightCaches.Clear();

        List<Light> foundLights = new List<Light>();

        // Find lights under "Spawned_Street_Lights" group
        GameObject spawnedGroup = FindObjectIncludingInactive("Spawned_Street_Lights");
        if (spawnedGroup != null)
        {
            foundLights.AddRange(spawnedGroup.GetComponentsInChildren<Light>(true));
        }

        // Find lights under "Street_Lights_OldTown" group
        GameObject oldTownGroup = FindObjectIncludingInactive("Street_Lights_OldTown");
        if (oldTownGroup != null)
        {
            foundLights.AddRange(oldTownGroup.GetComponentsInChildren<Light>(true));
        }

        foreach (var l in foundLights)
        {
            if (l == null) continue;
            
            // Tối ưu hóa URP: tắt đổ bóng thời gian thực cho đèn đường để giảm cực lớn số Draw Calls
            l.shadows = LightShadows.None;

            var cache = new StreetLightCache();
            cache.light = l;
            cache.maxIntensity = l.intensity;

            // Get renderers once
            Renderer[] renderers = l.transform.parent != null
                ? l.transform.parent.GetComponentsInChildren<Renderer>(true)
                : l.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Truy cập .materials ở đây để tạo clone 1 lần duy nhất lúc Start
                foreach (var mat in r.materials)
                {
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                    {
                        cache.materials.Add(mat);
                    }
                }
            }
            streetLightCaches.Add(cache);
        }
    }

    /// <summary>
    /// Auto-discover night-only objects (anhbanhmi, xebanhmi, etc).
    /// </summary>
    void DiscoverNightOnlyObjects()
    {
        nightOnlyObjects.Clear();

        string[] nightObjectNames = new string[]
        {
            "anhbanhmi",
            "xebanhmi_fbx"
        };

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            FindNightOnlyObjectsRecursive(root.transform, nightObjectNames);
        }
    }

    // ===== EDITOR GIZMO =====
#if UNITY_EDITOR
    void OnValidate()
    {
        // Allow live preview in editor when changing timeOfDay slider
        if (!Application.isPlaying && sunLight != null)
        {
            UpdateCycle();
        }
    }
#endif
}
