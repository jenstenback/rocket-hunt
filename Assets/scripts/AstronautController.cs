using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class AstronautController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 7f;
    public float sprintSpeed = 12f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Look Settings")]
    public float mouseSensitivity = 300f;
    public Transform playerCamera;

    [Header("Animation")]
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;
    private bool isDead = false;
    private Vector3 initialCameraLocalPos;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (mouseSensitivity <= 100f) mouseSensitivity = 300f;

        if (playerCamera != null)
        {
            initialCameraLocalPos = playerCamera.localPosition;
        }

        // Zorg dat colliders op de speler NIET trigger zijn (muren tegenhouden)
        foreach (Collider c in GetComponents<Collider>())
        {
            if (c != controller && c.isTrigger)
                c.isTrigger = false;
        }

        // Zorg dat ragdoll physics en bot-colliders de animatie en wapenpositie nooit verstoren!
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
        {
            if (rb != GetComponent<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        Collider[] childCols = GetComponentsInChildren<Collider>();
        foreach (Collider c in childCols)
        {
            if (c != controller && c != GetComponent<Collider>())
            {
                if (controller != null) Physics.IgnoreCollision(c, controller);
                c.isTrigger = true;
            }
        }
    }

    void Update()
    {
        if (isDead) return;
        if (Time.timeScale == 0f) return;

        if (GameManager.Instance != null && GameManager.Instance.gameStarted)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        HandleMovement();
        HandleMouseLook();
    }

    void LateUpdate()
    {
        // FIX: Voorkom hevige camera shake door root motion tijdens het sprinten.
        // We verankeren de camera lokaal op zijn beginpositie of strijken hem zachtjes glad.
        if (playerCamera != null && !isDead)
        {
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, initialCameraLocalPos, Time.deltaTime * 25f);
        }
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        if (move.magnitude > 1f) move.Normalize();

        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);

        // Springen - alleen als we op de grond staan
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Zwaartekracht & Anti-Float Bescherming
        if (!isGrounded && transform.position.y > 2.5f)
        {
            velocity.y -= 40f * Time.deltaTime; // Extra sterke zwaartekracht als je omhoog gelanceerd/gefloat bent
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        controller.Move(velocity * Time.deltaTime);

        // Animatie
        if (animator != null)
        {
            float speed = move.magnitude > 0.1f ? (isSprinting ? 1f : 0.5f) : 0f;
            animator.SetFloat("Speed", speed);
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * (mouseSensitivity * 0.012f);
        float mouseY = Input.GetAxis("Mouse Y") * (mouseSensitivity * 0.012f);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    public void AddRecoil(float amount)
    {
        xRotation -= amount;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
    }

    public void SetDead()
    {
        isDead = true;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }
}
