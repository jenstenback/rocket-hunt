using UnityEngine; // Importeert Unity Engine functionaliteit.

/* =====================================================================================================================
 * SCRIPT: CollectibleSystem.cs
 * DOEL: Hangt aan de 5 scheepsonderdelen (en andere collectibles). Het zorgt ervoor dat het object aantrekkelijk
 *       ronddraait in de 3D-wereld. Zodra de astronaut er doorheen loopt, speelt het een geluidje en particle-effect
 *       af en meldt het aan de GameManager dat er een onderdeel is gevonden!
 * 
 * EXAMEN / PRESENTATIE UITLEG:
 * 1. OnTriggerEnter: Dit is een event van de Unity physics engine. Omdat de collider van het onderdeel op 'Is Trigger'
 *    staat, bots je er niet hard tegenaan, maar loop je er soepel doorheen, wat deze methode direct activeert.
 * 2. PlayClipAtPoint: In plaats van een normaal schiet- of loopgeluid af te spelen op het object zelf (wat afgebroken
 *    zou worden zodra we 'Destroy(gameObject)' doen), spelen we het geluid af op een los wiskundig punt in de ruimte!
 * ===================================================================================================================== */

public class CollectibleSystem : MonoBehaviour
{
    [Header("Instellingen")]
    public float rotationSpeed = 50f;    // Draaisnelheid in graden per seconde.
    public AudioClip collectSound;       // Het geluidje dat afspeelt wanneer je hem oppakt.
    public GameObject collectEffect;     // Vonken of glitters (deeltjeseffect) bij het oppakken.

    // Update() draait elke frame om het object te animeren.
    void Update()
    {
        // Laat het object constant om zijn verticale Y-as (Vector3.up) ronddraaien.
        // Vermenigvuldig met Time.deltaTime zodat hij op 60fps en 144fps exact even snel draait.
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }

    // Wordt aangeroepen door Unity wanneer een ander object (zoals de speler) de triggerzone binnenloopt.
    void OnTriggerEnter(Collider other)
    {
        // Controleer of degene die er doorheen loopt de tag "Player" heeft óf het AstronautController script bezit.
        if (other.CompareTag("Player") || other.GetComponent<AstronautController>() != null)
        {
            Collect(); // Voer de verzamel-logica uit!
        }
    }

    // De hoofdfunctie voor het oppakken van het onderdeel.
    void Collect()
    {
        // Meld aan de GameManager dat we een scheepsonderdeel hebben gevonden!
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectPart();
        }

        // EXAMEN TIP (Audio bij stervende objecten):
        // Als we hier een normale AudioSource zouden gebruiken, stopt het geluid direct bij Destroy(gameObject).
        // AudioSource.PlayClipAtPoint maakt tijdelijk een onzichtbare audio-speler aan op deze coördinaat die zichzelf opruimt als het geluid klaar is!
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        // Spawn het glitter/vonken particle-effect op de positie van het onderdeel.
        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }

        // Vernietig het scheepsonderdeel definitief uit het level.
        Destroy(gameObject);
    }
}
