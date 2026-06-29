using UnityEngine;       // Importeert Unity Engine basisbibliotheek.
using UnityEngine.AI;     // Importeert NavMeshAgent logica om monsters automatisch looproutes te laten berekenen.
using System.Collections; // Importeert IEnumerators en Coroutines.

/* =====================================================================================================================
 * SCRIPT: EnemyAI.cs
 * DOEL: Beheert de Kunstmatige Intelligentie (AI) en gedragspatronen van de vijanden in het spel via een State Machine.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Finite State Machine (FSM): Het monster kiest elke frame wat hij doet op basis van zijn afstand tot de speler:
 *    - Staat 1 (Aanvallen): Is de speler dichtbij (<= attackRange)? -> Stop met lopen en sla/bijt (AttackPlayer).
 *    - Staat 2 (Achtervolgen): Is de speler in het zicht (<= chaseRange)? -> Ren achter de speler aan (ChasePlayer).
 *    - Staat 3 (Patrouilleren): Is de speler ver weg? -> Wandel rustig langs ingestelde waypoints (Patrol).
 * 2. NavMeshAgent: We gebruiken Unity's ingebouwde NavMesh pathfinding. Het monster berekent automatisch de slimste
 *    route om obstakels en muren heen, in plaats van vast te lopen tegen muren.
 * 3. Anti-Zweef Raycast: Om te voorkomen dat monsters in de lucht spawnen of hangen, schiet het script bij start een
 *    laserstraal naar beneden om de exacte hoogte van de vloer te vinden.
 * ===================================================================================================================== */

[RequireComponent(typeof(NavMeshAgent))] // Verplicht Unity om automatisch een NavMeshAgent aan het monster toe te voegen.
public class EnemyAI : MonoBehaviour
{
    [Header("State Instellingen (AI Afstanden & Snelheden)")]
    public float chaseRange = 500f;      // Het zichtbereik van het monster. 500 meter betekent dat hij je over de hele map hoort/ziet.
    public float attackRange = 3.5f;     // De afstand waarop het monster begint met zijn aanval.
    public float patrolSpeed = 2.5f;     // Rustige wandelsnelheid tijdens het patrouilleren.
    public float chaseSpeed = 6.5f;      // Agressieve rensnelheid wanneer hij achter de speler aan jaagt.

    [Header("Patrouille Waypoints")]
    public Transform[] waypoints;        // Array van coördinaten (Transform) waar het monster heen wandelt als hij rustig is.
    private int currentWaypointIndex = 0;// Houdt bij naar welk waypoint hij momenteel onderweg is.

    [Header("Aanval Instellingen")]
    public int attackDamage = 35;        // Hoeveel schade hij doet aan de speler. (Wordt verdubbeld per nieuwe wave!).
    public float attackCooldown = 1.2f;  // Minimale wachttijd tussen twee slagen in.
    public float attackDelay = 0.4f;     // Vertraging in seconden zodat de schade exact samenvalt met de klap-animatie van de arm.
    private float lastAttackTime = 0f;   // Slaat op wanneer de laatste aanval heeft plaatsgevonden.

    [Header("Referenties")]
    public Animator animator;            // Referentie naar de Animator om ren- en slaan-animaties af te spelen.
    
    private NavMeshAgent agent;          // De pathfinding component die berekent hoe het monster loopt.
    private Transform player;            // Referentie naar het 3D-object van de speler.
    private HealthSystem healthSystem;   // Referentie naar het levenssysteem van dit monster.

    // Start() draait exact één keer bij het spawnen van het monster.
    void Start()
    {
        // 1. Veiligheidschecks: Zorg dat monsterafstanden en schades nooit per ongeluk te laag staan.
        if (chaseRange < 45f) chaseRange = 500f;
        if (attackRange < 3f) attackRange = 3.5f;
        if (attackDamage < 30) attackDamage = 35;
        if (attackCooldown > 1.5f) attackCooldown = 1.2f;

        // 2. Controleer of het monster een goede CapsuleCollider heeft zodat kogels hem kunnen raken.
        CapsuleCollider existingCapsule = GetComponent<CapsuleCollider>();
        if (existingCapsule != null && existingCapsule.height < 1.5f)
        {
            existingCapsule.height = 2f;
            existingCapsule.radius = 0.5f;
            existingCapsule.center = new Vector3(0, 1f, 0);
        }
        else if (existingCapsule == null && GetComponent<Collider>() == null)
        {
            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.5f;
            col.center = new Vector3(0, 1f, 0);
        }

        // Haal de interne componenten op.
        agent = GetComponent<NavMeshAgent>();
        healthSystem = GetComponent<HealthSystem>();
        
        // Zet de NavMeshAgent aan als hij uit stond.
        if (agent != null && !agent.enabled) agent.enabled = true;

        // 3. EXAMEN TIP (Vloer Detectie & Anti-Zweef):
        // Schiet een Raycast recht naar beneden vanaf 10 meter boven het monster om de echte speelvloer te vinden.
        Vector3 rayStart = transform.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float lowestY = float.MaxValue;
        bool foundFloor = false;

        foreach (RaycastHit hit in hits)
        {
            // Negeer botsingen met speler, muntjes of andere monsters. We zoeken puur de grond/omgeving!
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<AstronautController>() != null) continue;
            if (hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hit.collider.GetComponentInParent<CoinPickup>() != null) continue;
            if (hit.point.y < lowestY)
            {
                lowestY = hit.point.y; // Sla de laagst gevonden vloerhoogte op.
                foundFloor = true;
            }
        }

