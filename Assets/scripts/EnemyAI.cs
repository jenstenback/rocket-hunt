using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/* =====================================================================================================================
 * SCRIPT: EnemyAI.cs
 * DOEL: De Kunstmatige Intelligentie (AI) en State Machine van de monsters (Patrouilleren, Achtervolgen, Aanvallen).
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Finite State Machine (FSM): Het monster kiest elke frame wat hij doet op basis van afstand tot de speler:
 *    - Is de speler dichtbij (<= attackRange)? -> Aanvallen (AttackPlayer).
 *    - Is de speler in het zicht (<= chaseRange)? -> Achtervolgen (ChasePlayer).
 *    - Is de speler ver weg? -> Patrouilleren langs waypoints (Patrol).
 * 2. NavMeshAgent: Wij gebruiken Unity's ingebouwde NavMesh (Navigation Mesh) pathfinding. Hierdoor lopen monsters 
 *    automatisch slim om muren en obstakels heen in plaats van blind door muren te duwen.
 * 3. Anti-Zweef Raycast: Bij het inladen schiet het monster een laserstraal naar beneden om de exacte hoogte van de 
 *    vloer te vinden, zodat hij nooit in de lucht blijft hangen.
 * ===================================================================================================================== */

[RequireComponent(typeof(NavMeshAgent))] // Zorgt dat Unity automatisch een NavMeshAgent component toevoegt.
public class EnemyAI : MonoBehaviour
{
    [Header("State Instellingen (AI Afstanden & Snelheden)")]
    public float chaseRange = 500f;      // Hoe ver het monster kan 'kijken' om de speler te spotten (500m = hele map).
    public float attackRange = 3.5f;     // Op welke afstand het monster begint met slaan/bijten.
    public float patrolSpeed = 2.5f;     // Rustige wandelsnelheid.
    public float chaseSpeed = 6.5f;      // Aggressieve rensnelheid tijdens achtervolging.

    [Header("Patrouille Waypoints")]
    public Transform[] waypoints;        // Punten in de map waar het monster langs loopt als hij de speler niet ziet.
    private int currentWaypointIndex = 0;

    [Header("Aanval Instellingen")]
    public int attackDamage = 35;        // Hoeveel HP van de speler afgaat per hit (wordt verdubbeld per wave!).
    public float attackCooldown = 1.2f;  // Tijd tussen aanvallen (voorkomt dat hij 60x per seconde slaat).
    public float attackDelay = 0.4f;     // Vertraging zodat de schade precies klopt met de klap-animatie van de arm.
    private float lastAttackTime = 0f;

    [Header("Referenties")]
    public Animator animator;            // Stuurt de ren- en aanvalanimaties aan.
    
    private NavMeshAgent agent;          // De Unity pathfinding component die looproutes berekent.
    private Transform player;            // Referentie naar de speler in de scene.
    private HealthSystem healthSystem;   // Referentie naar het eigen levenssysteem.

