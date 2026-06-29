using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

/* =====================================================================================================================
 * SCRIPT: HealthSystem.cs
 * DOEL: Universeel levenssysteem (HP) voor zowel de Speler als alle Vijanden in het spel.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Component Hergebruik (Modulariteit): Dit ene script wordt op zowel de speler als op aliens gezet. We kijken via 
 *    'CompareTag("Player")' of het om de speler gaat of om een vijand. Dit scheelt dubbele code schrijven!
 * 2. Unity Events: Via 'OnHealthChanged' sturen we een seintje naar de User Interface zodra HP verandert. Hierdoor 
 *    hoeft dit script niet zelf UI-balkjes te tekenen, wat zorgt voor een schone architectuur.
 * 3. Automatische UI & Loot Koppeling: Als een vijand doodgaat (Die), maken we via code dynamically een muntje ('CoinPickup')
 *    aan en melden we aan de 'WaveManager' dat er een alien minder is.
 * ===================================================================================================================== */

public class HealthSystem : MonoBehaviour
{
    [Header("Gezondheid Instellingen")]
    public int maxHealth = 100;          // Het maximale aantal levenspunten dat dit object kan hebben.
    public int currentHealth;            // Het huidige aantal levenspunten.

    [Header("Visuele Effecten")]
    public GameObject deathParticlesPrefab; // Extra particle effect bij overlijden.

    [Header("Gebeurtenissen (Unity Events)")]
    public UnityEvent<int, int> OnHealthChanged; // Wordt aangeroepen (met currentHP en maxHP) als schade wordt genomen.
    public UnityEvent OnDeath;                   // Wordt aangeroepen op het moment dat currentHP op 0 belandt.

    public bool isDead = false;          // Voorkomt dat een object meerdere keren kan sterven.

    // Statische cache voor bloed-effecten zodat we ze niet elke keer opnieuw van de harde schijf hoeven te laden (Optimalisatie!)
    private static GameObject cachedBloodSpray;
    private static GameObject cachedBloodExtra;
    private static GameObject cachedBloodChunks;
    private static bool prefabsCached = false;

    private static void EnsurePrefabsCached()
    {
        if (!prefabsCached)
        {
            // Laad de prefabs vanuit de 'Resources' map in het Unity project.
            cachedBloodSpray = Resources.Load<GameObject>("BloodSprayFX");
            cachedBloodExtra = Resources.Load<GameObject>("BloodSprayFX_Extra");
            cachedBloodChunks = Resources.Load<GameObject>("ChunkParticleSystem");
            prefabsCached = true;
        }
    }

    void Start()
    {
        EnsurePrefabsCached();
        currentHealth = maxHealth; // Begin met volle gezondheid.
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Update de healthbars bij start.

        // EXAMEN TIP (Automatische UI koppeling):
        // Als dit script op een vijand zit (niet op de speler), voegen we automatisch een 3D zwevende 
        // levensbalk (EnemyHealthBar) toe boven het hoofd van het monster!
        if (GetComponent<AstronautController>() == null && !CompareTag("Player"))
        {
            if (GetComponent<EnemyHealthBar>() == null)
            {
                gameObject.AddComponent<EnemyHealthBar>();
            }
        }
    }

