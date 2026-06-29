using UnityEngine;

/* =====================================================================================================================
 * SCRIPT: ShootingSystem.cs
 * DOEL: Beheert het wapen, richten (GTA-style camera zoom), terugslag (recoil), hitregistratie en wapen-vergrendeling.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Raycasting (Laser-meting): Wanneer de speler schiet, schieten we een onzichtbare laserlijn ('Ray') vanuit exact
 *    het midden van het scherm (Viewport point 0.5, 0.5). Waar deze lijn een object raakt, is het doelwit. Vanaf de 
 *    loop van het geweer ('firePoint') sturen we vervolgens de kogel precies naar dat raakpunt toe.
 * 2. FOV Interpolatie (Lerp): Bij het richten met de rechtermuisknop veranderen we de Field of View (FOV) van de camera 
 *    vloeiend van 60 naar 40 via 'Mathf.Lerp'. Dit geeft een cinematisch en soepel inzoomeffect.
 * 3. Wapen Vergrendeling in LateUpdate: Om te voorkomen dat animaties of fysica het geweer laten zweven, zetten we 
 *    het geweer aan het eind van elke frame exact terug op zijn opgeslagen handpositie.
 * ===================================================================================================================== */

public class ShootingSystem : MonoBehaviour
{
    [Header("Wapen Instellingen (Gun Settings)")]
    public int damage = 25;              // Schade per kogel (wordt na elke wave met 1.5x vermenigvuldigd!).
    public float range = 100f;           // Maximaal bereik van de raycast/kogel.
    public float fireRate = 0.5f;        // Tijd in seconden tussen twee schoten in.

    [Header("Wapen Referenties")]
    public Camera mainCamera;            // Referentie naar de hoofdcamera van de speler.
    public Transform firePoint;          // De punt van de loop waar de fysieke kogel uit spawnt.
    public GameObject bulletPrefab;      // Het kogel object dat we instantiëren (spawnen).
    public float bulletSpeed = 50f;      // De vliegsnelheid van de kogel.

    [Header("Effecten & Animatie")]
    public ParticleSystem muzzleFlash;   // Vlam-effect bij de loop tijdens het schieten.
    public AudioSource shootSound;       // Schietgeluid.
    public AudioClip hitSound;           // Geluidje ('tik') wanneer je een vijand raakt.
    private AudioSource audioSource;
    public Animator playerAnimator;      // Referentie naar de speler animator voor schiet-animaties.

    [Header("Richten / Aiming (GTA Style)")]
    public float normalFOV = 60f;        // Standaard gezichtsveld van de camera.
    public float aimFOV = 40f;           // Ingezoomd gezichtsveld bij het richten.
    public float aimSpeed = 10f;         // Hoe snel de camera in- en uitzoomt.
    private bool isAiming = false;

    [Header("Terugslag (Recoil)")]
    public float recoilAmount = 2f;       // Hoeveel graden de camera omhoog schopt bij elk schot.
    public float recoilRecoverySpeed = 5f; // Hoe snel de camera weer automatisch naar beneden herstelt.
    private float currentRecoil = 0f;
    private Transform cameraTransform;

    // Interne variabelen voor de Hitmarker (rood kruisje in beeld bij treffer)
    private bool hitRegistered = false;
    private bool headshotRegistered = false;
    private float hitmarkerTimer = 0f;
    private float hitmarkerDuration = 0.3f;

    private float nextTimeToFire = 0f;   // Houdt bij wanneer er weer geschoten mag worden.

    // Variabelen om het wapen muurvast in de hand te vergrendelen
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Transform parentHand;

