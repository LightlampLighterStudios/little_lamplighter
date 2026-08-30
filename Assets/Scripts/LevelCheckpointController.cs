using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelCheckpointController : MonoBehaviour
{
    [Serializable]
    private sealed class LampState
    {
        public LampController lamp;
        public bool isLit;
        public bool hasEverBeenLit;
    }

    private sealed class CheckpointSnapshot
    {
        public WorldScroller.CheckpointSnapshot world;
        public Vector3 playerPosition;
        public GameManager.ProgressSnapshot progress;
        public List<LampState> lampStates = new List<LampState>();
    }

    [Header("Level References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField] private PlayerController player;
    [SerializeField] private DarknessController darkness;

    [Header("Respawn Presentation")]
    [SerializeField] private Image fadeOverlay;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.3f;
    [SerializeField, Min(0f)] private float caughtHoldDuration = 0.35f;
    [Tooltip("Distance from the player to the visible darkness front after respawn.")]
    [SerializeField, Min(0f)] private float darknessRespawnDistance = 14f;

    private LampController[] lamps;
    private CheckpointSnapshot latestCheckpoint;
    private bool respawning;

    private IEnumerator Start()
    {
        lamps = FindObjectsByType<LampController>(FindObjectsSortMode.None);

        if (
            gameManager == null ||
            worldScroller == null ||
            player == null ||
            darkness == null)
        {
            Debug.LogError(
                "LevelCheckpointController requires all level references.",
                this);
            enabled = false;
            yield break;
        }

        if (fadeOverlay != null)
        {
            SetFadeAlpha(0f);
            fadeOverlay.raycastTarget = false;
        }

        // Wait until WorldScroller has initialized every scrolling group.
        yield return null;

        CaptureCheckpoint();
        gameManager.LampLitEvent += HandleLampLit;
        gameManager.RespawnRequestedEvent += HandleRespawnRequested;
    }

    private void OnDestroy()
    {
        if (gameManager == null)
        {
            return;
        }

        gameManager.LampLitEvent -= HandleLampLit;
        gameManager.RespawnRequestedEvent -= HandleRespawnRequested;
    }

    private void HandleLampLit(LampController lamp, bool isRelight)
    {
        if (!respawning && !gameManager.IsLevelEnded)
        {
            CaptureCheckpoint();
        }
    }

    private void HandleRespawnRequested()
    {
        if (!respawning && latestCheckpoint != null)
        {
            StartCoroutine(RestoreCheckpointRoutine());
        }
    }

    private void CaptureCheckpoint()
    {
        CheckpointSnapshot snapshot = new CheckpointSnapshot
        {
            world = worldScroller.CaptureCheckpoint(),
            playerPosition = player.transform.position,
            progress = gameManager.CaptureProgress()
        };

        foreach (LampController lamp in lamps)
        {
            if (lamp == null)
            {
                continue;
            }

            snapshot.lampStates.Add(new LampState
            {
                lamp = lamp,
                isLit = lamp.IsLit,
                hasEverBeenLit = lamp.HasEverBeenLit
            });
        }

        latestCheckpoint = snapshot;
    }

    private IEnumerator RestoreCheckpointRoutine()
    {
        respawning = true;
        gameManager.PauseWorld();
        player.SetControlsEnabled(false);

        if (caughtHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(caughtHoldDuration);
        }

        yield return FadeTo(1f);

        worldScroller.RestoreCheckpoint(latestCheckpoint.world);
        player.Teleport(latestCheckpoint.playerPosition);

        foreach (LampState lampState in latestCheckpoint.lampStates)
        {
            if (lampState.lamp != null)
            {
                lampState.lamp.RestoreState(
                    lampState.isLit,
                    lampState.hasEverBeenLit);
            }
        }

        gameManager.RestoreProgress(latestCheckpoint.progress, false);
        Vector3 darknessRespawnPosition = darkness.GetPositionWithFrontAt(
            player.transform.position.x - darknessRespawnDistance);
        darkness.RestorePosition(darknessRespawnPosition);

        yield return FadeTo(0f);

        player.SetControlsEnabled(true);
        respawning = false;
        gameManager.ResumeWorld();
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeOverlay == null)
        {
            yield break;
        }

        float startAlpha = fadeOverlay.color.a;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private void SetFadeAlpha(float alpha)
    {
        Color colour = fadeOverlay.color;
        colour.a = alpha;
        fadeOverlay.color = colour;
    }
}
