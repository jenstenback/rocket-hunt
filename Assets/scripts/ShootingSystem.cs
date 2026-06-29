using UnityEngine;

/* ==========================================================================================
 * SCRIPT: ShootingSystem.cs
 * DOEL: Beheert wapenacties, richten (aiming), terugslag (recoil) en hit-registratie.
 * UITLEG VOOR EXAMEN/PRESENTATIE:
 * Dit script combineert 'Raycasting' met fysieke kogel-tracers. Bij het vuren schieten we
 * een Raycast vanuit het midden van het scherm (ViewportPointToRay 0.5, 0.5) om exact
 * te bepalen waar de kogel heen moet vliegen. Verder gebruiken we Camera Field of View (FOV)
 * interpolatie (Mathf.Lerp) om een vloeiende GTA/Over-the-Shoulder aim-zoom te creëren.
 * ========================================================================================== */

public class ShootingSystem : MonoBehaviour
{
    [Header("Gun Settings")]
    public int damage = 25;
    public float range = 100f;
    public float fireRate = 0.5f;

    [Header("Gun References")]
    public Camera mainCamera;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float bulletSpeed = 50f;

    [Header("Effects & Animation")]
    public ParticleSystem muzzleFlash;
    public AudioSource shootSound;
    public AudioClip hitSound;           // "Tik" geluid bij treffer
    private AudioSource audioSource;
    public Animator playerAnimator;

    [Header("Aiming (GTA Style)")]
    public float normalFOV = 60f;
    public float aimFOV = 40f;
    public float aimSpeed = 10f;
    private bool isAiming = false;

    [Header("Recoil (Terugslag)")]
    public float recoilAmount = 2f;       // Hoeveel graden de camera omhoog schopt
    public float recoilRecoverySpeed = 5f; // Hoe snel hij weer recht gaat
    private float currentRecoil = 0f;
    private Transform cameraTransform;

    // Hitmarker systeem
    private bool hitRegistered = false;    // Was er een treffer?
    private bool headshotRegistered = false; // Was het een headshot?
    private float hitmarkerTimer = 0f;
    private float hitmarkerDuration = 0.3f; // Hoe lang het rode/gele kruisje zichtbaar is

    private float nextTimeToFire = 0f;

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Transform parentHand;

    void Start()
    {
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
        // Garandeer dat het wapen ROTSVAST in de rechterhand blijft en nooit kan wegstuiten of zweven
        if (parentHand != null && transform.parent == parentHand)
        {
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
        }
    }

    void Update()
    {
        // Doe niets als de game gepauzeerd is of nog niet is gestart
        if (Time.timeScale == 0f) return;

        // 1. Richten (Rechtermuisknop ingedrukt houden)
        isAiming = Input.GetButton("Fire2");

        // --- FIX 5: Animator check zonder try-catch ---
        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
            playerAnimator.SetBool("Aiming", isAiming);

        // 2. Camera soepel in- en uitzoomen
        if (mainCamera != null)
        {
            float targetFOV = isAiming ? aimFOV : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, Time.deltaTime * aimSpeed);
        }

        // 3. Recoil herstel (camera gaat langzaam terug naar normaal na terugslag)
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

        // 5. Schieten (Linkermuisknop)
        if (Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        // 1. Effecten en animatie
        if (muzzleFlash != null) muzzleFlash.Play();
        if (shootSound != null) shootSound.Play();
        SFXManager.Instance?.PlayLaser();

        // --- FIX 5: Animatie zonder try-catch ---
        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetTrigger("Attack");
        }

        // 2. Recoil: schop camera omhoog
        ApplyRecoil();

        // 3. Bereken doelwit (negeert de speler zelf)
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Schiet altijd recht naar het midden van het scherm (richtkruis), ook zonder richten!
        Vector3 targetPoint = ray.GetPoint(range);

        RaycastHit[] hits = Physics.RaycastAll(ray, range);
        float closestDistance = Mathf.Infinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.CompareTag("Player") || hit.transform.GetComponentInParent<AstronautController>() != null) continue;
            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                targetPoint = hit.point;
            }
        }

        // 4. Kogel aanmaken en afvuren
        Vector3 direction = targetPoint - firePoint.position;

        if (bulletPrefab != null && firePoint != null)
        {
            GameObject currentBullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            currentBullet.transform.forward = direction.normalized;

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

    // Wordt aangeroepen vanuit Bullet.cs als hij iets raakt
    public void RegisterHit(bool isHeadshot = false)
    {
        hitRegistered = true;
        headshotRegistered = isHeadshot;
        hitmarkerTimer = hitmarkerDuration;

        // Speel "tik" geluid af
        if (hitSound != null && audioSource != null)
            audioSource.PlayOneShot(hitSound);
        SFXManager.Instance?.PlayHit(isHeadshot);
    }

    // Tekent de crosshair + hitmarker + headshot tekst op het scherm
    void OnGUI()
    {
        // Crosshair is altijd zichtbaar (zowel vanuit de heup als tijdens het richten!)
        float crosshairSize = isAiming ? 24f : 18f;
        float cx = (Screen.width / 2f) - (crosshairSize / 2f);
        float cy = (Screen.height / 2f) - (crosshairSize / 2f);

        GUIStyle crosshairStyle = new GUIStyle(GUI.skin.label);
        crosshairStyle.fontSize = isAiming ? 26 : 20;
        crosshairStyle.alignment = TextAnchor.MiddleCenter;
        crosshairStyle.fontStyle = FontStyle.Bold;

        // Normale kleur = groen, treffer = rood, headshot = geel
        if (hitmarkerTimer > 0f && headshotRegistered)
            crosshairStyle.normal.textColor = Color.yellow;
        else if (hitmarkerTimer > 0f && hitRegistered)
            crosshairStyle.normal.textColor = Color.red;
        else
            crosshairStyle.normal.textColor = Color.green;

        GUI.Label(new Rect(cx, cy, crosshairSize, crosshairSize), "+", crosshairStyle);

        // Headshot tekst bovenaan het scherm
        if (hitmarkerTimer > 0f && headshotRegistered)
        {
            GUIStyle headshotStyle = new GUIStyle(GUI.skin.label);
            headshotStyle.fontSize = 36;
            headshotStyle.alignment = TextAnchor.MiddleCenter;
            headshotStyle.fontStyle = FontStyle.Bold;
            headshotStyle.normal.textColor = Color.yellow;

            // Fade-out effect: tekst wordt transparanter naarmate de timer afloopt
            Color c = headshotStyle.normal.textColor;
            c.a = hitmarkerTimer / hitmarkerDuration;
            headshotStyle.normal.textColor = c;

            GUI.Label(new Rect(0, Screen.height / 2f - 80f, Screen.width, 50f), "🎯 HEADSHOT!", headshotStyle);
        }
    }
}
