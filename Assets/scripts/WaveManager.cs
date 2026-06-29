using UnityEngine;       // Importeert basis Unity Engine functionaliteit.
using UnityEngine.AI;     // Importeert NavMesh logica (nodig om veilige posities op loopbare vloer te berekenen).
using System.Collections; // Importeert Coroutines (IEnumerator).
using System.Collections.Generic; // Importeert List<T> om lijsten met levende vijanden bij te houden.

/* =====================================================================================================================
 * SCRIPT: WaveManager.cs
 * DOEL: Dit script regelt de overlevings-golven (waves) van aliens. Het spawnt vijanden op veilige plekken,
 *       schaalt na elke golf de moeilijkheidsgraad ( HP & Attack Damage x2), en verhoogt gelijktijdig de
 *       wapenschade van de speler (x1.5) om de game in balans te houden.
 * 
 * ARCHITECTUUR & EXAMEN UITLEG:
 * 1. Singleton Design Pattern: Door 'public static WaveManager Instance' kan elk script in het hele project direct met
 *    deze manager communiceren via 'WaveManager.Instance'. Wanneer een alien sterft, roept zijn HealthSystem eenvoudig
 *    'WaveManager.Instance.OnEnemyKilled()' aan zonder ingewikkelde referenties te hoeven zoeken.
 * 2. Balans en Scaling: Om de game spannend te houden, verdubbelen we elke wave de stats van de monsters. Omdat dat
 *    anders onmogelijk moeilijk wordt, geven we het geweer van de speler na elke wave automatisch 1.5x meer schade.
 * 3. Veilige Spawning op NavMesh: We gebruiken een 'for'-lus om maximaal 15 keer een willekeurige coördinaat te
 *    proberen op 25 tot 48 meter rondom de speler. Via 'NavMesh.SamplePosition' controleren we of dat punt op het
 *    looprooster ligt én minstens 15 meter weg is van de speler. Zo spant er nooit een monster in je gezicht!
 * ===================================================================================================================== */

public class WaveManager : MonoBehaviour
{
    // Singleton variabele: hier slaat het script zichzelf in op. Er is er altijd precies 1 van in het spel.
    public static WaveManager Instance { get; private set; }

    private List<HealthSystem> activeEnemies = new List<HealthSystem>(); // Houdt een lijst bij van alle nog levende aliens in de actieve wave.
    private GameObject[] humanoidPrefabs;                                // Array met de 3 ingeladen 3D alien-modellen (AlienA, B, C).
    
    private int waveNumber = 1;         // Het huidige golfnummer (begint bij Wave 1).
    private int statMultiplier = 1;     // Vermenigvuldigingsfactor voor monster-stats (Wave 1 = 1x, Wave 2 = 2x, Wave 3 = 4x).
    private bool isSpawning = false;    // Voorkomt dat er dubbele waves tegelijk of achter elkaar starten.
    
    // Variabelen voor de rode waarschuwingsbanner die bovenin beeld verschijnt als een nieuwe wave start.
    private float bannerEndTime = 0f;
    private string bannerText = "";

    // Deze speciale attribuut-regel vertelt Unity: "Voer deze functie automatisch uit zodra een level is ingeladen."
    // Hierdoor hoeven we de WaveManager niet handmatig in de Unity scene te slepen; hij maakt zichzelf aan!
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        // Check of er al ergens in het level een WaveManager aanwezig is. Zo ja, stop.
        if (FindAnyObjectByType<WaveManager>() != null) return;
        
