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

    [Header("Taper attachment")]
    [SerializeField] private SpriteRenderer taperRenderer;
    // Normalized artwork coordinates, measured from the bottom-left corner.
    [SerializeField] private Vector2 idleHand = new Vector2(0.715f, 0.305f);
    [SerializeField] private Vector2[] rightHands = {
        new Vector2(0.695f, 0.300f), new Vector2(0.570f, 0.265f),
        new Vector2(0.460f, 0.245f) };
    [SerializeField] private float[] rightTaperAngles = { 0f, -20f, -35f };
    [SerializeField] private Vector2 taperGrip = new Vector2(0.505f, 0.300f);

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

        if (taperRenderer == null)
        {
            var taper = GetComponentInChildren<TaperPoleWet>(true);
            if (taper != null) taperRenderer = taper.GetComponent<SpriteRenderer>();
        }
    }

    // Called by CharacterWetState to turn the wet look on or off.
    public void SetWet(bool wet)
    {
        isWet = wet;
    }

    private void LateUpdate()
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
            spriteRenderer.flipX = false;
            if (taperRenderer != null) taperRenderer.flipX = false;
            spriteRenderer.sprite = isWet ? idleWetSprite : idleSprite;
            AttachTaper(idleHand, 0f, false);
            return;
        }

        // Advance the walk cycle at a fixed rate independent of frame rate.
        frameTimer += Time.deltaTime;

        if (frameTimer >= secondsPerFrame)
        {
            frameTimer -= secondsPerFrame;
            frameIndex = (frameIndex + 1) % 3;
        }

        Sprite[] rightFrames = isWet ? walkRightWetFrames : walkRightFrames;
        Sprite[] leftFrames = isWet ? walkLeftWetFrames : walkLeftFrames;
        bool hasDistinctLeftFrames = HasDistinctFrames(leftFrames, rightFrames);

        // Several authored scenes point both walk directions at the same
        // right-facing artwork. Mirror that artwork while moving left so the
        // character's feet face the direction of travel instead of moonwalking.
        bool mirrorRightArtwork = !facingRight && !hasDistinctLeftFrames;
        spriteRenderer.flipX = mirrorRightArtwork;
        if (taperRenderer != null) taperRenderer.flipX = mirrorRightArtwork;

        Sprite[] activeFrames = facingRight || !hasDistinctLeftFrames
            ? rightFrames
            : leftFrames;

        if (activeFrames != null && activeFrames.Length == 3 && activeFrames[frameIndex] != null)
        {
            spriteRenderer.sprite = activeFrames[frameIndex];
            if (rightHands != null && frameIndex < rightHands.Length)
            {
                float rightAngle = rightTaperAngles != null &&
                    frameIndex < rightTaperAngles.Length
                        ? rightTaperAngles[frameIndex]
                        : 0f;
                AttachTaper(rightHands[frameIndex],
                    facingRight ? rightAngle : -rightAngle,
                    facingRight);
            }
        }
    }

    private static bool HasDistinctFrames(Sprite[] leftFrames, Sprite[] rightFrames)
    {
        if (leftFrames == null || leftFrames.Length != 3)
        {
            return false;
        }

        for (int index = 0; index < leftFrames.Length; index++)
        {
            if (leftFrames[index] != null &&
                (rightFrames == null || index >= rightFrames.Length ||
                 leftFrames[index] != rightFrames[index]))
            {
                return true;
            }
        }

        return false;
    }

    private void AttachTaper(Vector2 hand, float angle, bool behindCharacter)
    {
        if (taperRenderer == null || taperRenderer.sprite == null ||
            spriteRenderer.sprite == null || taperRenderer.transform.parent != transform)
            return;

        Vector2 handPoint = SpritePoint(spriteRenderer.sprite, hand);
        if (spriteRenderer.flipX) handPoint.x = -handPoint.x;
        if (spriteRenderer.flipY) handPoint.y = -handPoint.y;
        Vector2 gripPoint = SpritePoint(taperRenderer.sprite, taperGrip);
        if (taperRenderer.flipX) gripPoint.x = -gripPoint.x;
        if (taperRenderer.flipY) gripPoint.y = -gripPoint.y;
        Transform pole = taperRenderer.transform;
        pole.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 gripOffset = pole.localRotation * Vector3.Scale(
            new Vector3(gripPoint.x, gripPoint.y, 0f), pole.localScale);
        // Work in player-local space: supplied player transforms have zero Z scale.
        pole.localPosition = new Vector3(
            handPoint.x - gripOffset.x, handPoint.y - gripOffset.y,
            pole.localPosition.z);
        // Right-facing poses hold the taper with the far hand, behind the body.
        // The mirrored left-facing pose brings it to the foreground.
        taperRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        taperRenderer.sortingOrder = spriteRenderer.sortingOrder +
            (behindCharacter ? -1 : 1);
    }

    private static Vector2 SpritePoint(Sprite sprite, Vector2 normalized)
    {
        return (Vector2.Scale(normalized, sprite.rect.size) - sprite.pivot)
            / sprite.pixelsPerUnit;
    }
}
