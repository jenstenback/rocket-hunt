using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/* =====================================================================================================================
 * SCRIPT: WaveManager.cs
 * DOEL: Beheert de golven (waves) van aliens, schaalt de moeilijkheidsgraad en spawnt vijanden op veilige posities.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Singleton Design Pattern: Door 'public static WaveManager Instance' kan elk ander script in de game (bijv. een 
 *    stervende alien) direct communiceren met deze manager via 'WaveManager.Instance.OnEnemyKilled()'.
 * 2. Dynamische Schaling (Balans): Na elke wave verdubbelen we de stats van de aliens ('statMultiplier *= 2'). Om het eerlijk 
 *    te houden, verhogen we tegelijk de schade van het wapen van de speler met 1.5x.
 * 3. NavMesh Veilige Spawning: Als nieuwe aliens spawnen, zoeken we via een lus ('for attempt < 15') een geldige plek op 
 *    het looprooster (NavMesh) die minstens 15 meter van de speler verwijderd is. Zo spant er nooit een monster in je gezicht!
 * ===================================================================================================================== */

public class WaveManager : MonoBehaviour
{
    // Singleton patroon: zorgt dat er altijd maar precies 1 WaveManager actief is in het spel.
    public static WaveManager Instance { get; private set; }

    private List<HealthSystem> activeEnemies = new List<HealthSystem>(); // Lijst met alle nog levende monsters in de wave.
    private GameObject[] humanoidPrefabs;                                // Array met de in geladen alien 3D modellen.
    
    private int waveNumber = 1;         // De huidige golf (begint bij 1).
    private int statMultiplier = 1;     // Wordt elke wave x2 gedaan (Wave 1 = 1x, Wave 2 = 2x, Wave 3 = 4x).
    private bool isSpawning = false;    // Voorkomt dat er dubbele waves tegelijk starten.
    
    // UI Banner variabelen
    private float bannerEndTime = 0f;
    private string bannerText = "";

    // Zorgt dat deze manager automatisch in het level wordt aangemaakt zodra het spel laadt.
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
        // Laad de 3 prefabs van de mensachtige aliens uit de 'Resources' map.
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

    // Zoekt bij het opstarten alle bestaande monsters op de map en voegt ze toe aan de levende lijst.
    IEnumerator InitialSetupRoutine()
    {
        yield return new WaitForSeconds(0.5f); // Wacht kort tot alle andere scripts zijn opgeladen.

        EnemyAI[] existingAI = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI ai in existingAI)
        {
            HealthSystem hs = ai.GetComponent<HealthSystem>();
            if (hs != null && !hs.isDead && !activeEnemies.Contains(hs))
            {
                activeEnemies.Add(hs);
            }
        }

        // Spawn extra vijanden om Wave 1 mee te beginnen.
        if (humanoidPrefabs.Length > 0)
        {
            SpawnAlienBatch(6, statMultiplier);
        }
    }

    // EXAMEN TIP (Event communicatie):
    // Deze methode wordt aangeroepen door HealthSystem.Die() wanneer een alien sterft.
    public void OnEnemyKilled(HealthSystem enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy); // Haal de dode alien uit de lijst.
        }

        // Als alle aliens van deze golf dood zijn én we zijn nog niet bezig met een nieuwe golf laden:
        if (activeEnemies.Count == 0 && !isSpawning)
        {
            StartCoroutine(SpawnNextWaveRoutine()); // Start de volgende wave!
        }
    }

    // Coroutine die de volgende wave voorbereidt en aftelt.
    IEnumerator SpawnNextWaveRoutine()
    {
        isSpawning = true;
        waveNumber++;          // Ga naar Wave 2, 3, 4 etc.
        statMultiplier *= 2;   // EXAMEN TIP: Verdubbel de HP en Attack Damage van de komende monsters!

        // Zoek het schietsysteem van de speler en verhoog zijn kogel-schade met 1.5x (balans).
        ShootingSystem ss = FindFirstObjectByType<ShootingSystem>();
        int newDmg = 25;
        if (ss != null)
        {
            ss.damage = Mathf.RoundToInt(ss.damage * 1.5f);
            newDmg = ss.damage;
        }

        // Toon de rode waarschuwingsbanner op het scherm gedurende 5 seconden.
        bannerText = "⚠️ GOLF " + waveNumber + " GEACTIVEERD!\nAliens 2x sterker | Wapen Damage verhoogd (x1.5 -> " + newDmg + " dmg)!";
        bannerEndTime = Time.unscaledTime + 5f;

        SFXManager.Instance?.PlayExplosion();

        yield return new WaitForSeconds(3f); // Geef de speler 3 seconden adempauze.

        // Hoe verder in het spel, hoe meer aliens er spawnen (6 + 2 per wave).
        int countToSpawn = 6 + (waveNumber * 2);
        SpawnAlienBatch(countToSpawn, statMultiplier);

        isSpawning = false;
    }

    // Spawnt een groep nieuwe aliens op veilige locaties.
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

            // EXAMEN TIP (Veilige Spawning Algoritme):
            // Probeer maximaal 15 keer een willekeurig punt te vinden in een cirkel rondom de speler (25m tot 48m ver).
            // Controleer via NavMesh.SamplePosition of dat punt op loopbare vloer ligt én ver genoeg is (>= 15m).
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(25f, 48f);
                Vector3 targetPos = centerPos + new Vector3(randomCircle.x, 5f, randomCircle.y);
                if (NavMesh.SamplePosition(targetPos, out navHit, 40f, NavMesh.AllAreas))
                {
                    if (Vector3.Distance(navHit.position, centerPos) >= 15f)
                    {
                        foundValid = true;
                        break; // Veilig punt gevonden! Stop met zoeken.
                    }
                }
            }
            if (!foundValid) continue; // Geen veilige plek gevonden? Sla deze alien over.

            // Instantiate (spawn) de alien op het gevonden NavMesh punt.
            GameObject newAlien = Instantiate(prefab, navHit.position, Quaternion.identity);
            newAlien.name = "HumanoidAlien_Wave" + waveNumber + "_" + i;

            // SPELERSVERZOEK: Wave 1 is klein (0.75x), vanaf Wave 2 worden ze flink groter en angstaanjagender!
            float scale = (waveNumber <= 1) ? 0.75f : Mathf.Min(1.3f + (waveNumber - 2) * 0.4f, 2.5f);
            newAlien.transform.localScale = Vector3.one * scale;

            // Voeg fysieke en AI componenten toe en schaal ze mee met de multiplier
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
            hs.maxHealth = 80 * multiplier; // Verdubbelde HP per wave!

            HeadshotCollider head = newAlien.GetComponentInChildren<HeadshotCollider>();
            if (head == null)
            {
                HeadshotCollider hc = newAlien.AddComponent<HeadshotCollider>();
                hc.headshotMultiplier = 2;
            }

            EnemyAI ai = newAlien.GetComponent<EnemyAI>();
            if (ai == null) ai = newAlien.AddComponent<EnemyAI>();
            ai.chaseRange = 500f; // Blijft speler overal achtervolgen
            ai.attackRange = 3.5f * (scale * 0.8f);
            ai.attackDamage = 25 * multiplier; // Verdubbelde attack damage per wave!
            ai.chaseSpeed = 6f + (multiplier * 0.5f);

            Animator anim = newAlien.GetComponentInChildren<Animator>();
            if (anim != null) ai.animator = anim;

            activeEnemies.Add(hs); // Voeg hem toe aan de actieve lijst.
        }
    }

    // Tekent de rode wave-banner bovenin het scherm.
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
