using UnityEngine;        // Importeert Unity Engine functionaliteit.
using TMPro;              // Importeert TextMeshPro voor tekstweergave.
using UnityEngine.SceneManagement; // Importeert SceneManager om het level te kunnen herstarten bij opnieuw spelen.

/* =====================================================================================================================
 * SCRIPT: GameManager.cs
 * DOEL: De centrale State Machine en User Interface (UI) manager van heel de game. Regel het startscherm,
 *       het pauzemenu, de tellers (onderdelen en munten), de levensbalk, en de Game Over / Victory schermen.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Singleton Design Pattern: 'public static GameManager Instance' garandeert dat andere scripts gemakkelijk
 *    kunnen communiceren met de spelleiding via 'GameManager.Instance.AddPart()'.
 * 2. OnGUI Direct Rendering: Om er 100% zeker van te zijn dat de UI (zoals de Healthbar en You Died schermen)
 *    altijd scherp, schaalbaar en betrouwbaar over het 3D-beeld heen liggen, gebruiken wij Unity's directe OnGUI
 *    teken-engine.
 * 3. TimeScale Management: Dit script stuurt de speltijd aan via 'Time.timeScale'. In menu's staat dit op 0
 *    (tijd stil), tijdens gameplay op 1 (normale snelheid), en bij Game Over op 0.2 voor een dramatisch slow-motion effect!
 * ===================================================================================================================== */

public class GameManager : MonoBehaviour
{
    // Singleton instantie variabele.
    public static GameManager Instance { get; private set; }

    [Header("Spel Voortgang Instellingen")]
    public int totalPartsNeeded = 5;     // Totaal aantal scheepsonderdelen nodig voor overwinning.
    private int partsCollected = 0;      // Actueel aantal verzamelde onderdelen.
    public int coinsCollected = 0;       // Actueel aantal verzamelde munten ($).
    public bool gameStarted = false;     // Houdt bij of we al uit het startscherm zijn.

    [Header("User Interface Referenties")]
    public TextMeshProUGUI partsText;    // Referentie naar TMP tekstobject in de Canvas (optioneel).
    public GameObject startScreen;       // Referentie naar het startscherm panel.
    public GameObject winScreen;         // Referentie naar het overwinningsscherm.
    public GameObject loseScreen;        // Referentie naar het verlies-scherm.

    [Header("Speler Referentie")]
    public HealthSystem playerHealth;    // Referentie naar het HealthSystem van de speler.

    private bool gameEnded = false;      // Houdt bij of de game is afgelopen (winst of verlies).
    private bool isPaused = false;       // Houdt bij of het spel op pauze staat via de ESC-toets.
    private bool showDeathScreen = false;// Trigger voor het You Died scherm.
    private bool showWinScreen = false;  // Trigger voor het Victory scherm.
    private float screenAlpha = 0f;      // Transparantie (0 = doorzichtig, 1 = ondoorzichtig) voor vloeiende fade-in animaties.

    // Variabelen voor de blauwe uitleg-banner na het vinden van het 5e onderdeel.
    private float escapePromptEndTime = 0f;
    private bool promptTriggered = false;

    // Awake() wordt aangeroepen vlak voordat Start() begint.
    void Awake()
    {
        // Singleton controle: zorg dat er maar 1 GameManager is.
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        MakeEnvironmentSolid(); // Roep hulpmethode aan om colliders op omgeving te garanderen.
        SetupCanvasScaling();
        SetupSpaceSkybox();
    }

    // Start() zet bij het laden van de scene alle beginwaarden klaar.
    void Start()
    {
        gameStarted = false;
        isPaused = false;
        showDeathScreen = false;
        showWinScreen = false;
        screenAlpha = 0f;
        promptTriggered = false;
        Time.timeScale = 0f; // Zet de tijd stil (0) zolang de speler naar het startscherm kijkt.

        // Zet de juiste UI-panelen aan en uit.
        if (startScreen != null) startScreen.SetActive(true);
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        if (partsText != null) partsText.gameObject.SetActive(false);

        // Als playerHealth nog niet is gekoppeld via de Inspector, zoek hem dan automatisch via code op!
        if (playerHealth == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerHealth = ac.GetComponent<HealthSystem>();
        }
        
        // Koppel een event-luisteraar aan het overlijden van de speler. Zodra hij sterft, roept Unity 'GameOverLose()' aan.
        if (playerHealth != null)
        {
            playerHealth.OnDeath.AddListener(GameOverLose);
        }
    }

