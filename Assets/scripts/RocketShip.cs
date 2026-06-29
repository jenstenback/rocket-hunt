using UnityEngine;
using System.Collections;

// Dit script zit op het Cargo Ship.
// Als de speler alle onderdelen heeft en op [E] drukt, vliegt het schip weg en wint de speler!

public class RocketShip : MonoBehaviour
{
    [Header("Escape Instellingen")]
    public float flySpeed = 5f;
    public float flyForwardSpeed = 8f;
    public float escapeTime = 4f;
    public AudioClip engineSound;

    private bool playerInRange = false;
    private bool isEscaping = false;
    private Transform playerTransform;
    private CharacterController playerController;
    private AstronautController playerMovement;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && engineSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (isEscaping || !playerInRange) return;
        if (Time.timeScale == 0f) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (GameManager.Instance != null && GameManager.Instance.AllPartsCollected())
            {
                StartCoroutine(EscapeSequence());
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check op tag OF op AstronautController component
        if (other.CompareTag("Player") || other.GetComponent<AstronautController>() != null || other.GetComponent<CharacterController>() != null)
        {
            playerInRange = true;
            playerTransform = other.transform;
            playerController = other.GetComponent<CharacterController>();
            playerMovement = other.GetComponent<AstronautController>();
            Debug.Log("Speler is bij het Cargo Ship!");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<AstronautController>() != null || other.GetComponent<CharacterController>() != null)
        {
            playerInRange = false;
            playerTransform = null;
            Debug.Log("Speler verlaat het Cargo Ship.");
        }
    }

    IEnumerator EscapeSequence()
    {
        isEscaping = true;
        Debug.Log("ESCAPE SEQUENCE GESTART!");

        // 1. Zet de speler vast
        if (playerMovement != null)
            playerMovement.enabled = false;
        if (playerController != null)
            playerController.enabled = false;

        // Maak de speler een kind van het schip
        if (playerTransform != null)
            playerTransform.SetParent(transform);

        // 2. Speel motorgeluid af
        if (engineSound != null && audioSource != null)
        {
            audioSource.clip = engineSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 3. Vlieg omhoog en vooruit!
        float timer = 0f;
        Vector3 flyDirection = (Vector3.up * flySpeed) + (transform.forward * flyForwardSpeed);

        while (timer < escapeTime)
        {
            transform.position += flyDirection * Time.deltaTime;
            flyDirection *= 1.005f;
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. Win de game!
        if (audioSource != null) audioSource.Stop();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOverWin();
        }
    }

    void OnGUI()
    {
        if (!playerInRange || isEscaping) return;
        if (Time.timeScale == 0f) return;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.fontSize = 28;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.fontStyle = FontStyle.Bold;

        float boxWidth = 500f;
        float boxHeight = 50f;
        float cx = (Screen.width - boxWidth) / 2f;
        float cy = Screen.height * 0.7f;

        if (GameManager.Instance != null && GameManager.Instance.AllPartsCollected())
        {
            promptStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(cx, cy, boxWidth, boxHeight), "Druk op [E] om te ontsnappen!", promptStyle);
        }
        else
        {
            promptStyle.normal.textColor = new Color(1f, 0.5f, 0f);
            int collected = GameManager.Instance != null ? GameManager.Instance.GetPartsCollected() : 0;
            int needed = GameManager.Instance != null ? GameManager.Instance.GetTotalPartsNeeded() : 0;
            int remaining = needed - collected;
            GUI.Label(new Rect(cx, cy, boxWidth, boxHeight), "Je hebt nog " + remaining + " onderdelen nodig!", promptStyle);
        }
    }
}
