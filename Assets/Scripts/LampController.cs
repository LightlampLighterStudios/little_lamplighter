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

    // set internal state variables
    // Lamps expose state so checkpoints and results can distinguish a unique
    // first light from the mandatory relight.
    private bool hasEverBeenLit;
    private PlayerController nearbyPlayer;
    private float holdTimer;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
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
        audioSource = GetComponent<AudioSource>();
        restingColour = spriteRenderer != null ? spriteRenderer.color : Color.white;
        ApplySprite();

        // grab the AudioSource if there is one (won't crash if missing)
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

        // play the lighting sound, only if one is attached
        if (audioSource != null)
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
