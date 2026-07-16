using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class FixWhiteboardLabels
{
    static FixWhiteboardLabels()
    {
        EditorApplication.delayCall += ApplyFix;
    }

    [MenuItem("Tools/Fix Whiteboard Labels")]
    public static void ApplyFix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        // 1. Open the intro scene
        var currentScene = EditorSceneManager.GetActiveScene().path;
        if (currentScene != "Assets/Scenes/Intro_CutScene.unity")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Intro_CutScene.unity", OpenSceneMode.Single);
        }

        // 2. Find Ban_Ghe_Giao_Chuyen_An
        GameObject boardParent = GameObject.Find("Ban_Ghe_Giao_Chuyen_An");
        if (boardParent == null)
        {
            Debug.LogError("[FixWhiteboardLabels] Ban_Ghe_Giao_Chuyen_An not found in scene!");
            return;
        }

        // 3. Find if we already applied the fix
        Transform existingFix = boardParent.transform.Find("MapLabelsFix");
        if (existingFix != null)
        {
            // Destroy the old one to apply fresh values
            GameObject.DestroyImmediate(existingFix.gameObject);
        }

        // Create the container GameObject
        GameObject fixContainer = new GameObject("MapLabelsFix");
        fixContainer.transform.SetParent(boardParent.transform, false);

        // Position the container relative to the board
        // Based on typical FBX imports, the whiteboard is located on the back panel of the model.
        // Let's set local coordinates. We can tweak these if they are offset.
        // We will create the labels relative to the root or a child.
        // Let's find the specific child mesh for the board if possible to align it perfectly.
        Transform boardMesh = null;
        foreach (Transform child in boardParent.GetComponentsInChildren<Transform>())
        {
            // The whiteboard is usually named board, whiteboard, cube, or similar
            if (child.name.ToLower().Contains("board") || child.name.ToLower().Contains("panel") || child.name.ToLower().Contains("map"))
            {
                boardMesh = child;
                break;
            }
        }

        if (boardMesh != null)
        {
            fixContainer.transform.SetParent(boardMesh, false);
            Debug.Log($"[FixWhiteboardLabels] Attached fix container to child mesh: {boardMesh.name}");
        }
        else
        {
            // Default offset relative to the parent object
            fixContainer.transform.localPosition = new Vector3(0f, 1.5f, 0.9f); 
            Debug.Log("[FixWhiteboardLabels] Attached fix container to parent root.");
        }

        // Create Hoàng Sa Label
        CreateLabel(fixContainer.transform, "HoangSaFix", 
            new Vector3(0.55f, 1.95f, -0.02f), // Local position relative to container
            new Vector3(0.12f, 0.05f, 0.01f),  // Mask Quad scale
            "Hoàng Sa",
            new Vector3(0.55f, 1.95f, -0.025f) // Text position
        );

        // Create Trường Sa Label
        CreateLabel(fixContainer.transform, "TruongSaFix", 
            new Vector3(0.48f, 1.25f, -0.02f), // Local position relative to container
            new Vector3(0.14f, 0.05f, 0.01f),  // Mask Quad scale
            "Trường Sa",
            new Vector3(0.48f, 1.25f, -0.025f) // Text position
        );

        // Mark scene dirty and save
        EditorUtility.SetDirty(boardParent);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        
        Debug.Log("[FixWhiteboardLabels] Successfully applied Hoàng Sa & Trường Sa labels to the whiteboard!");

        // Restore original scene if it was different
        if (!string.IsNullOrEmpty(currentScene) && currentScene != "Assets/Scenes/Intro_CutScene.unity")
        {
            EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);
        }
    }

    private static void CreateLabel(Transform parent, string name, Vector3 maskPos, Vector3 maskScale, string textValue, Vector3 textPos)
    {
        GameObject labelObj = new GameObject(name);
        labelObj.transform.SetParent(parent, false);

        // 1. Create a white Quad to cover the old gibberish text
        GameObject mask = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mask.name = "Mask";
        mask.transform.SetParent(labelObj.transform, false);
        mask.transform.localPosition = maskPos;
        mask.transform.localScale = maskScale;
        mask.transform.localRotation = Quaternion.identity;

        // Apply a plain white material to the mask (matching the whiteboard background)
        Renderer maskRenderer = mask.GetComponent<Renderer>();
        if (maskRenderer != null)
        {
            Material whiteMat = new Material(Shader.Find("Unlit/Color"));
            whiteMat.color = new Color(0.88f, 0.88f, 0.85f); // Off-white color matching the whiteboard
            maskRenderer.material = whiteMat;
        }
        
        // Remove Collider so it doesn't interfere with raycasts/physics
        Collider col = mask.GetComponent<Collider>();
        if (col != null) GameObject.DestroyImmediate(col);

        // 2. Create the TextMesh component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(labelObj.transform, false);
        textObj.transform.localPosition = textPos;
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f); // Scale down 3D TextMesh

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = textValue;
        textMesh.fontSize = 90;
        textMesh.characterSize = 0.2f;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.8f, 0.1f, 0.1f); // Dark Red to match the map style
    }
}
