using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

/* ==========================================================================================
 * SCRIPT: HealthSystem.cs
 * DOEL: Universeel gezondheidssysteem voor zowel de Speler als de Vijanden.
 * ========================================================================================== */

public class HealthSystem : MonoBehaviour
{
    [Header("Gezondheid Instellingen")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Visuele Effecten")]
    public GameObject deathParticlesPrefab;

    [Header("Gebeurtenissen (Unity Events)")]
    public UnityEvent<int, int> OnHealthChanged;
    public UnityEvent OnDeath;

    public bool isDead = false;

    private static GameObject cachedBloodSpray;
    private static GameObject cachedBloodExtra;
    private static GameObject cachedBloodChunks;
    private static bool prefabsCached = false;

    private static void EnsurePrefabsCached()
    {
        if (!prefabsCached)
        {
            cachedBloodSpray = Resources.Load<GameObject>("BloodSprayFX");
            cachedBloodExtra = Resources.Load<GameObject>("BloodSprayFX_Extra");
            cachedBloodChunks = Resources.Load<GameObject>("ChunkParticleSystem");
            prefabsCached = true;
        }
    }

    void Start()
    {
        EnsurePrefabsCached();
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (GetComponent<AstronautController>() == null && !CompareTag("Player"))
        {
            if (GetComponent<EnemyHealthBar>() == null)
            {
                gameObject.AddComponent<EnemyHealthBar>();
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (GetComponent<AstronautController>() == null)
        {
            EnsurePrefabsCached();
            if (cachedBloodSpray != null)
            {
                GameObject hitBlood = Instantiate(cachedBloodSpray, transform.position + Vector3.up * 1.2f, Quaternion.identity);
                EnforceBloodColor(hitBlood);
                Destroy(hitBlood, 2f);
            }
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

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

        SpawnBloodSpatter();
        SFXManager.Instance?.PlayAlienDeath();

        AstronautController playerController = GetComponent<AstronautController>();
        if (playerController != null)
        {
            playerController.SetDead();
            StartCoroutine(PlayerDeathSequence());
        }
        else
        {
            OnDeath?.Invoke();

            // DROP EEN MUNT ALS DE VIJAND DOODGAAT
            GameObject coinObj = new GameObject("DroppedCoin");
            coinObj.transform.position = transform.position + Vector3.up * 0.5f;
            coinObj.AddComponent<CoinPickup>();

            // MELD AAN WAVE MANAGER DAT VIJAND IS GESTORVEN
            WaveManager.Instance?.OnEnemyKilled(this);

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            Destroy(gameObject, 3f);
        }
    }

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
        Quaternion endRot = Quaternion.Euler(45f, startRot.eulerAngles.y + 15f, 25f);

        while (elapsed < deathDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / deathDuration;
            
            if (cam != null)
            {
                cam.localRotation = Quaternion.Slerp(startRot, endRot, t);
                cam.localPosition += Vector3.down * Time.unscaledDeltaTime * 0.3f;
            }
            yield return null;
        }

        OnDeath?.Invoke();
    }

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
