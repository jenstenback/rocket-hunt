using UnityEngine;

// Dit script kan op een apart child-object van het hoofd zitten,
// OF direct op het enemy root-object.
// Als het op het root-object zit, handelt Bullet.cs de headshot-logica af.

public class HeadshotCollider : MonoBehaviour
{
    [Header("Headshot Instellingen")]
    public int headshotMultiplier = 2;

    private HealthSystem parentHealth;
    private ShootingSystem shootingSystem;

    void Start()
    {
        // Zoek het HealthSystem in de parent
        parentHealth = GetComponentInParent<HealthSystem>();

        // Zoek de speler — eerst op tag, dan op component
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) player = ac.gameObject;
        }

        if (player != null)
            shootingSystem = player.GetComponentInChildren<ShootingSystem>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Controleer of het een kogel is
        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet == null) return;

        // Als dit script op hetzelfde object zit als het HealthSystem,
        // dan handelt Bullet.cs het al af. Niet dubbel doen!
        if (GetComponent<HealthSystem>() != null) return;

        // Dit is een apart child-object (hoofd) — wij handelen de headshot af
        if (parentHealth != null && !parentHealth.isDead)
        {
            int headshotDamage = bullet.damage * headshotMultiplier;
            parentHealth.TakeDamage(headshotDamage);

            if (shootingSystem != null)
                shootingSystem.RegisterHit(true);

            Debug.Log("HEADSHOT! " + headshotDamage + " damage!");
        }

        Destroy(other.gameObject);
    }
}
