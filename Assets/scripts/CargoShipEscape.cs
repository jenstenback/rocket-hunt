using UnityEngine;       // Importeert de standaard Unity Engine functionaliteit.
using System.Collections; // Importeert IEnumerator om coroutines (zoals de opstijg-animatie) te kunnen gebruiken.

/* =====================================================================================================================
 * SCRIPT: CargoShipEscape.cs
 * DOEL: Hangt aan het vrachtschip (Cargo Ship). Dit script controleert of de speler dichtbij staat en op [E] drukt.
 *       Als alle 5 onderdelen verzameld zijn, koppelt het script de speler vast aan het schip en laat het schip
 *       cinematisch de lucht in vliegen, waarna het overwinningsscherm getoond wordt!
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Trigger Colliders: Het schip heeft een onzichtbare 'Trigger' zone rondom zich. Via OnTriggerEnter detecteren
 *    we wanneer de astronaut binnen stapte. Alleen dan mag de [E] toets werken.
 * 2. Parent-Child Hierarchie: Tijdens het opstijgen doen we 'playerTransform.SetParent(transform)'. Hierdoor wordt
 *    de astronaut een 'kind' van het schip en vliegt hij exact mee omhoog de ruimte in.
 * 3. ResetPlayerForContinue: Als de speler na overwinning op 'Verder Spelen' klikt, zetten we het schip en de speler
 *    exact terug op de grond zodat hij verder kan vechten voor extra munten!
 * ===================================================================================================================== */

public class CargoShipEscape : MonoBehaviour
{
    [Header("Escape Instellingen")]
    public float flySpeed = 5f;          // De opwaartse snelheid waarmee het schip opstijgt.
    public float flyForwardSpeed = 8f;   // De voorwaartse snelheid van het schip tijdens het vliegen.
    public float escapeTime = 4f;        // Hoeveel seconden de vluchtanimatie duurt voordat het Victory scherm komt.
    public AudioClip engineSound;        // Het geluid van de ronkende raketmotoren.

    [Header("UI Prompt")]
    public string interactKey = "E";     // De toets waarop gedrukt moet worden om in te stappen.

    private bool playerInRange = false;  // Houdt bij of de speler momenteel in de triggerzone van het schip staat.
    private bool isEscaping = false;     // Houdt bij of we al bezig zijn met opstijgen (voorkomt dubbel klikken).
    private Transform playerTransform;   // Referentie naar de positie van de speler.
    private CharacterController playerController; // Referentie naar de speler controller om zwaartekracht te stoppen.
    private AstronautController playerMovement;   // Referentie naar het loopscript van de speler.
    private AudioSource audioSource;     // De geluidsspeler op het schip.

    // Opgeslagen beginposities om alles te herstellen bij 'Verder Spelen'
    private Vector3 initialShipPos;
    private Quaternion initialShipRot;
    private Vector3 initialPlayerPos;
    private Quaternion initialPlayerRot;

    // Start() wordt aan het begin van het level uitgevoerd.
    void Start()
    {
        // Sla de beginpositie en rotatie van het schip op.
        initialShipPos = transform.position;
        initialShipRot = transform.rotation;

        // Zoek of maak een AudioSource voor het motorgeluid.
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && engineSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Update() controleert elke frame op toetsinput.
    void Update()
    {
        // Als we al aan het vliegen zijn, of de speler staat niet bij het schip, doe dan niks.
        if (isEscaping || !playerInRange) return;
        if (Time.timeScale == 0f) return; // Doe niks als de game gepauzeerd is.

        // Controleer of de speler op de E-toets drukt.
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Vraag aan de GameManager of we alle onderdelen verzameld hebben (HasAllParts of AllPartsCollected).
            if (GameManager.Instance != null && (GameManager.Instance.HasAllParts() || GameManager.Instance.AllPartsCollected()))
            {
                // Start de opstijg-animatie!
                StartCoroutine(EscapeSequence());
            }
            else
            {
                Debug.Log("Niet genoeg onderdelen om te ontsnappen!");
            }
        }
    }

