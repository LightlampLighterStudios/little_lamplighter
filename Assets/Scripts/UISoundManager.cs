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
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // The authored main-menu manager is a root object. Keeping that one
        // alive lets every subsequent UI screen reuse the same clips.
        if (gameObject.name == "UISoundManager" && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    // Used by runtime-created menus (such as PauseMenu) that already own the
    // clips but do not need serialized AudioClipVariations components.
    public void Configure(AudioSource hover, AudioSource click)
    {
        hoverAudio = hover;
        hoverAudioVariations = null;
        clickAudio = click;
        clickAudioVariations = null;
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
