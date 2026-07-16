using UnityEngine;
using UnityEditor;

public class ArrangeHousesArea01
{
    [MenuItem("AntiBoBia/Arrange Houses on Ground_Map01")]
    public static void ArrangeHouses()
    {
        // ── 1. Find references ────────────────────────────────────────────────
        GameObject ground   = GameObject.Find("Ground_Map01");
        GameObject character = GameObject.Find("Meshy_AI_Arms_Outstretched_biped_Character_output");

        if (ground == null)   { Debug.LogError("[Arrange] Ground_Map01 not found!"); return; }
        if (character == null){ Debug.LogError("[Arrange] Character not found!");    return; }

        // ── 2. Measure character height (world-space bounds) ──────────────────
        Renderer[] charRenderers = character.GetComponentsInChildren<Renderer>();
        Bounds charBounds = new Bounds(character.transform.position, Vector3.zero);
        foreach (var r in charRenderers) charBounds.Encapsulate(r.bounds);
        float charHeight = charBounds.size.y;   // ≈ 1.8 Unity units typical
        if (charHeight < 0.1f) charHeight = 1.8f;
        Debug.Log($"[Arrange] Character height = {charHeight:F3}");

        // ── 3. Ground_Map01 reference ─────────────────────────────────────────
        float groundY = ground.transform.position.y;
        Vector3 groundPos = ground.transform.position;

        // ── 4. House configs: name, targetHeightMultiplier, layout position offset, rotY ──
        //   Houses are 2-storey ~3x charHeight tall; big Area01 = 4x
        //   Layout: 2 rows × 3 cols with a road lane through middle
        //
        //   Top row  (Z = +spacing):  House_Area01  |  House_Area_01_Fixed  |  House_02
        //   Bottom row (Z = -spacing): House_03      |  House_Store
        //
        //   spacing between houses: ~4 * charHeight for streets

        float streetW   = charHeight * 5f;   // gap between columns (street width)
        float streetD   = charHeight * 5f;   // gap between rows
        float houseSpX  = charHeight * 4f;   // house plot width
        float houseSpZ  = charHeight * 4.5f; // house plot depth

        // Target scale for each house: desired world height
        // Two-storey houses: ~3 × charHeight; big area01 model: ~4 × charHeight
        var configs = new (string name, float targetH, float offsetX, float offsetZ, float rotY)[]
        {
            // Row 1 (front, facing player at Z-)
            ("House_Area01",        charHeight * 4.5f,  -houseSpX * 1.2f,  streetD * 0.6f,  180f),
            ("House_Area_01_Fixed", charHeight * 2.8f,   0f,               streetD * 0.6f,  180f),
            ("House_02",            charHeight * 2.8f,   houseSpX * 1.2f,  streetD * 0.6f,  180f),

            // Row 2 (back)
            ("House_03",            charHeight * 2.8f,  -houseSpX * 0.6f, -streetD * 0.5f,  0f),
            ("House_Store",         charHeight * 2.8f,   houseSpX * 0.6f, -streetD * 0.5f,  0f),
        };

        foreach (var cfg in configs)
        {
            GameObject go = GameObject.Find(cfg.name);
            if (go == null)
            {
                Debug.LogWarning($"[Arrange] '{cfg.name}' not found in scene – skipped.");
                continue;
            }

            // ── 4a. Measure current world-space height ────────────────────────
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[Arrange] '{cfg.name}' has no Renderer – skipped.");
                continue;
            }

            // Reset scale to 1 first to get clean bounds
            go.transform.localScale = Vector3.one;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float currentH = b.size.y;
            if (currentH < 0.001f) currentH = 1f;

            // ── 4b. Compute uniform scale ─────────────────────────────────────
            float scaleFactor = cfg.targetH / currentH;
            go.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

            // ── 4c. Re-measure bounds after scaling for floor alignment ────────
            b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float floorOffset = go.transform.position.y - b.min.y; // dist from pivot to floor

            // ── 4d. Position: on Ground_Map01 surface, offset in XZ ──────────
            Vector3 pos = new Vector3(
                groundPos.x + cfg.offsetX,
                groundY + floorOffset,
                groundPos.z + cfg.offsetZ
            );
            go.transform.position = pos;

            // ── 4e. Rotation: only Y and Z, keep X unchanged ──────────────────
            Vector3 currentEuler = go.transform.eulerAngles;
            go.transform.eulerAngles = new Vector3(
                currentEuler.x,   // X unchanged (as per requirement)
                cfg.rotY,
                0f                // Z reset to 0 for upright
            );

            // ── 4f. Parent to Ground_Map01 ────────────────────────────────────
            go.transform.SetParent(ground.transform, true);

            Debug.Log($"[Arrange] '{cfg.name}' -> scale={scaleFactor:F3} | pos={pos} | rotY={cfg.rotY}");
        }

        // ── 5. Save scene ─────────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Arrange] Done! All houses placed on Ground_Map01. Please save the scene (Ctrl+S).");
    }
}
