using UnityEngine;

// Sits next to an AudioSource and decides which clip (and how much jitter)
// to play on it, instead of the AudioSource always playing the same fixed
// clip. See Docs/dynamic-audio-system.md for the design rationale.
[RequireComponent(typeof(AudioSource))]
public sealed class AudioClipVariations : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;

    [Header("Optional jitter (leave at 0 for no variation)")]
    [SerializeField, Range(0f, 0.15f)] private float pitchJitter = 0f;
    [SerializeField, Range(0f, 0.15f)] private float volumeJitter = 0f;

    [SerializeField] private bool avoidImmediateRepeat = true;

    private AudioSource source;
    private int lastClipIndex = -1;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    // Plays a random clip from the array. Falls back to whatever clip is
    // already assigned on the AudioSource when no variations are configured,
    // so adding this component with an empty array changes nothing.
    public void PlayRandom()
    {
        if (clips == null || clips.Length == 0)
        {
            source.Play();
            return;
        }

        int index = PickIndex();
        lastClipIndex = index;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        // Volume only ever moves down from the authored value, never up,
        // so a jittered play can't suddenly poke out above the mix.
        source.volume = baseVolume * (1f - Random.Range(0f, volumeJitter));
        source.PlayOneShot(clips[index]);
    }

    private int PickIndex()
    {
        if (clips.Length == 1 || !avoidImmediateRepeat)
        {
            return Random.Range(0, clips.Length);
        }

        int index;
        do { index = Random.Range(0, clips.Length); }
        while (index == lastClipIndex);
        return index;
    }

    [ContextMenu("Test Play Random")]
    private void TestPlayRandom()
    {
        // Lets whoever owns audio audition all variations from the Inspector
        // (right-click the component) without hunting for the trigger in Play mode.
        if (source == null)
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
        }

        PlayRandom();
    }
}
