using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/* ==========================================================================================
 * SCRIPT: EnemyAI.cs
 * DOEL: Kunstmatige Intelligentie (AI) en gedragspatronen van de monsters.
 * ========================================================================================== */

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("State Settings")]
    public float chaseRange = 50f;
    public float attackRange = 3.5f;
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 6.5f;

    [Header("Patrol Waypoints")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Attack Settings")]
    public int attackDamage = 35;
    public float attackCooldown = 1.2f;
    public float attackDelay = 0.4f;
    private float lastAttackTime = 0f;

    [Header("References")]
    public Animator animator;
    
    private NavMeshAgent agent;
    private Transform player;
    private HealthSystem healthSystem;

    void Start()
    {
        // Garandeer sterkere stats en verdere range voor alle bestaande monsters in de scene
        if (chaseRange < 45f) chaseRange = 50f;
        if (attackRange < 3f) attackRange = 3.5f;
        if (attackDamage < 30) attackDamage = 35;
        if (attackCooldown > 1.5f) attackCooldown = 1.2f;

        // Zorg voor een fatsoenlijke collider
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
        
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }

        // CRITICAL FIX VOOR ZWEVENDE ALIEN:
        // Raycast eerst vanaf hoog boven naar beneden om de echte vloer te vinden (negeer spelers en andere aliens)
        Vector3 rayStart = transform.position + Vector3.up * 10f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float lowestY = float.MaxValue;
        bool foundFloor = false;
        foreach (RaycastHit hit in hits)
        {
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
        
        // Warp vervolgens de agent naar de dichtstbijzijnde NavMesh positie
        if (agent != null && agent.enabled)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 50f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }
        }

        // Zoek de speler
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj == null)
        {
            CharacterController cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) playerObj = cc.gameObject;
        }

        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (healthSystem != null)
        {
            healthSystem.OnDeath.AddListener(OnEnemyDeath);
        }

        if (chaseRange < 100f) chaseRange = 500f; // Altijd achtervolgen

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
        if (player == null || (healthSystem != null && healthSystem.isDead)) 
            return;
        if (agent == null || !agent.enabled) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange + 0.5f)
        {
            AttackPlayer();
        }
        else if (distanceToPlayer <= chaseRange)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }

        UpdateAnimation();
    }

    void Patrol()
    {
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (waypoints == null || waypoints.Length == 0) return;

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
        agent.SetDestination(player.position);
    }

    void AttackPlayer()
    {
        agent.isStopped = true;
        
        Vector3 direction = (player.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8f);

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("Attack");

            StartCoroutine(DealDamageAfterDelay());
        }
    }

    private IEnumerator DealDamageAfterDelay()
    {
        yield return new WaitForSeconds(attackDelay);

        if (healthSystem != null && healthSystem.isDead) yield break;
        if (player == null) yield break;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange * 1.5f + 1f)
        {
            HealthSystem playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInParent<HealthSystem>();
            if (playerHealth == null) playerHealth = player.GetComponentInChildren<HealthSystem>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }
    }

    void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    void OnEnemyDeath()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.enabled = false;
        }

        this.enabled = false;
        StartCoroutine(FallOverRoutine());
    }

    private IEnumerator FallOverRoutine()
    {
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(-90f, 0f, 0f);
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
