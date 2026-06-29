using UnityEngine; // Importeert Unity Engine functionaliteit.

/* =====================================================================================================================
 * SCRIPT: CoinPickup.cs
 * DOEL: Wordt automatisch aan een muntje gekoppeld dat op de grond valt wanneer een alien sterft.
 *       Het bouwt dynamisch een 3D gouden cilinder, laat deze prachtig ronddraaien en gloeien in het donker,
 *       en stelt de speler in staat om de munt op te pakken door in de buurt op de [E] toets te drukken.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Procedurele 3D Generatie: Om te voorkomen dat we afhankelijk zijn van externe 3D munt-modellen die misschien
 *    ontbreken, bouwen we via code ('GameObject.CreatePrimitive') zelf een platte cilinder en geven we die een glimmend
 *    gouden materiaal met emissie (gloei-effect).
 * 2. Afstands-meting (Distance Check): In Update() meten we elke frame hoe ver de speler van de munt af staat.
 *    Binnen 3.5 meter verschijnt er automatisch een [E] prompt op het scherm.
 * ===================================================================================================================== */

public class CoinPickup : MonoBehaviour
{
    [Header("Munt Instellingen")]
    public int coinValue = 100;          // De waarde in dollars ($) van deze munt.
    private Transform playerTransform;   // Referentie naar de spelerpositie om afstand te meten.
    private bool playerInRange = false;  // Staat op true zodra de speler binnen 3.5 meter komt.

    // Start() wordt uitgevoerd op het moment dat de dode alien deze munt dropt.
    void Start()
    {
        // Zoek de speler op via zijn Tag of via zijn Controller script.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) playerTransform = playerObj.transform;

        // EXAMEN TIP (Dynamische 3D Munt Generatie):
        // Als dit munt-object nog leeg is (geen childs of renderer), bouwen we via code een glanzende gouden munt!
        if (transform.childCount == 0 && GetComponent<MeshRenderer>() == null)
        {
            // Maak een standaard Unity cilinder aan.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.parent = transform; // Maak de cilinder een kind van dit object.
            visual.transform.localPosition = new Vector3(0, 0.5f, 0); // Til hem iets boven de grond.
            visual.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f); // Maak hem plat en breed als een munt.
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Zet hem rechtop op zijn kant.

            // Verwijder de collider van het visuele model (we meten afstand via Vector3.Distance in Update).
            Collider primCol = visual.GetComponent<Collider>();
            if (primCol != null)
            {
                primCol.enabled = false;
                DestroyImmediate(primCol);
            }

            // Maak een glimmend goud materiaal aan met Universal Render Pipeline (URP) ondersteuning.
            Renderer rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                Material goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (goldMat.shader.name == "Hidden/InternalErrorShader")
                    goldMat = new Material(Shader.Find("Standard")); // Reserve shader indien URP ontbreekt.

                Color goldColor = new Color(1f, 0.84f, 0f, 1f); // RGB kleur voor goud.
                if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", goldColor);
                if (goldMat.HasProperty("_Color")) goldMat.SetColor("_Color", goldColor);
                if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.9f); // Hoog metaal-effect
                if (goldMat.HasProperty("_Smoothness")) goldMat.SetFloat("_Smoothness", 0.8f); // Gladde glans
                
                // Zet emissie (licht geven) aan zodat de munt opvalt in donkere ruimteschepen!
                if (goldMat.HasProperty("_EmissionColor"))
                {
                    goldMat.EnableKeyword("_EMISSION");
                    goldMat.SetColor("_EmissionColor", goldColor * 0.4f);
                }

                rend.material = goldMat; // Plak het materiaal op de munt.
            }
        }
    }

    // Update() draait elke frame om rotatie en speler-input te verwerken.
    void Update()
    {
        if (playerTransform == null) return;

        // Laat de munt constant met 90 graden per seconde om zijn verticale Y-as ronddraaien (zwevend icoon effect).
        transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);

        // Meet de 3D afstand tussen de munt en de speler.
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        playerInRange = (dist <= 3.5f); // Als we binnen 3.5 meter zijn, wordt playerInRange true.

        // Als we dichtbij staan ÉN de speler drukt op E...
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            CollectCoin(); // Pak de munt op!
        }
    }

    // OnGUI() tekent de zwarte/gouden [E] balk in beeld wanneer je dichtbij staat.
    void OnGUI()
    {
        if (playerInRange && GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            float boxW = 260f; float boxH = 45f;
            float boxX = (Screen.width - boxW) / 2f; float boxY = Screen.height * 0.7f;

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            GUI.color = new Color(1f, 0.84f, 0f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20; style.fontStyle = FontStyle.Bold; style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(1f, 0.9f, 0.2f, 1f);

            GUI.Label(new Rect(boxX, boxY, boxW, boxH), "[E] Pak Munt op (+$" + coinValue + ")", style);
        }
    }

    // Voert de oppak-logica uit en verwijdert de munt uit het level.
    void CollectCoin()
    {
        SFXManager.Instance?.PlayPickup(); // Speel een ping-geluidje af.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoins(coinValue); // Voeg het bedrag toe in de GameManager.
        }
        Destroy(gameObject); // Vernietig het munt 3D-object.
    }
}
