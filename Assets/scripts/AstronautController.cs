using UnityEngine; // Importeert de standaard Unity Engine bibliotheek voor 3D objecten, wiskunde en transformaties.

/* =====================================================================================================================
 * SCRIPT: AstronautController.cs
 * DOEL: Dit script beheert de volledige besturing van de speler (de ruimte-astronaut). Het zorgt voor het lopen
 *       via de WASD-toetsen, het sprinten met Shift, het springen met Spatie, het verwerken van zwaartekracht,
 *       en het rondkijken via de muisbeweging (Mouse Look).
 * 
 * ARCHITECTUUR & UITLEG VOOR EXAMEN / PRESENTATIE:
 * 1. CharacterController: In plaats van een standaard Rigidbody (natuurkunde-lichaam) gebruiken we Unity's speciale
 *    'CharacterController' component. Deze geeft ons directe, strakke controle over beweging zonder dat de speler
 *    glibbert of omvalt als hij tegen een muur loopt.
 * 2. Zwaartekracht Berekening: We berekenen de verticale val- en springsnelheid zelf (via 'velocity.y'). Als de
 *    speler door een bug of een alien omhoog gelanceerd wordt (>2.5 meter), schakelen we een extra zware zwaartekracht
 *    in zodat hij direct weer op de grond belandt.
 * 3. Ragdoll Isolatie: Het 3D-model van de astronaut heeft botten (armen, benen) met elk een eigen Rigidbody en Collider.
 *    Om te voorkomen dat de fysica-engine met de animaties vecht, zetten we in Start() alle bot-rigidbodies op 
 *    'isKinematic = true' (ongevoelig voor duwkrachten) en maken we de colliders triggers.
 * ===================================================================================================================== */

[RequireComponent(typeof(CharacterController))] // Verplicht Unity om altijd automatisch een CharacterController aan dit GameObject toe te voegen.
public class AstronautController : MonoBehaviour // MonoBehaviour is de basisklasse in Unity waardoor we Start() en Update() kunnen gebruiken.
{
    [Header("Beweging Instellingen (Movement Settings)")] // Groepskopje in de Unity Inspector om variabelen netjes te sorteren.
    public float walkSpeed = 7f;                          // De snelheid waarmee de speler normaal wandelt (7 meter per seconde).
    public float sprintSpeed = 12f;                       // De hogere snelheid wanneer de speler sprint (12 meter per seconde).
    public float jumpHeight = 1.2f;                       // De gewenste spronghoogte in meters.
    public float gravity = -20f;                          // De zwaartekracht (negatief getal trekt de speler naar beneden).

    [Header("Kijk Instellingen (Look Settings)")]
    public float mouseSensitivity = 300f;                 // Bepaalt hoe snel het scherm draait als je de muis beweegt.
    public Transform playerCamera;                        // De Transform van de camera (het oog van de speler) om omhoog/omlaag te kijken.

    [Header("Animatie Referenties")]
    public Animator animator;                             // De Animator component die poppetje-animaties (lopen, rennen, idle) afspeelt.

    // Interne variabelen (private) die niet in de Inspector zichtbaar zijn maar gebruikt worden voor berekeningen
    private CharacterController controller;               // Slaat de referentie op naar de CharacterController component op dit object.
    private Vector3 velocity;                             // Een 3D-vector (x, y, z) die de huidige val- en springsnelheid bijhoudt.
    private bool isGrounded;                              // Een booleaanse variabele (true/false) die bijhoudt of we de grond raken.
    private float xRotation = 0f;                         // Houdt de huidige verticale kijkhoek (omhoog/omlaag) in graden bij.
    private bool isDead = false;                          // Wordt 'true' als de speler doodgaat, blokkeert daarna alle input.
    private Vector3 initialCameraLocalPos;                // Bewaart de beginpositie van de camera om heftige schokken te herstellen.

