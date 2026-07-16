using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneBootstrapper
{
    private const string ScenePath = "Assets/Scenes/NeighborhoodScene.unity";
    private const string CartModelPath = "Assets/Models/Xe_Bo_Bia/Meshy_AI_Snack_Delivery_Scoote_0608045356_texture.fbx";
    private const string CartTexturePath = "Assets/Models/Xe_Bo_Bia/Meshy_AI_Snack_Delivery_Scoote_0608045356_texture.png";
    private const string CartMaterialPath = "Assets/Models/Xe_Bo_Bia/Xe_Bo_Bia_Material.mat";
    private const string MiuLePrefabPath = "Assets/Models/Miu Le/Miu Le.prefab";

    [MenuItem("AnhBoBia/Setup Gameplay Scene")]
    public static void SetupGameplayScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Material cartMaterial = EnsureCartMaterial();
        GameObject cartRoot = EnsureRoot("Xe_Bo_Bia_Root", new Vector3(0f, 0.035f, 0f));
        RemoveChildIfExists(cartRoot.transform, "Body Car");
        RemoveChildIfExists(cartRoot.transform, "Thung_Xop_Bo_Bia");
        EnsureCartModel(cartRoot.transform, cartMaterial);
        EnsureCartCollision(cartRoot);

        RemoveRootIfExists("V\u1ecb_Tr\u00ed_Kh\u00e1ch_\u0110\u1ee9ng");
        RemoveRootIfExists("Vi_Tri_Khach_Dung_Old");
        RemoveRootIfExists("Vi_Tri_Khach_Dung_1");

        GameObject approachPoint = EnsureRoot("Diem_Tiep_Can_Khach", new Vector3(1.65f, 0f, -4.0f));
        GameObject queuePoint = EnsureRoot("Vi_Tri_Khach_Dung", new Vector3(1.28f, 0f, -0.02f));
        FaceToward(approachPoint.transform, queuePoint.transform.position);
        FaceToward(queuePoint.transform, cartRoot.transform.position);

        GameObject spawnPoint = EnsureRoot("Diem_Spawn_Khach", new Vector3(-7f, 0f, -4f));
        FaceToward(spawnPoint.transform, approachPoint.transform.position);

        GameObject managers = GameObject.Find("_GameManagers");
        CustomerManager customerManager = managers != null ? managers.GetComponent<CustomerManager>() : Object.FindAnyObjectByType<CustomerManager>();
        if (customerManager != null)
        {
            GameObject miuLePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiuLePrefabPath);
            SerializedObject serializedCustomerManager = new SerializedObject(customerManager);
            serializedCustomerManager.FindProperty("autoStartOnPlay").boolValue = true;
            serializedCustomerManager.FindProperty("initialSpawnDelay").floatValue = 1f;
            serializedCustomerManager.FindProperty("spawnIntervalMin").floatValue = 8f;
            serializedCustomerManager.FindProperty("spawnIntervalMax").floatValue = 15f;
            serializedCustomerManager.FindProperty("spawnPoint").objectReferenceValue = spawnPoint.transform;
            serializedCustomerManager.FindProperty("arrivalDistance").floatValue = 0.35f;
            serializedCustomerManager.FindProperty("slowDownDistance").floatValue = 0.9f;
            SerializedProperty approachPointProperty = serializedCustomerManager.FindProperty("approachPoint");
            if (approachPointProperty != null)
            {
                approachPointProperty.objectReferenceValue = approachPoint.transform;
            }
            serializedCustomerManager.FindProperty("targetQueuePoint").objectReferenceValue = queuePoint.transform;
            serializedCustomerManager.FindProperty("customerPrefab").objectReferenceValue = miuLePrefab;
            SerializedProperty prefabs = serializedCustomerManager.FindProperty("customerPrefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = miuLePrefab;
            serializedCustomerManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(customerManager);
        }

        BoBiaMechanic boBiaMechanic = cartRoot.GetComponent<BoBiaMechanic>();
        if (boBiaMechanic != null)
        {
            SerializedObject serializedMechanic = new SerializedObject(boBiaMechanic);
            serializedMechanic.FindProperty("stallCenter").objectReferenceValue = cartRoot.transform;
            serializedMechanic.FindProperty("totalRollingDuration").floatValue = 3f;
            if (customerManager != null)
            {
                serializedMechanic.FindProperty("customerManager").objectReferenceValue = customerManager;
            }
            serializedMechanic.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boBiaMechanic);
        }

        EnsureRollingProgressUI(boBiaMechanic);

        GameTimeManager gameTimeManager = managers != null ? managers.GetComponent<GameTimeManager>() : Object.FindAnyObjectByType<GameTimeManager>();
        if (gameTimeManager != null && customerManager != null)
        {
            SerializedObject serializedGameTime = new SerializedObject(gameTimeManager);
            serializedGameTime.FindProperty("customerManager").objectReferenceValue = customerManager;
            serializedGameTime.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameTimeManager);
        }

        RemoveRootIfExists("Cube");
        EnsureRootList(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneBootstrapper] Phase 1 gameplay setup complete.");
    }

    private static Material EnsureCartMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CartMaterialPath);
        if (material == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CartMaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, CartMaterialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CartTexturePath);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static void EnsureCartModel(Transform parent, Material material)
    {
        Transform existing = parent.Find("Xe_Bo_Bia_Model");
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CartModelPath);
        if (modelPrefab == null)
        {
            Debug.LogError($"[SceneBootstrapper] Missing cart model at {CartModelPath}");
            return;
        }

        GameObject model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        model.name = "Xe_Bo_Bia_Model";
        model.transform.SetParent(parent, false);
        model.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        model.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
        model.transform.localScale = Vector3.one * 40f;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = material;
        }

    }

    private static void EnsureCartCollision(GameObject cartRoot)
    {
        BoxCollider collider = cartRoot.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = cartRoot.AddComponent<BoxCollider>();
        }

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in cartRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            collider.center = cartRoot.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size + new Vector3(0.35f, 0.15f, 0.35f);
        }
        else
        {
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.size = new Vector3(4.8f, 1.9f, 2.1f);
        }

        collider.isTrigger = false;
        collider.enabled = true;

        Rigidbody rigidbody = cartRoot.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = cartRoot.AddComponent<Rigidbody>();
        }

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
    }

    private static void EnsureRollingProgressUI(BoBiaMechanic boBiaMechanic)
    {
        GameObject canvasObject = EnsureRoot("Rolling_Progress_Canvas", Vector3.zero);
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureComponent<GraphicRaycaster>(canvasObject);
        CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(canvasObject);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject sliderObject = EnsureUiChild(canvasObject.transform, "Rolling_Progress_Slider");
        Slider slider = EnsureComponent<Slider>(sliderObject);
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.None;

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -150f);
        sliderRect.sizeDelta = new Vector2(520f, 34f);

        GameObject backgroundObject = EnsureUiChild(sliderObject.transform, "Background");
        Image background = EnsureComponent<Image>(backgroundObject);
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        StretchToParent(backgroundObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject fillAreaObject = EnsureUiChild(sliderObject.transform, "Fill Area");
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        StretchToParent(fillAreaRect, new Vector2(8f, 6f), new Vector2(-8f, -6f));

        GameObject fillObject = EnsureUiChild(fillAreaObject.transform, "Fill");
        Image fill = EnsureComponent<Image>(fillObject);
        fill.color = new Color(0.95f, 0.78f, 0.25f, 1f);
        StretchToParent(fillObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        GameObject handleAreaObject = EnsureUiChild(sliderObject.transform, "Handle Slide Area");
        RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        StretchToParent(handleAreaRect, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        GameObject handleObject = EnsureUiChild(handleAreaObject.transform, "Handle");
        Image handle = EnsureComponent<Image>(handleObject);
        handle.color = new Color(1f, 0.95f, 0.72f, 1f);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 42f);

        slider.targetGraphic = handle;
        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;

        RollingProgressBarUI progressUI = EnsureComponent<RollingProgressBarUI>(canvasObject);
        SerializedObject serializedProgressUI = new SerializedObject(progressUI);
        serializedProgressUI.FindProperty("boBiaMechanic").objectReferenceValue = boBiaMechanic;
        serializedProgressUI.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedProgressUI.FindProperty("progressSlider").objectReferenceValue = slider;
        serializedProgressUI.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progressUI);
        EditorUtility.SetDirty(canvasObject);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static GameObject EnsureUiChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        GameObject childObject = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform));
        childObject.transform.SetParent(parent, false);
        return childObject;
    }

    private static void StretchToParent(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static GameObject EnsureRoot(string name, Vector3 position)
    {
        GameObject gameObject = GameObject.Find(name);
        if (gameObject == null)
        {
            gameObject = new GameObject(name);
        }

        gameObject.transform.SetParent(null);
        gameObject.transform.position = position;
        return gameObject;
    }

    private static void RemoveChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void RemoveRootIfExists(string name)
    {
        GameObject gameObject = GameObject.Find(name);
        if (gameObject != null && gameObject.transform.parent == null)
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static void FaceToward(Transform transform, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    private static void EnsureRootList(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            root.transform.SetParent(null);
        }
    }
}
