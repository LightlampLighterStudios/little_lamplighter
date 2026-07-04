using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;     // how fast he walks left/right
    public float jumpForce = 12f;    // how high he jumps
    public bool isGrounded = true;

    // The little zone he's allowed to walk within (stops him leaving the screen)
    public float minX = -8f;
    public float maxX = 8f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // LEFT / RIGHT movement with arrow keys or A/D.
        // We move by changing velocity.x so it works in the air too (for jumping sideways over puddles).
        float input = Input.GetAxisRaw("Horizontal");   // -1 left, +1 right, 0 none
        rb.linearVelocity = new Vector2(input * moveSpeed, rb.linearVelocity.y);

        // keep him inside the allowed zone we don't want our littleLamplighter walking of the screen
        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);

        // JUMP with Space, only when on the ground
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }
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