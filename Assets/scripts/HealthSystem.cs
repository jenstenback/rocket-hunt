using UnityEngine;       // Importeert basis Unity Engine functionaliteit.
using UnityEngine.Events; // Importeert UnityEvents voor communicatie met User Interface (UI) balkjes.
using UnityEngine.AI;     // Importeert NavMeshAgent logica (nodig om AI te stoppen als een monster sterft).

/* =====================================================================================================================
 * SCRIPT: HealthSystem.cs
 * DOEL: Dit is een universeel en modulair gezondheidssysteem dat zowel op de Speler als op alle Vijanden (aliens)
 *       wordt gebruikt. Het beheert levenspunten (HP), het incasseren van schade, genezen door health packs,
 *       het spawnen van bloedspetters op trefferplekken, en de stervensprocedure.
 * 
 * ARCHITECTUUR & EXAMEN UITLEG:
 * 1. Component Hergebruik: Omdat ditzelfde script op zowel speler als vijand werkt, controleren we via 'CompareTag("Player")'
 *    of het om de speler gaat. Zo ja, triggeren we Game Over animaties. Zo nee, droppen we muntjes en melden we aan
 *    de WaveManager dat een alien gestorven is.
 * 2. Unity Events (Loose Coupling): Zodra schade wordt genomen, roepen we 'OnHealthChanged?.Invoke()' aan. De UI
 *    luistert naar dit signaal en tekent de levensbalkjes opnieuw. Het HealthSystem is dus volledig onafhankelijk van
 *    hoe de UI eruitziet!
 * ===================================================================================================================== */

public class HealthSystem : MonoBehaviour // MonoBehaviour maakt koppeling met Unity GameObjects mogelijk.
{
    [Header("Gezondheid Instellingen")]
    public int maxHealth = 100;           // Het maximale aantal levenspunten dat dit object kan bezitten.
    public int currentHealth;             // Het actuele aantal levenspunten op dit moment.

    [Header("Visuele Effecten")]
    public GameObject deathParticlesPrefab; // Extra particle explosie (deeltjeseffect) die afspeelt bij het sterven.

    [Header("Gebeurtenissen (Unity Events)")]
    public UnityEvent<int, int> OnHealthChanged; // Event dat wordt verzonden bij schade of healing. Stuurt currentHP en maxHP mee.
    public UnityEvent OnDeath;                   // Event dat wordt verzonden op exact het moment dat currentHP op 0 belandt.

    public bool isDead = false;           // Booleaan die voorkomt dat de sterf-logica (munten droppen) dubbel wordt uitgevoerd.

    // Statische variabelen (static) delen hun waarde over ALLE HealthSystem scripts in het hele spel.
    // Dit zorgt ervoor dat we de bloed-prefabs maar één enkele keer hoeven te inladen vanuit de harde schijf (geheugen-optimalisatie!).
    private static GameObject cachedBloodSpray;
    private static GameObject cachedBloodExtra;
    private static GameObject cachedBloodChunks;
    private static bool prefabsCached = false;

    // Deze functie controleert of de bloed-prefabs al ingeladen zijn, en zo niet, laadt ze uit de 'Resources' map.
    private static void EnsurePrefabsCached()
    {
        if (!prefabsCached)
        {
            cachedBloodSpray = Resources.Load<GameObject>("BloodSprayFX");      // Standaard rode bloedspetter.
            cachedBloodExtra = Resources.Load<GameObject>("BloodSprayFX_Extra"); // Extra bloed-nevel.
            cachedBloodChunks = Resources.Load<GameObject>("ChunkParticleSystem"); // Vlees- en bloedbrokjes explosie.
            prefabsCached = true;
        }
    }

    // Start() wordt door Unity één keer aangeroepen wanneer het level laadt.
    void Start()
    {
        EnsurePrefabsCached();             // Zorg dat bloed-effecten klaarstaan in het geheugen.
        currentHealth = maxHealth;         // Zet de actuele gezondheid op 100% vol.
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Stuur direct een signaal naar de UI om de balkjes goed te zetten.

        // EXAMEN TIP (Automatische UI Koppeling voor Vijanden):
        // Als dit script GEEN speler-controller heeft én niet de tag "Player" bezit, dan weten we 100% zeker dat dit een VIJAND is!
        if (GetComponent<AstronautController>() == null && !CompareTag("Player"))
        {
            // Controleer of de vijand al een zwevende 3D levensbalk (EnemyHealthBar) heeft. Zo niet, voeg deze automatisch toe via code!
            if (GetComponent<EnemyHealthBar>() == null)
            {
                gameObject.AddComponent<EnemyHealthBar>();
            }
        }
    }

