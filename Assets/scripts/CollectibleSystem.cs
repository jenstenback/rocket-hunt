using UnityEngine;

public class CollectibleSystem : MonoBehaviour
{
    [Header("Settings")]
    public float rotationSpeed = 50f;
    public AudioClip collectSound;
    public GameObject collectEffect;

    void Update()
    {
        // Make the object spin
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Check op Player tag OF AstronautController component
        if (other.CompareTag("Player") || other.GetComponent<AstronautController>() != null)
        {
            Collect();
        }
    }

    void Collect()
    {
        // Tell GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectPart();
        }

        // Play Sound at location so it's not cut off when object is destroyed
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        // Play particle effect
        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }

        // Destroy the collectible
        Destroy(gameObject);
    }
}