    // Update() controleert elke frame op toetsenbordinvoer (zoals ESC) en timers.
    void Update()
    {
        // Als we spelen en op Escape drukken, wissel dan tussen pauze en doorgaan.
        if (gameStarted && !gameEnded && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        // Zorg dat de muiscursor zichtbaar en vrij is wanneer we in een menu zitten of dood zijn.
        if (!gameStarted || gameEnded || isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Vloeiende fade-in berekening voor de eindschermen: verhoog de transparantie (screenAlpha) richting 1.
        if ((showDeathScreen || showWinScreen) && screenAlpha < 1f)
        {
            screenAlpha += Time.unscaledDeltaTime * 0.8f;
            if (screenAlpha > 1f) screenAlpha = 1f;
        }

        // Als de speler precies 5 (of meer) onderdelen heeft verzameld én we hebben de banner nog niet getoond...
        if (partsCollected >= totalPartsNeeded && !promptTriggered)
        {
            promptTriggered = true;
            // Stel in dat de banner 25 seconden lang zichtbaar blijft op het scherm.
            escapePromptEndTime = Time.unscaledTime + 25f;
        }
    }

    // OnGUI() wordt door Unity gebruikt om 2D schermen en tellers over de 3D game heen te tekenen.
    void OnGUI()
    {
        if (!gameStarted) return; // Teken niks als we nog in het startscherm zitten.

        if (!gameEnded)
        {
            DrawPartsCounter();       // Teken het zwarte vakje linksboven met onderdelen en munten.
            DrawEscapePromptBanner(); // Teken de blauwe banner als alle onderdelen gevonden zijn.
            DrawPlayerHealthBar();    // Teken de rode/groene levensbalk rechtsonder.
        }
        else
        {
            // Als de game is afgelopen, teken dan óf het verlies- óf het winstscherm.
            if (showDeathScreen) DrawDeathScreen();
            else if (showWinScreen) DrawWinScreen();
        }
    }

    // Tekent de teller linksboven met het aantal onderdelen en munten.
    void DrawPartsCounter()
    {
        bool allParts = (partsCollected >= totalPartsNeeded);
        float boxW = allParts ? 450f : 260f;
        float boxH = allParts ? 115f : 65f;
        float margin = 15f;

        // Teken een semi-transparant zwarte achtergrond.
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(margin, margin, boxW, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle styleParts = new GUIStyle(GUI.skin.label);
        styleParts.fontSize = 17; styleParts.fontStyle = FontStyle.Bold;
        styleParts.normal.textColor = new Color(0.4f, 0.9f, 1f, 1f);
        styleParts.padding = new RectOffset(10, 0, 5, 0);

        string textParts = allParts ? "✓ ALLE 5 ONDERDELEN VERZAMELD!" : "⚙ Onderdelen: " + partsCollected + " / " + totalPartsNeeded;
        GUI.Label(new Rect(margin, margin, boxW, 30f), textParts, styleParts);

        GUIStyle styleCoins = new GUIStyle(GUI.skin.label);
        styleCoins.fontSize = 17; styleCoins.fontStyle = FontStyle.Bold;
        styleCoins.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f);
        styleCoins.padding = new RectOffset(10, 0, 0, 0);

        GUI.Label(new Rect(margin, margin + 32f, boxW, 30f), "💰 Munten: $" + coinsCollected, styleCoins);

        if (allParts)
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 13; hintStyle.normal.textColor = new Color(0.8f, 1f, 0.8f, 0.95f);
            hintStyle.padding = new RectOffset(10, 10, 0, 0); hintStyle.wordWrap = true;

            string tip = "💡 Tip: Breng alle 5 onderdelen naar het cargo ship om direct te winnen, OF speel door de waves heen voor extra munten!";
            GUI.Label(new Rect(margin, margin + 62f, boxW, 50f), tip, hintStyle);
        }
    }

    // Tekent de grote blauwe instructiebanner bovenin beeld.
    void DrawEscapePromptBanner()
    {
        if (Time.unscaledTime < escapePromptEndTime)
        {
            float boxW = 750f; float boxH = 110f;
            float boxX = (Screen.width - boxW) / 2f; float boxY = 25f;

            GUI.color = new Color(0f, 0.15f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            GUI.color = new Color(0.3f, 0.9f, 1f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 3f, boxW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 17; style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter; style.normal.textColor = new Color(0.9f, 0.98f, 1f, 1f);
            style.wordWrap = true;

            string bannerMsg = "🎉 ALLE ONDERDELEN VERZAMELD!\n" +
                               "Als je munten wilt verzamelen speel door de waves heen en verzamel de munten,\n" +
                               "je kan op elk punt de game winnen door alle 5 de onderdelen naar het cargo ship te brengen en weg te vliegen!";

            GUI.Label(new Rect(boxX + 15f, boxY, boxW - 30f, boxH), bannerMsg, style);
        }
    }

    // Tekent de levensbalk rechtsonder op het scherm, die vloeiend van kleur verandert.
    void DrawPlayerHealthBar()
    {
        if (playerHealth == null) return;

        float boxW = 320f; float boxH = 50f; float margin = 20f;
        float boxX = Screen.width - boxW - margin; float boxY = Screen.height - boxH - margin;

        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(boxX - 3, boxY - 3, boxW + 6, boxH + 6), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

        // Bereken percentage HP (tussen 0 en 1).
        float pct = Mathf.Clamp01((float)playerHealth.currentHealth / (float)playerHealth.maxHealth);

        // Color.Lerp mengt felrood en felgroen op basis van het percentage.
        GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.2f, 1f, 0.4f), pct);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW * pct, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20; style.fontStyle = FontStyle.Bold; style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(boxX, boxY, boxW, boxH), "❤ HEALTH: " + playerHealth.currentHealth + " / " + playerHealth.maxHealth, style);
    }

