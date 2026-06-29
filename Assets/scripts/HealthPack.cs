using UnityEngine;

// Maak een leeg GameObject, sleep HealthPack.cs erop.
// Zorg voor een Collider met "Is Trigger" AAN.
// De speler pakt hem op als hij erover heen loopt!

public class HealthPack : MonoBehaviour
{
    [Header("Health Pack Instellingen")]
    public int healAmount = 30;          // Hoeveel health hij herstelt
    public float rotationSpeed = 90f;   // Hoe snel hij ronddraait
    public AudioClip pickupSound;       // Geluid bij oppakken
    public GameObject pickupEffect;     // Particle effect bij oppakken

    void Update()
    {
        // Laat het item ronddraaiend zweven
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
        transform.position += Vector3.up * Mathf.Sin(Time.time * 2f) * 0.001f;
    }

    void OnTriggerEnter(Collider other)
    {
        // Check op Player tag OF AstronautController component
        if (!other.CompareTag("Player") && other.GetComponent<AstronautController>() == null) return;

        // Heal de speler
        HealthSystem playerHealth = other.GetComponent<HealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.Heal(healAmount);
            Debug.Log("Speler geheald met " + healAmount + " HP!");
        }

        // Speel geluid af
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        // Speel effect af
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        // Vernietigt het health pack
        Destroy(gameObject);
    }
}
