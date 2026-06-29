using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class CargoShipURPUpgrader
{
    static CargoShipURPUpgrader()
    {
        EditorApplication.delayCall += UpgradeAllProjectMaterials;
    }

    [MenuItem("Tools/Upgrade All Project Materials to URP")]
    public static void UpgradeAllProjectMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Shader tmpMobileSDF = Shader.Find("TextMeshPro/Mobile/Distance Field");

        if (urpLit == null)
        {
            Debug.LogError("Could not find shader 'Universal Render Pipeline/Lit'");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            // Skip skyboxes and UI
            if (mat.shader.name.Contains("Skybox") || mat.shader.name.Contains("UI/") || mat.shader.name.Contains("GUI")) continue;

            // Handle TextMeshPro SDF Fonts carefully so they never turn into blocks!
            bool isFont = path.Contains("SDF") || mat.name.Contains("SDF") || path.Contains("Font") || mat.name.Contains("Font") || path.Contains("TextMesh Pro");
            if (isFont)
            {
                if (tmpMobileSDF != null && !mat.shader.name.Contains("TextMeshPro"))
                {
                    mat.shader = tmpMobileSDF;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
                continue; // Do not apply standard URP Lit to fonts
            }

            bool isParticle = path.Contains("Blood") || path.Contains("Particle") || mat.shader.name.Contains("Particle");
            Shader targetShader = isParticle ? (urpParticles != null ? urpParticles : urpLit) : urpLit;

            bool changed = false;

            if (mat.shader != targetShader && !mat.shader.name.Contains("Universal Render Pipeline"))
            {
                mat.shader = targetShader;
                changed = true;
            }

            // Ensure textures are mapped to _BaseMap
            if (mat.HasProperty("_MainTex") && mat.HasProperty("_BaseMap"))
            {
                Texture mainTex = mat.GetTexture("_MainTex");
                Texture baseMap = mat.GetTexture("_BaseMap");
                if (mainTex != null && baseMap == null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    changed = true;
                }
            }

            // Ensure colors are mapped to _BaseColor
            if (mat.HasProperty("_Color") && mat.HasProperty("_BaseColor"))
            {
                Color color = mat.GetColor("_Color");
                Color baseColor = mat.GetColor("_BaseColor");
                if (color != Color.white && baseColor == Color.white)
                {
                    mat.SetColor("_BaseColor", color);
                    changed = true;
                }
            }

            // Enable Normal Map keyword if bump map exists
            if (mat.HasProperty("_BumpMap"))
            {
                Texture bumpMap = mat.GetTexture("_BumpMap");
                if (bumpMap != null && !mat.IsKeywordEnabled("_NORMALMAP"))
                {
                    mat.EnableKeyword("_NORMALMAP");
                    changed = true;
                }
            }

            // Configure Glass / Transparent materials
            if (path.Contains("Glass") || path.Contains("glass") || mat.name.Contains("Glass") || mat.name.Contains("glass"))
            {
                if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") != 1.0f)
                {
                    mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
                    mat.SetFloat("_Blend", 0.0f); // 0 = Alpha
                    mat.SetFloat("_ZWrite", 0.0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.SetShaderPassEnabled("ShadowCaster", false);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        if (count > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"URP Upgrader successfully upgraded/configured {count} materials across the project.");
        }
    }
}
