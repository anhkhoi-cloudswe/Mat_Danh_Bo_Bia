using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Cung cấp MenuItem thiết lập Main Menu scene theo yêu cầu.
/// Sao chép phần trình diễn cần thiết từ BaoScene, không mang gameplay component sang menu.
/// </summary>
public class SetupMainMenuScene
{
    private const string ScenePath = "Assets/Scenes/Main_Menu.unity";
    private const string SourceScenePath = "Assets/Scenes/BaoScene.unity";

    private static bool isSettingUp = false;

    private static IEnumerable<GameObject> EnumerateSceneObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                yield return child.gameObject;
            }
        }
    }

    [MenuItem("Tools/Setup Main Menu Scene")]
    public static void Setup()
    {
        if (isSettingUp) return;
        isSettingUp = true;

        try
        {
            // 1. Kiểm tra xem scene Main_Menu có tồn tại không
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"Không tìm thấy file scene tại {ScenePath}. Hãy tạo scene trước!");
                return;
            }

            // 2. Mở scene Main_Menu trong Editor làm scene chính
            Scene mainMenuScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Dọn dẹp tất cả các đối tượng cũ tạo thủ công để tránh trùng lặp
            string[] objectsToClean = { 
                "Ground_Map01", "AsphaltRoad", "AtmosphericSpotLight", "AlleyHouse", 
                "MainMenuCanvas", "EventSystem", "Xe_Bo_Bia_Root", "Xe_Bo_Bia", 
                "Global Volume", "directional light", "Directional Light", "MainMenuManager", "MenuLight", 
                "AlleyHouse(Clone)", "MenuPlayerCharacter"
            };
            foreach (string objName in objectsToClean)
            {
                GameObject go = GameObject.Find(objName);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            // 4. Mở BaoScene additively chỉ để copy phần trình diễn mới nhất.
            Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

            // 5. Xác định các đối tượng môi trường và nhân vật cần thiết từ Phase1
            GameObject groundObj = null;
            GameObject volumeObj = null;
            GameObject lightObj = null;
            GameObject cartObj = null;
            GameObject playerObj = null;

            foreach (GameObject rootGo in EnumerateSceneObjects(sourceScene))
            {
                if (rootGo.name == "Ground_Map01")
                {
                    groundObj = rootGo;
                }
                else if (rootGo.name == "Global Volume" || rootGo.name.Contains("Volume"))
                {
                    volumeObj = rootGo;
                }
                else if (rootGo.GetComponent<Light>() is Light candidateLight && candidateLight.type == LightType.Directional)
                {
                    lightObj = rootGo;
                }
                else if (rootGo.name == "Xe_Bo_Bia_Root" || rootGo.name == "Xe_Bo_Bia")
                {
                    cartObj = rootGo;
                }
                else if (rootGo.CompareTag("Player") || rootGo.GetComponent<PlayerMovement>() != null)
                {
                    playerObj = rootGo;
                }
            }

            // 6. Sao chép các đối tượng sang Main_Menu scene
            if (groundObj != null)
            {
                GameObject dup = Object.Instantiate(groundObj);
                dup.name = "Ground_Map01";
                EditorSceneManager.MoveGameObjectToScene(dup, mainMenuScene);
                Debug.Log("[Setup] Đã copy Ground_Map01 từ BaoScene sang Main Menu.");
            }
            else
            {
                Debug.LogWarning("[Setup] Không tìm thấy Ground_Map01 trong BaoScene!");
            }

            if (volumeObj != null)
            {
                GameObject dup = Object.Instantiate(volumeObj);
                dup.name = volumeObj.name;
                EditorSceneManager.MoveGameObjectToScene(dup, mainMenuScene);
                Debug.Log("[Setup] Đã copy Global Volume hậu kỳ từ BaoScene.");
            }

            if (lightObj != null)
            {
                GameObject dup = Object.Instantiate(lightObj);
                dup.name = lightObj.name;
                EditorSceneManager.MoveGameObjectToScene(dup, mainMenuScene);
                Debug.Log("[Setup] Đã copy ánh sáng chính từ BaoScene.");
            }

            if (cartObj != null)
            {
                GameObject dup = Object.Instantiate(cartObj);
                dup.name = cartObj.name;
                
                // Xóa các script di chuyển/điều khiển hoặc camera ko cần thiết trên Cart để tránh lỗi Main Menu
                var cartController = dup.GetComponent<CartController>();
                if (cartController != null) Object.DestroyImmediate(cartController);
                var mechanic = dup.GetComponent<BoBiaMechanic>();
                if (mechanic != null) Object.DestroyImmediate(mechanic);
                var rb = dup.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                EditorSceneManager.MoveGameObjectToScene(dup, mainMenuScene);
                Debug.Log("[Setup] Đã copy Xe_Bo_Bia_Root từ Phase1.");
            }

            if (playerObj != null)
            {
                GameObject dup = Object.Instantiate(playerObj);
                dup.name = "MenuPlayerCharacter";
                
                // Xóa các script điều khiển di chuyển để nhân vật đứng yên làm phông nền
                var pm = dup.GetComponent<PlayerMovement>();
                if (pm != null) Object.DestroyImmediate(pm);
                dup.tag = "Untagged";
                
                // Vị trí đứng chuẩn cạnh xe như trong Phase1
                EditorSceneManager.MoveGameObjectToScene(dup, mainMenuScene);
                Debug.Log("[Setup] Đã copy nhân vật chính đứng cạnh xe.");
            }

            // 7. Đóng BaoScene nguồn mà không lưu thay đổi vào file gốc.
            EditorSceneManager.CloseScene(sourceScene, true);

            // 8. Định vị lại Camera chính góc nhìn chéo trước cửa nhà nhân vật chính (như hình 2)
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            cam.transform.position = new Vector3(-5.51f, 1.13f, -3.49f);
            cam.transform.rotation = Quaternion.Euler(4.934f, 25.632f, -0.813f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);

            // Đảm bảo Directional Light giữ độ sáng màu tối ban đêm
            Light[] directionalLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in directionalLights)
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0.05f;
                    l.color = new Color(0.1f, 0.15f, 0.25f, 1f);
                }
            }

            // Bật sương mù tối sẫm màu
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.05f;

            // Tạo đèn Spot Light chiếu xuống đầu MenuPlayerCharacter
            GameObject playerSpot = GameObject.Find("PlayerSpotLight");
            if (playerSpot == null)
            {
                playerSpot = new GameObject("PlayerSpotLight");
            }
            playerSpot.transform.position = new Vector3(-4.22f, 3.5f, -1.16f);
            playerSpot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light pLight = playerSpot.GetComponent<Light>();
            if (pLight == null) pLight = playerSpot.AddComponent<Light>();
            pLight.type = LightType.Spot;
            pLight.range = 6f;
            pLight.spotAngle = 60f;
            pLight.intensity = 5.0f;
            pLight.color = new Color(1f, 0.95f, 0.8f);
            pLight.shadows = LightShadows.Soft;

            var tpCam = cam.GetComponent<ThirdPersonCamera>();
            if (tpCam != null) Object.DestroyImmediate(tpCam);
            var fpCam = cam.GetComponent<FirstPersonCamera>();
            if (fpCam != null) Object.DestroyImmediate(fpCam);

            // 9. Tìm hoặc Tạo GameObject điều khiển Menu chính
            MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>();
            if (controller == null)
            {
                GameObject menuManager = new GameObject("MainMenuManager");
                controller = menuManager.AddComponent<MainMenuController>();
                Debug.Log("[Setup] Đã tạo mới MainMenuManager.");
            }
            
            // 10. Lưu Scene
            EditorSceneManager.MarkSceneDirty(mainMenuScene);
            bool saveSuccess = EditorSceneManager.SaveScene(mainMenuScene);

            if (saveSuccess)
            {
                Debug.Log("[Setup] Thiết lập scene Main_Menu từ phần trình diễn BaoScene hoàn tất!");
            }
            else
            {
                Debug.LogError("[Setup] Không thể lưu scene Main_Menu.");
            }
        }
        finally
        {
            isSettingUp = false;
        }
    }
}
