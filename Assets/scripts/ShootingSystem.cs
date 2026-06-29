using UnityEngine; // Importeert de Unity engine basisbibliotheek voor 3D logica, vectoren en transformaties.

/* =====================================================================================================================
 * SCRIPT: ShootingSystem.cs
 * DOEL: Dit script regelt alles wat met schieten te maken heeft: het vuren van kogels, het berekenen van raakpunten
 *       via Raycasting (lasermeting), het inzoomen met de rechtermuisknop (GTA-style richten), de terugslag (recoil),
 *       en het vergrendelen van het wapen in de rechterhand.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Raycasting (Laser-meting): Wanneer je schiet, sturen we een onzichtbare wiskundige lijn ('Ray') vanuit exact het
 *    midden van het camerascherm (Viewport coördinaat 0.5, 0.5) naar voren. Het eerste object dat deze lijn raakt, is
 *    ons doelwit. We sturen de fysieke kogel vervolgens vanaf de loop van het geweer precies naar dat 3D raakpunt toe.
 * 2. FOV Interpolatie (Lerp): Bij het richten veranderen we de Field of View (FOV) van de camera vloeiend van 60 naar 40
 *    graden met behulp van 'Mathf.Lerp'. Dit geeft een cinematisch en soepel inzoomeffect.
 * 3. Wapen Vergrendeling in LateUpdate: Om te garanderen dat ragdoll-fysica of loop-animaties het geweer nooit losrukken,
 *    zetten we het wapen aan het eind van elke frame exact terug op de opgeslagen positie in de handpalm.
 * ===================================================================================================================== */

public class ShootingSystem : MonoBehaviour // MonoBehaviour maakt het mogelijk om dit script op een GameObject in Unity te plaatsen.
{
    [Header("Wapen Instellingen (Gun Settings)")] // Kopje in de Inspector voor overzichtelijkheid.
    public int damage = 25;                       // Basis schade per kogel. Dit getal wordt na elke wave vermenigvuldigd met 1.5x.
    public float range = 100f;                    // Het maximale bereik in meters van onze laser-meting (Raycast).
    public float fireRate = 0.5f;                 // De minimale wachttijd in seconden tussen twee schoten in (vuursnelheid).

    [Header("Wapen Referenties")]
    public Camera mainCamera;                     // Referentie naar de hoofdcamera van waaruit we het midden van het scherm meten.
    public Transform firePoint;                   // Het punt aan het uiteinde van de geweerloop waar de kogel fysiek uit spawnt.
    public GameObject bulletPrefab;               // Het kogel 3D-object dat we instantiëren (spawnen) en wegschieten.
    public float bulletSpeed = 50f;               // De snelheid waarmee de afgevuurde kogel door de lucht vliegt.

    [Header("Effecten & Animatie")]
    public ParticleSystem muzzleFlash;            // Vuurdeeltjes (vuurvlam) bij de loop tijdens het schieten.
    public AudioSource shootSound;                // De geluidsspeler voor het schietgeluid van het laser-geweer.
    public AudioClip hitSound;                    // Het korte geluidje ('tik') dat afspeelt wanneer je een vijand raakt.
    private AudioSource audioSource;              // Interne geluidsspeler voor hitgeluidjes.
    public Animator playerAnimator;               // Referentie naar de poppetje-animator om schiet-animaties te triggeren.

    [Header("Richten / Aiming (GTA Style)")]
    public float normalFOV = 60f;                 // Het standaard gezichtsveld (Field of View) van de camera als je niet richt.
    public float aimFOV = 40f;                    // Het ingezoomde gezichtsveld wanneer je de rechtermuisknop ingedrukt houdt.
    public float aimSpeed = 10f;                  // De snelheid waarmee de camera in- en uitzoomt.
    private bool isAiming = false;                // Booleaanse variabele die bijhoudt of we op dit moment aan het richten zijn.

    [Header("Terugslag (Recoil)")]
    public float recoilAmount = 2f;                // Het aantal graden dat de camera omhoog schopt bij elk schot.
    public float recoilRecoverySpeed = 5f;         // De snelheid waarmee de camera na terugslag automatisch weer naar beneden zakt.
    private float currentRecoil = 0f;              // Houdt bij hoeveel terugslag er momenteel nog actief is.
    private Transform cameraTransform;             // Slaat de Transform van de camera op.

    // Interne variabelen voor de Hitmarker (het rode of gele kruisje in beeld bij een treffer)
    private bool hitRegistered = false;            // Stelt vast of we net iets geraakt hebben.
    private bool headshotRegistered = false;       // Stelt vast of het een headshot (hoofdtraject) was.
    private float hitmarkerTimer = 0f;             // Aftelklokje hoe lang het kruisje nog zichtbaar moet blijven.
    private float hitmarkerDuration = 0.3f;        // Het kruisje blijft precies 0.3 seconden op het scherm staan.

    private float nextTimeToFire = 0f;             // Slaat de exacte gametijd op waarop het volgende schot pas gelost mag worden.

