using UnityEngine;

// Hang dit script op het monster.
// Vul in de Inspector in welke items kunnen droppen.
// Als het monster doodgaat, laat het automatisch loot vallen!

public class LootDrop : MonoBehaviour
{
    [System.Serializable]
    public class LootItem
    {
        public string naam;           // Naam (voor jezelf)
        public GameObject prefab;     // Het item dat er uitkomt
        [Range(0f, 100f)]
        public float kans = 50f;      // Kans in procenten (50 = 50%)
    }

    [Header("Loot Instellingen")]
    public LootItem[] mogelijkeLoot;

    [Header("Explosie bij Dood (Optioneel)")]
    public GameObject explosionEffect; // Particle system explosie-effect

    private HealthSystem healthSystem;

    void Start()
    {
        healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            // Koppel aan het OnDeath event van het monster
            healthSystem.OnDeath.AddListener(DropLoot);
        }
    }

    void DropLoot()
    {
        // Speel explosie-effect af als het monster doodgaat
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position + Vector3.up, Quaternion.identity);
        }

        // Loop door alle mogelijke loot items
        foreach (LootItem item in mogelijkeLoot)
        {
            // Gooi een willekeurig getal tussen 0 en 100
            float roll = Random.Range(0f, 100f);

            if (roll <= item.kans && item.prefab != null)
            {
                // Laat het item vallen op een iets willekeurige positie zodat items niet stapelen
                Vector3 dropPosition = transform.position + new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    0.5f,  // Iets boven de grond
                    Random.Range(-0.5f, 0.5f)
                );

                Instantiate(item.prefab, dropPosition, Quaternion.identity);
                Debug.Log(gameObject.name + " dropt: " + item.naam);
            }
        }
    }
}
