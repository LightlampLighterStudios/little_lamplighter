using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialDirector : MonoBehaviour
{
    private enum TutorialState
    {
        Opening,
        NormalPlay,
        WaitingForFirstLamp,
        WaitingForPuddleJump,
        ClearingTutorialPuddle,
        WaitingForGustLamp,
        WaitingForRelight,
        Complete
    }

    [Serializable]
    private sealed class LampState
    {
        public LampController lamp;
        public bool isLit;
        public bool hasEverBeenLit;
    }

    [Serializable]
    private sealed class CheckpointSnapshot
    {
        public Vector3 routePosition;
        public Vector3 playerPosition;
        public Vector3 darknessPosition;
        public GameManager.ProgressSnapshot progress;
        public List<LampState> lampStates = new List<LampState>();
    }

    [Header("Level References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform tutorialRoute;
    [SerializeField] private DarknessController darkness;
    [SerializeField] private LampController firstLamp;
    [SerializeField] private PuddleHazard tutorialPuddle;
    [SerializeField] private LampController gustLamp;
    [SerializeField] private LampController[] allLamps;

    [Header("Tutorial UI")]
    [SerializeField] private GameObject movementPrompt;
    [SerializeField] private GameObject lampPrompt;
    [SerializeField] private GameObject jumpPrompt;
    [SerializeField] private GameObject relightPrompt;
    [SerializeField] private Image fadeOverlay;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.3f;
    [SerializeField, Min(0f)] private float splashHoldDuration = 0.35f;

    private TutorialState state = TutorialState.Opening;
    private CheckpointSnapshot latestCheckpoint;
    private bool respawning;

    private void Start()
    {
        gameManager.LevelStartedEvent += NotifyLevelStarted;
        gameManager.RespawnRequestedEvent += HandleRespawnRequested;
        gameManager.LevelEndedEvent += HandleLevelEnded;
        player.MovementPerformed += HandleOpeningMovement;
        player.JumpPerformed += HandleJump;
        firstLamp.Lit += HandleLampLit;
        gustLamp.Lit += HandleLampLit;
        tutorialPuddle.PlayerEntered += HandlePuddleEntered;

        movementPrompt?.SetActive(true);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);

        if (fadeOverlay != null)
        {
            SetFadeAlpha(0f);
            fadeOverlay.raycastTarget = false;
        }

        latestCheckpoint = CaptureCheckpoint();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.LevelStartedEvent -= NotifyLevelStarted;
            gameManager.RespawnRequestedEvent -= HandleRespawnRequested;
            gameManager.LevelEndedEvent -= HandleLevelEnded;
        }

        if (player != null)
        {
            player.MovementPerformed -= HandleOpeningMovement;
            player.JumpPerformed -= HandleJump;
        }

        if (firstLamp != null)
        {
            firstLamp.Lit -= HandleLampLit;
        }

        if (gustLamp != null)
        {
            gustLamp.Lit -= HandleLampLit;
        }

        if (tutorialPuddle != null)
        {
            tutorialPuddle.PlayerEntered -= HandlePuddleEntered;
        }
    }

    public void Configure(
        GameManager manager,
        WorldScroller scroller,
        PlayerController targetPlayer,
        Transform route,
        DarknessController darknessController,
        LampController requiredFirstLamp,
        PuddleHazard requiredPuddle,
        LampController requiredGustLamp,
        LampController[] lamps,
        GameObject movePrompt,
        GameObject lightPrompt,
        GameObject spacePrompt,
        GameObject windPrompt,
        Image fade)
    {
        gameManager = manager;
        worldScroller = scroller;
        player = targetPlayer;
        tutorialRoute = route;
        darkness = darknessController;
        firstLamp = requiredFirstLamp;
        tutorialPuddle = requiredPuddle;
        gustLamp = requiredGustLamp;
        allLamps = lamps;
        movementPrompt = movePrompt;
        lampPrompt = lightPrompt;
        jumpPrompt = spacePrompt;
        relightPrompt = windPrompt;
        fadeOverlay = fade;
    }

    public void NotifyLevelStarted()
    {
        movementPrompt?.SetActive(false);
        state = TutorialState.NormalPlay;
        latestCheckpoint = CaptureCheckpoint();
    }

    public void EnterSection(TutorialSectionType sectionType)
    {
        if (respawning || gameManager.IsLevelComplete)
        {
            return;
        }

        switch (sectionType)
        {
            case TutorialSectionType.FirstLamp:
                latestCheckpoint = CaptureCheckpoint();
                state = TutorialState.WaitingForFirstLamp;
                gameManager.PauseWorld();
                lampPrompt?.SetActive(true);
                break;

            case TutorialSectionType.FirstPuddle:
                latestCheckpoint = CaptureCheckpoint();
                state = TutorialState.WaitingForPuddleJump;
                gameManager.PauseWorld();
                jumpPrompt?.SetActive(true);
                break;

            case TutorialSectionType.PuddleCleared:
                if (state == TutorialState.ClearingTutorialPuddle)
                {
                    state = TutorialState.NormalPlay;
                    jumpPrompt?.SetActive(false);
                    latestCheckpoint = CaptureCheckpoint();
                }
                break;

            case TutorialSectionType.GustLamp:
                state = TutorialState.WaitingForGustLamp;
                gameManager.PauseWorld();
                lampPrompt?.SetActive(true);
                break;
        }
    }

    public void BeginRelightTutorial(LampController lamp)
    {
        if (respawning || lamp != gustLamp)
        {
            return;
        }

        state = TutorialState.WaitingForRelight;
        gameManager.PauseWorld();
        relightPrompt?.SetActive(true);
    }

    public void RespawnAtLatestCheckpoint(bool restoreLives)
    {
        if (!respawning)
        {
            StartCoroutine(RestoreCheckpointRoutine(restoreLives));
        }
    }

    public void HideAllPrompts()
    {
        state = TutorialState.Complete;
        movementPrompt?.SetActive(false);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
    }

    private void HandleRespawnRequested()
    {
        RespawnAtLatestCheckpoint(false);
    }

    private void HandleLevelEnded(bool succeeded)
    {
        HideAllPrompts();
    }

    private void HandleOpeningMovement()
    {
        if (state == TutorialState.Opening)
        {
            gameManager.BeginLevel();
        }
    }

    private void HandleJump()
    {
        if (state != TutorialState.WaitingForPuddleJump)
        {
            return;
        }

        state = TutorialState.ClearingTutorialPuddle;
        gameManager.ResumeWorld();
    }

    private void HandleLampLit(LampController lamp, bool isRelight)
    {
        if (state == TutorialState.WaitingForFirstLamp && lamp == firstLamp)
        {
            lampPrompt?.SetActive(false);
            darkness.SetActiveThreat(true);
            state = TutorialState.NormalPlay;
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
            return;
        }

        if (state == TutorialState.WaitingForRelight && lamp == gustLamp && isRelight)
        {
            relightPrompt?.SetActive(false);
            state = TutorialState.NormalPlay;
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
        }

        if (state == TutorialState.WaitingForGustLamp && lamp == gustLamp)
        {
            lampPrompt?.SetActive(false);
            state = TutorialState.NormalPlay;
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
        }
    }

    private void HandlePuddleEntered(PuddleHazard puddle, PlayerController targetPlayer)
    {
        if (
            puddle == tutorialPuddle &&
            (state == TutorialState.WaitingForPuddleJump ||
            state == TutorialState.ClearingTutorialPuddle) &&
            !respawning)
        {
            StartCoroutine(RestoreCheckpointRoutine(true));
        }
    }

    private CheckpointSnapshot CaptureCheckpoint()
    {
        CheckpointSnapshot snapshot = new CheckpointSnapshot
        {
            routePosition = tutorialRoute.position,
            playerPosition = player.transform.position,
            darknessPosition = darkness.transform.position,
            progress = gameManager.CaptureProgress()
        };

        foreach (LampController lamp in allLamps)
        {
            snapshot.lampStates.Add(new LampState
            {
                lamp = lamp,
                isLit = lamp.IsLit,
                hasEverBeenLit = lamp.HasEverBeenLit
            });
        }

        return snapshot;
    }

    private IEnumerator RestoreCheckpointRoutine(bool restoreLives)
    {
        if (latestCheckpoint == null)
        {
            yield break;
        }

        respawning = true;
        gameManager.PauseWorld();
        player.SetControlsEnabled(false);

        yield return new WaitForSeconds(splashHoldDuration);
        yield return FadeTo(1f);

        tutorialRoute.position = latestCheckpoint.routePosition;
        player.Teleport(latestCheckpoint.playerPosition);
        tutorialPuddle.ResetHazard();
        darkness.RestorePosition(latestCheckpoint.darknessPosition);
        gameManager.RestoreProgress(latestCheckpoint.progress, restoreLives);

        foreach (LampState lampState in latestCheckpoint.lampStates)
        {
            lampState.lamp.RestoreState(lampState.isLit, lampState.hasEverBeenLit);
        }

        state = restoreLives
            ? TutorialState.WaitingForPuddleJump
            : TutorialState.NormalPlay;
        jumpPrompt?.SetActive(restoreLives);
        lampPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);

        yield return FadeTo(0f);
        player.SetControlsEnabled(true);
        respawning = false;

        if (!restoreLives)
        {
            gameManager.ResumeWorld();
        }
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeOverlay == null)
        {
            yield break;
        }

        fadeOverlay.raycastTarget = true;
        float startAlpha = fadeOverlay.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
        fadeOverlay.raycastTarget = targetAlpha > 0.01f;
    }

    private void SetFadeAlpha(float alpha)
    {
        Color colour = fadeOverlay.color;
        colour.a = Mathf.Clamp01(alpha);
        fadeOverlay.color = colour;
    }
}
