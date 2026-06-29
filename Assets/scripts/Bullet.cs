using UnityEngine; // Importeert Unity Engine functionaliteit.

/* =====================================================================================================================
 * SCRIPT: Bullet.cs
 * DOEL: Beheert het vliegen en botsen van afgeschoten kogels in de 3D ruimte.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Continuous Raycast Collision Detection: Snelle 3D objecten (zoals kogels) vliegen zo snel dat ze bij normale
 *    physics in 1 frame dóór een dunne muur of monster heen kunnen springen (tunneling effect). Om dit 100% te 
 *    voorkomen, trekken wij in Update() elke frame een wiskundige laserlijn tussen de vorige positie en de nieuwe
 *    positie. Raakt die lijn iets? Dan registreren we direct een treffer!
 * 2. Headshot Multiplier: We controleren via 'col.GetComponent<HeadshotCollider>()' of de kogel exact het hoofdbotje
 *    van het monster raakte. Zo ja, dan doen we 2x zoveel schade en kleurt het richtkruis geel!
 * ===================================================================================================================== */

public class Bullet : MonoBehaviour
{
    [Header("Kogel Instellingen")]
    public float speed = 50f;            // Vliegsnelheid in meters per seconde.
    public int damage = 25;              // Schade die de kogel toebrengt bij impact.
    public float lifetime = 3f;          // Maximale levensduur in seconden (daarna verdwijnt hij automatisch).
    public GameObject impactEffect;      // Vonkjes of stof-effect bij het raken van een muur of vloer.

    [HideInInspector] public ShootingSystem shootingSystem; // Referentie naar het geweer dat deze kogel afschoot.

    private Vector3 previousPosition;    // Houdt de exacte 3D positie van de vorige frame bij voor de laser-meting.

    // Start() draait op het exacte moment dat de kogel uit de loop komt.
    void Start()
    {
        previousPosition = transform.position; // Sla de startpositie op.

        // Schakel normale physics zwaartekracht uit op dit object.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Schakel fysieke colliders uit op de kogel zelf, omdat wij botsingen wiskundig berekenen via Raycasts.
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        // Visuele correctie: Sommige 3D kogel-modellen staan verticaal rechtop opgebouwd.
        // We kantelen alle visuele onderdelen 90 graden zodat de punt van de kogel netjes naar voren wijst.
        foreach (Transform child in transform)
        {
            child.localRotation = Quaternion.Euler(90f, 0f, 0f) * child.localRotation;
        }

        // Plan de vernietiging van de kogel over 3 seconden in om geheugenlekken te voorkomen als hij in de lucht verdwijnt.
        Destroy(gameObject, lifetime);
    }

    // Update() berekent elke frame de nieuwe vliegpositie en voert de botsingsdetectie uit.
    void Update()
    {
        // Bereken hoe ver de kogel deze frame verplaatst (snelheid * tijd per frame).
        float movementStep = speed * Time.deltaTime;
        Vector3 currentDirection = transform.forward; // De vliegrichting naar voren.
        Vector3 nextPosition = transform.position + currentDirection * movementStep; // De berekende nieuwe coördinaat.

        // Meet de exacte afstand tussen waar we stonden en waar we naartoe gaan.
        float rayDistance = Vector3.Distance(previousPosition, nextPosition);

        // EXAMEN TIP (Continuous Collision Check):
        // Schiet een Raycast (laserlijn) van de oude naar de nieuwe positie over precies 'rayDistance' lengte.
        if (Physics.Raycast(previousPosition, currentDirection, out RaycastHit hit, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            // Veiligheidscheck: Als we per ongeluk de speler zelf raken, negeer dat dan en vlieg gewoon verder!
            if (hit.collider.GetComponent<AstronautController>() != null || hit.collider.GetComponentInParent<AstronautController>() != null)
            {
                previousPosition = nextPosition;
                transform.position = nextPosition;
                return;
            }

            // We hebben een muur of monster geraakt! Voer schade uit en stop.
            HandleHit(hit.collider, hit.point);
            return;
        }

        // Niets geraakt? Schuif de kogel naar de nieuwe positie en onthoud deze positie voor de volgende frame.
        previousPosition = nextPosition;
        transform.position = nextPosition;
    }

    // Verwerkt de schade en visuele effecten bij impact.
    void HandleHit(Collider col, Vector3 hitPoint)
    {
        // Zoek of het geraakte object een HealthSystem heeft (rechtstreeks of op zijn ouder-object).
        HealthSystem health = col.GetComponent<HealthSystem>();
        if (health == null) health = col.GetComponentInParent<HealthSystem>();

        // Controleer of de collider een speciaal HeadshotCollider component heeft.
        HeadshotCollider headshot = col.GetComponent<HeadshotCollider>();

        bool isHeadshot = false;
        int finalDamage = damage; // Begin met de standaard schade.

        // Als we een headshot collider raken...
        if (headshot != null && col.gameObject != col.transform.root.gameObject)
        {
            isHeadshot = true;
            // Vermenigvuldig de schade met de headshot factor (bijv. 25 * 2 = 50 damage!).
            finalDamage = damage * headshot.headshotMultiplier;
        }

        // Als we een levend wezen raakten...
        if (health != null && !health.isDead)
        {
            health.TakeDamage(finalDamage); // Breng de schade toe aan de alien.
            if (shootingSystem != null) shootingSystem.RegisterHit(isHeadshot); // Meld aan het geweer dat we raakten (voor de rood/gele hitmarker).
        }

        // Spawn eventuele vonkjes of stofwolkjes op het exacte trefferpunt.
        if (impactEffect != null) Instantiate(impactEffect, hitPoint, Quaternion.identity);
        
        // Vernietig de kogel direct na impact.
        Destroy(gameObject);
    }
}
