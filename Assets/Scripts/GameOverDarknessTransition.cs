using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Completes the darkness already visible during gameplay, then reveals the
// static game-over backdrop. The card animation begins after this finishes.
public sealed class GameOverDarknessTransition : MonoBehaviour
{
    [SerializeField] private Image streetDimmer;
    [SerializeField] private CanvasGroup settledDarkness;
    [SerializeField, Min(0.01f)] private float fadeToBlackDuration = 0.35f;
    [SerializeField, Min(0f)] private float fullBlackHoldDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float backdropRevealDuration = 0.2f;

    private bool started;

    private void Awake()
    {
        SetAlpha(streetDimmer, 0f);
        settledDarkness.alpha = 0f;
    }

    public IEnumerator PlayTakeover()
    {
        if (started) yield break;
        started = true;

        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0.01f, fadeToBlackDuration);
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
            SetAlpha(streetDimmer, t);
            yield return null;
        }
        SetAlpha(streetDimmer, 1f);

        elapsed = 0f;
        while (elapsed < fullBlackHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        float revealDuration = Mathf.Max(0.01f, backdropRevealDuration);
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            settledDarkness.alpha = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / revealDuration));
            yield return null;
        }
        settledDarkness.alpha = 1f;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color colour = image.color;
        colour.a = alpha;
        image.color = colour;
    }
}
