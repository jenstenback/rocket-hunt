using UnityEngine;
using System.Collections;

// Hang dit script op het Cargo Ship.
// Zorg dat er een BoxCollider met "Is Trigger" AAN op het ship zit.
// Als de speler alle onderdelen heeft verzameld en op [E] drukt, vliegt het schip weg en win je!

public class CargoShipEscape : MonoBehaviour
{
    [Header("Escape Instellingen")]
    public float flySpeed = 5f;
    public float flyForwardSpeed = 8f;
    public float escapeTime = 4f;
    public AudioClip engineSound;

    [Header("UI Prompt")]
    public string interactKey = "E";

    private bool playerInRange = false;
    private bool isEscaping = false;
    private Transform playerTransform;
    private CharacterController playerController;
    private AstronautController playerMovement;
    private AudioSource audioSource;

    private Vector3 initialShipPos;
    private Quaternion initialShipRot;
    private Vector3 initialPlayerPos;
    private Quaternion initialPlayerRot;

    void Start()
    {
        initialShipPos = transform.position;
        initialShipRot = transform.rotation;

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
            else
            {
                Debug.Log("Niet genoeg onderdelen om te ontsnappen!");
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerTransform = other.transform;
            playerController = other.GetComponent<CharacterController>();
            playerMovement = other.GetComponent<AstronautController>();

            initialPlayerPos = playerTransform.position;
            initialPlayerRot = playerTransform.rotation;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerTransform = null;
        }
    }

    IEnumerator EscapeSequence()
    {
        isEscaping = true;

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerController != null) playerController.enabled = false;

        if (playerTransform != null) playerTransform.SetParent(transform);

        if (engineSound != null && audioSource != null)
        {
            audioSource.clip = engineSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        float timer = 0f;
        Vector3 flyDirection = (Vector3.up * flySpeed) + (transform.forward * flyForwardSpeed);

        while (timer < escapeTime)
        {
            transform.position += flyDirection * Time.deltaTime;
            flyDirection *= 1.005f;
            timer += Time.deltaTime;
            yield return null;
        }

        if (audioSource != null) audioSource.Stop();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOverWin();
        }
    }

    public void ResetPlayerForContinue()
    {
        isEscaping = false;
        
        // Zet schip terug op zijn plek
        transform.position = initialShipPos;
        transform.rotation = initialShipRot;

        // Maak speler los van schip en zet stevig op de grond
        if (playerTransform != null)
        {
            playerTransform.SetParent(null);
            if (playerController != null)
            {
                playerController.enabled = false;
                playerTransform.position = initialPlayerPos;
                playerTransform.rotation = initialPlayerRot;
                playerController.enabled = true;
            }
            else
            {
                playerTransform.position = initialPlayerPos;
                playerTransform.rotation = initialPlayerRot;
            }
        }

        if (playerMovement != null) playerMovement.enabled = true;
        if (playerController != null) playerController.enabled = true;
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
            GUI.Label(new Rect(cx, cy, boxWidth, boxHeight), "Druk op [" + interactKey + "] om te ontsnappen! 🚀", promptStyle);
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
