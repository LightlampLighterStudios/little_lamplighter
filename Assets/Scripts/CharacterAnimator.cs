using UnityEngine;

// Cycles the character's walk frames while moving, shows a still jump
// sprite while airborne whilst shows the idle sprite while standing still and
// swaps to the wet frame sets when SetWet(true) is called (see
// CharacterWetState.cs)
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CharacterAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PlayerController player;

    [Header("Idle")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite idleWetSprite;

    [Header("Jump (shown while airborne, replaces the walk cycle)")]
    [SerializeField] private Sprite jumpRightSprite;
    [SerializeField] private Sprite jumpLeftSprite;
    [SerializeField] private Sprite jumpRightWetSprite; // optional, leave empty if you don't have one yet
    [SerializeField] private Sprite jumpLeftWetSprite;   // optional, leave empty if you don't have one yet

    [Header("Walk Right (3 frames)")]
    [SerializeField] private Sprite[] walkRightFrames = new Sprite[3];
    [SerializeField] private Sprite[] walkRightWetFrames = new Sprite[3];

    [Header("Walk Left (3 frames)")]
    [SerializeField] private Sprite[] walkLeftFrames = new Sprite[3];
    [SerializeField] private Sprite[] walkLeftWetFrames = new Sprite[3];

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float secondsPerFrame = 0.12f;
    [SerializeField, Min(0f)] private float moveThreshold = 0.05f;

    private bool isWet;
    private bool facingRight = true;
    private int frameIndex;
    private float frameTimer;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }
    }

    // Called by CharacterWetState to turn the wet look on or off.
    public void SetWet(bool wet)
    {
        isWet = wet;
    }

    private void Update()
    {
        if (spriteRenderer == null || player == null || player.Body == null)
        {
            return;
        }

        float velocityX = player.Body.linearVelocity.x;
        bool moving = Mathf.Abs(velocityX) > moveThreshold;

        // Remember which way he was last facing, since velocityX can be
        // near zero mid-jump (e.g. jumping straight up over a puddle) This will help make things look more professional.
        if (moving)
        {
            facingRight = velocityX > 0f;
        }

        // Airborne takes priority over everything else: freeze the walk
        // cycle and show a single jump pose instead facing right wat
        if (!player.IsGrounded)
        {
            frameIndex = 0;
            frameTimer = 0f;

            Sprite jumpSprite = facingRight
                ? (isWet && jumpRightWetSprite != null ? jumpRightWetSprite : jumpRightSprite)
                : (isWet && jumpLeftWetSprite != null ? jumpLeftWetSprite : jumpLeftSprite);

            if (jumpSprite != null)
            {
                spriteRenderer.sprite = jumpSprite;
            }

            return;
        }

        if (!moving)
        {
            // Standing still: reset the walk cycle and show the idle sprite.
            frameIndex = 0;
            frameTimer = 0f;
            spriteRenderer.sprite = isWet ? idleWetSprite : idleSprite;
            return;
        }

        // Advance the walk cycle at a fixed rate independent of frame rate.
        frameTimer += Time.deltaTime;

        if (frameTimer >= secondsPerFrame)
        {
            frameTimer -= secondsPerFrame;
            frameIndex = (frameIndex + 1) % 3;
        }

        Sprite[] activeFrames = facingRight
            ? (isWet ? walkRightWetFrames : walkRightFrames)
            : (isWet ? walkLeftWetFrames : walkLeftFrames);

        if (activeFrames != null && activeFrames.Length == 3 && activeFrames[frameIndex] != null)
        {
            spriteRenderer.sprite = activeFrames[frameIndex];
        }
    }
}