    // Publieke functie die wordt aangeroepen door kogels (Bullet.cs) of monsters (EnemyAI.cs) om schade toe te brengen.
    public void TakeDamage(int amount)
    {
        if (isDead) return; // Als we al dood zijn, negeer dan alle verdere schade.

        currentHealth -= amount; // Trek de schade ('amount') af van de actuele gezondheid.
        // Mathf.Clamp zorgt ervoor dat het getal nooit kleiner dan 0 of groter dan maxHealth kan worden.
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Als dit een VIJAND is (dus geen AstronautController aanwezig)...
        if (GetComponent<AstronautController>() == null)
        {
            EnsurePrefabsCached();
            if (cachedBloodSpray != null)
            {
                // Instantiate (spawn) een rode bloedspetter iets boven het midden van het monster (Vector3.up * 1.2).
                GameObject hitBlood = Instantiate(cachedBloodSpray, transform.position + Vector3.up * 1.2f, Quaternion.identity);
                EnforceBloodColor(hitBlood); // Garandeer dat de kleur van het bloed dieprood is.
                Destroy(hitBlood, 2f);       // Vernietig het bloed-object na 2 seconden om het computergeheugen schoon te houden.
            }
        }

        // Stuur een signaal naar de gekoppelde levensbalken dat de HP is veranderd.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Als het aantal levenspunten op of onder de 0 is beland...
        if (currentHealth <= 0)
        {
            Die(); // Start de stervensprocedure.
        }
    }

    // Publieke functie die wordt aangeroepen wanneer de speler een Health Pickup of Medkit oppakt.
    public void Heal(int amount)
    {
        if (isDead) return;          // Dode spelers kunnen niet genezen.
        currentHealth += amount;     // Tel de genezing op bij de gezondheid.
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Zorg dat we niet boven maxHealth uitkomen.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);        // Update de levensbalk in de UI.
    }

    // De centrale stervensprocedure voor zowel speler als vijand.
    private void Die()
    {
        if (isDead) return; // Veiligheidscheck om dubbele sterfte te voorkomen.
        isDead = true;

        SpawnBloodSpatter(); // Spawn een grote bloed- en brokjesexplosie!
        SFXManager.Instance?.PlayAlienDeath(); // Speel het sterfgeluid af via de SFXManager.

        // Controleer of dit object de SPELER is door te zoeken naar de AstronautController.
        AstronautController playerController = GetComponent<AstronautController>();
        if (playerController != null)
        {
            // HET IS DE SPELER DIE STIERF:
            playerController.SetDead();            // Blokkeer spelerbeweging en input.
            StartCoroutine(PlayerDeathSequence()); // Start de animatie waarbij de camera schuin naar de grond valt.
        }
        else
        {
            // HET IS EEN VIJAND DIE STIERF:
            OnDeath?.Invoke(); // Trigger eventuele speciale sterf-events.

            // 1. DROP EEN MUNTJE ($) VOOR DE SPELER (EXAMEN TIP):
            // We maken via code een gloednieuw, leeg GameObject aan in de wereld en noemen het "DroppedCoin".
            GameObject coinObj = new GameObject("DroppedCoin");
            // Zet de positie iets boven het dode lichaam.
            coinObj.transform.position = transform.position + Vector3.up * 0.5f;
            // Voeg het CoinPickup script toe, wat automatisch het draaiende munt-model en de collider aanmaakt!
            coinObj.AddComponent<CoinPickup>();

            // 2. MELD AAN DE WAVEMANAGER DAT DEZE ALIEN DOOD IS:
            // Dit zorgt ervoor dat de WaveManager weet wanneer alle aliens op zijn, zodat de volgende wave kan starten.
            WaveManager.Instance?.OnEnemyKilled(this);

            // Schakel de NavMeshAgent (loop-AI) uit, anders blijft het dode lichaam rechtop staan proberen te lopen.
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // Schakel alle colliders uit zodat de speler en andere aliens niet over het lijk struikelen.
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            // Vernietig het dode monster na 3 seconden definitief uit de game.
            Destroy(gameObject, 3f);
        }
    }

    // Coroutine: een functie die over meerdere frames wordt uitgesmeerd via 'yield return'.
    // Zorgt ervoor dat de camera van de speler in 2 seconden langzaam schuin naar beneden kantelt (Game Over effect).
    private System.Collections.IEnumerator PlayerDeathSequence()
    {
        Transform cam = null;
        AstronautController ac = GetComponent<AstronautController>();
        if (ac != null && ac.playerCamera != null)
        {
            cam = ac.playerCamera;
        }

        float elapsed = 0f;          // Verstreken tijd sinds overlijden.
        float deathDuration = 2f;    // Duur van de val-animatie in seconden.
        Quaternion startRot = cam != null ? cam.localRotation : Quaternion.identity;
        Quaternion endRot = Quaternion.Euler(45f, startRot.eulerAngles.y + 15f, 25f); // Eindrotatie: schuin naar de vloer.

        while (elapsed < deathDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaledDeltaTime telt door, zelfs als het spel in slow-motion gaat!
            float t = elapsed / deathDuration;
            
            if (cam != null)
            {
                // Slerp rolt de camera zachtjes van de startrotatie naar de eindrotatie.
                cam.localRotation = Quaternion.Slerp(startRot, endRot, t);
                // Laat de camera ook langzaam een stukje zakken.
                cam.localPosition += Vector3.down * Time.unscaledDeltaTime * 0.3f;
            }
            yield return null; // Wacht tot de volgende frame en ga dan verder in de while-loop.
        }

        OnDeath?.Invoke(); // Als de val klaar is, trigger het Game Over scherm in de GameManager!
    }

    // Spawnt alle verschillende bloed-effecten tegelijkertijd op de sterfplek.
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

    // Loop door alle deeltjes-renderers van een effect heen en dwing de materiaalkleur op dieprood.
    private void EnforceBloodColor(GameObject fx)
    {
        if (fx == null) return;
        Color bloodRed = new Color(0.75f, 0.05f, 0.05f, 1f); // RGB waarde voor donkerrood.
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
