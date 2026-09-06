using System;
using UnityEngine;

public sealed class LampController : MonoBehaviour
{
    [SerializeField] private string lampId = "Lamp";
    [SerializeField] private Sprite unlitSprite;
    [SerializeField] private Sprite litSprite;
    // This will be the time of how long the player must hold E to light this lamp will later install loading bar sp player can see
    [SerializeField, Min(0.1f)] private float timeToLight = 0.55f;
    [SerializeField] private bool isLit;

    [Header("Audio - two separate sounds, two separate slots")]
    // CASTING: plays once at the start of the hold, for the whole
    // timeToLight duration (the "charging" whoosh).
    [SerializeField] private AudioSource castingAudio;
    [SerializeField] private AudioClipVariations castingAudioVariations;
    // LIGHTING: plays once, only when the lamp actually lights (the
    // "finish/impact" sound). Leave empty until that clip exists.
    [SerializeField] private AudioSource lightingAudio;
    [SerializeField] private AudioClipVariations lightingAudioVariations;

    [Header("Lighting pitch escalation - rises with each unique lamp lit")]
    [SerializeField, Min(0f)] private float lightingPitchStep = 0.05f;
    [SerializeField, Min(1f)] private float lightingPitchMax = 1.5f;

    // set internal state variables
    // Lamps expose state so checkpoints and results can distinguish a unique
    // first light from the mandatory relight.
    private bool hasEverBeenLit;
    private PlayerController nearbyPlayer;
    private float holdTimer;
    private SpriteRenderer spriteRenderer;
    private Color restingColour = Color.white;
    private bool tutorialHighlighted;

    public event Action<LampController, bool> Lit;

    public string LampId => ResolveLampId();
    public bool IsLit => isLit;
    public bool HasEverBeenLit => hasEverBeenLit;
    public float LightingProgress => Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, timeToLight));

    private void Awake()
    {
        // this will get the SpriteRenderer so we can swap sprites when lit
        spriteRenderer = GetComponent<SpriteRenderer>();
        restingColour = spriteRenderer != null ? spriteRenderer.color : Color.white;
        ApplySprite();

        // castingAudio/lightingAudio are assigned by hand in the Inspector
        // (not auto-fetched with GetComponent) because a lamp can have two
        // separate AudioSource components - one per sound - and GetComponent
        // would only ever find the first one, silently grabbing the wrong one.
    }

    private void Update()
    {
        UpdateTutorialHighlight();

        if (isLit)
        {
            return;
        }

        // only run this code if the lamp isn't already lit
        // This will check players proximity so if player is nearby AND holding E
        if (nearbyPlayer != null && PlayerController.InteractHeld)
        {
            if (holdTimer <= 0f)
            {
                // First frame of this hold: play the CASTING sound once.
                // The separate LIGHTING sound plays later, in LightLamp().
                PlayCastingSfx();
            }

            // adding to the hold timer
            holdTimer += Time.deltaTime;
            // Updating said loading bar through GameManager
            GameManager.instance?.UpdateLoadingBar(this, LightingProgress);

            if (holdTimer >= timeToLight)
            {
                // If held long enough then light the lamp
                LightLamp();
            }

            return;
        }

        ResetHold();
    }

    public void Configure(
        string id,
        Sprite unlit,
        Sprite lit,
        float holdDuration = 0.55f)
    {
        // The editor builder uses this to create route-local lamps without
        // requiring manual Inspector wiring.
        lampId = id;
        unlitSprite = unlit;
        litSprite = lit;
        timeToLight = Mathf.Max(0.1f, holdDuration);
        ApplySprite();
    }

    public void Extinguish()
    {
        // Gusts remove the current light but preserve HasEverBeenLit so the
        // relight awards the separate relighting Spark exactly once.
        if (!isLit)
        {
            return;
        }

        isLit = false;
        holdTimer = 0f;
        ApplySprite();
    }

    public void SetTutorialHighlighted(bool highlighted)
    {
        tutorialHighlighted = highlighted;

        if (!highlighted && spriteRenderer != null)
        {
            spriteRenderer.color = restingColour;
        }
    }

    public void RestoreState(bool lit, bool everLit)
    {
        // Checkpoint restoration must restore both visual state and scoring
        // history, not just the sprite.
        isLit = lit;
        hasEverBeenLit = everLit;
        holdTimer = 0f;
        ApplySprite();
        GameManager.instance?.HideLoadingBar();
    }

    private void LightLamp()
    {
        // A lamp's first light and its relight are reported separately.
        bool isRelight = hasEverBeenLit;
        isLit = true;
        hasEverBeenLit = true;
        holdTimer = 0f;
        ApplySprite();
        GameManager.instance?.HideLoadingBar();
        PlayLightingSfx();

        // will tell the GameManager a lamp was lit
        GameManager.instance?.RegisterLampLit(this, isRelight);
        Lit?.Invoke(this, isRelight);
    }

    private void ResetHold()
    {
        if (holdTimer <= 0f)
        {
            return;
        }

        holdTimer = 0f;
        // must reset timer if player lets go or walks away (like later on when dog comes to steal tapperPole)
        GameManager.instance?.HideLoadingBar();

        // Cut the CASTING sound short if it was interrupted mid-hold, so it
        // never keeps playing over a lamp that didn't actually light.
        castingAudio?.Stop();
    }

    private void PlayCastingSfx()
    {
        if (castingAudioVariations != null)
        {
            castingAudioVariations.PlayRandom();
        }
        else if (castingAudio != null)
        {
            castingAudio.Play();
        }
    }

    private void PlayLightingSfx()
    {
        float pitch = ComputeLightingPitch();

        if (lightingAudioVariations != null)
        {
            lightingAudioVariations.PlayRandom(pitch);
        }
        else if (lightingAudio != null)
        {
            lightingAudio.pitch = pitch;
            lightingAudio.Play();
        }
    }

    private float ComputeLightingPitch()
    {
        // Rises a little with each unique lamp already lit this run, so the
        // "acender" sound feels more triumphant the further along you are.
        // Relighting the same lamp after a gust does not count again -
        // UniqueLampsLit only tracks distinct lamp IDs.
        int lampsLitSoFar = GameManager.instance != null ? GameManager.instance.UniqueLampsLit : 0;
        return Mathf.Min(1f + lampsLitSoFar * lightingPitchStep, lightingPitchMax);
    }

    private void UpdateTutorialHighlight()
    {
        if (!tutorialHighlighted || spriteRenderer == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        spriteRenderer.color = Color.Lerp(
            restingColour,
            new Color(1f, 0.82f, 0.32f, restingColour.a),
            pulse * 0.65f);
    }

    private void ApplySprite()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isLit ? litSprite : unlitSprite;
        }
    }

    private string ResolveLampId()
    {
        if (!string.IsNullOrWhiteSpace(lampId) &&
            !string.Equals(lampId, "Lamp", StringComparison.OrdinalIgnoreCase))
        {
            return lampId;
        }

        Transform current = transform;
        string hierarchyPath = current.GetSiblingIndex().ToString();

        while (current.parent != null)
        {
            current = current.parent;
            hierarchyPath = current.GetSiblingIndex() + "/" + hierarchyPath;
        }

        return gameObject.scene.name + ":" + hierarchyPath;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Called when player enters the trigger zone
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            nearbyPlayer = player;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Will be called when player leaves the trigger zone
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null || player != nearbyPlayer)
        {
            return;
        }

        nearbyPlayer = null;
        ResetHold();
    }
}
