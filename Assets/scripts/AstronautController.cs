using UnityEngine;

/* =====================================================================================================================
 * SCRIPT: AstronautController.cs
 * DOEL: Beheert alle bewegingen (WASD), het sprinten, springen, zwaartekracht en muis-richten van de speler.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. CharacterController: Wij gebruiken Unity's ingebouwde 'CharacterController' component in plaats van een normale 
 *    Rigidbody. Dit geeft ons strakke, directe arcade-achtige controle over de speler zonder dat we glip- of glij-
 *    effecten van natuurkunde hebben.
 * 2. Zwaartekracht & Anti-Zweef: We berekenen zelf de val-snelheid (velocity.y). Als de speler door een bug of vijand 
 *    omhoog gelanceerd wordt (hoger dan 2.5 meter), passen we extra sterke zwaartekracht toe zodat hij direct weer 
 *    stevig met zijn voeten op de grond belandt.
 * 3. Ragdoll Isolatie: In de Start() methode schakelen we fysieke botsingen op de arm- en handbotten uit (isKinematic 
 *    en isTrigger). Hierdoor kan de physics-engine het wapen of de handen nooit uit positie duwen als je tegen een muur loopt.
 * ===================================================================================================================== */

[RequireComponent(typeof(CharacterController))] // Garandeert dat dit GameObject altijd een CharacterController heeft.
public class AstronautController : MonoBehaviour
{
    [Header("Beweging Instellingen (Movement Settings)")]
    public float walkSpeed = 7f;         // Standaard loopsnelheid.
    public float sprintSpeed = 12f;      // Snelheid wanneer de LeftShift toets wordt ingedrukt.
    public float jumpHeight = 1.2f;      // Hoe hoog de speler kan springen met Spatie.
    public float gravity = -20f;         // De neerwaartse trekkracht (zwaartekracht).

    [Header("Kijk Instellingen (Look Settings)")]
    public float mouseSensitivity = 300f;// Gevoeligheid van de muisbeweging voor het rondkijken.
    public Transform playerCamera;       // Referentie naar de camera die op het hoofd/schouder zit.

    [Header("Animatie Referenties")]
    public Animator animator;            // Stuurt de loop- en ren-animaties aan op basis van snelheid.

    // Interne variabelen voor berekeningen
    private CharacterController controller; // Referentie naar de component die de speler fysiek verplaatst.
    private Vector3 velocity;               // Houdt de actuele val- en springsnelheid (Y-as) bij.
    private bool isGrounded;                // Staan we momenteel op de grond?
    private float xRotation = 0f;           // De huidige verticale kijkhoek (omhoog/omlaag kijken).
    private bool isDead = false;            // Wordt true als de speler af is, stopt alle besturing.
    private Vector3 initialCameraLocalPos;  // Slaat de originele camerapositie op om camera-shake te stabiliseren.

