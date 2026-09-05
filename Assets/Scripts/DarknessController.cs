using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public sealed class DarknessController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject darkness;
    [SerializeField, Min(0f)] private float advanceSpeed = 0.35f;
    [SerializeField, Min(0f)] private float pushBackDistance = 5f;
    [SerializeField] private float resetX = -31f;
    [SerializeField, Min(0f)] private float tutorialCaptureDistance = 14f;
    [SerializeField, Min(0f)] private float tutorialCaptureOverlap = 20f;
    [SerializeField, Min(0.05f)] private float retreatDuration = 0.9f;
    [SerializeField, Min(0f)] private float retreatMargin = 2f;

    private bool activeThreat;
    private bool contactLocked;
    private Coroutine retreatRoutine;

    public bool IsActiveThreat => activeThreat;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (
            !activeThreat ||
            gameManager == null ||
            !gameManager.IsClockRunning)
        {
            return;
        }

        transform.position += Vector3.right * advanceSpeed * Time.deltaTime;

        float distanceApart = getSqrDistance(transform.position, player.position);

        //Convert 0 and 200 distance range to 0f and 1f range
        float lerp = mapValue(distanceApart, 0, 200, 0f, 1f);

        darkness.GetComponent<RawImage>().color = new Color(0, 0, 0, Mathf.Lerp(0.8f, 0, lerp));
    }

    public float getSqrDistance(Vector3 v1, Vector3 v2)
    {
        return (v1 - v2).sqrMagnitude;
    }

    float mapValue(float mainValue, float inValueMin, float inValueMax, float outValueMin, float outValueMax)
    {
        return (mainValue - inValueMin) * (outValueMax - outValueMin) / (inValueMax - inValueMin) + outValueMin;
    }

    public void Configure(Transform targetPlayer, GameManager manager)
    {
        player = targetPlayer;
        gameManager = manager;
        resetX = transform.position.x;
    }

    public void SetActiveThreat(bool active)
    {
        activeThreat = active;
        gameObject.SetActive(active);
    }

    public void RetreatOffScreen(Camera targetCamera)
    {
        activeThreat = false;
        contactLocked = true;
        gameObject.SetActive(true);

        if (retreatRoutine != null)
        {
            StopCoroutine(retreatRoutine);
        }

        retreatRoutine = StartCoroutine(RetreatOffScreenRoutine(targetCamera));
    }

    public void PushBack()
    {
        if (!activeThreat)
        {
            activeThreat = true;
            gameObject.SetActive(true);
        }

        Vector3 position = transform.position;
        position.x = Mathf.Max(resetX, position.x - pushBackDistance);
        transform.position = position;
    }

    public void ResetThreat()
    {
        Vector3 position = transform.position;
        position.x = resetX;
        transform.position = position;
        contactLocked = false;
    }

    public void RestorePosition(Vector3 position)
    {
        transform.position = position;
        contactLocked = false;
    }

    public Vector3 GetPositionWithFrontAt(float frontX)
    {
        Vector3 position = transform.position;
        position.x = frontX - GetVisibleFrontOffsetX();
        return position;
    }

    public IEnumerator CaptureTutorialPlayer(Transform target, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        // The tutorial owns this contact so the moving trigger cannot fire early.
        activeThreat = false;
        contactLocked = true;
        gameObject.SetActive(true);

        float visibleFrontOffset = GetVisibleFrontOffsetX();
        Vector3 startPosition = transform.position;
        startPosition.x =
            target.position.x - visibleFrontOffset - tutorialCaptureDistance;
        Vector3 contactPosition = startPosition;
        contactPosition.x =
            target.position.x - visibleFrontOffset + tutorialCaptureOverlap;
        transform.position = startPosition;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            contactPosition.x =
                target.position.x - visibleFrontOffset + tutorialCaptureOverlap;
            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / safeDuration));
            transform.position = Vector3.Lerp(
                startPosition,
                contactPosition,
                progress);
            yield return null;
        }

        contactPosition.x =
            target.position.x - visibleFrontOffset + tutorialCaptureOverlap;
        transform.position = contactPosition;
        gameManager?.HandleDarknessContact();
    }

    private IEnumerator RetreatOffScreenRoutine(Camera targetCamera)
    {
        float cameraDistance = targetCamera != null
            ? Mathf.Abs(targetCamera.transform.position.z - transform.position.z)
            : 0f;
        float leftEdge = targetCamera != null
            ? targetCamera.ViewportToWorldPoint(
                new Vector3(0f, 0.5f, cameraDistance)).x
            : transform.position.x - 50f;
        float targetX =
            leftEdge - retreatMargin - GetVisibleFrontOffsetX();
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition;
        targetPosition.x = Mathf.Min(startPosition.x, targetX);
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, retreatDuration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / safeDuration));
            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);
            yield return null;
        }

        transform.position = targetPosition;
        retreatRoutine = null;
    }

    private float GetVisibleFrontOffsetX()
    {
        float frontX = transform.position.x;

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (
                !renderer.enabled ||
                !renderer.gameObject.activeSelf ||
                renderer.sprite == null)
            {
                continue;
            }

            Bounds spriteBounds = renderer.sprite.bounds;
            Vector3 min = spriteBounds.min;
            Vector3 max = spriteBounds.max;
            frontX = Mathf.Max(
                frontX,
                renderer.transform.TransformPoint(new Vector3(min.x, min.y, 0f)).x,
                renderer.transform.TransformPoint(new Vector3(min.x, max.y, 0f)).x,
                renderer.transform.TransformPoint(new Vector3(max.x, min.y, 0f)).x,
                renderer.transform.TransformPoint(new Vector3(max.x, max.y, 0f)).x);
        }

        return frontX - transform.position.x;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (contactLocked || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        contactLocked = true;
        gameManager?.HandleDarknessContact();
    }
}
