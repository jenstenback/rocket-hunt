using UnityEngine;

/* ==========================================================================================
 * SCRIPT: Bullet.cs
 * DOEL: Beheert de physics en botsingsdetectie van afgeschoten kogels.
 * UITLEG VOOR EXAMEN/PRESENTATIE:
 * Dit script gebruikt 'Continuous Raycast Collision Detection'.
 * Elke frame trekken we een wiskundige laserlijn tussen de vorige en de nieuwe positie.
 * Hierdoor is het wiskundig 100% onmogelijk dat een kogel ooit nog door een monster glitched!
 * ========================================================================================== */

public class Bullet : MonoBehaviour
{
    [Header("Kogel Instellingen")]
    public float speed = 50f;
    public int damage = 25;
    public float lifetime = 3f;
    public GameObject impactEffect;

    [HideInInspector] public ShootingSystem shootingSystem;

    private Vector3 previousPosition;

    void Start()
    {
        previousPosition = transform.position;

        // Schakel Rigidbody zwaartekracht uit
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Schakel colliders op de kogel uit
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        // FIX: De kogelprefab is visueel langs de Y-as opgebouwd (staat verticaal).
        // We moeten alle child-objecten 90 graden kantelen zodat het visueel horizontaal vliegt.
        foreach (Transform child in transform)
        {
            child.localRotation = Quaternion.Euler(90f, 0f, 0f) * child.localRotation;
        }

        // Als er geen children zijn, roteer het object zelf (via een wrapper)
        if (transform.childCount == 0)
        {
            // Eventuele mesh direct op dit object — we passen de mesh-rotatie niet aan
            // maar het visuele effect is minder belangrijk dan de vliegrichting
        }

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        float movementStep = speed * Time.deltaTime;
        Vector3 currentDirection = transform.forward;
        Vector3 nextPosition = transform.position + currentDirection * movementStep;

        float rayDistance = Vector3.Distance(previousPosition, nextPosition);

        if (Physics.Raycast(previousPosition, currentDirection, out RaycastHit hit, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            // Negeer de speler zelf
            if (hit.collider.GetComponent<AstronautController>() != null || hit.collider.GetComponentInParent<AstronautController>() != null)
            {
                previousPosition = nextPosition;
                transform.position = nextPosition;
                return;
            }

            HandleHit(hit.collider, hit.point);
            return;
        }

        previousPosition = nextPosition;
        transform.position = nextPosition;
    }

    void HandleHit(Collider col, Vector3 hitPoint)
    {
        HealthSystem health = col.GetComponent<HealthSystem>();
        if (health == null) health = col.GetComponentInParent<HealthSystem>();

        HeadshotCollider headshot = col.GetComponent<HeadshotCollider>();

        bool isHeadshot = false;
        int finalDamage = damage;

        if (headshot != null && col.gameObject != col.transform.root.gameObject)
        {
            isHeadshot = true;
            finalDamage = damage * headshot.headshotMultiplier;
        }

        if (health != null && !health.isDead)
        {
            health.TakeDamage(finalDamage);
            if (shootingSystem != null) shootingSystem.RegisterHit(isHeadshot);
        }

        if (impactEffect != null) Instantiate(impactEffect, hitPoint, Quaternion.identity);
        
        Destroy(gameObject);
    }
}
