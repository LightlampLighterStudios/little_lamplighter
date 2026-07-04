using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;     // how fast he walks left/right
    public float jumpForce = 12f;    // how high he jumps
    public bool isGrounded = true;

    // The little zone he's allowed to walk within (stops him leaving the screen)
    public float minX = -8f;
    public float maxX = 8f;

    // Stores left/right movement from the new Unity Input System.
    private float moveInput = 0f;

    // Stores whether the player is currently holding the interact button.
    // LampController reads this to check if the player is holding E near a lamp.
    public static bool InteractHeld { get; private set; }

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction interactAction;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Gets the PlayerInput component so we can read the Interact action directly.
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            interactAction = playerInput.actions.FindAction("Interact");
        }
        else
        {
            Debug.LogWarning("PlayerInput component is missing from the Player.");
        }
    }

    void Update()
    {
        // Check every frame whether E / Interact is currently being held.
        // This avoids InteractHeld getting stuck on true after the button is released.
        InteractHeld = interactAction != null && interactAction.IsPressed();

        // LEFT / RIGHT movement with arrow keys or A/D.
        // We move by changing velocity.x so it works in the air too (for jumping sideways over puddles).
        // Input now comes from OnMove instead of Input.GetAxisRaw("Horizontal").
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // keep him inside the allowed zone we don't want our littleLamplighter walking of the screen
        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
    }

    // Called by the new Unity Input System when the Move action changes.
    // This should be linked to A/D and Left/Right arrows in InputSystem_Actions.
    public void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        moveInput = input.x;
    }

    // JUMP with Space, only when on the ground.
    // Called by the new Unity Input System when the Jump action is pressed.
    public void OnJump(InputValue value)
    {
        if (!value.isPressed || !isGrounded)
        {
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false;
    }

    // INTERACT with E.
    // This method can stay here for PlayerInput Send Messages,
    // but Update() is the main place where InteractHeld is checked.
    public void OnInteract(InputValue value)
    {
        InteractHeld = interactAction != null && interactAction.IsPressed();
    }

    void OnDisable()
    {
        moveInput = 0f;
        InteractHeld = false;
    }

    // While he is touching the ground, he is allowed to jump.
    void OnCollisionStay2D(Collision2D col)
    {
        isGrounded = true;
    }

    // The moment he leaves the ground, he's airborne.
    void OnCollisionExit2D(Collision2D col)
    {
        isGrounded = false;
    }
}