    // Wordt aangeroepen door Unity zodra een object de Trigger van het schip binnenkomt.
    void OnTriggerEnter(Collider other)
    {
        // Controleer of het binnengekomen object de tag "Player" heeft.
        if (other.CompareTag("Player"))
        {
            playerInRange = true; // Speler is nu dichtbij genoeg.
            playerTransform = other.transform;
            playerController = other.GetComponent<CharacterController>();
            playerMovement = other.GetComponent<AstronautController>();

            // Sla de exacte positie op waar de speler het schip in stapte.
            initialPlayerPos = playerTransform.position;
            initialPlayerRot = playerTransform.rotation;
        }
    }

    // Wordt aangeroepen wanneer de speler bij het schip wegloopt.
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false; // Speler is weggelopen, verberg de E-prompt.
            playerTransform = null;
        }
    }

    // De Coroutine die het schip in 4 seconden de ruimte in vliegt.
    IEnumerator EscapeSequence()
    {
        isEscaping = true; // Blokkeer verdere input.

        // Schakel de besturing van de speler uit zodat hij niet van het schip af kan wandelen of springen.
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.enabled = false;

        // Maak de speler een kind (child) van het schip, zodat hij automatisch meebeweegt.
        if (playerTransform != null) playerTransform.SetParent(transform);

        // Speel het motorgeluid af in een loop.
        if (engineSound != null && audioSource != null)
        {
            audioSource.clip = engineSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        float timer = 0f;
        // Bereken de vliegrichting (omhoog + naar voren).
        Vector3 flyDirection = (Vector3.up * flySpeed) + (transform.forward * flyForwardSpeed);

        // Zolang de timer onder de 4 seconden is...
        while (timer < escapeTime)
        {
            // Verplaats het schip in de vliegrichting.
            transform.position += flyDirection * Time.deltaTime;
            // Versnel elke frame een klein beetje (1.005x) voor een echte raketlancering!
            flyDirection *= 1.005f;
            timer += Time.deltaTime;
            yield return null; // Wacht tot de volgende frame.
        }

        if (audioSource != null) audioSource.Stop(); // Stop motorgeluid.

        // Roep de overwinning aan in de GameManager!
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOverWin();
        }
    }

    // Wordt aangeroepen wanneer de speler in het Victory scherm op 'Verder Spelen' klikt.
    public void ResetPlayerForContinue()
    {
        isEscaping = false;
        
        // Zet het schip exact terug op zijn startcoördinaten op de grond.
        transform.position = initialShipPos;
        transform.rotation = initialShipRot;

        // Maak de speler los van het schip.
        if (playerTransform != null)
        {
            playerTransform.SetParent(null);
            if (playerController != null)
            {
                playerController.enabled = false; // Even uit om positie te forceren
                playerTransform.position = initialPlayerPos;
                playerTransform.rotation = initialPlayerRot;
                playerController.enabled = true;  // Weer aan
            }
            else
            {
                playerTransform.position = initialPlayerPos;
                playerTransform.rotation = initialPlayerRot;
            }
        }

        // Schakel loopbesturing weer in.
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerController != null) playerController.enabled = true;
    }

    // OnGUI() tekent de instructie-tekst in het midden van het scherm.
    void OnGUI()
    {
        if (!playerInRange || isEscaping) return; // Toon niks als je niet bij het schip staat.
        if (Time.timeScale == 0f) return;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.fontSize = 28;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.fontStyle = FontStyle.Bold;

        float boxWidth = 500f; float boxHeight = 50f;
        float cx = (Screen.width - boxWidth) / 2f;
        float cy = Screen.height * 0.7f;

        // Als we alle onderdelen hebben, toon groene tekst dat je kan ontsnappen.
        if (GameManager.Instance != null && (GameManager.Instance.HasAllParts() || GameManager.Instance.AllPartsCollected()))
        {
            promptStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(cx, cy, boxWidth, boxHeight), "Druk op [" + interactKey + "] om te ontsnappen! 🚀", promptStyle);
        }
        else
        {
            // Zo niet, toon oranje tekst met hoeveel onderdelen je nog mist.
            promptStyle.normal.textColor = new Color(1f, 0.5f, 0f);
            int collected = GameManager.Instance != null ? GameManager.Instance.GetPartsCollected() : 0;
            int needed = GameManager.Instance != null ? GameManager.Instance.GetTotalPartsNeeded() : 5;
            int remaining = needed - collected;
            GUI.Label(new Rect(cx, cy, boxWidth, boxHeight), "Je hebt nog " + remaining + " onderdelen nodig!", promptStyle);
        }
    }
}
