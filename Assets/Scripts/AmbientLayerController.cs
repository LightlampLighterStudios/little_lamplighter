using UnityEngine;

// Crossfades two looping ambience layers (a "day" layer and a "night"
// layer) based on GameManager.NightProgress (0 = sunset, 1 = full night).
// Both layers loop continuously from the start of the level; only their
// volume changes over time - nothing is ever fully silent-then-triggered,
// so the mix always feels present, just evolving.
public sealed class AmbientLayerController : MonoBehaviour
{
    [Header("Day layer (e.g. bird/wind ambience)")]
    [SerializeField] private AudioSource dayAmbience;
    [SerializeField, Range(0f, 1f)] private float dayMaxVolume = 1f;

    [Header("Night layer (e.g. dark synth drone)")]
    [SerializeField] private AudioSource nightDrone;
    [SerializeField, Range(0f, 1f)] private float nightMaxVolume = 1f;

    private void Start()
    {
        // Both layers loop the whole level; only their volume changes.
        // Play() here just starts them - Update() sets the real volume
        // every frame, so it doesn't matter what volume they start at.
        if (dayAmbience != null)
        {
            dayAmbience.loop = true;
            dayAmbience.Play();
        }

        if (nightDrone != null)
        {
            nightDrone.loop = true;
            nightDrone.Play();
        }
    }

    private void Update()
    {
        float t = GameManager.instance != null ? GameManager.instance.NightProgress : 0f;

        // SmoothStep instead of a straight Lerp: the day layer lingers a
        // little longer before dropping, and the night layer eases in
        // underneath rather than rising at a constant rate the whole time.
        if (dayAmbience != null)
        {
            dayAmbience.volume = Mathf.SmoothStep(dayMaxVolume, 0f, t);
        }

        if (nightDrone != null)
        {
            nightDrone.volume = Mathf.SmoothStep(0f, nightMaxVolume, t);
        }
    }
}
