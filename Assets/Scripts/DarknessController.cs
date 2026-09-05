using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DarknessController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameManager gameManager;
    [SerializeField, Min(0f)] private float advanceSpeed = 0.35f;
    [SerializeField, Min(0f)] private float pushBackDistance = 5f;
    [SerializeField] private float resetX = -31f;
    [SerializeField] private bool clampTrailingDistance;
    [SerializeField, Min(0f)] private float maximumTrailingDistance = 24f;
    [SerializeField, Min(0f)] private float tutorialCaptureDistance = 14f;
    [SerializeField, Min(0f)] private float tutorialCaptureOverlap = 20f;
    [SerializeField, Min(0.05f)] private float retreatDuration = 0.9f;
    [SerializeField, Min(0f)] private float retreatMargin = 2f;

    private bool activeThreat;
    private bool contactLocked;
    private Coroutine retreatRoutine;
    private float trailingDistanceAllowance;

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

        float advanceDistance = advanceSpeed * Time.deltaTime;
        transform.position += Vector3.right * advanceDistance;
        trailingDistanceAllowance = Mathf.Max(
            0f,
            trailingDistanceAllowance - advanceDistance);
        ClampTrailingDistance();
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

        if (active)
        {
            ClampTrailingDistance();
        }
        else
        {
            trailingDistanceAllowance = 0f;
        }
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
        PushBack(1f);
    }

    public void PushBack(float distanceMultiplier)
    {
        if (!activeThreat)
        {
            activeThreat = true;
            gameObject.SetActive(true);
        }

        Vector3 position = transform.position;
        position.x -= pushBackDistance * Mathf.Max(0.1f, distanceMultiplier);
        transform.position = position;
        ExpandAllowanceToCurrentTrailingDistance();
        ClampTrailingDistance();
    }

    public void ApplyWorldScroll(float signedWorldDistance)
    {
        if (Mathf.Approximately(signedWorldDistance, 0f))
        {
            return;
        }

        // Positive route travel moves world-anchored content left. Darkness
        // then continues its independent advance in Update.
        transform.position += Vector3.left * signedWorldDistance;

        if (activeThreat)
        {
            ClampTrailingDistance();
        }
    }

    public void ResetThreat()
    {
        Vector3 position = transform.position;
        position.x = resetX;
        transform.position = position;
        contactLocked = false;
        trailingDistanceAllowance = 0f;
    }

    public void RestorePosition(Vector3 position)
    {
        transform.position = position;
        contactLocked = false;
        trailingDistanceAllowance = 0f;

        if (activeThreat)
        {
            ExpandAllowanceToCurrentTrailingDistance();
            ClampTrailingDistance();
        }
    }

    public void RestoreJustOffScreen(
        Camera targetCamera,
        float margin = 1f)
    {
        float visibleFrontOffset = GetVisibleFrontOffsetX();
        float targetFrontX;

        if (targetCamera != null)
        {
            float cameraDistance = Mathf.Abs(
                targetCamera.transform.position.z - transform.position.z);
            float leftEdge = targetCamera.ViewportToWorldPoint(
                new Vector3(0f, 0.5f, cameraDistance)).x;
            targetFrontX = leftEdge - Mathf.Max(0f, margin);
        }
        else if (player != null)
        {
            targetFrontX = player.position.x - maximumTrailingDistance;
        }
        else
        {
            ResetThreat();
            return;
        }

        Vector3 position = transform.position;
        position.x = targetFrontX - visibleFrontOffset;
        transform.position = position;
        contactLocked = false;
        trailingDistanceAllowance = 0f;

        if (activeThreat)
        {
            ClampTrailingDistance();
        }
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

    private void ClampTrailingDistance()
    {
        if (!clampTrailingDistance || player == null)
        {
            return;
        }

        float visibleFrontOffset = GetVisibleFrontOffsetX();
        float visibleFrontX = transform.position.x + visibleFrontOffset;
        float allowedTrailingDistance =
            maximumTrailingDistance + trailingDistanceAllowance;
        float minimumFrontX = player.position.x - allowedTrailingDistance;

        if (visibleFrontX < minimumFrontX)
        {
            Vector3 position = transform.position;
            position.x += minimumFrontX - visibleFrontX;
            transform.position = position;
            visibleFrontX = minimumFrontX;
        }

        // Keep only allowance that is represented by real separation. Walking
        // back toward darkness cannot bank an old lamp push for later.
        float currentTrailingDistance = player.position.x - visibleFrontX;
        trailingDistanceAllowance = Mathf.Min(
            trailingDistanceAllowance,
            Mathf.Max(
                0f,
                currentTrailingDistance - maximumTrailingDistance));
    }

    private void ExpandAllowanceToCurrentTrailingDistance()
    {
        if (!clampTrailingDistance || player == null)
        {
            return;
        }

        float visibleFrontX = transform.position.x + GetVisibleFrontOffsetX();
        float currentTrailingDistance = player.position.x - visibleFrontX;
        trailingDistanceAllowance = Mathf.Max(
            trailingDistanceAllowance,
            Mathf.Max(
                0f,
                currentTrailingDistance - maximumTrailingDistance));
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
