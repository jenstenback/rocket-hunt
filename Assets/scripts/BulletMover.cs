using UnityEngine;

// Dit script wordt automatisch toegevoegd als een bullet-prefab geen Rigidbody heeft.
// (Bijvoorbeeld een particle-effect blaster zoals "PRO Effects Sci-Fi")
// Het beweegt het object elke frame vooruit in de juiste richting.

public class BulletMover : MonoBehaviour
{
    [HideInInspector] public float speed = 50f;
    [HideInInspector] public Vector3 direction;

    private float lifetime = 3f;

    void Start()
    {
        // Automatisch vernietigen na 3 seconden zodat er geen rommel blijft zweven
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Beweeg de blaster elke frame vooruit
        transform.position += direction * speed * Time.deltaTime;
    }
}
