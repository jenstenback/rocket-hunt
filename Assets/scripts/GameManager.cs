using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/* =====================================================================================================================
 * SCRIPT: GameManager.cs
 * DOEL: Het centrale hersencentrum en User Interface (UI) manager van heel de game.
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. Singleton Patroon: 'public static GameManager Instance' garandeert dat er overal in de code gemakkelijk met 
 *    deze manager gepraat kan worden via 'GameManager.Instance'.
 * 2. OnGUI Rendering: Voor maximale betrouwbaarheid en compatibiliteit tekenen wij de Healthbar, Banners, en 
 *    de You Died/Victory schermen via Unity's direct-rendering OnGUI systeem. Dit zorgt dat de schermen altijd op de
 *    juiste resolutie en schaal over de 3D-wereld heen liggen.
 * 3. Pauze en Game State management: Dit script regelt de overgangen tussen het Startscherm, Gameplay (Time.timeScale = 1),
 *    Pauze en Game Over schermen.
 * ===================================================================================================================== */

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Spel Voortgang Instellingen")]
    public int totalPartsNeeded = 5;     // Hoeveel scheepsonderdelen nodig zijn om te winnen.
    private int partsCollected = 0;      // Hoeveel onderdelen momenteel verzameld zijn.
    public int coinsCollected = 0;       // Aantal verzamelde munten ($).
    public bool gameStarted = false;     // Is het startscherm weg en zijn we aan het spelen?

    [Header("User Interface Referenties")]
    public TextMeshProUGUI partsText;
    public GameObject startScreen;
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Speler Referentie")]
    public HealthSystem playerHealth;

    private bool gameEnded = false;      // Is het spel afgelopen (winst of verlies)?
    private bool isPaused = false;       // Stelt vast of we op pauze staan [ESC].
    private bool showDeathScreen = false;
    private bool showWinScreen = false;
    private float screenAlpha = 0f;      // Voor het vloeiend indimmen (fade-in) van eindschermen.

    // Voor de uitleg-banner na het verzamelen van 5 onderdelen
    private float escapePromptEndTime = 0f;
    private bool promptTriggered = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        MakeEnvironmentSolid();
        SetupCanvasScaling();
        SetupSpaceSkybox();
    }

    void Start()
    {
        // Reset alle begin-statistieken
        gameStarted = false;
        isPaused = false;
        showDeathScreen = false;
        showWinScreen = false;
        screenAlpha = 0f;
        promptTriggered = false;
        Time.timeScale = 0f; // Zet de gametijd stil zolang we in het startscherm zitten.

        if (startScreen != null) startScreen.SetActive(true);
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
        if (partsText != null) partsText.gameObject.SetActive(false);

        if (playerHealth == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerHealth = ac.GetComponent<HealthSystem>();
        }
        if (playerHealth != null)
        {
            playerHealth.OnDeath.AddListener(GameOverLose); // Koppel Game Over aan speler overlijden.
        }
    }

    void Update()
    {
        // Druk op ESC om te pauzeren of te hervatten.
        if (gameStarted && !gameEnded && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        // Zorg dat de muiscursor weer zichtbaar wordt in menu's of na Game Over.
        if (!gameStarted || gameEnded || isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Vloeiende Fade-In animatie voor de eindschermen.
        if ((showDeathScreen || showWinScreen) && screenAlpha < 1f)
        {
            screenAlpha += Time.unscaledDeltaTime * 0.8f;
            if (screenAlpha > 1f) screenAlpha = 1f;
        }

        // Check of alle 5 onderdelen verzameld zijn en toon de uitlegbanner.
        if (partsCollected >= totalPartsNeeded && !promptTriggered)
        {
            promptTriggered = true;
            escapePromptEndTime = Time.unscaledTime + 25f; // Toon 25 seconden lang.
        }
    }

    // EXAMEN TIP (GUI Rendering):
    // OnGUI wordt door Unity aangeroepen om 2D schermen en knoppen direct over de game te tekenen.
    void OnGUI()
    {
        if (!gameStarted) return;

        if (!gameEnded)
        {
            DrawPartsCounter();       // Teller linksboven (Onderdelen & Munten).
            DrawEscapePromptBanner(); // Banner na 5 onderdelen.
            DrawPlayerHealthBar();    // Levensbalk rechtsonder.
        }
        else
        {
            if (showDeathScreen) DrawDeathScreen();
            else if (showWinScreen) DrawWinScreen();
        }
    }

    // Tekent het vakje linksboven met hoeveel onderdelen en munten je hebt.
    void DrawPartsCounter()
    {
        bool allParts = (partsCollected >= totalPartsNeeded);
        float boxW = allParts ? 450f : 260f;
        float boxH = allParts ? 115f : 65f;
        float margin = 15f;

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

    // Tekent de grote blauwe uitlegbanner zodra je alle 5 onderdelen hebt
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

    // Tekent de dynamische Speler Healthbar rechtsonderin die verkleurt bij schade
    void DrawPlayerHealthBar()
    {
        if (playerHealth == null) return;

        float boxW = 320f; float boxH = 50f; float margin = 20f;
        float boxX = Screen.width - boxW - margin; float boxY = Screen.height - boxH - margin;

        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(boxX - 3, boxY - 3, boxW + 6, boxH + 6), Texture2D.whiteTexture);

        GUI.color = new Color(0.3f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

        float pct = Mathf.Clamp01((float)playerHealth.currentHealth / (float)playerHealth.maxHealth);

        GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.2f, 1f, 0.4f), pct);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW * pct, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20; style.fontStyle = FontStyle.Bold; style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(boxX, boxY, boxW, boxH), "❤ HEALTH: " + playerHealth.currentHealth + " / " + playerHealth.maxHealth, style);
    }

    void DrawDeathScreen()
    {
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

        if (screenAlpha > 0.8f) DrawRestartButton(Screen.height * 0.52f, new Color(0.8f, 0.1f, 0.1f, 1f), new Color(0.3f, 0.05f, 0.05f, 1f));
    }

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
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void AddPart()
    {
        partsCollected++;
        SFXManager.Instance?.PlayPickup();
    }

    public void AddCoin()
    {
        coinsCollected += 10;
        SFXManager.Instance?.PlayPickup();
    }

    public bool HasAllParts() { return partsCollected >= totalPartsNeeded; }

    public void StartGame()
    {
        gameStarted = true;
        Time.timeScale = 1f;
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
        Time.timeScale = 0.2f; // Slow motion effect
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