        // Zo nee, maak via code een leeg GameObject aan, noem het "WaveManager" en plak dit script erop!
        GameObject go = new GameObject("WaveManager");
        go.AddComponent<WaveManager>();
    }

    // Awake() wordt aangeroepen voordat Start() begint. Perfect voor Singleton opzet.
    void Awake()
    {
        // Als de Singleton variabele nog leeg is, vul hem dan met dit script.
        if (Instance == null) Instance = this;
        // Als er per ongeluk al een tweede WaveManager bestaat, vernietig deze nieuwe kopie dan direct.
        else Destroy(gameObject);
    }

    // Start() draait exact één keer bij het laden van het spel.
    void Start()
    {
        // Laad de 3 alien prefabs rechtstreeks vanuit de 'Resources' map op de harde schijf.
        GameObject a = Resources.Load<GameObject>("AlienA_Pr");
        GameObject b = Resources.Load<GameObject>("AlienB_Pr");
        GameObject c = Resources.Load<GameObject>("AlienC_Pr");
        
        // Voeg alle geladen prefabs toe aan een tijdelijke lijst en zet ze om naar een vaste Array.
        List<GameObject> loaded = new List<GameObject>();
        if (a != null) loaded.Add(a);
        if (b != null) loaded.Add(b);
        if (c != null) loaded.Add(c);
        humanoidPrefabs = loaded.ToArray();

        // Start de Coroutine die na een halve seconde checkt welke monsters er al in de scene staan.
        StartCoroutine(InitialSetupRoutine());
    }

    // InitialSetupRoutine zoekt bij start alle bestaande monsters in het level op.
    IEnumerator InitialSetupRoutine()
    {
        yield return new WaitForSeconds(0.5f); // Wacht 0.5s zodat alle andere scripts zijn opgeladen.

        // Zoek alle EnemyAI scripts in de hele scene.
        EnemyAI[] existingAI = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI ai in existingAI)
        {
            HealthSystem hs = ai.GetComponent<HealthSystem>();
            // Als dit monster nog leeft en nog niet in onze lijst staat, voeg hem dan toe.
            if (hs != null && !hs.isDead && !activeEnemies.Contains(hs))
            {
                activeEnemies.Add(hs);
            }
        }

        // Als we alien prefabs hebben geladen, spawn direct 6 aliens om Wave 1 mee te beginnen.
        if (humanoidPrefabs.Length > 0)
        {
            SpawnAlienBatch(6, statMultiplier);
        }
    }

    // EXAMEN TIP (Communicatie tussen scripts):
    // Deze publieke functie wordt aangeroepen door HealthSystem.Die() wanneer een monster sterft.
    public void OnEnemyKilled(HealthSystem enemy)
    {
        // Als het dode monster in onze lijst zat, verwijder hem eruit.
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        // Als de hele lijst leeg is (aantal == 0) én we zijn niet al bezig met een nieuwe wave te laden...
        if (activeEnemies.Count == 0 && !isSpawning)
        {
            StartCoroutine(SpawnNextWaveRoutine()); // Start de aftel-routine voor de volgende wave!
        }
    }

    // Coroutine die de nieuwe wave voorbereidt, de stats verhoogt en de banner toont.
    IEnumerator SpawnNextWaveRoutine()
    {
        isSpawning = true;     // Zet blokkade aan zodat deze routine niet dubbel start.
        waveNumber++;          // Verhoog het golfnummer met 1 (naar Wave 2, 3, 4 etc.).
        statMultiplier *= 2;   // EXAMEN TIP: Verdubbel de HP en Attack Damage van alle toekomstige monsters!

        // Zoek het schietsysteem van de speler om zijn schade mee te verhogen als beloning en balans.
        ShootingSystem ss = FindFirstObjectByType<ShootingSystem>();
        int newDmg = 25;
        if (ss != null)
        {
            // Vermenigvuldig de huidige wapenschade met 1.5x en rond af naar een heel getal.
            ss.damage = Mathf.RoundToInt(ss.damage * 1.5f);
            newDmg = ss.damage;
        }

        // Stel de tekst in voor de rode waarschuwingsbanner bovenin het scherm.
        bannerText = "⚠️ GOLF " + waveNumber + " GEACTIVEERD!\nAliens 2x sterker | Wapen Damage verhoogd (x1.5 -> " + newDmg + " dmg)!";
        bannerEndTime = Time.unscaledTime + 5f; // Toon exact 5 seconden lang.

        SFXManager.Instance?.PlayExplosion(); // Speel een explosiegeluidje als startsein.

        yield return new WaitForSeconds(3f); // Geef de speler 3 seconden adempauze om te herladen.

        // Bereken hoeveel monsters we moeten spawnen: 6 + (2 * wavenummer). Bij Wave 2 zijn dat er dus 10!
        int countToSpawn = 6 + (waveNumber * 2);
        SpawnAlienBatch(countToSpawn, statMultiplier);

        isSpawning = false; // Geef de spawner weer vrij voor de toekomst.
    }

    // De functie die daadwerkelijk 'count' aantal aliens spawnt op veilige plekken.
    void SpawnAlienBatch(int count, int multiplier)
    {
        if (humanoidPrefabs == null || humanoidPrefabs.Length == 0) return;

        // Zoek de speler op om zijn positie als middelpunt te gebruiken.
        Transform playerTransform = null;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) playerTransform = playerObj.transform;

        Vector3 centerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        // Loop 'count' keer om 'count' aliens te maken.
        for (int i = 0; i < count; i++)
        {
            // Kies een willekeurige prefab uit de array van 3 aliens.
            GameObject prefab = humanoidPrefabs[Random.Range(0, humanoidPrefabs.Length)];
            
            NavMeshHit navHit = new NavMeshHit();
            bool foundValid = false;

            // EXAMEN TIP (Veilige Spawning Algoritme):
            // We proberen maximaal 15 keer een willekeurige coördinaat te prikken in een cirkel van 25 tot 48 meter rondom de speler.
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(25f, 48f);
                Vector3 targetPos = centerPos + new Vector3(randomCircle.x, 5f, randomCircle.y);
                
                // Controleer via SamplePosition of dit punt op een loopbare NavMesh vloer ligt binnen 40 meter.
                if (NavMesh.SamplePosition(targetPos, out navHit, 40f, NavMesh.AllAreas))
                {
                    // Controleer of de gevonden vloerpositie minstens 15 meter verwijderd is van de speler.
                    if (Vector3.Distance(navHit.position, centerPos) >= 15f)
                    {
                        foundValid = true;
                        break; // Perfecte plek gevonden! Stop de pogingen-lus.
                    }
                }
            }
            if (!foundValid) continue; // Geen veilige plek gevonden na 15 pogingen? Sla deze alien dan over.

            // Instantiate (spawn) het 3D-model van de alien op de gevonden veilige NavMesh coördinaat.
            GameObject newAlien = Instantiate(prefab, navHit.position, Quaternion.identity);
            newAlien.name = "HumanoidAlien_Wave" + waveNumber + "_" + i;

            // SPELERSVERZOEK: Wave 1 is relatief klein (0.75x), vanaf Wave 2 worden ze steeds groter en angstaanjagender!
            float scale = (waveNumber <= 1) ? 0.75f : Mathf.Min(1.3f + (waveNumber - 2) * 0.4f, 2.5f);
            newAlien.transform.localScale = Vector3.one * scale; // Schaal het 3D-model in X, Y en Z.

            // Voeg een CapsuleCollider toe en schaal deze passend mee.
            CapsuleCollider col = newAlien.GetComponent<CapsuleCollider>();
            if (col == null) col = newAlien.AddComponent<CapsuleCollider>();
            col.height = 2f;
            col.radius = 0.45f;
            col.center = new Vector3(0, 1f, 0);

            // Voeg de NavMeshAgent toe en pas snelheden en afmetingen aan op basis van de wave-multiplier en schaal.
            NavMeshAgent agent = newAlien.GetComponent<NavMeshAgent>();
            if (agent == null) agent = newAlien.AddComponent<NavMeshAgent>();
            agent.speed = 4f + (multiplier * 0.5f);
            agent.acceleration = 12f;
            agent.radius = 0.4f * scale;
            agent.height = 1.8f * scale;

            // Voeg het HealthSystem toe en verdubbel de levenspunten (80 * multiplier).
            HealthSystem hs = newAlien.GetComponent<HealthSystem>();
            if (hs == null) hs = newAlien.AddComponent<HealthSystem>();
            hs.maxHealth = 80 * multiplier;

            // Zorg dat het monster een HeadshotCollider heeft met 2x schademultiplier.
            HeadshotCollider head = newAlien.GetComponentInChildren<HeadshotCollider>();
            if (head == null)
            {
                HeadshotCollider hc = newAlien.AddComponent<HeadshotCollider>();
                hc.headshotMultiplier = 2;
            }

            // Voeg het EnemyAI script toe en stel de verdubbelde aanvalsschade (25 * multiplier) in.
            EnemyAI ai = newAlien.GetComponent<EnemyAI>();
            if (ai == null) ai = newAlien.AddComponent<EnemyAI>();
            ai.chaseRange = 500f; // Blijft speler overal zoeken.
            ai.attackRange = 3.5f * (scale * 0.8f);
            ai.attackDamage = 25 * multiplier; // Verdubbelde attack damage!
            ai.chaseSpeed = 6f + (multiplier * 0.5f);

            Animator anim = newAlien.GetComponentInChildren<Animator>();
            if (anim != null) ai.animator = anim;

            activeEnemies.Add(hs); // Voeg het nieuwe monster toe aan onze actieve lijst.
        }
    }

    // OnGUI() tekent de rode golf-waarschuwingsbanner direct op het scherm van de speler.
    void OnGUI()
    {
        if (Time.unscaledTime < bannerEndTime && GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            float boxW = 580f; float boxH = 75f;
            float boxX = (Screen.width - boxW) / 2f; float boxY = Screen.height * 0.15f;

            // Teken een zwarte doorschuif-achtergrond
            GUI.color = new Color(0.1f, 0f, 0f, 0.9f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            // Teken rode randen aan de boven- en onderkant
            GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 3f, boxW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Bepaal de tekststijl: groot, vetgedrukt en rood gekleurd.
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 22; style.fontStyle = FontStyle.Bold; style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(1f, 0.4f, 0.4f, 1f);

            GUI.Label(new Rect(boxX, boxY, boxW, boxH), bannerText, style);
        }
    }
}
