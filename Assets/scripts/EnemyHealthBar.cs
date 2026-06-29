using UnityEngine;

/* ==========================================================================================
 * SCRIPT: EnemyHealthBar.cs
 * DOEL: Tekent een zwevende healthbar direct boven het hoofd van een vijand in 3D-wereldruimte.
 * ========================================================================================== */

public class EnemyHealthBar : MonoBehaviour
{
    private HealthSystem healthSystem;
    private float heightOffset = 2.2f;

    void Start()
    {
        healthSystem = GetComponent<HealthSystem>();
        
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
        {
            heightOffset = col.height * transform.localScale.y + 0.3f;
        }
        else
        {
            heightOffset = 2f * transform.localScale.y + 0.3f;
        }
    }

    void OnGUI()
    {
        if (healthSystem == null || healthSystem.isDead) return;
        if (Camera.main == null) return;

        // Bepaal wereldpositie vlak boven de vijand
        Vector3 worldPos = transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // Alleen tekenen als de vijand vóór de camera staat en niet te ver weg is (max 35 meter)
        if (screenPos.z > 0 && screenPos.z < 35f)
        {
            float distanceFactor = 1f - (screenPos.z / 40f);
            float barW = Mathf.Clamp(100f * distanceFactor, 45f, 100f);
            float barH = Mathf.Clamp(14f * distanceFactor, 8f, 14f);
            float barX = screenPos.x - (barW / 2f);
            float barY = Screen.height - screenPos.y; // Inverteer Y voor OnGUI

            // Donkere rand/achtergrond
            GUI.color = new Color(0.1f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(barX - 2, barY - 2, barW + 4, barH + 4), Texture2D.whiteTexture);

            // Gekleurde balk gebaseerd op health percentage
            float pct = (float)healthSystem.currentHealth / (float)healthSystem.maxHealth;
            pct = Mathf.Clamp01(pct);

            GUI.color = Color.Lerp(Color.red, Color.green, pct);
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
