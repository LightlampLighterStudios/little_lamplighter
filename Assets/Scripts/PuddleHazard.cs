using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class PuddleHazard : MonoBehaviour
{
    [SerializeField] private bool tutorialPuddle;
    [SerializeField, Range(0.05f, 1f)] private float speedMultiplier = 0.4f;
    [SerializeField, Min(0f)] private float slowDuration = 2f;
    [SerializeField] private AudioSource splashAudio;

    private bool occupied;
    private SpriteRenderer puddleRenderer;
    private Coroutine splashRoutine;
    private Vector3 restingScale;
    private Color restingColour;

    public event Action<PuddleHazard, PlayerController> PlayerEntered;
    public bool IsTutorialPuddle => tutorialPuddle;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (splashAudio == null)
        {
            splashAudio = GetComponent<AudioSource>();
        }
        puddleRenderer = GetComponent<SpriteRenderer>();
        restingScale = transform.localScale;
        restingColour = puddleRenderer != null ? puddleRenderer.color : Color.white;
    }

    public void Configure(bool isTutorial)
    {
        tutorialPuddle = isTutorial;
    }

    public void ResetHazard()
    {
        occupied = false;

        if (splashRoutine != null)
        {
            StopCoroutine(splashRoutine);
            splashRoutine = null;
        }

        transform.localScale = restingScale;

        if (puddleRenderer != null)
        {
            puddleRenderer.color = restingColour;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null || occupied)
        {
            return;
        }

        occupied = true;
        if (splashAudio != null)
        {
            splashAudio.Play();
        }
        splashRoutine = StartCoroutine(PlaySplashFeedback());
        player.ApplySlow(speedMultiplier, slowDuration);
        PlayerEntered?.Invoke(this, player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
        {
            occupied = false;
        }
    }

    private IEnumerator PlaySplashFeedback()
    {
        const float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(normalized * Mathf.PI);
            transform.localScale = Vector3.Scale(
                restingScale,
                new Vector3(1f + arc * 0.22f, 1f + arc * 0.65f, 1f));

            if (puddleRenderer != null)
            {
                puddleRenderer.color = Color.Lerp(
                    restingColour,
                    new Color(0.72f, 0.9f, 1f, restingColour.a),
                    arc);
            }

            yield return null;
        }

        transform.localScale = restingScale;

        if (puddleRenderer != null)
        {
            puddleRenderer.color = restingColour;
        }

        splashRoutine = null;
    }
}