    void Start()
    {
        // 1. Haal de CharacterController component op van dit object.
        controller = GetComponent<CharacterController>();
        
        // Zorg dat de muisgevoeligheid nooit per ongeluk te laag staat ingesteld.
        if (mouseSensitivity <= 100f) mouseSensitivity = 300f;

        // Sla de beginpositie van de camera op.
        if (playerCamera != null)
        {
            initialCameraLocalPos = playerCamera.localPosition;
        }

        // 2. Veiligheidscheck: Zorg dat de hoofd-colliders van de speler NIET als trigger staan ingesteld,
        // anders zou de speler dwars door muren en objecten heen kunnen lopen!
        foreach (Collider c in GetComponents<Collider>())
        {
            if (c != controller && c.isTrigger)
                c.isTrigger = false;
        }

        // 3. Ragdoll & Wapen Isolatie (EXAMEN TIP):
        // Een 3D-model met ragdoll heeft op elk botje (armen, benen) een Rigidbody en Collider.
        // Tijdens gameplay willen we dat de Animatie 100% controle heeft. Daarom zetten we alle bot-rigidbodies
        // op 'isKinematic = true' (ongevoelig voor fysieke duw-krachten) en maken we de colliders triggers.
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb != GetComponent<Rigidbody>())
            {
                rb.isKinematic = true;  // Schakelt externe fysieke krachten uit.
                rb.useGravity = false;  // Geen losse zwaartekracht op armen/wapens.
            }
        }

        Collider[] childCols = GetComponentsInChildren<Collider>();
        foreach (Collider c in childCols)
        {
            if (c != controller && c != GetComponent<Collider>())
            {
                // Negeer interne botsingen tussen de arm-colliders en de speler zelf.
                if (controller != null) Physics.IgnoreCollision(c, controller);
                c.isTrigger = true; // Zorgt dat botten nooit achter muren blijven haken.
            }
        }
    }

    void Update()
    {
        // Als de speler dood is of het spel gepauzeerd is (Time.timeScale == 0), voer dan geen besturing uit.
        if (isDead) return;
        if (Time.timeScale == 0f) return;

        // Zorg dat de muiscursor verborgen en vergrendeld is in het midden van het scherm tijdens het spelen.
        if (GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Roep elke frame de bewegings- en kijkfuncties aan.
        HandleMovement();
        HandleMouseLook();
    }

    void LateUpdate()
    {
        // LateUpdate wordt aan het eind van de frame aangeroepen nadat alle animaties zijn berekend.
        // FIX: Voorkom hevige, schokkende camera shake door loop-animaties tijdens het sprinten door
        // de camera vloeiend (Lerp) terug te sturen naar zijn stabiele beginpositie.
        if (playerCamera != null && !isDead)
        {
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, initialCameraLocalPos, Time.deltaTime * 25f);
        }
    }

    void HandleMovement()
    {
        // 1. Controleer via de CharacterController of de speler de grond raakt.
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Klein min-getal houdt de speler stevig tegen de grond gedrukt op hellingen.
        }

        // 2. Lees toetsenbord input uit (-1 tot 1). A/D = Horizontal, W/S = Vertical.
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Bereken de looprichting relatief aan waar de speler naartoe kijkt (transform.right en transform.forward).
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        if (move.magnitude > 1f) move.Normalize(); // Voorkomt dat schuin lopen sneller is dan recht lopen.

        // 3. Controleer of LeftShift ingedrukt is om te sprinten.
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Verplaats de speler horizontaal via de CharacterController. Move = richting * snelheid * frame-tijd.
        controller.Move(move * currentSpeed * Time.deltaTime);

        // 4. Springen: Als op Spatie (Jump) wordt gedrukt én we staan op de grond.
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Natuurkundige formule voor springen: v = wortel(hoogte * -2 * zwaartekracht).
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 5. Zwaartekracht & Anti-Float (EXAMEN TIP):
        // Als de speler per ongeluk gaat zweven of door een alien omhoog gelanceerd wordt (> 2.5m hoog),
        // verdubbelen we de neerwaartse zwaartekracht (-40) zodat hij direct weer op de grond belandt!
        if (!isGrounded && transform.position.y > 2.5f)
        {
            velocity.y -= 40f * Time.deltaTime;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        // Pas de verticale valsnelheid toe op de speler.
        controller.Move(velocity * Time.deltaTime);

        // 6. Stuur de snelheid door naar de Animator zodat de poppetje-animatie sneller of langzamer rent.
        if (animator != null)
        {
            float speed = move.magnitude > 0.1f ? (isSprinting ? 1f : 0.5f) : 0f;
            animator.SetFloat("Speed", speed);
        }
    }

    void HandleMouseLook()
    {
        // Lees de muisbeweging uit en vermenigvuldig met de gevoeligheid.
        float mouseX = Input.GetAxis("Mouse X") * (mouseSensitivity * 0.012f);
        float mouseY = Input.GetAxis("Mouse Y") * (mouseSensitivity * 0.012f);

        // Pas de verticale kijkhoek aan (omhoog / omlaag).
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f); // Klem tussen -80 en 80 graden zodat je je nek niet breekt.

        // Roteer de camera omhoog/omlaag rond de lokale X-as.
        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // Roteer het hele spelerslichaam links/rechts rond de Y-as (Vector3.up).
        transform.Rotate(Vector3.up * mouseX);
    }

    // Wordt aangeroepen door ShootingSystem.cs om de camera omhoog te schoppen bij het schieten (terugslag).
    public void AddRecoil(float amount)
    {
        xRotation -= amount;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
    }

    // Wordt aangeroepen als de speler af is om animaties en besturing stil te leggen.
    public void SetDead()
    {
        isDead = true;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }
}
