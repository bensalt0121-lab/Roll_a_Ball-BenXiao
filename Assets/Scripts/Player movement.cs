using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Header("Jump")]
    public float jumpForce = 7f;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public Vector3 groundCheckOffset = new Vector3(0f, -0.7f, 0f);
    public float groundCheckRadius = 0.35f;

    private Rigidbody rb;
    private Vector2 moveInput;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // -------------------------
        // INPUT
        // -------------------------

        moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                moveInput.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                moveInput.y -= 1f;

            if (Keyboard.current.aKey.isPressed)
                moveInput.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                moveInput.x += 1f;

            // Jump
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CheckGround();

                if (IsGrounded)
                    Jump();
            }
        }

        // Ground check ONLY determines IsGrounded
        CheckGround();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        if (cameraTransform == null)
            return;

        // Camera forward/right
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Remove vertical camera angle
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // Camera-relative movement
        Vector3 movement =
            forward * moveInput.y +
            right * moveInput.x;

        movement = Vector3.ClampMagnitude(movement, 1f);

        // ONLY change horizontal velocity
        Vector3 velocity = rb.linearVelocity;

        velocity.x = movement.x * moveSpeed;
        velocity.z = movement.z * moveSpeed;

        rb.linearVelocity = velocity;
    }

    private void Jump()
    {
        Vector3 velocity = rb.linearVelocity;

        // Remove downward velocity
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );
    }

    private void CheckGround()
    {
        Vector3 checkPosition =
            transform.position + groundCheckOffset;

        IsGrounded = Physics.CheckSphere(
            checkPosition,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition =
            transform.position + groundCheckOffset;

        Gizmos.color = IsGrounded
            ? Color.green
            : Color.red;

        Gizmos.DrawWireSphere(
            checkPosition,
            groundCheckRadius
        );
    }
}