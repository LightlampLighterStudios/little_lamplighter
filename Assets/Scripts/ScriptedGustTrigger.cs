using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ScriptedGustTrigger : MonoBehaviour
{
    [SerializeField] private LampController targetLamp;
    [SerializeField] private TutorialDirector director;
    [SerializeField] private SpriteRenderer gustVisual;
    [SerializeField] private AudioSource gustAudio;

    [Header("Gust event")]
    [SerializeField, Min(0.05f)] private float travelDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float approachDistance = 5f;
    [SerializeField, Min(0.05f)] private float exitDistance = 2f;
    [Tooltip("Distance travelled after touching the lamp, measured as a fraction of the camera width.")]
    [SerializeField, Range(0.05f, 0.5f)] private float postImpactScreenWidth = 0.2f;
    [SerializeField] private Vector2 windDirection = Vector2.left;
    [SerializeField, Range(0f, 1f)] private float lampHeadHeight = 0.55f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.32f;
    [SerializeField, Range(0f, 0.25f)] private float randomScaleJitter = 0.1f;
    [SerializeField] private bool randomFlipX = true;

    [Header("Single-wisp organic motion")]
    [SerializeField, Range(0.1f, 1f)] private float wispOpacity = 0.82f;
    [SerializeField, Range(0.05f, 0.2f)] private float fadeInDuration = 0.1f;
    [SerializeField, Range(0f, 0.3f)] private float flutterAmplitude = 0.1f;
    [SerializeField, Range(0.5f, 5f)] private float flutterCycles = 2.2f;
    [SerializeField, Range(0f, 6f)] private float rotationSway = 2f;

    [Header("Relight reminder")]
    [SerializeField, Range(1, 4)] private int relightPulseCount = 2;
    [SerializeField, Min(0.05f)] private float relightPulseDuration = 0.18f;
    [SerializeField, Min(0f)] private float relightPulseGap = 0.12f;

    private AudioClipVariations gustVariations;
    private bool triggered;
    private Collider2D triggerCollider;
    private Coroutine gustRoutine;
    private Vector3 originalGustLocalPosition;
    private Vector3 originalGustLocalScale;
    private Quaternion originalGustLocalRotation;
    private Color originalGustColor = Color.white;

    public bool IsTriggered => triggered;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
        gustVariations = GetComponent<AudioClipVariations>();
        CacheGustVisualState();
        HideAndResetGustVisual();
    }

    public void Configure(LampController lamp, TutorialDirector tutorial, SpriteRenderer visual)
    {
        targetLamp = lamp;
        director = tutorial;
        gustVisual = visual;
        CacheGustVisualState();
        HideAndResetGustVisual();
    }

    public void Configure(LampController lamp, SpriteRenderer visual)
    {
        Configure(lamp, null, visual);
    }

    public void RestoreState(bool wasTriggered)
    {
        if (gustRoutine != null)
        {
            StopCoroutine(gustRoutine);
            gustRoutine = null;
        }

        triggered = wasTriggered;
        triggerCollider ??= GetComponent<Collider2D>();
        triggerCollider.enabled = !triggered;
        HideAndResetGustVisual();
        gameObject.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        triggered = true;
        triggerCollider.enabled = false;
        PlayGustAudio();
        gustRoutine = StartCoroutine(PlayGust());
    }

    private IEnumerator PlayGust()
    {
        Vector2 direction = windDirection.sqrMagnitude > 0.0001f
            ? windDirection.normalized
            : Vector2.left;

        Vector3 lampHead = ResolveLampHeadPosition();
        Vector3 direction3 = new Vector3(direction.x, direction.y, 0f);
        float postImpactDistance = ResolvePostImpactDistance(lampHead);
        // Preserve the original pass's average speed (7 units in 0.55s by
        // default), but apply it linearly across the entire new route.
        float speed = (approachDistance + exitDistance) /
            Mathf.Max(0.05f, travelDuration);
        float totalDistance = approachDistance + postImpactDistance;
        float fadeDistance = Mathf.Min(postImpactDistance, speed * fadeDuration);
        float fadeStartDistance = totalDistance - fadeDistance;
        Vector3 start = lampHead - direction3 * approachDistance;
        Vector3 lampAnchorAtStart = targetLamp != null
            ? targetLamp.transform.position
            : lampHead;

        ShowGustAt(start);
        Vector3 visibleScale = gustVisual != null
            ? gustVisual.transform.localScale
            : Vector3.one;
        Quaternion visibleRotation = gustVisual != null
            ? gustVisual.transform.localRotation
            : Quaternion.identity;
        float impactProgress = approachDistance / totalDistance;
        float elapsed = 0f;

        bool impacted = false;
        float travelled = 0f;
        while (travelled < totalDistance)
        {
            elapsed += Time.deltaTime;
            travelled = Mathf.Min(totalDistance, travelled + speed * Time.deltaTime);

            if (!impacted && travelled >= approachDistance)
            {
                impacted = true;
                ImpactLamp(direction);
            }

            if (gustVisual != null)
            {
                // Linear distance accumulation keeps the sprite at one constant
                // speed through the impact instead of easing or pausing there.
                Vector3 lampMovement = targetLamp != null
                    ? targetLamp.transform.position - lampAnchorAtStart
                    : Vector3.zero;
                float routeProgress = travelled / totalDistance;
                float flutter = Mathf.Sin(
                    (routeProgress - impactProgress) *
                    Mathf.PI * 2f * flutterCycles);
                gustVisual.transform.position =
                    start +
                    lampMovement +
                    direction3 * travelled +
                    Vector3.up * (flutter * flutterAmplitude);
                gustVisual.transform.localRotation =
                    visibleRotation * Quaternion.Euler(
                        0f,
                        0f,
                        flutter * rotationSway);

                float fade = travelled >= fadeStartDistance
                    ? Mathf.InverseLerp(
                        fadeStartDistance,
                        totalDistance,
                        travelled)
                    : 0f;
                float fadeIn = Mathf.Clamp01(elapsed / fadeInDuration);
                SetGustAlpha(
                    originalGustColor.a *
                    wispOpacity *
                    fadeIn *
                    (1f - fade));
                gustVisual.transform.localScale = Vector3.Lerp(
                    visibleScale,
                    visibleScale * 0.86f,
                    fade);
            }

            yield return null;
        }

        if (!impacted)
        {
            ImpactLamp(direction);
        }

        HideAndResetGustVisual();
        gustRoutine = null;
    }

    private void ImpactLamp(Vector2 direction)
    {
        targetLamp?.Extinguish(direction);
        targetLamp?.PlayRelightReminder(relightPulseCount, relightPulseDuration, relightPulseGap);
        director?.BeginRelightTutorial(targetLamp);
    }

    private float ResolvePostImpactDistance(Vector3 lampHead)
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            return exitDistance;
        }

        Vector3 lampViewport = sceneCamera.WorldToViewportPoint(lampHead);
        Vector3 left = sceneCamera.ViewportToWorldPoint(
            new Vector3(0f, lampViewport.y, lampViewport.z));
        Vector3 right = sceneCamera.ViewportToWorldPoint(
            new Vector3(1f, lampViewport.y, lampViewport.z));

        return Mathf.Max(0.05f, Vector3.Distance(left, right) * postImpactScreenWidth);
    }

    private Vector3 ResolveLampHeadPosition()
    {
        if (targetLamp == null)
        {
            return transform.position;
        }

        SpriteRenderer lampRenderer = targetLamp.GetComponent<SpriteRenderer>();
        if (lampRenderer != null && lampRenderer.sprite != null)
        {
            Bounds lampBounds = lampRenderer.bounds;
            return lampBounds.center + Vector3.up * lampBounds.extents.y * lampHeadHeight;
        }

        return targetLamp.transform.position;
    }

    private void ShowGustAt(Vector3 start)
    {
        if (gustVisual == null)
        {
            return;
        }

        CacheGustVisualState();
        gustVisual.transform.localScale = originalGustLocalScale * Random.Range(
            1f - randomScaleJitter,
            1f + randomScaleJitter);

        if (randomFlipX && Random.value > 0.5f)
        {
            Vector3 scale = gustVisual.transform.localScale;
            scale.x *= -1f;
            gustVisual.transform.localScale = scale;
        }

        gustVisual.transform.position = start;
        gustVisual.enabled = true;
        SetGustAlpha(originalGustColor.a);
    }

    private void CacheGustVisualState()
    {
        if (gustVisual == null)
        {
            return;
        }

        originalGustLocalPosition = gustVisual.transform.localPosition;
        originalGustLocalScale = gustVisual.transform.localScale;
        originalGustLocalRotation = gustVisual.transform.localRotation;
        originalGustColor = gustVisual.color;
    }

    private void HideAndResetGustVisual()
    {
        if (gustVisual == null)
        {
            return;
        }

        gustVisual.transform.localPosition = originalGustLocalPosition;
        gustVisual.transform.localScale = originalGustLocalScale;
        gustVisual.transform.localRotation = originalGustLocalRotation;
        gustVisual.color = originalGustColor;
        gustVisual.enabled = false;
    }

    private void SetGustAlpha(float alpha)
    {
        Color colour = gustVisual.color;
        colour.a = alpha;
        gustVisual.color = colour;
    }

    private void PlayGustAudio()
    {
        if (gustVariations != null)
        {
            gustVariations.PlayRandom();
        }
        else if (gustAudio != null)
        {
            gustAudio.Play();
        }
    }
}


