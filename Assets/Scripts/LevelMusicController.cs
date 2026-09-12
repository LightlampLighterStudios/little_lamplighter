using System.Collections;
using UnityEngine;

// Reacts to two different "the player got hurt" moments:
// - Losing a life to the darkness but the run continues (DarknessCaughtPlayer):
//   fades the music out, restarts it from the beginning, and fades back in -
//   nothing else brings it back here, since the scene isn't reloaded.
// - Losing the whole run (LevelEndedEvent, failure only): just fades out.
//   RestartLevel() reloads the whole scene, so a fresh AudioSource naturally
//   starts the track over from the beginning on its own.
public sealed class LevelMusicController : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private GameManager gameManager;
    [SerializeField, Min(0.1f)] private float fadeOutDuration = 2f;
    [SerializeField, Min(0.1f)] private float fadeInDuration = 1.5f;

    private Coroutine fadeRoutine;
    private float baseVolume = 1f;

    private void Start()
    {
        // Start (not Awake) so GameManager.instance is guaranteed to be
        // set by the time this runs, regardless of script execution order.
        if (gameManager == null)
        {
            gameManager = GameManager.instance;
        }

        if (musicSource != null)
        {
            baseVolume = musicSource.volume;
        }

        if (gameManager != null)
        {
            gameManager.LevelEndedEvent += HandleLevelEnded;
            gameManager.DarknessCaughtPlayer += HandleDarknessCaughtPlayer;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.LevelEndedEvent -= HandleLevelEnded;
            gameManager.DarknessCaughtPlayer -= HandleDarknessCaughtPlayer;
        }
    }

    private void HandleDarknessCaughtPlayer()
    {
        // Losing a life (but not the run) never reloads the scene, so
        // nothing else brings the music back afterwards - this has to
        // handle both the fade-out AND the restart-and-fade-back-in itself.
        if (musicSource == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOutThenRestart());
    }

    private IEnumerator FadeOutThenRestart()
    {
        yield return Fade(musicSource.volume, 0f, fadeOutDuration);

        musicSource.Stop();
        musicSource.Play();

        yield return Fade(0f, baseVolume, fadeInDuration);

        fadeRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        // Unscaled time: the game-over sequence sets Time.timeScale to 0,
        // and a fade using scaled deltaTime would just freeze mid-fade and
        // never reach the end (never call Stop()) while paused.
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        musicSource.volume = to;
    }

    private void HandleLevelEnded(bool completed)
    {
        // Only fades on failure/death for now - completing the level keeps
        // the music as-is. Easy to widen to "completed ||" later if wanted.
        if (completed || musicSource == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return Fade(musicSource.volume, 0f, fadeOutDuration);

        musicSource.Stop();
        fadeRoutine = null;
    }
}
