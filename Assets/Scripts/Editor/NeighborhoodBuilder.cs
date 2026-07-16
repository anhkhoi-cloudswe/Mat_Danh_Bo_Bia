using UnityEngine;
using UnityEditor;

public class NeighborhoodBuilder : EditorWindow
{
    private static Color ochreYellow = new Color(0.78f, 0.62f, 0.22f);   // Weathered Ochre Yellow
    private static Color cementGrey = new Color(0.48f, 0.48f, 0.46f);    // Cement Grey
    private static Color oldCream = new Color(0.85f, 0.8f, 0.72f);      // Old Dirty Cream
    private static Color weatheredGreen = new Color(0.4f, 0.5f, 0.42f);   // Weathered Green
    private static Color mossyGrey = new Color(0.55f, 0.58f, 0.52f);     // Mossy Grey

    private static Color[] wallColors = new Color[] {
        ochreYellow,
        cementGrey,
        oldCream,
        weatheredGreen,
        mossyGrey
    };

    [MenuItem("Tools/Build Vietnamese Neighborhood")]
    public static void BuildNeighborhood()
    {
        // 1. Open the active scene
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/NeighborhoodScene.unity");

        // Delete old environments if they exist in the scene
        GameObject oldHouse = GameObject.Find("Vietnamese_Townhouse");
        if (oldHouse != null)
        {
            Undo.DestroyObjectImmediate(oldHouse);
        }

        GameObject oldAlley = GameObject.Find("Vietnamese_Alley");
        if (oldAlley != null)
        {
            Undo.DestroyObjectImmediate(oldAlley);
        }

        GameObject oldNeighborhood = GameObject.Find("Vietnamese_Neighborhood");
        if (oldNeighborhood != null)
        {
            Undo.DestroyObjectImmediate(oldNeighborhood);
        }

        // 2. Create root GameObject
        GameObject mapRoot = new GameObject("Vietnamese_Neighborhood");
        mapRoot.transform.SetParent(null);
        mapRoot.transform.position = Vector3.zero;
        mapRoot.transform.rotation = Quaternion.identity;
        mapRoot.transform.localScale = Vector3.one;

        // 3. Prepare Materials
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null) urpLitShader = Shader.Find("Standard");

        Material mainWallMat = new Material(urpLitShader);
        mainWallMat.color = ochreYellow;
        mainWallMat.name = "MainHouse_Wall_Mat";

        Material sideWallMat = new Material(urpLitShader);
        sideWallMat.color = cementGrey;
        sideWallMat.name = "House_SideWall_Mat";

        Material wallpaperMat = new Material(urpLitShader);
        wallpaperMat.color = new Color(0.82f, 0.78f, 0.68f); // Dirty vintage wallpaper
        wallpaperMat.name = "House_Wallpaper_Mat";

        Material floorMat = new Material(urpLitShader);
        floorMat.color = new Color(0.6f, 0.58f, 0.55f); // Stained grey tiles
        floorMat.name = "House_Floor_Mat";

        Material greenShutterMat = new Material(urpLitShader);
        greenShutterMat.color = new Color(0.05f, 0.22f, 0.12f); // Dark green rolling door
        greenShutterMat.name = "House_Shutter_Mat";

        Material woodMat = new Material(urpLitShader);
        woodMat.color = new Color(0.35f, 0.15f, 0.1f); // Reddish-brown wood frame
        woodMat.name = "House_Wood_Mat";

        Material whiteFrameMat = new Material(urpLitShader);
        whiteFrameMat.color = new Color(0.9f, 0.9f, 0.9f);
        whiteFrameMat.name = "House_WhiteFrame_Mat";

        Material glassMat = new Material(urpLitShader);
        glassMat.color = new Color(0.7f, 0.85f, 0.9f, 0.4f);
        if (glassMat.HasProperty("_Surface")) glassMat.SetFloat("_Surface", 1); 
        if (glassMat.HasProperty("_Blend")) glassMat.SetFloat("_Blend", 0); 
        glassMat.name = "House_Glass_Mat";

        Material acMat = new Material(urpLitShader);
        acMat.color = new Color(0.85f, 0.85f, 0.85f);
        acMat.name = "House_AC_Mat";

        Material acGrillMat = new Material(urpLitShader);
        acGrillMat.color = new Color(0.15f, 0.15f, 0.15f);
        acGrillMat.name = "House_AC_Grill_Mat";

        Material stepMat = new Material(urpLitShader);
        stepMat.color = new Color(0.4f, 0.38f, 0.35f); // Stained grey stone
        stepMat.name = "House_Step_Mat";

        Material pavementMat = new Material(urpLitShader);
        pavementMat.color = new Color(0.22f, 0.22f, 0.22f); // Dark weathered pavement
        pavementMat.name = "Walkway_Pavement_Mat";

        Material leafMat = new Material(urpLitShader);
        leafMat.color = new Color(0.15f, 0.32f, 0.15f); // Weathered dark green leaves
        leafMat.name = "Leaf_Mat";

        Material potMat = new Material(urpLitShader);
        potMat.color = new Color(0.5f, 0.3f, 0.2f); // Clay pot brown
        potMat.name = "Pot_Mat";

        Material villaMat = new Material(urpLitShader);
        villaMat.color = new Color(0.88f, 0.85f, 0.8f); // Off-white villa wall
        villaMat.name = "Villa_Wall_Mat";

        // Helper to remove collider
        System.Action<GameObject> removeCollider = (go) => {
            if (go != null) {
                var col = go.GetComponent<Collider>();
                if (col != null) {
                    UnityEngine.Object.DestroyImmediate(col);
                }
            }
        };

