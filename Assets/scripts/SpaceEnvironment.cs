using UnityEngine;

/* ==========================================================================================
 * SCRIPT: SpaceEnvironment.cs
 * DOEL: Genereert procedureel een sterrenhemel met sterren-deeltjes en verbetert de vloer.
 * ========================================================================================== */

public class SpaceEnvironment : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        // Alleen als er geen SpaceEnvironment al bestaat
        if (FindAnyObjectByType<SpaceEnvironment>() != null) return;

        GameObject go = new GameObject("SpaceEnvironment");
        go.AddComponent<SpaceEnvironment>();
    }

    void Start()
    {
        CreateStarfield();
        ImproveFloor();
    }

    void CreateStarfield()
    {
        // Maak een particle system dat sterren simuleert rondom de speler
        GameObject starObj = new GameObject("Starfield");
        starObj.transform.parent = transform;
        starObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = starObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 50f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.maxParticles = 800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.9f, 1f, 0.8f), 
            new Color(1f, 0.85f, 0.7f, 1f)
        );

        var emission = ps.emission;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 80f;

        // Maak sterren die twinkelen
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0f), 
                new GradientColorKey(Color.white, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(1f, 0.1f), 
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f) 
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // Gebruik een standaard particle material
        ParticleSystemRenderer psr = starObj.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        
        // Maak een emissieve material voor de sterren
        Material starMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (starMat.shader.name == "Hidden/InternalErrorShader")
            starMat = new Material(Shader.Find("Particles/Standard Unlit"));
        
        starMat.SetColor("_BaseColor", Color.white);
        if (starMat.HasProperty("_Color")) starMat.SetColor("_Color", Color.white);
        psr.material = starMat;
    }

    void ImproveFloor()
    {
        // Zoek de vloer (ground/floor) en geef het een betere kleur/material
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            string name = obj.name.ToLower();
            if (name.Contains("floor") || name.Contains("ground") || name.Contains("plane"))
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    // Maak een donker metalen vloer-material
                    Material floorMat = new Material(rend.sharedMaterial);
                    Color darkMetal = new Color(0.08f, 0.08f, 0.1f, 1f);
                    if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", darkMetal);
                    if (floorMat.HasProperty("_Color")) floorMat.SetColor("_Color", darkMetal);
                    if (floorMat.HasProperty("_Metallic")) floorMat.SetFloat("_Metallic", 0.7f);
                    if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.4f);
                    rend.material = floorMat;
                }
            }
        }
    }
}
