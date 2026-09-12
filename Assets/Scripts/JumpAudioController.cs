using UnityEngine;

// Plays two layered SFX together every time the player jumps: a cartoon
// "boing" for personality, and a realistic body-effort foley underneath for
// weight. Both fire from PlayerController.JumpPerformed - no new gameplay
// hook was needed, that event already existed and was unused.
public sealed class JumpAudioController : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    [Header("Boing layer")]
    [SerializeField] private AudioSource boingAudio;
    [SerializeField] private AudioClipVariations boingAudioVariations;

    [Header("Foley layer (body effort)")]
    [SerializeField] private AudioSource foleyAudio;
    [SerializeField] private AudioClipVariations foleyAudioVariations;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.JumpPerformed += HandleJump;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.JumpPerformed -= HandleJump;
        }
    }

    private void HandleJump()
    {
        PlayLayer(boingAudioVariations, boingAudio);
        PlayLayer(foleyAudioVariations, foleyAudio);
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
