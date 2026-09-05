using System;
using UnityEngine;

public enum LampType
{
    Classic,
    Scrollwork,
    Heritage,
    Botanical,
    Hooded,
    Triple,
    Golden
}

public enum LampGameplayCategory
{
    Standard,
    Damaged,
    Power,
    Golden
}

public sealed class LampController : MonoBehaviour
{
    [SerializeField] private string lampId = "Lamp";
    [SerializeField] private LampType lampType = LampType.Classic;
    [SerializeField] private Sprite unlitSprite;
    [SerializeField] private Sprite litSprite;
    // This will be the time of how long the player must hold E to light this lamp will later install loading bar sp player can see
    [SerializeField, Min(0.1f)] private float timeToLight = 0.55f;
    [SerializeField, Min(1)] private int firstLightSparkReward = 1;
    [SerializeField, Min(1)] private int relightSparkReward = 1;
    [SerializeField, Min(0.1f)] private float darknessPushMultiplier = 1f;
    [SerializeField] private bool isLit;

    // set internal state variables
    // Lamps expose state so checkpoints and results can distinguish a unique
    // first light from the mandatory relight.
    private bool hasEverBeenLit;
    private PlayerController nearbyPlayer;
    private float holdTimer;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private AudioClipVariations audioVariations;
    private Color restingColour = Color.white;
    private bool tutorialHighlighted;

    public event Action<LampController, bool> Lit;

    public string LampId => ResolveLampId();
    public LampType Type => lampType;
    public LampGameplayCategory GameplayCategory => ResolveGameplayCategory();
    public bool IsLit => isLit;
    public bool HasEverBeenLit => hasEverBeenLit;
    public float TimeToLight => timeToLight;
    public float DarknessPushMultiplier => darknessPushMultiplier;
    public float LightingProgress => Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, timeToLight));
    public Color ProgressColour => ResolveProgressColour();

    private void Awake()
    {
        // this will get the SpriteRenderer so we can swap sprites when lit
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        audioVariations = GetComponent<AudioClipVariations>();
        restingColour = spriteRenderer != null ? spriteRenderer.color : Color.white;
        ApplySprite();

        // grab the AudioSource if there is one (won't crash if missing)
    }

    private void Update()
    {
        UpdateVisualFeedback();

        if (isLit)
        {
            return;
        }

        // only run this code if the lamp isn't already lit
        // This will check players proximity so if player is nearby AND holding E
        if (nearbyPlayer != null && PlayerController.InteractHeld)
        {
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

    public void ConfigureType(
        LampType type,
        int firstLightReward = 1,
        int relightReward = 1,
        float pushMultiplier = 1f)
    {
        lampType = type;
        firstLightSparkReward = Mathf.Max(1, firstLightReward);
        relightSparkReward = Mathf.Max(1, relightReward);
        darknessPushMultiplier = Mathf.Max(0.1f, pushMultiplier);
    }

    public int GetSparkReward(bool isRelight)
    {
        return isRelight
            ? Mathf.Max(1, relightSparkReward)
            : Mathf.Max(1, firstLightSparkReward);
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

        // play the lighting sound, only if one is attached
        if (audioVariations != null)
        {
            audioVariations.PlayRandom();
        }
        else if (audioSource != null)
        {
            audioSource.Play();
        }

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
    }

    private void UpdateVisualFeedback()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;

        if (tutorialHighlighted)
        {
            spriteRenderer.color = Color.Lerp(
                restingColour,
                new Color(1f, 0.82f, 0.32f, restingColour.a),
                pulse * 0.65f);
            return;
        }

        if (nearbyPlayer == null || isLit || GameplayCategory == LampGameplayCategory.Standard)
        {
            spriteRenderer.color = restingColour;
            return;
        }

        Color categoryColour = ProgressColour;
        categoryColour.a = restingColour.a;
        spriteRenderer.color = Color.Lerp(restingColour, categoryColour, 0.16f + pulse * 0.2f);
    }

    private LampGameplayCategory ResolveGameplayCategory()
    {
        switch (lampType)
        {
            case LampType.Hooded:
                return LampGameplayCategory.Damaged;
            case LampType.Triple:
                return LampGameplayCategory.Power;
            case LampType.Golden:
                return LampGameplayCategory.Golden;
            default:
                return LampGameplayCategory.Standard;
        }
    }

    private Color ResolveProgressColour()
    {
        switch (GameplayCategory)
        {
            case LampGameplayCategory.Damaged:
                return new Color(1f, 0.42f, 0.16f, 1f);
            case LampGameplayCategory.Power:
                return new Color(0.38f, 0.86f, 1f, 1f);
            case LampGameplayCategory.Golden:
                return new Color(1f, 0.78f, 0.08f, 1f);
            default:
                return new Color(1f, 0.82f, 0.32f, 1f);
        }
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
