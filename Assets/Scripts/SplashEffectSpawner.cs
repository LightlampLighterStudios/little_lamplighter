using System.Collections;
using UnityEngine;

// Sits next to PuddleHazard on the same puddle GameObject. Listens to its
// PlayerEntered event (this is fired the moment said player jumps INTO the
// puddle, not over it) and spawns one random splash sprite, then removes
// it after a short lifetime. Does not modify PuddleHazard.cs in any way.
[RequireComponent(typeof(PuddleHazard))]
public sealed class SplashEffectSpawner : MonoBehaviour
{
    [SerializeField] private Sprite[] splashSprites = new Sprite[6];
    [SerializeField, Min(0.05f)] private float splashLifetime = 0.4f;
    [SerializeField] private Vector3 splashOffset = Vector3.zero;
    [SerializeField] private int splashSortingOrder = 5;
    [SerializeField, Min(0f)] private float splashWidthRatio = 1f;

    [SerializeField, Min(1f)] private float targetScaleMultiplier = 1.5f;

    private PuddleHazard puddleHazard;
    private SpriteRenderer puddleRenderer;

    private void Awake()
    {
        puddleHazard = GetComponent<PuddleHazard>();
        puddleRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        puddleHazard.PlayerEntered += HandlePlayerEntered;
    }

    private void OnDisable()
    {
        puddleHazard.PlayerEntered -= HandlePlayerEntered;
    }

    private void HandlePlayerEntered(PuddleHazard hazard, PlayerController player)
    {
        SpawnRandomSplash();

        // Tells the player's wet-state conductor to start the wet timer.
        CharacterWetState wetState = player.GetComponent<CharacterWetState>();
        wetState?.TriggerWet();
    }

    private void SpawnRandomSplash()
    {
        Vector3 initialScale = Vector3.one;
        if (splashSprites == null || splashSprites.Length == 0)
        {
            return;
        }

        Sprite chosenSprite = splashSprites[Random.Range(0, splashSprites.Length)];

        GameObject splashObject = new GameObject("SplashEffect");
        splashObject.transform.SetParent(transform, false);
        splashObject.transform.localPosition = splashOffset;

        SpriteRenderer splashRenderer = splashObject.AddComponent<SpriteRenderer>();
        splashRenderer.sprite = chosenSprite;

        if (
            puddleRenderer != null &&
            puddleRenderer.sprite != null &&
            chosenSprite != null &&
            chosenSprite.bounds.size.x > 0f)
        {
            float scale =
                puddleRenderer.sprite.bounds.size.x /
                chosenSprite.bounds.size.x *
                splashWidthRatio;
            splashObject.transform.localScale = Vector3.one * scale;
            initialScale *= scale;
        }

        if (puddleRenderer != null)
        {
            splashRenderer.sortingLayerID = puddleRenderer.sortingLayerID;
        }

        splashRenderer.sortingOrder = splashSortingOrder;

        StartCoroutine(AnimateSplash(splashObject, splashRenderer, initialScale));
    }

    private IEnumerator AnimateSplash(GameObject splashObject, SpriteRenderer splashRenderer, Vector3 startScale)
    {
        float elapsed = 0f;
        Color startColor = splashRenderer.color;
        Vector3 targetScale = startScale * targetScaleMultiplier;

        while (elapsed < splashLifetime)
        {
            if (splashObject == null) yield break;

            elapsed += Time.deltaTime;

            float pct = elapsed / splashLifetime;

            //splashObject.transform.Translate(0, pct / 8, 0);
            splashObject.transform.localScale = Vector3.Lerp(startScale, targetScale, pct);

            splashRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1.0f, 0.0f, pct));

            yield return null;
        }

        if (splashObject != null)
        {
            Destroy(splashObject);
        }
    }
}