    // Start() wordt exact één keer aangeroepen door Unity zodra het spel of dit object wordt ingeladen.
    void Start()
    {
        // Haal de CharacterController component op die op hetzelfde GameObject zit en sla deze op in de variabele 'controller'.
        controller = GetComponent<CharacterController>();
        
        // Veiligheidscheck: Als de muisgevoeligheid per ongeluk op 100 of lager staat ingesteld in Unity, zet hem dan automatisch op 300.
        if (mouseSensitivity <= 100f) mouseSensitivity = 300f;

        // Als er een camera is gekoppeld aan de speler...
        if (playerCamera != null)
        {
            // Sla de exacte lokale startpositie van de camera op in 'initialCameraLocalPos'.
            initialCameraLocalPos = playerCamera.localPosition;
        }

        // Loop door ALLE Collider componenten heen die direct op dit hoofd-object zitten.
        foreach (Collider c in GetComponents<Collider>())
        {
            // Als dit NIET de CharacterController is én de collider staat per ongeluk als 'Trigger' (doorlaatbaar) ingesteld...
            if (c != controller && c.isTrigger)
            {
                // Zet isTrigger op false, zodat de speler solide is en niet door muren heen kan vallen.
                c.isTrigger = false;
            }
        }

        // EXAMEN UITLEG (Ragdoll en Wapen Isolatie):
        // Haal een lijst op van alle Rigidbody componenten op de speler EN al zijn onderliggende botten (armen, benen, wapen).
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs) // Loop door elke gevonden Rigidbody heen...
        {
            // Als deze Rigidbody niet op het hoofdobject zit (dus het is een botje of wapen)...
            if (rb != GetComponent<Rigidbody>())
            {
                rb.isKinematic = true;  // Zet op Kinematic: fysieke duw- en botskrachten hebben geen invloed meer op dit bot!
                rb.useGravity = false;  // Schakel zwaartekracht voor dit afzonderlijke botje uit.
            }
        }