    void Start()
    {
        // Sla de beginpositie en rotatie van het wapen in de handpalm op bij de start.
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
        parentHand = transform.parent;

        cameraTransform = mainCamera != null ? mainCamera.transform : null;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && hitSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void LateUpdate()
    {
        // EXAMEN TIP (Wapen Vergrendeling):
        // LateUpdate draait helemaal aan het einde van de frame. Mocht de ragdoll-fysica of een loop-animatie
        // proberen het geweer uit positie te drukken, dan zetten wij hem hier wiskundig precies terug op zijn plek!
        if (parentHand != null && transform.parent == parentHand)
        {
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
        }
    }

    void Update()
    {
        // Doe niets als het spel gepauzeerd is of nog niet gestart.
        if (Time.timeScale == 0f) return;

        // 1. Richten: Controleer of de rechtermuisknop ('Fire2') ingedrukt wordt gehouden.
        isAiming = Input.GetButton("Fire2");

        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
            playerAnimator.SetBool("Aiming", isAiming);

        // 2. Camera FOV Interpolatie (EXAMEN TIP):
        // Mathf.Lerp berekent vloeiend tussen de huidige FOV en de doel-FOV. Dit zorgt voor een zachte zoom.
        if (mainCamera != null)
        {
            float targetFOV = isAiming ? aimFOV : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * aimSpeed);
        }

        // 3. Recoil Herstel: Als de camera door terugslag omhoog staat, zak dan langzaam terug.
        if (currentRecoil > 0f)
        {
            float recoilThisFrame = Mathf.Min(currentRecoil, recoilRecoverySpeed * Time.deltaTime);
            currentRecoil -= recoilThisFrame;
            AstronautController controller = GetComponentInParent<AstronautController>();
            if (controller != null) controller.AddRecoil(-recoilThisFrame);
        }

        // 4. Hitmarker timer aftellen
        if (hitmarkerTimer > 0f)
            hitmarkerTimer -= Time.deltaTime;
        else
        {
            hitRegistered = false;
            headshotRegistered = false;
        }

        // 5. Schieten: Als op linkermuisknop ('Fire1') wordt gedrukt én de vuursnelheid-timer is verstreken.
        if (Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + fireRate; // Zet de volgende toegestane schiettijd vast.
            Shoot();
        }
    }

    void Shoot()
    {
        // 1. Speel visuele effecten en geluiden af
        if (muzzleFlash != null) muzzleFlash.Play();
        if (shootSound != null) shootSound.Play();
        SFXManager.Instance?.PlayLaser();

        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetTrigger("Attack");
        }

        // 2. Voeg terugslag toe aan de camera
        ApplyRecoil();

        // 3. Raycast Richting Bepalen (EXAMEN TIP):
        // We schieten een onzichtbare laserlijn (Ray) vanuit exact het midden van de camera/scherm (0.5, 0.5).
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint = ray.GetPoint(range); // Standaard doelwit op 100 meter afstand als we niets raken.

        // Haal alle objecten op die de raycast raakt binnen het bereik.
        RaycastHit[] hits = Physics.RaycastAll(ray, range);
        float closestDistance = Mathf.Infinity;

        // Zoek het dichtstbijzijnde geraakte object (negeer de speler zelf).
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.CompareTag("Player") || hit.transform.GetComponentInParent<AstronautController>() != null) continue;
            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                targetPoint = hit.point; // Dit is de exacte 3D-coördinaat waar ons richtkruis naar wijst!
            }
        }

        // 4. Kogel spawnen en richting het doelwit sturen
        Vector3 direction = targetPoint - firePoint.position;

        if (bulletPrefab != null && firePoint != null)
        {
            GameObject currentBullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            currentBullet.transform.forward = direction.normalized; // Draai kogel met de neus naar het doel.

            Bullet bulletScript = currentBullet.GetComponent<Bullet>();
            if (bulletScript == null) bulletScript = currentBullet.AddComponent<Bullet>();

            bulletScript.speed = bulletSpeed;
            bulletScript.damage = damage;
            bulletScript.shootingSystem = this;
        }
    }

    void ApplyRecoil()
    {
        if (cameraTransform == null) return;
        currentRecoil += recoilAmount;
        AstronautController controller = GetComponentInParent<AstronautController>();
        if (controller != null)
        {
            controller.AddRecoil(recoilAmount);
        }
        else
        {
            cameraTransform.localEulerAngles -= new Vector3(recoilAmount, 0f, 0f);
        }
    }

    // Wordt aangeroepen door Bullet.cs wanneer een kogel een vijand raakt om het hitmarker kruisje te tonen.
    public void RegisterHit(bool isHeadshot = false)
    {
        hitRegistered = true;
        headshotRegistered = isHeadshot;
        hitmarkerTimer = hitmarkerDuration;

        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    // Tekent de crosshair (richtkruis) en hitmarker op het scherm.
    void OnGUI()
    {
        if (Time.timeScale == 0f) return;

        float size = isAiming ? 16f : 24f;
        float x = (Screen.width - size) / 2f;
        float y = (Screen.height - size) / 2f;

        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);

        // Teken een rood (of geel bij headshot) kruisje als we net iets geraakt hebben!
        if (hitRegistered)
        {
            GUI.color = headshotRegistered ? Color.yellow : Color.red;
            float hitSize = size * 1.5f;
            float hx = (Screen.width - hitSize) / 2f;
            float hy = (Screen.height - hitSize) / 2f;
            GUI.DrawTexture(new Rect(hx, hy, hitSize, hitSize), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}