    void Start()
    {
        // 1. Veiligheidschecks: Zorg dat statistieken altijd sterk genoeg staan ingesteld.
        if (chaseRange < 45f) chaseRange = 500f;
        if (attackRange < 3f) attackRange = 3.5f;
        if (attackDamage < 30) attackDamage = 35;
        if (attackCooldown > 1.5f) attackCooldown = 1.2f;

        // 2. Zorg dat het monster een goede CapsuleCollider heeft zodat kogels hem raken.
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

        agent = GetComponent<NavMeshAgent>();
        healthSystem = GetComponent<HealthSystem>();
        
        if (agent != null && !agent.enabled) agent.enabled = true;

        // 3. EXAMEN TIP (Vloer Detectie / Anti-Zweef):
        // Om te voorkomen dat monsters boven de grond zweven, schieten we een Raycast recht naar beneden.
        // We verplaatsen (Warp) het monster vervolgens exact naar de gevonden NavMesh vloer!
        Vector3 rayStart = transform.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float lowestY = float.MaxValue;
        bool foundFloor = false;
        foreach (RaycastHit hit in hits)
        {
            // Negeer speler, andere monsters en muntjes
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<AstronautController>() != null) continue;
            if (hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
            if (hit.collider.GetComponentInParent<CoinPickup>() != null) continue;
            if (hit.point.y < lowestY)
            {
                lowestY = hit.point.y;
                foundFloor = true;
            }
        }
        if (foundFloor && lowestY < transform.position.y + 3f)
        {
            transform.position = new Vector3(transform.position.x, lowestY, transform.position.z);
        }
        
        if (agent != null && agent.enabled)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 50f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position); // Klikt hem vast op het looprooster.
            }
        }

        // 4. Zoek automatisch de speler in het level via Tag of Controller.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) player = playerObj.transform;

        // Luister naar het OnDeath event: als we sterven, roep OnEnemyDeath aan.
        if (healthSystem != null) healthSystem.OnDeath.AddListener(OnEnemyDeath);

        if (chaseRange < 100f) chaseRange = 500f; // Garandeer dat hij je overal hoort.

        // Begin met lopen
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

    void Update()
    {
        // Als de speler dood is of dit monster zelf dood is, doe niets.
        if (player == null || (healthSystem != null && healthSystem.isDead)) return;
        if (agent == null || !agent.enabled) return;

        // Meet de exacte afstand (in meters) tussen het monster en de speler.
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // DE FINITE STATE MACHINE LOGICA (EXAMEN TIP):
        if (distanceToPlayer <= attackRange + 0.5f)
        {
            AttackPlayer(); // Staat 1: Aanvallen
        }
        else if (distanceToPlayer <= chaseRange)
        {
            ChasePlayer();  // Staat 2: Achtervolgen
        }
        else
        {
            Patrol();       // Staat 3: Patrouilleren
        }

        UpdateAnimation();  // Update loopanimatie op basis van snelheid.
    }

    void Patrol()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (waypoints == null || waypoints.Length == 0) return;

        // Als we bij het huidige waypoint zijn aangekomen (< 0.5m), ga naar de volgende.
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            if (waypoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(waypoints[currentWaypointIndex].position);
            }
        }
    }

    void ChasePlayer()
    {
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(player.position); // Vertel de NavMeshAgent om naar de speler te rennen!
    }

    void AttackPlayer()
    {
        agent.isStopped = true; // Stop met lopen om te slaan.
        
        // Draai het gezicht van het monster langzaam naar de speler toe.
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);

        // Controleer of de cooldown (1.2 sec) verstreken is.
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("Attack"); // Speel klap animatie.

            StartCoroutine(DealDamageAfterDelay()); // Wacht kort op de animatie voordat HP afgaat.
        }
    }

    // Coroutine: wacht op het exacte moment dat de klauwen/handen van de alien de speler raken.
    private IEnumerator DealDamageAfterDelay()
    {
        yield return new WaitForSeconds(attackDelay);

        if (healthSystem != null && healthSystem.isDead) yield break;
        if (player == null) yield break;

        // Controleer of de speler niet stiekem is weggerend tijdens de animatie!
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange * 1.5f + 1f)
        {
            HealthSystem playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<HealthSystem>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage); // Haal HP van de speler af!
            }
        }
    }

    void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat("Speed", agent.velocity.magnitude); // Koppel rensnelheid aan animatie parameter.
    }

    void OnEnemyDeath()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false; // Stop pathfinding.
        }

        if (animator != null) animator.enabled = false; // Zet animator uit voor Ragdoll effect.

        this.enabled = false; // Schakel dit AI script uit.
        StartCoroutine(FallOverRoutine()); // Val omver.
    }

    private IEnumerator FallOverRoutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(-90f, 0f, 0f); // Kantel 90 graden naar achter
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos - new Vector3(0f, 0.5f, 0f);

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