        // --- 1. DỰNG ĐƯỜNG ĐI BỘ VÒNG QUANH (LOOP WALKWAY) ---
        // Sàn đường đi bộ rộng đúng 1.8m bao quanh 4 phía (rộng rãi và thoáng hơn)
        // Left Walkway: chạy dọc hết chiều sâu bản đồ ở sườn trái
        GameObject walkLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkLeft.name = "Walkway_Left";
        walkLeft.transform.parent = mapRoot.transform;
        walkLeft.transform.localScale = new Vector3(1.8f, 0.2f, 12.0f);
        walkLeft.transform.localPosition = new Vector3(-4.4f, -0.1f, 0f); // Top surface Y = 0
        walkLeft.GetComponent<Renderer>().sharedMaterial = pavementMat;

        // Bottom Walkway: chạy ngang ở đáy
        GameObject walkBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkBottom.name = "Walkway_Bottom";
        walkBottom.transform.parent = mapRoot.transform;
        walkBottom.transform.localScale = new Vector3(8.8f, 0.2f, 1.8f);
        walkBottom.transform.localPosition = new Vector3(0.9f, -0.1f, -2.65f);
        walkBottom.GetComponent<Renderer>().sharedMaterial = pavementMat;

        // Top Walkway: chạy ngang ở đỉnh
        GameObject walkTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkTop.name = "Walkway_Top";
        walkTop.transform.parent = mapRoot.transform;
        walkTop.transform.localScale = new Vector3(8.8f, 0.2f, 1.8f);
        walkTop.transform.localPosition = new Vector3(0.9f, -0.1f, 2.65f);
        walkTop.GetComponent<Renderer>().sharedMaterial = pavementMat;

        // Right Walkway: chạy dọc sườn phải kẹp giữa top & bottom walkways
        GameObject walkRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkRight.name = "Walkway_Right";
        walkRight.transform.parent = mapRoot.transform;
        walkRight.transform.localScale = new Vector3(1.8f, 0.2f, 3.5f);
        walkRight.transform.localPosition = new Vector3(4.4f, -0.1f, 0f);
        walkRight.GetComponent<Renderer>().sharedMaterial = pavementMat;


        // --- 2. DỰNG KHỐI TRUNG TÂM (CENTRAL BLOCK) ---
        GameObject centralBlock = new GameObject("Central_Block");
        centralBlock.transform.parent = mapRoot.transform;
        centralBlock.transform.localPosition = Vector3.zero;
        centralBlock.transform.localRotation = Quaternion.identity;
        centralBlock.transform.localScale = Vector3.one;

        // HOUSE B: Nhà Nhân Vật Chính
        // Rộng 2.2m, Sâu 3.5m, Chiều cao trần trệt cố định 2.1m. Sơn màu vàng nghệ
        GameObject houseB = new GameObject("MainHouse_B");
        houseB.transform.parent = centralBlock.transform;
        houseB.transform.localPosition = new Vector3(0.8f, 0f, 0f); // Center Z = 0f, Side-by-side layout
        houseB.transform.localRotation = Quaternion.identity;
        houseB.transform.localScale = Vector3.one;

        float widthB = 2.2f;
        float depthB = 3.5f;
        float floorThickness = 0.2f;
        float firstFloorHeight = 2.1f; 
        float secondFloorHeight = 2.7f;
        float thirdFloorHeight = 2.7f;
        float ceilingThickness = 0.15f;

        // Ground Floor Base
        GameObject floorBaseB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorBaseB.name = "FloorBase";
        floorBaseB.transform.parent = houseB.transform;
        floorBaseB.transform.localScale = new Vector3(widthB - 0.1f, floorThickness, depthB - 0.2f);
        floorBaseB.transform.localPosition = new Vector3(0, -floorThickness / 2.0f, 0);
        floorBaseB.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Left wall
        GameObject leftWallB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWallB.name = "LeftWall";
        leftWallB.transform.parent = houseB.transform;
        leftWallB.transform.localScale = new Vector3(0.15f, firstFloorHeight + floorThickness, depthB);
        leftWallB.transform.localPosition = new Vector3(-widthB / 2.0f + 0.075f, (firstFloorHeight - floorThickness) / 2.0f, 0);
        leftWallB.GetComponent<Renderer>().sharedMaterial = sideWallMat;
        removeCollider(leftWallB);

        // Wallpaper left
        GameObject leftWallpaperB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWallpaperB.name = "LeftWallpaper";
        leftWallpaperB.transform.parent = houseB.transform;
        leftWallpaperB.transform.localScale = new Vector3(0.01f, firstFloorHeight, depthB - 0.2f);
        leftWallpaperB.transform.localPosition = new Vector3(-widthB / 2.0f + 0.155f, firstFloorHeight / 2.0f, 0);
        leftWallpaperB.GetComponent<Renderer>().sharedMaterial = wallpaperMat;
        removeCollider(leftWallpaperB);

        // Right wall
        GameObject rightWallB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallB.name = "RightWall";
        rightWallB.transform.parent = houseB.transform;
        rightWallB.transform.localScale = new Vector3(0.15f, firstFloorHeight + floorThickness, depthB);
        rightWallB.transform.localPosition = new Vector3(widthB / 2.0f - 0.075f, (firstFloorHeight - floorThickness) / 2.0f, 0);
        rightWallB.GetComponent<Renderer>().sharedMaterial = sideWallMat;
        removeCollider(rightWallB);