    // Wordt aangeroepen wanneer een kogel of vijand schade toebrengt aan dit object.
    public void TakeDamage(int amount)
    {
        if (isDead) return; // Als we al dood zijn, negeer extra schade.

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Zorgt dat HP nooit onder 0 of boven maxHealth komt.

        // Als een VIJAND geraakt wordt (dus niet de speler), spawn dan direct rode bloedspetters op de trefferplek!
        if (GetComponent<AstronautController>() == null)
        {
            EnsurePrefabsCached();
            if (cachedBloodSpray != null)
            {
                GameObject hitBlood = Instantiate(cachedBloodSpray, transform.position + Vector3.up * 1.2f, Quaternion.identity);
                EnforceBloodColor(hitBlood); // Garandeer dat het bloed rood kleurt.
                Destroy(hitBlood, 2f);       // Ruim het particle object na 2 seconden op om geheugen te besparen.
            }
        }

        // Stuur event naar de UI healthbars dat de HP is aangepast.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Check of we dood zijn gegaan door deze klap.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Wordt aangeroepen wanneer de speler een Medkit of Health Pickup oppakt.
    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        SpawnBloodSpatter(); // Grote bloedexplosie bij overlijden.
        SFXManager.Instance?.PlayAlienDeath();

        // Check of het de SPELER is die stierf
        AstronautController playerController = GetComponent<AstronautController>();
        if (playerController != null)
        {
            playerController.SetDead(); // Stop speler besturing
            StartCoroutine(PlayerDeathSequence()); // Start camera val-animatie
        }
        else
        {
            // Het is een VIJAND die sterft:
            OnDeath?.Invoke();

            // 1. DROP EEN MUNTJE ($) (EXAMEN TIP):
            // We maken dynamisch een nieuw GameObject aan op de sterfplek en geven hem het CoinPickup script.
            GameObject coinObj = new GameObject("DroppedCoin");
            coinObj.transform.position = transform.position + Vector3.up * 0.5f;
            coinObj.AddComponent<CoinPickup>();

            // 2. MELD AAN DE WAVEMANAGER DAT DEZE ALIEN DOOD IS:
            WaveManager.Instance?.OnEnemyKilled(this);

            // Schakel loop-AI (NavMeshAgent) en colliders uit zodat hij levenloos neervalt (ragdoll).
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            // Ruim het lijk na 3 seconden op uit het level.
            Destroy(gameObject, 3f);
        }
    }

    // Coroutine die de camera van de speler langzaam naar de grond laat vallen bij een Game Over.
    private System.Collections.IEnumerator PlayerDeathSequence()
    {
        Transform cam = null;
        AstronautController ac = GetComponent<AstronautController>();
        if (ac != null && ac.playerCamera != null)
        {
            cam = ac.playerCamera;
        }

        float elapsed = 0f;
        float deathDuration = 2f;
        Quaternion startRot = cam != null ? cam.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(45f, startRot.eulerAngles.y + 15f, 25f); // Schuine invalshoek

        while (elapsed < deathDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / deathDuration;
            
            if (cam != null)
            {
                cam.localRotation = Quaternion.Slerp(startRot, endRot, t);
                cam.localPosition += Vector3.down * Time.unscaledDeltaTime * 0.3f;
            }
            yield return null; // Wacht tot de volgende frame.
        }

        OnDeath?.Invoke(); // Trigger Game Over scherm.
    }

    // Spawnt alle bloed-prefabs (sprays en brokjes) tegelijk bij dood.
    private void SpawnBloodSpatter()
    {
        EnsurePrefabsCached();
        Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
        
        if (cachedBloodSpray != null)
        {
            GameObject go = Instantiate(cachedBloodSpray, spawnPos, Quaternion.identity);
            EnforceBloodColor(go);
            Destroy(go, 5f);
        }
        if (cachedBloodExtra != null)
        {
            GameObject go = Instantiate(cachedBloodExtra, spawnPos, Quaternion.Euler(-90, 0, 0));
            EnforceBloodColor(go);
            Destroy(go, 5f);
        }
        if (cachedBloodChunks != null)
        {
            GameObject go = Instantiate(cachedBloodChunks, spawnPos, Quaternion.identity);
            EnforceBloodColor(go);
            Destroy(go, 5f);
        }

        if (deathParticlesPrefab != null && deathParticlesPrefab != cachedBloodSpray)
        {
            GameObject go = Instantiate(deathParticlesPrefab, spawnPos, Quaternion.identity);
            EnforceBloodColor(go);
            Destroy(go, 5f);
        }
    }

    // Dwingt alle materialen van het bloed-effect om dieprood te zijn.
    private void EnforceBloodColor(GameObject fx)
    {
        if (fx == null) return;
        Color bloodRed = new Color(0.75f, 0.05f, 0.05f, 1f);
        ParticleSystemRenderer[] renderers = fx.GetComponentsInChildren<ParticleSystemRenderer>();
        foreach (ParticleSystemRenderer r in renderers)
        {
            if (r.material != null)
            {
                if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", bloodRed);
                if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", bloodRed);
                if (r.material.HasProperty("_TintColor")) r.material.SetColor("_TintColor", bloodRed);
            }
        }
    }
}