    // Variabelen om het wapen muurvast in de rechterhand te vergrendelen
    private Vector3 initialLocalPos;               // De originele positie van het wapen relatief aan de hand.
    private Quaternion initialLocalRot;            // De originele rotatie van het wapen relatief aan de hand.
    private Transform parentHand;                  // Referentie naar het hand-botje waar het geweer aan vast zit.

    // Start() wordt door Unity één keer uitgevoerd zodra het level laadt.
    void Start()
    {
        // Sla de beginpositie en rotatie van het geweer in de handpalm op.
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
        // Sla op wie de ouder (het handbotje) is van dit geweer.
        parentHand = transform.parent;

        // Als er een mainCamera is ingesteld, sla dan zijn transform op.
        cameraTransform = mainCamera != null ? mainCamera.transform : null;
        
        // Zoek of er een AudioSource op dit geweer zit.
        audioSource = GetComponent<AudioSource>();
        // Als die er niet zit, maar we hebben wel een hitSound, maak er dan automatisch eentje aan via code.
        if (audioSource == null && hitSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // LateUpdate() wordt door Unity aan het allereind van elke frame berekend, nadat alle animaties klaar zijn.
    void LateUpdate()
    {
        // EXAMEN UITLEG (Wapen Vergrendeling):
        // Als het geweer nog steeds netjes aan de rechterhand vastzit...
        if (parentHand != null && transform.parent == parentHand)
        {
            // Overschrijf de lokale positie en rotatie exact met de beginwaarden.
            // Zelfs als ragdoll physics of loop-animaties proberen het geweer te verschuiven, zetten wij het hier rotsvast terug!
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
        }
    }

    // Update() draait elke frame om muisklikken en timers te controleren.
    void Update()
    {
        // Als het spel gepauzeerd is (tijd staat stil: Time.timeScale == 0), voer dan geen acties uit.
        if (Time.timeScale == 0f) return;

        // 1. Richten (Aiming): Controleer of de rechtermuisknop ('Fire2') ingedrukt wordt gehouden.
        isAiming = Input.GetButton("Fire2");

        // Stuur de booleaan 'isAiming' door naar de Animator, zodat het poppetje zijn geweer schouderklaar houdt.
        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
            playerAnimator.SetBool("Aiming", isAiming);

        // 2. Camera FOV Interpolatie (EXAMEN TIP):
        if (mainCamera != null)
        {
            // Bepaal de gewenste FOV: 40 als we richten, 60 als we normaal kijken.
            float targetFOV = isAiming ? aimFOV : normalFOV;
            // Mathf.Lerp berekent een vloeiende overgang tussen de huidige FOV en de targetFOV op basis van aimSpeed.
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * aimSpeed);
        }

        // 3. Recoil Herstel: Als de camera door een schot omhoog staat (currentRecoil > 0)...
        if (currentRecoil > 0f)
        {
            // Bereken hoeveel graden herstel we deze frame mogen doen (snelheid * frametijd).
            float recoilThisFrame = Mathf.Min(currentRecoil, recoilRecoverySpeed * Time.deltaTime);
            // Haal dat van de huidige terugslag af.
            currentRecoil -= recoilThisFrame;
            
            // Zoek de AstronautController en duw de camera weer zachtjes naar beneden (-recoilThisFrame).
            AstronautController controller = GetComponentInParent<AstronautController>();
            if (controller != null) controller.AddRecoil(-recoilThisFrame);
        }

        // 4. Hitmarker timer: Als de hitmarker nog zichtbaar is (timer > 0)...
        if (hitmarkerTimer > 0f)
        {
            // Tel de frametijd af van de timer.
            hitmarkerTimer -= Time.deltaTime;
        }
        else
        {
            // Timer is op 0: verberg de hitmarker kruisjes.
            hitRegistered = false;
            headshotRegistered = false;
        }

        // 5. Schieten: Als de linkermuisknop ('Fire1') wordt ingedrukt ÉN de huidige gametijd is groter/gelijk aan nextTimeToFire...
        if (Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire)
        {
            // Bereken wanneer we weer mogen schieten (huidige tijd + wachttijd fireRate).
            nextTimeToFire = Time.time + fireRate;
            // Voer het schot uit!
            Shoot();
        }
    }

    // De hoofdfunctie die het daadwerkelijke vuren en richten van de kogel berekent.
    void Shoot()
    {
        // 1. Visuele en geluidseffecten afspelen
        if (muzzleFlash != null) muzzleFlash.Play(); // Speel de vuurvlam af bij de loop.
        if (shootSound != null) shootSound.Play();   // Speel het laser-schietgeluid af.
        SFXManager.Instance?.PlayLaser();            // Speel extra procedureel lasergeluid via SFXManager.

        // Trigger de schiet-animatie in de Animator.
        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetTrigger("Attack");
        }

        // 2. Voeg opwaartse terugslag toe aan de camera.
        ApplyRecoil();

        // 3. Raycast Richting Bepalen (EXAMEN TIP):
        // We creëren een 3D Ray (straal) die start bij de camera en exact door het midden van de viewport (0.5, 0.5) schiet.
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Als we helemaal niets raken, is ons richtpunt standaard op 100 meter recht vooruit in de lucht.
        Vector3 targetPoint = ray.GetPoint(range);

        // Schiet de laserstraal door de hele wereld heen en verzamel ALLE objecten die hij op zijn pad raakt binnen 100 meter.
        RaycastHit[] hits = Physics.RaycastAll(ray, range);
        float closestDistance = Mathf.Infinity; // Begin met een oneindig grote afstand om te vergelijken.

        // Loop door alle geraakte objecten heen om de aller-dichtstbijzijnde te vinden.
        foreach (RaycastHit hit in hits)
        {
            // Negeer botsingen met de speler zelf (we willen onszelf niet in onze rug schieten!).
            if (hit.transform.CompareTag("Player") || hit.transform.GetComponentInParent<AstronautController>() != null) continue;
            
            // Als deze treffer dichterbij is dan de vorige gevonden treffer...
            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance; // Update de kortste afstand.
                targetPoint = hit.point;        // Sla dit exacte 3D-raakpunt op als ons officiële doelwit!
            }
        }