        // Als we een vloer hebben gevonden én die is redelijk dichtbij...
        if (foundFloor && lowestY < transform.position.y + 3f)
        {
            // Zet het monster qua Y-coördinaat exact op de vloerhoogte.
            transform.position = new Vector3(transform.position.x, lowestY, transform.position.z);
        }
        
        // Klik (Warp) de NavMeshAgent vast op het dichtstbijzijnde geldige looprooster (NavMesh).
        if (agent != null && agent.enabled)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 50f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }
        }

        // 4. Zoek automatisch naar de speler in de scene via zijn "Player" tag.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) player = playerObj.transform;

        // Koppel een luisteraar aan het OnDeath event van het HealthSystem. Als hij sterft, roep OnEnemyDeath aan.
        if (healthSystem != null) healthSystem.OnDeath.AddListener(OnEnemyDeath);

        if (chaseRange < 100f) chaseRange = 500f; // Garandeer dat monsters je over de hele map najagen.

        // Bepaal startgedrag: als er waypoints zijn, ga patrouilleren. Zo niet, ga direct achtervolgen.
        if (agent != null && agent.enabled)
        {
            if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                agent.speed = patrolSpeed;
                agent.SetDestination(waypoints[0].position);
            }
            else
            {
                agent.speed = chaseSpeed;
            }
        }
    }

    // Update() draait elke frame om de afstand tot de speler te meten en beslissingen te nemen.
    void Update()
    {
        // Als de speler niet bestaat, of dit monster zelf al dood is, voer dan geen AI logica uit.
        if (player == null || (healthSystem != null && healthSystem.isDead)) return;
        if (agent == null || !agent.enabled) return;

        // Meet de exacte 3D afstand in meters tussen het monster en de speler.
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // DE FINITE STATE MACHINE LOGICA (EXAMEN TIP):
        if (distanceToPlayer <= attackRange + 0.5f)
        {
            AttackPlayer(); // Staat 1: Speler binnen bereik -> Aanvallen!
        }
        else if (distanceToPlayer <= chaseRange)
        {
            ChasePlayer();  // Staat 2: Speler gezien -> Ren erachteraan!
        }
        else
        {
            Patrol();       // Staat 3: Geen speler -> Wandel rustig rond.
        }

        UpdateAnimation();  // Update de loopanimatie op basis van de actuele bewegingssnelheid.
    }

    // Laat het monster rustig van waypoint naar waypoint wandelen.
    void Patrol()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false; // Zorg dat hij mag lopen.

        if (waypoints == null || waypoints.Length == 0) return;

        // Als het monster bijna bij zijn doel-waypoint is (< 0.5m)...
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // Kies het volgende waypoint (en begin weer bij 0 als we bij het laatste waypoint zijn via modulo '%').
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            if (waypoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(waypoints[currentWaypointIndex].position);
            }
        }
    }

    // Vertel de NavMeshAgent om op hoge snelheid direct naar de positie van de speler te rennen.
    void ChasePlayer()
    {
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(player.position); // Update het doel naar de spelercoördinaten.
    }

    // Beheert de aanvalsprocedure.
    void AttackPlayer()
    {
        agent.isStopped = true; // Stop direct met lopen, anders duwt het monster de speler door muren heen.
        
        // Bepaal de kijkrichting naar de speler toe (negeer Y-as zodat hij niet schuin omhoog/omlaag kantelt).
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // Slerp draait de neus van het monster vloeiend naar de speler toe.
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);

        // Controleer of de wachttijd (cooldown) verstreken is sinds de vorige klap.
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time; // Sla de huidige tijd op als nieuwe laatste aanvalstijd.
            
            // Speel de sla/bijt animatie af.
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("Attack");

            // Start de Coroutine die na een korte vertraging (0.4s) pas de echte schade toepast.
            StartCoroutine(DealDamageAfterDelay());
        }
    }

    // Coroutine die wacht op het exacte moment dat de klauw van het monster de speler raakt.
    private IEnumerator DealDamageAfterDelay()
    {
        // Wacht 'attackDelay' seconden (bijvoorbeeld 0.4s) op de animatie.
        yield return new WaitForSeconds(attackDelay);

        if (healthSystem != null && healthSystem.isDead) yield break; // Als het monster intussen stierf, doe dan geen schade.
        if (player == null) yield break;

        // Controleer of de speler niet intussen snel is weggerend!
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange * 1.5f + 1f)
        {
            // Zoek het HealthSystem van de speler en breng de schade toe!
            HealthSystem playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<HealthSystem>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }
    }

    // Stuur de actuele snelheid van de NavMeshAgent door naar de Animator.
    void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat("Speed", agent.velocity.magnitude); // magnitude is de lengte van de snelheidsvector.
    }

    // Wordt aangeroepen door HealthSystem zodra dit monster sterft.
    void OnEnemyDeath()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false; // Schakel pathfinding uit.
        }

        if (animator != null) animator.enabled = false; // Zet Animator uit zodat Ragdoll of val-animatie het overneemt.

        this.enabled = false;              // Schakel dit AI-script uit.
        StartCoroutine(FallOverRoutine()); // Kantel het lijk omver.
    }

    // Kantel het monster in 0.4 seconden 90 graden achterover zodat het levenloos op de grond ligt.
    private IEnumerator FallOverRoutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(-90f, 0f, 0f); // 90 graden kantelen.
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos - new Vector3(0f, 0.5f, 0f); // Laat hem iets zakken in de vloer.

        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.4f;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }
}
