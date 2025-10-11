using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
    // Movement variables
    public float moveSpeed = 5f; // How fast the character moves
    public float jumpForce = 5f; // How high the character jumps
    public float gravityScale = 2f; // How gravity affects the character

    private Vector3 moveDirection;
    private bool isGrounded;
    private float verticalVelocity;

    private Rigidbody rb;

    private void Start()
    {
        // Get the Rigidbody component attached to the character
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // Check if the character is grounded
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        // Handle player movement input
        HandleMovement();

        // Apply gravity
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // Get player input for movement
        float moveX = Input.GetAxis("Horizontal"); // A/D or Left/Right Arrow
        float moveZ = Input.GetAxis("Vertical");   // W/S or Up/Down Arrow

        // Move the character (using local space for relative movement)
        moveDirection = new Vector3(moveX, 0f, moveZ).normalized;

        // Move the character based on input and speed
        Vector3 move = moveDirection * moveSpeed * Time.deltaTime;
        rb.MovePosition(transform.position + move);

        // Jumping logic
        if (isGrounded && Input.GetButtonDown("Jump")) // Spacebar to jump
        {
            verticalVelocity = jumpForce; // Apply jump force
        }
    }

    private void ApplyGravity()
    {
        // If the character is not grounded, apply gravity
        if (!isGrounded)
        {
            verticalVelocity -= gravityScale * Time.deltaTime; // Simulate gravity
        }
        else
        {
            // Reset vertical velocity when grounded
            verticalVelocity = 0f;
        }

        // Apply vertical velocity (falling/jumping)
        Vector3 velocity = rb.linearVelocity;
        velocity.y = verticalVelocity;
        rb.linearVelocity = velocity;
    }
}
