using UnityEngine;
using UnityEditor;

public class FixHouseArea01Material
{
    [MenuItem("AntiBoBia/Fix House_Area_01_Fixed Material")]
    public static void FixMaterial()
    {
        // Load textures
        Texture2D colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Models/Environment/Buildings/Area_01/Textures/House_Area_01_Color.jpg");
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Models/Environment/Buildings/Area_01/Textures/House_Area_01_Normal.png");

        if (colorTex == null) { Debug.LogError("Color texture not found!"); return; }

        // Set normal map import settings
        if (normalTex != null)
        {
            string normalPath = AssetDatabase.GetAssetPath(normalTex);
            TextureImporter ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                AssetDatabase.ImportAsset(normalPath);
            }
        }

        // Create or load material
        string matPath = "Assets/Models/Environment/Buildings/Area_01/House_Area_01_Fixed.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // Fix material properties - bright like Blender (Metallic was 1.0 causing black)
        mat.SetTexture("_BaseMap", colorTex);        // URP
        mat.SetTexture("_MainTex", colorTex);        // Standard fallback
        mat.SetFloat("_Metallic", 0f);               // Fix: 1.0 -> 0 (no longer black)
        mat.SetFloat("_Smoothness", 0.3f);
        mat.SetFloat("_GlossMapScale", 0.3f);
        mat.SetColor("_BaseColor", Color.white);     // URP
        mat.SetColor("_Color", Color.white);         // Standard

        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // Apply to object in scene
        GameObject obj = GameObject.Find("House_Area_01_Fixed");
        if (obj != null)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
            Debug.Log($"[FixMat] Material applied to {renderers.Length} renderer(s) on House_Area_01_Fixed");
        }
        else
        {
            Debug.LogWarning("[FixMat] House_Area_01_Fixed not found in scene! Material saved at: " + matPath);
        }

        AssetDatabase.Refresh();
        Debug.Log("[FixMat] Done! Metallic=0, Color texture applied, Smoothness=0.3");
    }
}
