using UnityEngine;

// Central place for UI hover/click SFX - one shared pair of layers for the
// whole menu, instead of an AudioSource on every button. UI sound isn't
// positional, so sharing makes sense here (unlike gameplay SFX elsewhere).
// Attach ButtonSfx to any Button that should make sound.
public sealed class UISoundManager : MonoBehaviour
{
    public static UISoundManager instance;

    [Header("Hover")]
    [SerializeField] private AudioSource hoverAudio;
    [SerializeField] private AudioClipVariations hoverAudioVariations;

    [Header("Click")]
    [SerializeField] private AudioSource clickAudio;
    [SerializeField] private AudioClipVariations clickAudioVariations;

    private void Awake()
    {
        instance = this;
    }

    public void PlayHover()
    {
        PlayLayer(hoverAudioVariations, hoverAudio);
    }

    public void PlayClick()
    {
        PlayLayer(clickAudioVariations, clickAudio);
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