        // 4. Kogel Spawnen en Richting Bepalen:
        // Bereken de 3D richting van de loop van het geweer (firePoint) naar het gevonden doelwit (targetPoint).
        Vector3 direction = targetPoint - firePoint.position;

        // Als we een kogel-prefab hebben en een loop-referentie...
        if (bulletPrefab != null && firePoint != null)
        {
            // Maak een gloednieuwe kogel aan op de positie van de loop.
            GameObject currentBullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            // Draai de neus van de kogel in de exacte richting van ons doelwit (direction.normalized).
            currentBullet.transform.forward = direction.normalized;

            // Haal het Bullet script op van de gespawnde kogel.
            Bullet bulletScript = currentBullet.GetComponent<Bullet>();
            if (bulletScript == null) bulletScript = currentBullet.AddComponent<Bullet>(); // Voeg toe indien ontbrekend.

            // Geef de instellingen (snelheid, schade en referentie naar dit schietsysteem) door aan de kogel.
            bulletScript.speed = bulletSpeed;
            bulletScript.damage = damage;
            bulletScript.shootingSystem = this;
        }
    }

    // Voegt terugslag toe door de camera een stukje omhoog te kantelen.
    void ApplyRecoil()
    {
        if (cameraTransform == null) return;
        currentRecoil += recoilAmount; // Voeg de hoeveelheid toe aan de actieve terugslag.
        
        AstronautController controller = GetComponentInParent<AstronautController>();
        if (controller != null)
        {
            controller.AddRecoil(recoilAmount); // Stuur door naar de speler controller.
        }
        else
        {
            cameraTransform.localEulerAngles -= new Vector3(recoilAmount, 0f, 0f); // Reserve-optie als controller ontbreekt.
        }
    }

    // Publieke functie die door Bullet.cs wordt aangeroepen wanneer een kogel doel raakt.
    public void RegisterHit(bool isHeadshot = false)
    {
        hitRegistered = true;            // Zet hitmarker aan.
        headshotRegistered = isHeadshot; // Onthoud of het een headshot was (voor de gele kleur).
        hitmarkerTimer = hitmarkerDuration; // Reset de aftelklok op 0.3 seconden.

        // Speel het hit-geluidje ('tik') af in het oor van de speler.
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    // OnGUI() wordt door Unity aangeroepen om 2D-elementen (zoals het richtkruis) direct over het 3D-scherm te tekenen.
    void OnGUI()
    {
        if (Time.timeScale == 0f) return; // Verberg richtkruis als game gepauzeerd is.

        // Bepaal de grootte van het richtkruis: klein (16px) als we richten, groter (24px) als we vanaf de heup schieten.
        float size = isAiming ? 16f : 24f;
        // Bereken exact het midden van het scherm.
        float x = (Screen.width - size) / 2f;
        float y = (Screen.height - size) / 2f;

        // Teken het witte standaard richtkruisje met een kleine transparantie (0.8 alpha).
        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);

        // Als we net iets geraakt hebben (hitRegistered == true)...
        if (hitRegistered)
        {
            // Kies geel bij een headshot, anders rood bij een normale treffer.
            GUI.color = headshotRegistered ? Color.yellow : Color.red;
            // Maak de hitmarker iets groter (1.5x) dan het normale richtkruis.
            float hitSize = size * 1.5f;
            float hx = (Screen.width - hitSize) / 2f;
            float hy = (Screen.height - hitSize) / 2f;
            // Teken het rode/gele vierkantje over het richtkruis heen als visuele feedback.
            GUI.DrawTexture(new Rect(hx, hy, hitSize, hitSize), Texture2D.whiteTexture);
        }
        GUI.color = Color.white; // Herstel de kleur naar wit voor andere UI elementen.
    }
}
