using UnityEngine;

/* ==========================================================================================
 * SCRIPT: CoinPickup.cs
 * DOEL: Munten die vijanden achterlaten. Speler kan ze met 'E' oppakken.
 * ========================================================================================== */

public class CoinPickup : MonoBehaviour
{
    public int coinValue = 100;
    private Transform playerTransform;
    private bool playerInRange = false;

    void Start()
    {
        // Zoek speler
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            AstronautController ac = FindFirstObjectByType<AstronautController>();
            if (ac != null) playerObj = ac.gameObject;
        }
        if (playerObj != null) playerTransform = playerObj.transform;

        // Als dit object nog geen visuele munt is, bouw een mooie gouden munt!
        if (transform.childCount == 0 && GetComponent<MeshRenderer>() == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.parent = transform;
            visual.transform.localPosition = new Vector3(0, 0.5f, 0);
            visual.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Rechtop staand disc

            Collider primCol = visual.GetComponent<Collider>();
            if (primCol != null)
            {
                primCol.enabled = false;
                DestroyImmediate(primCol);
            }

            Renderer rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                Material goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (goldMat.shader.name == "Hidden/InternalErrorShader")
                    goldMat = new Material(Shader.Find("Standard"));

                Color goldColor = new Color(1f, 0.84f, 0f, 1f);
                if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", goldColor);
                if (goldMat.HasProperty("_Color")) goldMat.SetColor("_Color", goldColor);
                if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.9f);
                if (goldMat.HasProperty("_Smoothness")) goldMat.SetFloat("_Smoothness", 0.8f);
                
                // Beetje emissie zodat hij gloeit in het donker
                if (goldMat.HasProperty("_EmissionColor"))
                {
                    goldMat.EnableKeyword("_EMISSION");
                    goldMat.SetColor("_EmissionColor", goldColor * 0.4f);
                }

                rend.material = goldMat;
            }
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Draai de munt mooi rond
        transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);

        // Check afstand tot speler
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        playerInRange = (dist <= 3.5f);

        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            CollectCoin();
        }
    }

    void OnGUI()
    {
        if (playerInRange && GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            float boxW = 260f;
            float boxH = 45f;
            float boxX = (Screen.width - boxW) / 2f;
            float boxY = Screen.height * 0.7f;

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);
            
            GUI.color = new Color(1f, 0.84f, 0f, 1f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(1f, 0.9f, 0.2f, 1f);

            GUI.Label(new Rect(boxX, boxY, boxW, boxH), "[E] Pak Munt op (+$" + coinValue + ")", style);
        }
    }

    void CollectCoin()
    {
        SFXManager.Instance?.PlayPickup();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCoins(coinValue);
        }
        Destroy(gameObject);
    }
}