        // Haal ook alle onderliggende Colliders op van de botten (zoals hand- of arm-colliders).
        Collider[] childCols = GetComponentsInChildren<Collider>();
        foreach (Collider c in childCols) // Loop door elke child collider heen...
        {
            // Als het niet de hoofdcontroller of hoofdcollider is...
            if (c != controller && c != GetComponent<Collider>())
            {
                // Vertel de Unity fysica-engine om botsingen tussen dit botje en de speler zelf volledig te negeren.
                if (controller != null) Physics.IgnoreCollision(c, controller);
                
                // Zet het botje op 'isTrigger = true' zodat het nooit achter muren of deurausparingen blijft haken.
                c.isTrigger = true;
            }
        }
    }

    // Update() wordt door Unity elke frame (ongeveer 60 tot 144 keer per seconde) aangeroepen om besturing te verwerken.
    void Update()
    {
        // Als de speler dood is gegaan (isDead == true), stop dan direct deze functie via 'return'. Geen besturing meer mogelijk.
        if (isDead) return;
        
        // Als de game gepauzeerd is (tijd staat stil: Time.timeScale == 0), voer dan ook geen bewegingen uit.
        if (Time.timeScale == 0f) return;

        // Controleer of de GameManager bestaat én of het spel daadwerkelijk gestart is (startscherm is weg).
        if (GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            // Als de muiscursor nog niet vergrendeld is in het scherm...
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked; // Vergrendel de muiscursor exact in het midden van het scherm.
                Cursor.visible = false;                   // Maak de muiscursor onzichtbaar voor een echte shooter-ervaring.
            }
        }

        // Roep elke frame de functies aan die beweging en muis-richting verwerken.
        HandleMovement();
        HandleMouseLook();
    }

    // LateUpdate() wordt aan het allereind van elke frame aangeroepen, NADAT alle Update() functies en animaties zijn klaarzet.
    void LateUpdate()
    {
        // Als we een camera hebben en we zijn nog in leven...
        if (playerCamera != null && !isDead)
        {
            // Gebruik Vector3.Lerp (vloeiende interpolatie) om de camera zachtjes terug te duwen naar zijn stabiele beginpositie.
            // Dit voorkomt dat de camera wild schokt wanneer de ren-animatie het hoofd van de astronaut heen en weer schudt.
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, initialCameraLocalPos, Time.deltaTime * 25f);
        }
    }

    // Beheert het lopen, sprinten, springen en vallen van de astronaut.
    void HandleMovement()
    {
        // 1. Vraag aan de CharacterController of de onderkant van de speler de grond raakt. Sla dit op in 'isGrounded'.
        isGrounded = controller.isGrounded;
        
        // Als we op de grond staan én onze verticale valsnelheid (velocity.y) is nog steeds naar beneden gericht...
        if (isGrounded && velocity.y < 0)
        {
            // Zet de valsnelheid vast op een klein negatief getal (-2). Dit duwt de speler stevig tegen schuine hellingen aan.
            velocity.y = -2f;
        }

        // 2. Lees de horizontale input uit (-1 voor A/Links, +1 voor D/Rechts, 0 voor niks).
        float horizontal = Input.GetAxis("Horizontal");
        // Lees de verticale input uit (-1 voor S/Achteruit, +1 voor W/Vooruit, 0 voor niks).
        float vertical = Input.GetAxis("Vertical");

        // Bereken de 3D verplaatsingsrichting: combineer de richting naar rechts (transform.right) met de richting naar voren (transform.forward).
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        
        // Als de lengte van de bewegingsvector groter is dan 1 (bij schuin lopen schuif je anders 1.41x sneller)...
        if (move.magnitude > 1f) move.Normalize(); // Normalize() maakt de vector precies 1 lang, zodat schuin lopen even snel is als recht lopen.

        // 3. Controleer of de LeftShift toets ingedrukt wordt gehouden om te sprinten.
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        // Kies de actuele snelheid: als isSprinting waar is, kies sprintSpeed (12), anders kies walkSpeed (7).
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Verplaats de speler horizontaal over de grond via controller.Move().
        // Vermenigvuldig richting * snelheid * Time.deltaTime (tijd per frame) voor een soepele, framerate-onafhankelijke beweging.
        controller.Move(move * currentSpeed * Time.deltaTime);

        // 4. Springen: Als de speler op de Spatiebalk (Jump button) drukt ÉN we staan op de grond...
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Natuurkundige formule voor springen: v = wortel(gewenste hoogte * -2 * zwaartekracht).
            // Dit berekent de exacte opwaartse snelheid die nodig is om precies 'jumpHeight' meter hoog te springen.
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 5. Zwaartekracht & Anti-Zweef bescherming (EXAMEN TIP):
        // Als we NIET op de grond staan én onze Y-positie is hoger dan 2.5 meter (bijvoorbeeld gelanceerd door een alien)...
        if (!isGrounded && transform.position.y > 2.5f)
        {
            // Trek de speler extra hard naar beneden (-40 m/s^2) zodat hij direct weer op de speelvloer belandt.
            velocity.y -= 40f * Time.deltaTime;
        }
        else
        {
            // Pas de normale zwaartekracht toe op de verticale snelheid (velocity.y += -20 * frametijd).
            velocity.y += gravity * Time.deltaTime;
        }
        
        // Voer de verticale val- of springbeweging daadwerkelijk uit op de CharacterController.
        controller.Move(velocity * Time.deltaTime);

        // 6. Stuur de actuele snelheid door naar de Animator, zodat het 3D poppetje begint te rennen of wandelen.
        if (animator != null)
        {
            // Bereken een waarde tussen 0 en 1: als we bewegen (>0.1) kiest hij 1 (sprint) of 0.5 (loop). Stilstaand is 0.
            float speed = move.magnitude > 0.1f ? (isSprinting ? 1f : 0.5f) : 0f;
            // Stuur deze waarde naar de parameter "Speed" in de Unity Animator Controller.
            animator.SetFloat("Speed", speed);
        }
    }

    // Beheert het draaien van de camera (omhoog/omlaag) en het draaien van het spelerslichaam (links/rechts).
    void HandleMouseLook()
    {
        // Lees de horizontale muisverplaatsing uit en vermenigvuldig met de gevoelighied.
        float mouseX = Input.GetAxis("Mouse X") * (mouseSensitivity * 0.012f);
        // Lees de verticale muisverplaatsing uit en vermenigvuldig met de gevoeligheid.
        float mouseY = Input.GetAxis("Mouse Y") * (mouseSensitivity * 0.012f);

        // Trek de verticale muisbeweging af van de xRotation (aftrekken omdat de Y-as in 3D andersom werkt).
        xRotation -= mouseY;
        // Klem (Clamp) de verticale kijkhoek tussen -80 graden (naar boven) en +80 graden (naar beneden) zodat je niet over de kop slaat.
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // Als de camera gekoppeld is...
        if (playerCamera != null)
        {
            // Pas de verticale rotatie (X-as) toe op de camera, terwijl Y en Z op 0 blijven.
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // Draai het hele 3D-object van de speler links of rechts (rondom de Y-as / Vector3.up) op basis van de muis X beweging.
        transform.Rotate(Vector3.up * mouseX);
    }

    // Publieke functie die door ShootingSystem.cs wordt aangeroepen wanneer de speler schiet om terugslag (recoil) te geven.
    public void AddRecoil(float amount)
    {
        // Duw de camera extra omhoog door 'amount' van de xRotation af te halen.
        xRotation -= amount;
        // Klem opnieuw tussen -80 en 80 graden voor de veiligheid.
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
    }

    // Publieke functie die wordt aangeroepen wanneer de speler stert (HP <= 0).
    public void SetDead()
    {
        // Zet de dood-booleaan op true, wat direct alle verdere input in Update() blokkeert.
        isDead = true;
        // Als we een animator hebben...
        if (animator != null)
        {
            // Zet de ren-snelheid op 0 zodat het poppetje stopt met de loop-animatie.
            animator.SetFloat("Speed", 0f);
        }
    }
}
