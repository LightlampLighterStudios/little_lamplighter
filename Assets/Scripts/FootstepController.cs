using UnityEngine;

// Plays footstep audio in sync with CharacterAnimator's walk cycle. Two
// independent layers fire on every step: the ground surface (concrete, or
// water when wet) and a quiet cloth-movement layer underneath it, giving
// far more natural variety than either layer could alone.
public sealed class FootstepController : MonoBehaviour
{
    [SerializeField] private CharacterAnimator characterAnimator;

    [Header("Surface - dry")]
    [SerializeField] private AudioSource surfaceAudio;
    [SerializeField] private AudioClipVariations surfaceAudioVariations;

    [Header("Surface - wet (optional, falls back to dry surface above if empty)")]
    [SerializeField] private AudioSource wetSurfaceAudio;
    [SerializeField] private AudioClipVariations wetSurfaceAudioVariations;

    [Header("Cloth layer (plays under every step, dry or wet)")]
    [SerializeField] private AudioSource clothAudio;
    [SerializeField] private AudioClipVariations clothAudioVariations;

    private void Awake()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<CharacterAnimator>();
        }
    }

    private void OnEnable()
    {
        if (characterAnimator != null)
        {
            characterAnimator.Footstep += HandleFootstep;
        }
    }

    private void OnDisable()
    {
        if (characterAnimator != null)
        {
            characterAnimator.Footstep -= HandleFootstep;
        }
    }

    private void HandleFootstep(bool isWet)
    {
        bool hasWetSurface = wetSurfaceAudio != null || wetSurfaceAudioVariations != null;

        PlayLayer(
            isWet && hasWetSurface ? wetSurfaceAudioVariations : surfaceAudioVariations,
            isWet && hasWetSurface ? wetSurfaceAudio : surfaceAudio);

        PlayLayer(clothAudioVariations, clothAudio);
    }

    private static void PlayLayer(AudioClipVariations variations, AudioSource source)
    {
        if (variations != null)
        {
            variations.PlayRandom();
        }
        else if (source != null)
        {
            source.Play();
        }
    }
}