    // Tekent het rode "YOU DIED" scherm wanneer de speler sterft.
    void DrawDeathScreen()
    {
        // Teken een zwarte overlay over het hele scherm heen die langzaam donkerder wordt op basis van screenAlpha.
        GUI.color = new Color(0f, 0f, 0f, screenAlpha * 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = new Color(0.5f, 0f, 0f, screenAlpha * 0.4f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 72; titleStyle.fontStyle = FontStyle.Bold; titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.8f, 0.1f, 0.1f, screenAlpha);

        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100f), "YOU DIED", titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 24; subStyle.alignment = TextAnchor.MiddleCenter; subStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, screenAlpha);
        GUI.Label(new Rect(0, Screen.height * 0.2f + 90f, Screen.width, 40f), "De aliens hebben je te pakken gekregen...", subStyle);

        // Als de animatie bijna klaar is (> 0.8), toon dan de herstart knop.
        if (screenAlpha > 0.8f) DrawRestartButton(Screen.height * 0.52f, new Color(0.8f, 0.1f, 0.1f, 1f), new Color(0.3f, 0.05f, 0.05f, 1f));
    }

    // Tekent het groene overwinningsscherm wanneer de speler ontsnapt met het schip.
    void DrawWinScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, screenAlpha * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = new Color(0f, 0.4f, 0.1f, screenAlpha * 0.35f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 64; titleStyle.fontStyle = FontStyle.Bold; titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.2f, 0.9f, 0.4f, screenAlpha);

        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 90f), "VICTORY - ONTSNAPPING GESLAAGD!", titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 22; subStyle.alignment = TextAnchor.MiddleCenter; subStyle.normal.textColor = new Color(0.8f, 0.9f, 0.8f, screenAlpha);
        GUI.Label(new Rect(0, Screen.height * 0.18f + 80f, Screen.width, 40f), "Je hebt de planeet verlaten met het Cargo Ship!", subStyle);

        if (screenAlpha > 0.8f)
        {
            DrawRestartButton(Screen.height * 0.52f, new Color(0.2f, 0.8f, 0.4f, 1f), new Color(0.05f, 0.3f, 0.1f, 1f));

            float btnW = 260f; float btnH = 50f;
            float btnX = (Screen.width - btnW) / 2f; float btnY = Screen.height * 0.52f + 70f;
            Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

            GUI.color = new Color(0.15f, 0.2f, 0.25f, 0.95f);
            GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.8f, 1f, 1f);
            GUI.DrawTexture(new Rect(btnX, btnY, btnW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(btnX, btnY + btnH - 2f, btnW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.label);
            btnStyle.fontSize = 22; btnStyle.fontStyle = FontStyle.Bold; btnStyle.alignment = TextAnchor.MiddleCenter; btnStyle.normal.textColor = Color.white;

            // Knoop om verder te spelen na overwinning.
            if (GUI.Button(btnRect, "▶ VERDER SPELEN", btnStyle))
            {
                showWinScreen = false;
                gameEnded = false;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                CargoShipEscape shipEscape = FindFirstObjectByType<CargoShipEscape>();
                if (shipEscape != null) shipEscape.ResetPlayerForContinue();
            }
        }
    }

    // Hulpmethode die een knop tekent waarmee het level opnieuw geladen kan worden.
    void DrawRestartButton(float yPos, Color borderCol, Color bgCol)
    {
        float btnW = 260f; float btnH = 55f;
        float btnX = (Screen.width - btnW) / 2f;
        Rect btnRect = new Rect(btnX, yPos, btnW, btnH);

        GUI.color = bgCol;
        GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
        GUI.color = borderCol;
        GUI.DrawTexture(new Rect(btnX, yPos, btnW, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, yPos + btnH - 3f, btnW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.label);
        btnStyle.fontSize = 24; btnStyle.fontStyle = FontStyle.Bold; btnStyle.alignment = TextAnchor.MiddleCenter; btnStyle.normal.textColor = Color.white;

        if (GUI.Button(btnRect, "🔄 OPNIEUW SPELEN", btnStyle))
        {
            Time.timeScale = 1f; // Herstel speltijd naar normaal
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); // Herlaad de huidige scene
        }
    }

    // Publieke methode om een scheepsonderdeel toe te voegen aan de teller.
    public void AddPart()
    {
        partsCollected++;
        SFXManager.Instance?.PlayPickup();
    }

    // Alias methode die door CollectibleSystem.cs wordt aangeroepen.
    public void CollectPart()
    {
        partsCollected++;
        SFXManager.Instance?.PlayPickup();
    }


    // Publieke methode om 10 dollar/munten toe te voegen.
    public void AddCoin()
    {
        coinsCollected += 10;
        SFXManager.Instance?.PlayPickup();
    }

    // Publieke methode om een specifiek bedrag aan munten toe te voegen (wordt aangeroepen door CoinPickup.cs).
    public void AddCoins(int amount)
    {
        coinsCollected += amount;
        SFXManager.Instance?.PlayPickup();
    }


    // Retourneert true als het aantal verzamelde onderdelen groter of gelijk is aan wat nodig is.
    public bool HasAllParts() { return partsCollected >= totalPartsNeeded; }
    public bool AllPartsCollected() { return partsCollected >= totalPartsNeeded; } // Alias voor CargoShipEscape.cs

    // Getters om gegevens veilig uit te lezen in andere scripts (zoals UI teksten of schip prompts)
    public int GetPartsCollected() { return partsCollected; }
    public int GetTotalPartsNeeded() { return totalPartsNeeded; }


    // Wordt aangeroepen door de Start knop in het startscherm.
    public void StartGame()
    {
        gameStarted = true;
        Time.timeScale = 1f; // Start de gametijd!
        if (startScreen != null) startScreen.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void GameOverLose()
    {
        if (gameEnded) return;
        gameEnded = true;
        showDeathScreen = true;
        screenAlpha = 0f;
        Time.timeScale = 0.2f; // Slow motion bij overlijden.
    }

    public void GameOverWin()
    {
        if (gameEnded) return;
        gameEnded = true;
        showWinScreen = true;
        screenAlpha = 0f;
        Time.timeScale = 0.1f;
    }

    void MakeEnvironmentSolid()
    {
        GameObject env = GameObject.Find("Environment");
        if (env != null)
        {
            foreach (Transform child in env.GetComponentsInChildren<Transform>())
            {
                if (child.GetComponent<MeshRenderer>() != null && child.GetComponent<Collider>() == null)
                {
                    child.gameObject.AddComponent<BoxCollider>();
                }
            }
        }
    }

    void SetupCanvasScaling() { }
    void SetupSpaceSkybox() { }
}
