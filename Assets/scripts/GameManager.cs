using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/* ==========================================================================================
 * SCRIPT: GameManager.cs
 * DOEL: Het centrale hersencentrum (State Machine) van de hele game.
 * ========================================================================================== */

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Spel Voortgang Instellingen")]
    public int totalPartsNeeded = 5;
    private int partsCollected = 0;
    public int coinsCollected = 0;
    public bool gameStarted = false;

    [Header("User Interface Referenties")]
    public TextMeshProUGUI partsText;
    public GameObject startScreen;
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Speler Referentie")]
    public HealthSystem playerHealth;

    private bool gameEnded = false;
    private bool isPaused = false;
    private bool showDeathScreen = false;
    private bool showWinScreen = false;
    private float screenAlpha = 0f;

    // Voor de melding na 5 onderdelen
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
        gameStarted = false;
        isPaused = false;
        showDeathScreen = false;
        showWinScreen = false;
        screenAlpha = 0f;
        promptTriggered = false;
        Time.timeScale = 0f;

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
            playerHealth.OnDeath.AddListener(GameOverLose);
        }
    }

    void Update()
    {
        if (gameStarted && !gameEnded && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (!gameStarted || gameEnded || isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if ((showDeathScreen || showWinScreen) && screenAlpha < 1f)
        {
            screenAlpha += Time.unscaledDeltaTime * 0.8f;
            if (screenAlpha > 1f) screenAlpha = 1f;
        }

        // Check veiligheidshalve ook in Update of we op 5 zitten en de melding nog niet geactiveerd is
        if (partsCollected >= totalPartsNeeded && !promptTriggered)
        {
            promptTriggered = true;
            escapePromptEndTime = Time.unscaledTime + 25f; // 25 seconden lang zichtbaar (onafhankelijk van timeScale)
        }
    }

    void OnGUI()
    {
        if (!gameStarted) return;

        if (!gameEnded)
        {
            DrawPartsCounter();
            DrawEscapePromptBanner();
            DrawPlayerHealthBar();
        }
        else
        {
            if (showDeathScreen) DrawDeathScreen();
            else if (showWinScreen) DrawWinScreen();
        }
    }

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
        styleParts.fontSize = 17;
        styleParts.fontStyle = FontStyle.Bold;
        styleParts.normal.textColor = new Color(0.4f, 0.9f, 1f, 1f);
        styleParts.padding = new RectOffset(10, 0, 5, 0);

        string textParts = allParts 
            ? "✓ ALLE 5 ONDERDELEN VERZAMELD!" 
            : "⚙ Onderdelen: " + partsCollected + " / " + totalPartsNeeded;
        GUI.Label(new Rect(margin, margin, boxW, 30f), textParts, styleParts);

        GUIStyle styleCoins = new GUIStyle(GUI.skin.label);
        styleCoins.fontSize = 17;
        styleCoins.fontStyle = FontStyle.Bold;
        styleCoins.normal.textColor = new Color(1f, 0.85f, 0.2f, 1f);
        styleCoins.padding = new RectOffset(10, 0, 0, 0);

        GUI.Label(new Rect(margin, margin + 32f, boxW, 30f), "💰 Munten: $" + coinsCollected, styleCoins);

        // Permanent hintje onder de teller zodra je alle onderdelen hebt
        if (allParts)
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 13;
            hintStyle.normal.textColor = new Color(0.8f, 1f, 0.8f, 0.95f);
            hintStyle.padding = new RectOffset(10, 10, 0, 0);
            hintStyle.wordWrap = true;

            string tip = "💡 Tip: Breng alle 5 onderdelen naar het cargo ship om direct te winnen, OF speel door de waves heen voor extra munten!";
            GUI.Label(new Rect(margin, margin + 62f, boxW, 50f), tip, hintStyle);
        }
    }

    void DrawEscapePromptBanner()
    {
        if (Time.unscaledTime < escapePromptEndTime)
        {
            float boxW = 750f;
            float boxH = 110f;
            float boxX = (Screen.width - boxW) / 2f;
            float boxY = 25f;

            GUI.color = new Color(0f, 0.15f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            GUI.color = new Color(0.3f, 0.9f, 1f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 3f, boxW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 17;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(0.9f, 0.98f, 1f, 1f);
            style.wordWrap = true;

            // PRECIES DE TEKST GEVRAAGD DOOR DE GEBRUIKER:
            string bannerMsg = "🎉 ALLE ONDERDELEN VERZAMELD!\n" +
                               "Als je munten wilt verzamelen speel door de waves heen en verzamel de munten,\n" +
                               "je kan op elk punt de game winnen door alle 5 de onderdelen naar het cargo ship te brengen en weg te vliegen!";

            GUI.Label(new Rect(boxX + 15f, boxY, boxW - 30f, boxH), bannerMsg, style);
        }
    }

    void DrawPlayerHealthBar()
    {
        if (playerHealth == null) return;

        float boxW = 320f;
        float boxH = 50f;
        float margin = 20f;
        float boxX = Screen.width - boxW - margin;
        float boxY = Screen.height - boxH - margin;

        // Rand / achtergrond
        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(boxX - 3, boxY - 3, boxW + 6, boxH + 6), Texture2D.whiteTexture);

        // Donkerrode achtergrond
        GUI.color = new Color(0.3f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

        // Fill balk (kleurt van rood naar felgroen)
        float pct = (float)playerHealth.currentHealth / (float)playerHealth.maxHealth;
        pct = Mathf.Clamp01(pct);

        GUI.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.2f, 1f, 0.4f), pct);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW * pct, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Tekst eroverheen
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
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
        titleStyle.fontSize = 72;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.8f, 0.1f, 0.1f, screenAlpha);

        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100f), "YOU DIED", titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 24;
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, screenAlpha);
        GUI.Label(new Rect(0, Screen.height * 0.2f + 90f, Screen.width, 40f), "De aliens hebben je te pakken gekregen...", subStyle);

        if (screenAlpha > 0.8f)
        {
            DrawRestartButton(Screen.height * 0.52f, new Color(0.8f, 0.1f, 0.1f, 1f), new Color(0.3f, 0.05f, 0.05f, 1f));
        }
    }

    void DrawWinScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, screenAlpha * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = new Color(0f, 0.4f, 0.1f, screenAlpha * 0.35f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 64;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(0.2f, 0.9f, 0.4f, screenAlpha);

        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 90f), "VICTORY - ONTSNAPPING GESLAAGD!", titleStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 22;
        subStyle.alignment = TextAnchor.MiddleCenter;
        subStyle.normal.textColor = new Color(0.8f, 0.9f, 0.8f, screenAlpha);
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
            btnStyle.fontSize = 22; btnStyle.fontStyle = FontStyle.Bold;
            btnStyle.alignment = TextAnchor.MiddleCenter; btnStyle.normal.textColor = Color.white;

            if (btnRect.Contains(Event.current.mousePosition))
            {
                GUI.color = new Color(0.1f, 0.3f, 0.4f, 1f);
                GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (GUI.Button(btnRect, "", GUIStyle.none))
            {
                ContinuePlaying();
            }
            GUI.Label(btnRect, "▶ VERDER SPELEN (SCOREN)", btnStyle);
        }
    }

    void DrawRestartButton(float btnY, Color borderColor, Color hoverColor)
    {
        float btnW = 260f; float btnH = 55f;
        float btnX = (Screen.width - btnW) / 2f;
        Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

        GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        GUI.DrawTexture(btnRect, Texture2D.whiteTexture);

        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(btnX, btnY, btnW, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnY + btnH - 2f, btnW, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnY, 2f, btnH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX + btnW - 2f, btnY, 2f, btnH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.label);
        btnStyle.fontSize = 26; btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.alignment = TextAnchor.MiddleCenter; btnStyle.normal.textColor = Color.white;

        if (btnRect.Contains(Event.current.mousePosition))
        {
            GUI.color = hoverColor;
            GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        if (GUI.Button(btnRect, "", GUIStyle.none))
        {
            RestartGame();
        }
        GUI.Label(btnRect, "⟳ OPNIEUW SPELEN", btnStyle);

        GUIStyle scoreStyle = new GUIStyle(GUI.skin.label);
        scoreStyle.fontSize = 18; scoreStyle.alignment = TextAnchor.MiddleCenter;
        scoreStyle.normal.textColor = new Color(0.8f, 0.9f, 1f, screenAlpha);
        GUI.Label(new Rect(0, btnY + btnH + 80f, Screen.width, 30f), 
            "Onderdelen: " + partsCollected + " / " + totalPartsNeeded + "   |   Munten verdiend: $" + coinsCollected, scoreStyle);
    }

    public void AddCoins(int amount) { coinsCollected += amount; }

    public void ContinuePlaying()
    {
        gameEnded = false; showWinScreen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;

        CargoShipEscape escapeShip = FindFirstObjectByType<CargoShipEscape>();
        if (escapeShip != null)
        {
            escapeShip.ResetPlayerForContinue();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            Time.timeScale = 0f;
            if (startScreen != null) startScreen.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            if (startScreen != null) startScreen.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    public void StartGame()
    {
        if (isPaused) { TogglePause(); return; }
        gameStarted = true; isPaused = false; Time.timeScale = 1f;
        if (startScreen != null) startScreen.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    public void CollectPart()
    {
        if (gameEnded) return;
        partsCollected++;
        SFXManager.Instance?.PlayPickup();

        if (partsCollected >= totalPartsNeeded && !promptTriggered)
        {
            promptTriggered = true;
            escapePromptEndTime = Time.unscaledTime + 25f;
        }
    }

    public bool AllPartsCollected() { return partsCollected >= totalPartsNeeded; }
    public int GetPartsCollected() { return partsCollected; }
    public int GetTotalPartsNeeded() { return totalPartsNeeded; }

    public void AttemptRepairShip()
    {
        if (partsCollected >= totalPartsNeeded) GameOverWin();
    }

    public void GameOverWin()
    {
        gameEnded = true; SFXManager.Instance?.PlayWin();
        if (winScreen != null) winScreen.SetActive(false);
        showWinScreen = true; showDeathScreen = false; screenAlpha = 0f;
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    void GameOverLose()
    {
        gameEnded = true; SFXManager.Instance?.PlayLose();
        showDeathScreen = true; showWinScreen = false; screenAlpha = 0f;
        if (loseScreen != null) loseScreen.SetActive(false);
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    public void RestartGame()
    {
        showDeathScreen = false; showWinScreen = false; Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void MakeEnvironmentSolid()
    {
        Collider[] allCols = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider c in allCols)
        {
            if (!c.isTrigger) continue;
            if (c.GetComponent<CollectibleSystem>() != null || c.GetComponentInParent<CollectibleSystem>() != null) continue;
            if (c.GetComponent<HealthPack>() != null || c.GetComponentInParent<HealthPack>() != null) continue;
            if (c.GetComponent<HeadshotCollider>() != null || c.GetComponentInParent<HeadshotCollider>() != null) continue;
            if (c.GetComponent<CargoShipEscape>() != null || c.GetComponentInParent<CargoShipEscape>() != null) continue;
            if (c.GetComponent<CoinPickup>() != null || c.GetComponentInParent<CoinPickup>() != null) continue;
            if (c.CompareTag("Player") || c.GetComponentInParent<AstronautController>() != null) continue;
            if (c.GetComponent<EnemyAI>() != null || c.GetComponentInParent<EnemyAI>() != null) continue;
            c.isTrigger = false;
        }
    }

    void SetupCanvasScaling()
    {
        UnityEngine.UI.CanvasScaler[] scalers = FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None);
        foreach (var s in scalers)
        {
            s.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
        }
    }

    void SetupSpaceSkybox()
    {
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null) skyShader = Shader.Find("Universal Render Pipeline/Skybox/Procedural");
        if (skyShader != null)
        {
            Material spaceSkybox = new Material(skyShader);
            spaceSkybox.SetFloat("_SunSize", 0.02f);
            spaceSkybox.SetFloat("_SunSizeConvergence", 5f);
            spaceSkybox.SetFloat("_AtmosphereThickness", 0.1f);
            spaceSkybox.SetFloat("_Exposure", 0.3f);
            spaceSkybox.SetColor("_SkyTint", new Color(0.02f, 0.01f, 0.05f, 1f));
            spaceSkybox.SetColor("_GroundColor", new Color(0.01f, 0.01f, 0.02f, 1f));
            RenderSettings.skybox = spaceSkybox;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.06f, 0.12f, 1f);
        RenderSettings.fog = false;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.backgroundColor = new Color(0.01f, 0.005f, 0.03f, 1f);
        }
    }
}
