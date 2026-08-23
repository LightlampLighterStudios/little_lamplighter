using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField, Min(0f)] private float backwardScrollCompensation = 1f;
    [SerializeField, Min(0f)] private float moveSpeed = 5f;     // how fast he walks left/right
    [SerializeField, Min(0f)] private float jumpForce = 15f;    // how high he jumps
    // The little zone he's allowed to walk within (stops him leaving the screen)
    [SerializeField] private float minX = -27f;
    [SerializeField] private float maxX = 27f;

    private Rigidbody2D body;
    private PlayerInput playerInput;
    private InputAction interactAction;
    // Stores left/right movement from the new Unity Input System.
    private float moveInput;
    private float speedMultiplier = 1f;
    private bool grounded = true;
    private bool controlsEnabled = true;
    private Coroutine slowRoutine;

    // Stores whether the player is currently holding the interact button.
    // LampController reads this to check if the player is holding E near a lamp.
    public static bool InteractHeld { get; private set; }
    public event Action MovementPerformed;
    public event Action JumpPerformed;

    public Rigidbody2D Body => body;
    public bool IsGrounded => grounded;

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            // Gets the PlayerInput component so we can read the Interact action directly.
            interactAction = playerInput.actions.FindAction("Interact");
        }

        if (worldScroller == null)
        {
            worldScroller = FindFirstObjectByType<WorldScroller>();
        }
    }

    private void Update()
    {
        // Check every frame whether E / Interact is currently being held.
        // This avoids InteractHeld getting stuck on true after the button is released.
        InteractHeld =
            controlsEnabled &&
            interactAction != null &&
            interactAction.IsPressed();

        if (body == null)
        {
            return;
        }

        // LEFT / RIGHT movement with arrow keys or A/D.
        // We move by changing velocity.x so it works in the air too (for jumping sideways over puddles).
        // Input now comes from OnMove instead of Input.GetAxisRaw("Horizontal").
        float horizontalSpeed = moveSpeed * speedMultiplier;

        if (moveInput < 0f && worldScroller != null)
        {
            horizontalSpeed +=
                worldScroller.CurrentScrollSpeed *
                backwardScrollCompensation;
        }

        body.linearVelocity = new Vector2(
            controlsEnabled ? moveInput * horizontalSpeed : 0f,
            body.linearVelocity.y);

        // keep him inside the allowed zone we don't want our littleLamplighter walking of the screen
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        transform.position = position;
    }

    public void Configure(WorldScroller scroller)
    {
        worldScroller = scroller;
    }

    public void OnMove(InputValue value)
    {
        // Called by the new Unity Input System when the Move action changes.
        // This should be linked to A/D and Left/Right arrows in InputSystem_Actions.
        Vector2 input = value.Get<Vector2>();
        moveInput = controlsEnabled ? input.x : 0f;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            MovementPerformed?.Invoke();
        }
    }

    public void OnJump(InputValue value)
    {
        // JUMP with Space, only when on the ground.
        // Called by the new Unity Input System when the Jump action is pressed.
        if (!controlsEnabled || !value.isPressed || !grounded || body == null)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
        grounded = false;
        JumpPerformed?.Invoke();
    }

    public void OnInteract(InputValue value)
    {
        // INTERACT with E.
        // This method can stay here for PlayerInput Send Messages,
        // but Update() is the main place where InteractHeld is checked.
        InteractHeld =
            controlsEnabled &&
            interactAction != null &&
            interactAction.IsPressed();
    }

    public void ApplySlow(float multiplier, float duration)
    {
        // Replacing the current routine makes repeated puddle contacts use a
        // single predictable slowdown window.
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
        }

        slowRoutine = StartCoroutine(SlowRoutine(multiplier, duration));
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled)
        {
            moveInput = 0f;
            InteractHeld = false;
        }
    }

    public void Teleport(Vector3 position)
    {
        // Checkpoint restores also clear residual physics velocity.
        transform.position = position;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        grounded = true;
    }

    private IEnumerator SlowRoutine(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        speedMultiplier = 1f;
        slowRoutine = null;
    }

    private void OnDisable()
    {
        moveInput = 0f;
        InteractHeld = false;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // While he is touching the ground, he is allowed to jump.
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.35f)
            {
                grounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // The moment he leaves the ground, he's airborne.
        grounded = false;
    }
}