        // Wallpaper right
        GameObject rightWallpaperB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWallpaperB.name = "RightWallpaper";
        rightWallpaperB.transform.parent = houseB.transform;
        rightWallpaperB.transform.localScale = new Vector3(0.01f, firstFloorHeight, depthB - 0.2f);
        rightWallpaperB.transform.localPosition = new Vector3(widthB / 2.0f - 0.155f, firstFloorHeight / 2.0f, 0);
        rightWallpaperB.GetComponent<Renderer>().sharedMaterial = wallpaperMat;
        removeCollider(rightWallpaperB);

        // Back wall
        GameObject backWallB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWallB.name = "BackWall";
        backWallB.transform.parent = houseB.transform;
        backWallB.transform.localScale = new Vector3(widthB, firstFloorHeight + floorThickness, 0.15f);
        backWallB.transform.localPosition = new Vector3(0, (firstFloorHeight - floorThickness) / 2.0f, depthB / 2.0f - 0.075f);
        backWallB.GetComponent<Renderer>().sharedMaterial = mainWallMat;
        removeCollider(backWallB);

        // Wallpaper back
        GameObject backWallpaperB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWallpaperB.name = "BackWallpaper";
        backWallpaperB.transform.parent = houseB.transform;
        backWallpaperB.transform.localScale = new Vector3(widthB - 0.3f, firstFloorHeight, 0.01f);
        backWallpaperB.transform.localPosition = new Vector3(0, firstFloorHeight / 2.0f, depthB / 2.0f - 0.155f);
        backWallpaperB.GetComponent<Renderer>().sharedMaterial = wallpaperMat;
        removeCollider(backWallpaperB);

        // Ceiling 1
        GameObject ceiling1B = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling1B.name = "Ceiling1";
        ceiling1B.transform.parent = houseB.transform;
        ceiling1B.transform.localScale = new Vector3(widthB, ceilingThickness, depthB);
        ceiling1B.transform.localPosition = new Vector3(0, firstFloorHeight + ceilingThickness / 2.0f, 0);
        ceiling1B.GetComponent<Renderer>().sharedMaterial = mainWallMat;
        removeCollider(ceiling1B);

        // Front Header
        float headerHeight = 0.4f;
        GameObject frontHeaderB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontHeaderB.name = "FrontHeader";
        frontHeaderB.transform.parent = houseB.transform;
        frontHeaderB.transform.localScale = new Vector3(widthB - 0.3f, headerHeight, 0.25f);
        frontHeaderB.transform.localPosition = new Vector3(0, firstFloorHeight - (headerHeight / 2.0f), -depthB / 2.0f + 0.125f);
        frontHeaderB.GetComponent<Renderer>().sharedMaterial = mainWallMat;
        removeCollider(frontHeaderB);

        // Rolled up Shutter Door
        GameObject shutterRollB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shutterRollB.name = "RolledShutterDoor";
        shutterRollB.transform.parent = houseB.transform;
        shutterRollB.transform.localScale = new Vector3(0.18f, (widthB - 0.35f) / 2.0f, 0.18f);
        shutterRollB.transform.localRotation = Quaternion.Euler(0, 0, 90);
        shutterRollB.transform.localPosition = new Vector3(0, firstFloorHeight - headerHeight + 0.06f, -depthB / 2.0f + 0.15f);
        shutterRollB.GetComponent<Renderer>().sharedMaterial = greenShutterMat;
        removeCollider(shutterRollB);

        // Air Conditioner
        GameObject acUnitB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        acUnitB.name = "AirConditioner";
        acUnitB.transform.parent = houseB.transform;
        acUnitB.transform.localScale = new Vector3(0.5f, 0.28f, 0.2f);
        acUnitB.transform.localPosition = new Vector3(-widthB / 4.0f, firstFloorHeight - (headerHeight / 2.0f), -depthB / 2.0f - 0.04f);
        acUnitB.GetComponent<Renderer>().sharedMaterial = acMat;
        removeCollider(acUnitB);

        GameObject acFanB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        acFanB.name = "ACFan";
        acFanB.transform.parent = acUnitB.transform;
        acFanB.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
        acFanB.transform.localRotation = Quaternion.Euler(90, 0, 0);
        acFanB.transform.localPosition = new Vector3(0.12f, 0, -0.51f);
        acFanB.GetComponent<Renderer>().sharedMaterial = acGrillMat;
        removeCollider(acFanB);

        // Entry Transition
        GameObject entranceStepB = GameObject.CreatePrimitive(PrimitiveType.Cube);
        entranceStepB.name = "EntranceStep";
        entranceStepB.transform.parent = houseB.transform;
        entranceStepB.transform.localScale = new Vector3(widthB - 0.3f, 0.02f, 0.3f);
        entranceStepB.transform.localPosition = new Vector3(0f, -0.01f, -depthB / 2.0f - 0.15f);
        entranceStepB.GetComponent<Renderer>().sharedMaterial = stepMat;
        removeCollider(entranceStepB);

        // Plaster patches on House B front wall
        GameObject patchB1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        patchB1.name = "PeeledPlaster_B1";
        patchB1.transform.parent = houseB.transform;
        patchB1.transform.localScale = new Vector3(0.4f, 0.5f, 0.01f);
        patchB1.transform.localPosition = new Vector3(widthB * 0.2f, firstFloorHeight * 0.6f, -depthB / 2.0f + 0.115f);
        patchB1.GetComponent<Renderer>().sharedMaterial = sideWallMat;
        removeCollider(patchB1);

