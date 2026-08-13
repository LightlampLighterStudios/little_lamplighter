using UnityEngine;

public sealed class SkyProgressController : MonoBehaviour
{
    // References used to read progress and fade sky decorations.
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SpriteRenderer moon;
    [SerializeField] private SpriteRenderer stars;

    // The moon fades in between 25% and 55% night progress.
    [Header("Moon Fade")]
    [SerializeField, Range(0f, 1f)] private float moonFadeStart = 0.25f;
    [SerializeField, Range(0f, 1f)] private float moonFadeEnd = 0.55f;

    // The stars fade in between 45% and 80% night progress.
    [Header("Stars Fade")]
    [SerializeField, Range(0f, 1f)] private float starsFadeStart = 0.45f;
    [SerializeField, Range(0f, 1f)] private float starsFadeEnd = 0.80f;

    // Preserve each sprite's original colour while changing alpha.
    private Color moonBaseColor = Color.white;
    private Color starsBaseColor = Color.white;

    private void Awake()
    {
        // Store the original moon colour.
        if (moon != null)
        {
            moonBaseColor = moon.color;
        }

        // Store the original stars colour.
        if (stars != null)
        {
            starsBaseColor = stars.color;
        }
    }

    private void Update()
    {
        // Find GameManager if Unity initialized it after this component.
        if (gameManager == null)
        {
            gameManager = GameManager.instance;
        }

        // Before the level starts, progress remains zero.
        float progress =
            gameManager == null
                ? 0f
                : gameManager.NightProgress;

        // Convert night progress into opacity values.
        float moonAlpha =
            Mathf.InverseLerp(
                moonFadeStart,
                moonFadeEnd,
                progress
            );

        float starsAlpha =
            Mathf.InverseLerp(
                starsFadeStart,
                starsFadeEnd,
                progress
            );

        SetAlpha(moon, moonBaseColor, moonAlpha);
        SetAlpha(stars, starsBaseColor, starsAlpha);
    }

    private static void SetAlpha(
        SpriteRenderer renderer,
        Color baseColor,
        float alpha
    )
    {
        // Empty references are allowed.
        if (renderer == null)
        {
            return;
        }

        Color colour = baseColor;
        colour.a =
            baseColor.a *
            Mathf.Clamp01(alpha);

        renderer.color = colour;
    }
}