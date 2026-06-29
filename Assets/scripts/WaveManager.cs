using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/* ==========================================================================================
 * SCRIPT: WaveManager.cs
 * DOEL: Beheert de golven van vijanden en spawnt mensachtige aliens die steeds groter worden.
 * ========================================================================================== */

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    private List<HealthSystem> activeEnemies = new List<HealthSystem>();
    private GameObject[] humanoidPrefabs;
    
    private int waveNumber = 1;
    private int statMultiplier = 1;
    private bool isSpawning = false;
    private float bannerEndTime = 0f;
    private string bannerText = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (FindAnyObjectByType<WaveManager>() != null) return;
        GameObject go = new GameObject("WaveManager");
        go.AddComponent<WaveManager>();
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        GameObject a = Resources.Load<GameObject>("AlienA_Pr");
        GameObject b = Resources.Load<GameObject>("AlienB_Pr");
        GameObject c = Resources.Load<GameObject>("AlienC_Pr");
        
        List<GameObject> loaded = new List<GameObject>();
        if (a != null) loaded.Add(a);
        if (b != null) loaded.Add(b);
        if (c != null) loaded.Add(c);
        humanoidPrefabs = loaded.ToArray();

        StartCoroutine(InitialSetupRoutine());
    }

    IEnumerator InitialSetupRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        EnemyAI[] existingAI = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI ai in existingAI)
        {
            HealthSystem hs = ai.GetComponent<HealthSystem>();
            if (hs != null && !hs.isDead && !activeEnemies.Contains(hs))
            {
                activeEnemies.Add(hs);
            }
        }

        // Wave 1 bij start: iets kleinere mensachtige aliens
        if (humanoidPrefabs.Length > 0)
        {
            SpawnAlienBatch(6, statMultiplier);
        }
    }

    public void OnEnemyKilled(HealthSystem enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        if (activeEnemies.Count == 0 && !isSpawning)
        {
            StartCoroutine(SpawnNextWaveRoutine());
        }
    }

    IEnumerator SpawnNextWaveRoutine()
    {
        isSpawning = true;
        waveNumber++;
        statMultiplier *= 2; // Verdubbel stats!

        // Verhoog wapendmg van de speler met x1.5 per wave
        ShootingSystem ss = FindFirstObjectByType<ShootingSystem>();
        int newDmg = 25;
        if (ss != null)
        {
            ss.damage = Mathf.RoundToInt(ss.damage * 1.5f);
            newDmg = ss.damage;
        }

        bannerText = "⚠️ GOLF " + waveNumber + " GEACTIVEERD!\nAliens 2x sterker | Wapen Damage verhoogd (x1.5 -> " + newDmg + " dmg)!";
        bannerEndTime = Time.unscaledTime + 5f;

        SFXManager.Instance?.PlayExplosion();

        yield return new WaitForSeconds(3f);

        int countToSpawn = 6 + (waveNumber * 2);
        SpawnAlienBatch(countToSpawn, statMultiplier);

        isSpawning = false;
    }

    void SpawnAlienBatch(int count, int multiplier)
    {
        if (humanoidPrefabs == null || humanoidPrefabs.Length == 0) return;

        Transform playerTransform = null;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) playerTransform = playerObj.transform;

        Vector3 centerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = humanoidPrefabs[Random.Range(0, humanoidPrefabs.Length)];
            
            NavMeshHit navHit = new NavMeshHit();
            bool foundValid = false;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(25f, 48f);
                Vector3 targetPos = centerPos + new Vector3(randomCircle.x, 5f, randomCircle.y);
                if (NavMesh.SamplePosition(targetPos, out navHit, 40f, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(navHit.position, centerPos) >= 15f)
                    {
                        foundValid = true;
                        break;
                    }
                }
            }
            if (!foundValid) continue; // Alleen spawnen als we zeker weten dat het op de NavMesh en op veilige afstand is!

            GameObject newAlien = Instantiate(prefab, navHit.position, Quaternion.identity);
                newAlien.name = "HumanoidAlien_Wave" + waveNumber + "_" + i;

                // SPELERSVERZOEK: Wave 1 is klein (0.75x), vanaf Wave 2 worden ze flink groter (1.3x, 1.7x, etc.)!
                float scale = (waveNumber <= 1) ? 0.75f : Mathf.Min(1.3f + (waveNumber - 2) * 0.4f, 2.5f);
                newAlien.transform.localScale = Vector3.one * scale;

                CapsuleCollider col = newAlien.GetComponent<CapsuleCollider>();
                if (col == null) col = newAlien.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.45f;
                col.center = new Vector3(0, 1f, 0);

                NavMeshAgent agent = newAlien.GetComponent<NavMeshAgent>();
                if (agent == null) agent = newAlien.AddComponent<NavMeshAgent>();
                agent.speed = 4f + (multiplier * 0.5f);
                agent.acceleration = 12f;
                agent.radius = 0.4f * scale;
                agent.height = 1.8f * scale;

                HealthSystem hs = newAlien.GetComponent<HealthSystem>();
                if (hs == null) hs = newAlien.AddComponent<HealthSystem>();
                hs.maxHealth = 80 * multiplier;

                HeadshotCollider head = newAlien.GetComponentInChildren<HeadshotCollider>();
                if (head == null)
                {
                    HeadshotCollider hc = newAlien.AddComponent<HeadshotCollider>();
                    hc.headshotMultiplier = 2;
                }

                EnemyAI ai = newAlien.GetComponent<EnemyAI>();
                if (ai == null) ai = newAlien.AddComponent<EnemyAI>();
                ai.chaseRange = 60f;
                ai.attackRange = 3.5f * (scale * 0.8f); // Grotere aliens auotmatisch iets grotere attack range
                ai.attackDamage = 25 * multiplier;
                ai.chaseSpeed = 6f + (multiplier * 0.5f);

                Animator anim = newAlien.GetComponentInChildren<Animator>();
                if (anim != null) ai.animator = anim;

                activeEnemies.Add(hs);
        }
    }

    void OnGUI()
    {
        if (Time.unscaledTime < bannerEndTime && GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            float boxW = 580f;
            float boxH = 75f;
            float boxX = (Screen.width - boxW) / 2f;
            float boxY = Screen.height * 0.15f;

            GUI.color = new Color(0.1f, 0f, 0f, 0.9f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 3f, boxW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 22;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(1f, 0.4f, 0.4f, 1f);

            GUI.Label(new Rect(boxX, boxY, boxW, boxH), bannerText, style);
        }
    }
}