        // Build upper floors for House B
        System.Action<int, float, float> BuildUpperFloor = (floorIndex, baseHeight, floorHeight) =>
        {
            string fName = "Floor_" + floorIndex;
            GameObject floorGroup = new GameObject(fName);
            floorGroup.transform.parent = houseB.transform;
            floorGroup.transform.localPosition = Vector3.zero;

            // Left wall
            GameObject fLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fLeft.name = fName + "_LeftWall";
            fLeft.transform.parent = floorGroup.transform;
            fLeft.transform.localScale = new Vector3(0.15f, floorHeight, depthB);
            fLeft.transform.localPosition = new Vector3(-widthB / 2.0f + 0.075f, baseHeight + (floorHeight / 2.0f), 0);
            fLeft.GetComponent<Renderer>().sharedMaterial = sideWallMat;
            removeCollider(fLeft);

            // Right wall
            GameObject fRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fRight.name = fName + "_RightWall";
            fRight.transform.parent = floorGroup.transform;
            fRight.transform.localScale = new Vector3(0.15f, floorHeight, depthB);
            fRight.transform.localPosition = new Vector3(widthB / 2.0f - 0.075f, baseHeight + (floorHeight / 2.0f), 0);
            fRight.GetComponent<Renderer>().sharedMaterial = sideWallMat;
            removeCollider(fRight);

            // Back wall
            GameObject fBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fBack.name = fName + "_BackWall";
            fBack.transform.parent = floorGroup.transform;
            fBack.transform.localScale = new Vector3(widthB, floorHeight, 0.15f);
            fBack.transform.localPosition = new Vector3(0, baseHeight + (floorHeight / 2.0f), depthB / 2.0f - 0.075f);
            fBack.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fBack);

            // Ceiling/Floor
            GameObject fCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fCeiling.name = fName + "_Ceiling";
            fCeiling.transform.parent = floorGroup.transform;
            fCeiling.transform.localScale = new Vector3(widthB, ceilingThickness, depthB);
            fCeiling.transform.localPosition = new Vector3(0, baseHeight + floorHeight + ceilingThickness / 2.0f, 0);
            fCeiling.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fCeiling);

            // Front facade wall with window opening
            float wallBottomH = 0.5f;
            GameObject fFrontBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fFrontBottom.name = fName + "_FrontBottom";
            fFrontBottom.transform.parent = floorGroup.transform;
            fFrontBottom.transform.localScale = new Vector3(widthB - 0.3f, wallBottomH, 0.15f);
            fFrontBottom.transform.localPosition = new Vector3(0, baseHeight + (wallBottomH / 2.0f), -depthB / 2.0f + 0.075f);
            fFrontBottom.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fFrontBottom);

            // Concrete ledge
            GameObject fLedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fLedge.name = fName + "_Ledge";
            fLedge.transform.parent = floorGroup.transform;
            fLedge.transform.localScale = new Vector3(widthB - 0.1f, 0.08f, 0.25f);
            fLedge.transform.localPosition = new Vector3(0, baseHeight + wallBottomH + 0.04f, -depthB / 2.0f + 0.05f);
            fLedge.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fLedge);

            // Top solid part
            float wallTopH = 0.35f;
            GameObject fFrontTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fFrontTop.name = fName + "_FrontTop";
            fFrontTop.transform.parent = floorGroup.transform;
            fFrontTop.transform.localScale = new Vector3(widthB - 0.3f, wallTopH, 0.15f);
            fFrontTop.transform.localPosition = new Vector3(0, baseHeight + floorHeight - (wallTopH / 2.0f), -depthB / 2.0f + 0.075f);
            fFrontTop.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fFrontTop);

            // Left solid part
            float windowH = floorHeight - wallBottomH - wallTopH;
            float sideWallW = 0.15f;
            GameObject fFrontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fFrontLeft.name = fName + "_FrontLeft";
            fFrontLeft.transform.parent = floorGroup.transform;
            fFrontLeft.transform.localScale = new Vector3(sideWallW, windowH, 0.15f);
            fFrontLeft.transform.localPosition = new Vector3(-widthB / 2.0f + 0.15f + (sideWallW / 2.0f), baseHeight + wallBottomH + (windowH / 2.0f), -depthB / 2.0f + 0.075f);
            fFrontLeft.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fFrontLeft);

            // Right solid part
            GameObject fFrontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fFrontRight.name = fName + "_FrontRight";
            fFrontRight.transform.parent = floorGroup.transform;
            fFrontRight.transform.localScale = new Vector3(sideWallW, windowH, 0.15f);
            fFrontRight.transform.localPosition = new Vector3(widthB / 2.0f - 0.15f - (sideWallW / 2.0f), baseHeight + wallBottomH + (windowH / 2.0f), -depthB / 2.0f + 0.075f);
            fFrontRight.GetComponent<Renderer>().sharedMaterial = mainWallMat;
            removeCollider(fFrontRight);

            // WINDOW
            float windowW = widthB - 0.3f - (2.0f * sideWallW);
            GameObject windowObj = new GameObject(fName + "_Window");
            windowObj.transform.parent = floorGroup.transform;
            windowObj.transform.localPosition = new Vector3(0, baseHeight + wallBottomH + (windowH / 2.0f), -depthB / 2.0f + 0.11f);

            float frameThick = 0.04f;
            GameObject wFrameBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wFrameBottom.transform.parent = windowObj.transform;
            wFrameBottom.transform.localScale = new Vector3(windowW, frameThick, 0.06f);
            wFrameBottom.transform.localPosition = new Vector3(0, -windowH / 2.0f + (frameThick / 2.0f), 0);
            wFrameBottom.GetComponent<Renderer>().sharedMaterial = woodMat;
            removeCollider(wFrameBottom);

            GameObject wFrameTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wFrameTop.transform.parent = windowObj.transform;
            wFrameTop.transform.localScale = new Vector3(windowW, frameThick, 0.06f);
            wFrameTop.transform.localPosition = new Vector3(0, windowH / 2.0f - (frameThick / 2.0f), 0);
            wFrameTop.GetComponent<Renderer>().sharedMaterial = woodMat;
            removeCollider(wFrameTop);

            GameObject wFrameLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wFrameLeft.transform.parent = windowObj.transform;
            wFrameLeft.transform.localScale = new Vector3(frameThick, windowH, 0.06f);
            wFrameLeft.transform.localPosition = new Vector3(-windowW / 2.0f + (frameThick / 2.0f), 0, 0);
            wFrameLeft.GetComponent<Renderer>().sharedMaterial = woodMat;
            removeCollider(wFrameLeft);

            GameObject wFrameRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wFrameRight.transform.parent = windowObj.transform;
            wFrameRight.transform.localScale = new Vector3(frameThick, windowH, 0.06f);
            wFrameRight.transform.localPosition = new Vector3(windowW / 2.0f - (frameThick / 2.0f), 0, 0);
            wFrameRight.GetComponent<Renderer>().sharedMaterial = woodMat;
            removeCollider(wFrameRight);

            GameObject wGlass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wGlass.name = "Glass";
            wGlass.transform.parent = windowObj.transform;
            wGlass.transform.localScale = new Vector3(windowW - (2.0f * frameThick), windowH - (2.0f * frameThick), 0.02f);
            wGlass.transform.localPosition = Vector3.zero;
            wGlass.GetComponent<Renderer>().sharedMaterial = glassMat;
            removeCollider(wGlass);

            GameObject wGridVMid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wGridVMid.transform.parent = windowObj.transform;
            wGridVMid.transform.localScale = new Vector3(0.02f, windowH - (2.0f * frameThick), 0.02f);
            wGridVMid.transform.localPosition = new Vector3(0, 0, 0.005f);
            wGridVMid.GetComponent<Renderer>().sharedMaterial = whiteFrameMat;
            removeCollider(wGridVMid);

            float innerH = windowH - (2.0f * frameThick);
            GameObject wGridH1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wGridH1.transform.parent = windowObj.transform;
            wGridH1.transform.localScale = new Vector3(windowW - (2.0f * frameThick), 0.02f, 0.02f);
            wGridH1.transform.localPosition = new Vector3(0, -innerH / 6.0f, 0.005f);
            wGridH1.GetComponent<Renderer>().sharedMaterial = whiteFrameMat;
            removeCollider(wGridH1);

            GameObject wGridH2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wGridH2.transform.parent = windowObj.transform;
            wGridH2.transform.localScale = new Vector3(windowW - (2.0f * frameThick), 0.02f, 0.02f);
            wGridH2.transform.localPosition = new Vector3(0, innerH / 6.0f, 0.005f);
            wGridH2.GetComponent<Renderer>().sharedMaterial = whiteFrameMat;
            removeCollider(wGridH2);
        };

        float floor1BaseHeight = firstFloorHeight + ceilingThickness;
        BuildUpperFloor(1, floor1BaseHeight, secondFloorHeight);

        // Roof overhang
        float totalHouseHeight = floor1BaseHeight + secondFloorHeight + ceilingThickness;
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "RoofAwning";
        roof.transform.parent = houseB.transform;
        roof.transform.localScale = new Vector3(widthB + 0.15f, 0.05f, 0.7f);
        roof.transform.localRotation = Quaternion.Euler(15, 0, 0);
        roof.transform.localPosition = new Vector3(0, totalHouseHeight + 0.025f, -depthB / 2.0f - 0.12f);
        roof.GetComponent<Renderer>().sharedMaterial = sideWallMat;
        removeCollider(roof);


        // PROCEDURAL SURROUNDING HOUSES (LEVEL 4 - 1 STORY TALL, 1.8M - 2.0M HEIGHT)
        // House A (Left of House B)
        Material matA = new Material(urpLitShader);
        matA.color = oldCream;
        matA.name = "HouseA_Mat";
        Material roofMatA = new Material(urpLitShader);
        roofMatA.color = new Color(0.22f, 0.35f, 0.22f); // mossy green
        BuildSurroundingHouse(
            "HouseA",
            centralBlock,
            new Vector3(-2.7f, 0f, 0f),
            Quaternion.identity,
            1.6f,
            3.5f,
            1,
            new float[] { 1.8f },
            matA,
            sideWallMat,
            floorMat,
            woodMat,
            glassMat,
            whiteFrameMat,
            roofMatA,
            leafMat,
            removeCollider
        );

        // House C (Right of House B)
        Material matC = new Material(urpLitShader);
        matC.color = weatheredGreen;
        matC.name = "HouseC_Mat";
        Material roofMatC = new Material(urpLitShader);
        roofMatC.color = new Color(0.6f, 0.35f, 0.22f); // rusty orange
        BuildSurroundingHouse(
            "HouseC",
            centralBlock,
            new Vector3(-1.1f, 0f, 0f),
            Quaternion.identity,
            1.6f,
            3.5f,
            1,
            new float[] { 1.8f },
            matC,
            sideWallMat,
            floorMat,
            woodMat,
            glassMat,
            whiteFrameMat,
            roofMatC,
            leafMat,
            removeCollider
        );

        // House D (Right of House B)
        Material matD = new Material(urpLitShader);
        matD.color = mossyGrey;
        matD.name = "HouseD_Mat";
        Material roofMatD = new Material(urpLitShader);
        roofMatD.color = cementGrey;
        BuildSurroundingHouse(
            "HouseD",
            centralBlock,
            new Vector3(2.7f, 0f, 0f),
            Quaternion.identity,
            1.6f,
            3.5f,
            1,
            new float[] { 2.0f },
            matD,
            sideWallMat,
            floorMat,
            woodMat,
            glassMat,
            whiteFrameMat,
            roofMatD,
            leafMat,
            removeCollider
        );


        // --- 3. DỰNG TƯỜNG RÀO VÀ BIỆT THỰ VILLA BAO BỌC (THEO SƠ ĐỒ HÌNH 2) ---
        // Biệt thự Villa: Đáy bản đồ (Z = -4.3f), làm ranh giới mặt tiền
        GameObject villa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        villa.name = "Villa_Boundary";
        villa.transform.parent = mapRoot.transform;
        villa.transform.localScale = new Vector3(10.0f, 3.0f, 1.5f);
        villa.transform.localPosition = new Vector3(0.9f, 1.4f, -4.3f); // sits on ground base
        villa.GetComponent<Renderer>().sharedMaterial = villaMat;
        // Keep BoxCollider to block player

        // Left Boundary Wall (Continuous Left Wall)
        GameObject wallLeftBound = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallLeftBound.name = "BoundaryWall_Left";
        wallLeftBound.transform.parent = mapRoot.transform;
        wallLeftBound.transform.localScale = new Vector3(0.3f, 1.2f, 12.0f);
        wallLeftBound.transform.localPosition = new Vector3(-5.45f, 0.5f, 0f);
        wallLeftBound.GetComponent<Renderer>().sharedMaterial = sideWallMat;

        // Second Left Boundary Wall segment (Above the houses at X = -3.65f)
        GameObject wallLeftBoundSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallLeftBoundSeg.name = "BoundaryWall_LeftSeg";
        wallLeftBoundSeg.transform.parent = mapRoot.transform;
        wallLeftBoundSeg.transform.localScale = new Vector3(0.3f, 1.2f, 4.25f);
        wallLeftBoundSeg.transform.localPosition = new Vector3(-3.65f, 0.5f, 3.875f);
        wallLeftBoundSeg.GetComponent<Renderer>().sharedMaterial = sideWallMat;

        // Top Boundary Wall (Back - Lowered to 1.2m)
        GameObject wallTopBound = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallTopBound.name = "BoundaryWall_Top";
        wallTopBound.transform.parent = mapRoot.transform;
        wallTopBound.transform.localScale = new Vector3(8.8f, 1.2f, 0.3f);
        wallTopBound.transform.localPosition = new Vector3(0.9f, 0.5f, 3.7f);
        wallTopBound.GetComponent<Renderer>().sharedMaterial = sideWallMat;

        // Right Boundary Wall (Lowered to 1.2m)
        GameObject wallRightBound = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallRightBound.name = "BoundaryWall_Right";
        wallRightBound.transform.parent = mapRoot.transform;
        wallRightBound.transform.localScale = new Vector3(0.3f, 1.2f, 7.1f);
        wallRightBound.transform.localPosition = new Vector3(5.45f, 0.5f, 0f);
        wallRightBound.GetComponent<Renderer>().sharedMaterial = sideWallMat;


        // --- 4. HÀNG CÂY XANH PHÍA SAU TƯỜNG RÀO ---
        // Left side trees (behind continuous Left Wall at X = -5.9f)
        float[] treeLeftZ = new float[] { -5.0f, -3.5f, -2.0f, -0.5f, 1.0f, 2.5f, 4.0f, 5.5f };
        for (int i = 0; i < treeLeftZ.Length; i++)
        {
            CreateProceduralTree(mapRoot, new Vector3(-5.9f, 0f, treeLeftZ[i]), leafMat, woodMat, removeCollider);
        }

        // Right side trees (behind Right Wall at X = 5.9f)
        float[] treeRightZ = new float[] { -3.0f, -1.5f, 0f, 1.5f, 3.0f };
        for (int i = 0; i < treeRightZ.Length; i++)
        {
            CreateProceduralTree(mapRoot, new Vector3(5.9f, 0f, treeRightZ[i]), leafMat, woodMat, removeCollider);
        }

        // Top/Back trees (behind Top Wall at Z = 4.2f)
        float[] treeTopX = new float[] { -3.0f, -1.5f, 0f, 1.5f, 3.0f, 4.5f };
        for (int i = 0; i < treeTopX.Length; i++)
        {
            CreateProceduralTree(mapRoot, new Vector3(treeTopX[i], 0f, 4.2f), leafMat, woodMat, removeCollider);
        }


        // --- 5. STREET DETAILS (NO POWER WIRES) ---
        // Potted plants next to the central block and outer walls
        CreatePottedPlant(mapRoot, new Vector3(-3.3f, 0f, -1.6f), potMat, leafMat, removeCollider);
        CreatePottedPlant(mapRoot, new Vector3(3.3f, 0f, -1.6f), potMat, leafMat, removeCollider);
        CreatePottedPlant(mapRoot, new Vector3(-3.3f, 0f, 1.6f), potMat, leafMat, removeCollider);
        CreatePottedPlant(mapRoot, new Vector3(3.3f, 0f, 1.6f), potMat, leafMat, removeCollider);


        // --- 6. POSITION CHARACTERS & WAYPOINTS ---
        // Player position: Grounded Y = 0.05f on Bottom Walkway, facing House B
        var player = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");
        if (player != null) {
            player.transform.SetParent(null);
            player.transform.rotation = Quaternion.Euler(0, 0f, 0); // Facing House B (+Z)
            player.transform.position = new Vector3(0.8f, 0.05f, -2.65f); // Center X = 0.8f, Z = -2.65f

            var prb = player.GetComponent<Rigidbody>();
            if (prb != null) {
                UnityEngine.Object.DestroyImmediate(prb);
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc == null) {
                cc = player.AddComponent<CharacterController>();
            }
            cc.center = new Vector3(0, 0.85f, 0);
            cc.height = 1.7f;
            cc.radius = 0.22f;
            cc.stepOffset = 0.30f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0.001f;
        }

        // Cart (Xe Bo Bia) position: Grounded Y = 0.05f near the player
        var cart = GameObject.Find("Xe_Bo_Bia_Root");
        if (cart != null) {
            cart.transform.SetParent(null);
            cart.transform.rotation = Quaternion.Euler(0, 90f, 0);
            cart.transform.position = new Vector3(1.2f, 0.05f, -2.55f);
            var rb = cart.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = true;
            }
        }

        // Customer Waypoints: Grounded at Y = 0.0f
        var customerPos = GameObject.Find("Vi_Tri_Khach_Dung");
        if (customerPos != null) {
            customerPos.transform.position = new Vector3(1.2f, 0.0f, -1.8f);
            customerPos.transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        var approachPos = GameObject.Find("Diem_Tiep_Can_Khach");
        if (approachPos != null) {
            approachPos.transform.position = new Vector3(1.2f, 0.0f, -1.9f);
            approachPos.transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        var spawnPos = GameObject.Find("Diem_Spawn_Khach");
        if (spawnPos != null) {
            spawnPos.transform.position = new Vector3(1.2f, 0.0f, -3.0f);
            spawnPos.transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        // Save active scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        // --- 7. SAVE AS PREFAB ---
        string prefabPath = "Assets/Vietnamese_Neighborhood.prefab";
        PrefabUtility.SaveAsPrefabAsset(mapRoot, prefabPath);
        Debug.Log("Successfully generated and saved Neighborhood Prefab: " + prefabPath);

        Undo.RegisterCreatedObjectUndo(mapRoot, "Build Vietnamese Neighborhood");
        Selection.activeGameObject = mapRoot;

        Debug.Log("Successfully built Vietnamese Neighborhood in the scene!");
    }

    private static void BuildSurroundingHouse(
        string name, 
        GameObject parent, 
        Vector3 position, 
        Quaternion rotation,
        float width, 
        float depth, 
        int floors, 
        float[] floorHeights, 
        Material wallMat, 
        Material sideWallMat,
        Material floorMat,
        Material woodMat,
        Material glassMat,
        Material whiteFrameMat,
        Material roofMat,
        Material leafMat,
        System.Action<GameObject> removeCollider)
    {
        GameObject houseObj = new GameObject(name);
        houseObj.transform.parent = parent.transform;
        houseObj.transform.localPosition = position;
        houseObj.transform.localRotation = rotation;
        houseObj.transform.localScale = Vector3.one;

        float floorThickness = 0.2f;
        float ceilingThickness = 0.15f;

        // Ground Floor Base
        GameObject floorBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorBase.name = "FloorBase";
        floorBase.transform.parent = houseObj.transform;
        floorBase.transform.localScale = new Vector3(width - 0.05f, floorThickness, depth - 0.05f);
        floorBase.transform.localPosition = new Vector3(0, -floorThickness / 2.0f, 0);
        floorBase.GetComponent<Renderer>().sharedMaterial = floorMat;

        BoxCollider floorCollider = floorBase.GetComponent<BoxCollider>();
        if (floorCollider != null)
        {
            floorCollider.center = Vector3.zero;
            floorCollider.size = Vector3.one;
        }

        float currentY = 0f;
        for (int i = 0; i < floors; i++)
        {
            float floorHeight = floorHeights[Mathf.Min(i, floorHeights.Length - 1)];
            string fName = "Floor_" + i;
            GameObject floorGroup = new GameObject(fName);
            floorGroup.transform.parent = houseObj.transform;
            floorGroup.transform.localPosition = Vector3.zero;

            // Left Wall
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = fName + "_LeftWall";
            leftWall.transform.parent = floorGroup.transform;
            leftWall.transform.localScale = new Vector3(0.12f, floorHeight + (i == 0 ? floorThickness : 0), depth);
            leftWall.transform.localPosition = new Vector3(-width / 2.0f + 0.06f, currentY + floorHeight / 2.0f - (i == 0 ? floorThickness / 2.0f : 0), 0);
            leftWall.GetComponent<Renderer>().sharedMaterial = sideWallMat;
            removeCollider(leftWall);

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = fName + "_RightWall";
            rightWall.transform.parent = floorGroup.transform;
            rightWall.transform.localScale = new Vector3(0.12f, floorHeight + (i == 0 ? floorThickness : 0), depth);
            rightWall.transform.localPosition = new Vector3(width / 2.0f - 0.06f, currentY + floorHeight / 2.0f - (i == 0 ? floorThickness / 2.0f : 0), 0);
            rightWall.GetComponent<Renderer>().sharedMaterial = sideWallMat;
            removeCollider(rightWall);

            // Back Wall
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = fName + "_BackWall";
            backWall.transform.parent = floorGroup.transform;
            backWall.transform.localScale = new Vector3(width, floorHeight + (i == 0 ? floorThickness : 0), 0.12f);
            backWall.transform.localPosition = new Vector3(0, currentY + floorHeight / 2.0f - (i == 0 ? floorThickness / 2.0f : 0), depth / 2.0f - 0.06f);
            backWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            removeCollider(backWall);

            // Front Wall
            GameObject frontWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontWall.name = fName + "_FrontWall";
            frontWall.transform.parent = floorGroup.transform;
            frontWall.transform.localScale = new Vector3(width, floorHeight + (i == 0 ? floorThickness : 0), 0.12f);
            frontWall.transform.localPosition = new Vector3(0, currentY + floorHeight / 2.0f - (i == 0 ? floorThickness / 2.0f : 0), -depth / 2.0f + 0.06f);
            frontWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            removeCollider(frontWall);

            // Ceiling
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = fName + "_Ceiling";
            ceiling.transform.parent = floorGroup.transform;
            ceiling.transform.localScale = new Vector3(width, ceilingThickness, depth);
            ceiling.transform.localPosition = new Vector3(0, currentY + floorHeight + ceilingThickness / 2.0f, 0);
            ceiling.GetComponent<Renderer>().sharedMaterial = wallMat;
            removeCollider(ceiling);

            // Window
            float winW = width * 0.4f;
            float winH = floorHeight * 0.4f;
            GameObject winObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            winObj.name = fName + "_Window";
            winObj.transform.parent = floorGroup.transform;
            winObj.transform.localScale = new Vector3(winW, winH, 0.15f);
            winObj.transform.localPosition = new Vector3(0, currentY + floorHeight / 2.0f, -depth / 2.0f + 0.07f);
            winObj.GetComponent<Renderer>().sharedMaterial = glassMat;
            removeCollider(winObj);

            // Peeled Plaster
            if (wallMat.color != cementGrey && Random.value > 0.3f)
            {
                GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                patch.name = fName + "_PeeledPlaster";
                patch.transform.parent = floorGroup.transform;
                patch.transform.localScale = new Vector3(width * 0.3f, floorHeight * 0.3f, 0.01f);
                patch.transform.localPosition = new Vector3(Random.Range(-width * 0.2f, width * 0.2f), currentY + floorHeight * 0.5f, -depth / 2.0f - 0.005f);
                patch.GetComponent<Renderer>().sharedMaterial = sideWallMat;
                removeCollider(patch);
            }

            currentY += floorHeight + ceilingThickness;
        }

        // Simple Roof
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.parent = houseObj.transform;
        roof.transform.localScale = new Vector3(width + 0.1f, 0.05f, depth + 0.1f);
        roof.transform.localPosition = new Vector3(0, currentY + 0.025f, 0);
        roof.GetComponent<Renderer>().sharedMaterial = roofMat;
        removeCollider(roof);
    }

    private static void CreateProceduralTree(GameObject parent, Vector3 pos, Material leafMat, Material woodMat, System.Action<GameObject> removeCollider)
    {
        GameObject tree = new GameObject("ProceduralTree");
        tree.transform.parent = parent.transform;
        tree.transform.localPosition = pos;
        tree.transform.localScale = Vector3.one;

        // Trunk (Taller and thicker)
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.parent = tree.transform;
        trunk.transform.localScale = new Vector3(0.2f, 2.0f, 0.2f);
        trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f); // Grounded Y = -0.1f
        trunk.GetComponent<Renderer>().sharedMaterial = woodMat;
        removeCollider(trunk);

        // Foliage spheres (Enlarged)
        float[] yOffs = new float[] { 1.8f, 2.4f, 3.0f };
        float[] scales = new float[] { 2.2f, 1.8f, 1.4f };
        for (int i = 0; i < yOffs.Length; i++)
        {
            GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage_" + i;
            foliage.transform.parent = tree.transform;
            foliage.transform.localScale = new Vector3(scales[i], scales[i] * 0.9f, scales[i]);
            foliage.transform.localPosition = new Vector3(Random.Range(-0.05f, 0.05f), yOffs[i], Random.Range(-0.05f, 0.05f));
            foliage.GetComponent<Renderer>().sharedMaterial = leafMat;
            removeCollider(foliage);
        }
    }

    private static void CreatePottedPlant(GameObject parent, Vector3 pos, Material potMat, Material leafMat, System.Action<GameObject> removeCollider)
    {
        GameObject potGroup = new GameObject("PottedPlant");
        potGroup.transform.parent = parent.transform;
        potGroup.transform.localPosition = pos;
        potGroup.transform.localScale = Vector3.one;

        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.parent = potGroup.transform;
        pot.transform.localScale = new Vector3(0.18f, 0.1f, 0.18f);
        pot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pot.GetComponent<Renderer>().sharedMaterial = potMat;
        removeCollider(pot);

        int leavesCount = Random.Range(2, 4);
        for (int i = 0; i < leavesCount; i++)
        {
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaf.name = "Leaf_" + i;
            leaf.transform.parent = potGroup.transform;
            float leafScale = Random.Range(0.15f, 0.25f);
            leaf.transform.localScale = new Vector3(leafScale, leafScale * 0.8f, leafScale);
            leaf.transform.localPosition = new Vector3(
                Random.Range(-0.06f, 0.06f),
                0.12f + Random.Range(0f, 0.06f),
                Random.Range(-0.06f, 0.06f)
            );
            leaf.GetComponent<Renderer>().sharedMaterial = leafMat;
            removeCollider(leaf);
        }
    }

    private static void CreatePowerWire(GameObject parent, Vector3 p1, Vector3 p2, Material wireMat, System.Action<GameObject> removeCollider)
    {
        GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wire.name = "PowerWire";
        wire.transform.parent = parent.transform;
        
        Vector3 direction = p2 - p1;
        float distance = direction.magnitude;
        wire.transform.position = p1 + direction / 2.0f;
        wire.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        wire.transform.localScale = new Vector3(0.015f, distance / 2.0f, 0.015f);
        
        wire.GetComponent<Renderer>().sharedMaterial = wireMat;
        removeCollider(wire);
    }
}
