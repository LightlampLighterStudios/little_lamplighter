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
        ScriptedDarknessCapture,
        WaitingForDarknessRecoveryLamp,
        WaitingForFirstLamp,
        WaitingForPuddlePractice,
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
        public WorldScroller.CheckpointSnapshot world;
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
    [SerializeField] private Transform darknessRecoveryCheckpoint;
    [SerializeField] private LampController firstLamp;
    [SerializeField] private PuddleHazard tutorialPuddle;
    [SerializeField] private PuddleHazard[] tutorialPracticePuddles;
    [SerializeField] private LampController gustLamp;
    [SerializeField] private LampController[] allLamps;

    [Header("Tutorial UI")]
    [SerializeField] private GameObject movementPrompt;
    [SerializeField] private GameObject lampPrompt;
    [SerializeField] private GameObject jumpPrompt;
    [SerializeField] private GameObject relightPrompt;
    [SerializeField] private Text lampPromptText;
    [SerializeField] private Image fadeOverlay;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.3f;
    [SerializeField, Min(0f)] private float splashHoldDuration = 0.35f;
    [SerializeField, Min(0f)] private float openingDarknessDelay = 0.4f;
    [SerializeField, Min(0.05f)] private float darknessCaptureDuration = 4f;
    [SerializeField, Min(0f)] private float darknessRecoveryDistance = 8f;
    [SerializeField, Min(0f)] private float recoveryLampHorizontalOffset = 1.5f;
    [SerializeField, Min(0f)] private float darknessRespawnMargin = 1f;

    private TutorialState state = TutorialState.Opening;
    private CheckpointSnapshot latestCheckpoint;
    private bool respawning;
    private string defaultLampPromptText;

    private bool IsLampMovementLocked()
    {
        return
            state == TutorialState.WaitingForDarknessRecoveryLamp ||
            state == TutorialState.WaitingForFirstLamp ||
            state == TutorialState.WaitingForGustLamp;
    }

    private void ApplyLampMovementLock()
    {
        player?.SetMovementEnabled(!IsLampMovementLocked());
    }

    private void Start()
    {
        gameManager.LevelStartedEvent += NotifyLevelStarted;
        gameManager.RespawnRequestedEvent += HandleRespawnRequested;
        gameManager.LevelEndedEvent += HandleLevelEnded;
        player.MovementPerformed += HandleOpeningMovement;
        firstLamp.Lit += HandleLampLit;
        gustLamp.Lit += HandleLampLit;

        if (
            (tutorialPracticePuddles == null || tutorialPracticePuddles.Length == 0) &&
            tutorialPuddle != null)
        {
            tutorialPracticePuddles = new[] { tutorialPuddle };
        }

        movementPrompt?.SetActive(true);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
        defaultLampPromptText = lampPromptText != null ? lampPromptText.text : string.Empty;

        if (fadeOverlay != null)
        {
            SetFadeAlpha(0f);
            fadeOverlay.raycastTarget = false;
        }

        latestCheckpoint = CaptureCheckpoint();
        ApplyLampMovementLock();
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
        }

        if (firstLamp != null)
        {
            firstLamp.Lit -= HandleLampLit;
        }

        if (gustLamp != null)
        {
            gustLamp.Lit -= HandleLampLit;
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
        tutorialPracticePuddles = requiredPuddle != null
            ? new[] { requiredPuddle }
            : Array.Empty<PuddleHazard>();
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

        if (state == TutorialState.Opening)
        {
            state = TutorialState.NormalPlay;
            latestCheckpoint = CaptureCheckpoint();
        }

        ApplyLampMovementLock();
    }

    public void EnterSection(TutorialSectionType sectionType)
    {
        if (respawning || gameManager.IsLevelEnded)
        {
            return;
        }

        switch (sectionType)
        {
            case TutorialSectionType.FirstLamp:
                if (state == TutorialState.WaitingForDarknessRecoveryLamp)
                {
                    ApplyLampMovementLock();
                    gameManager.PauseWorld();
                    ResetLampPromptText();
                    lampPrompt?.SetActive(true);
                    break;
                }

                latestCheckpoint = CaptureCheckpoint();
                state = TutorialState.WaitingForFirstLamp;
                ApplyLampMovementLock();
                gameManager.PauseWorld();
                lampPrompt?.SetActive(true);
                break;

            case TutorialSectionType.FirstPuddle:
                state = TutorialState.WaitingForPuddlePractice;
                latestCheckpoint = CaptureCheckpoint();
                jumpPrompt?.SetActive(true);
                break;

            case TutorialSectionType.PuddleCleared:
                if (state == TutorialState.WaitingForPuddlePractice)
                {
                    state = TutorialState.NormalPlay;
                    jumpPrompt?.SetActive(false);
                    latestCheckpoint = CaptureCheckpoint();
                }
                break;

            case TutorialSectionType.GustLamp:
                if (gustLamp.IsLit)
                {
                    state = TutorialState.NormalPlay;
                    ApplyLampMovementLock();
                    lampPrompt?.SetActive(false);
                    latestCheckpoint = CaptureCheckpoint();
                    gameManager.ResumeWorld();
                    break;
                }

                state = TutorialState.WaitingForGustLamp;
                ApplyLampMovementLock();
                gameManager.PauseWorld();
                ResetLampPromptText();
                lampPrompt?.SetActive(true);
                break;
        }
    }

    public void BeginRelightTutorial(LampController lamp)
    {
        if (respawning || gameManager.IsLevelEnded || lamp != gustLamp)
        {
            return;
        }

        state = TutorialState.WaitingForRelight;
        gameManager.PauseWorld();
        lampPrompt?.SetActive(false);
        relightPrompt?.SetActive(true);
    }

    public void RespawnAtLatestCheckpoint(bool restoreLives)
    {
        if (!respawning && !gameManager.IsLevelEnded)
        {
            StartCoroutine(RestoreCheckpointRoutine(restoreLives));
        }
    }

    public void BeginScriptedDarknessLesson(Transform recoveryCheckpoint)
    {
        if (
            respawning ||
            gameManager.IsLevelEnded ||
            state != TutorialState.NormalPlay ||
            recoveryCheckpoint == null)
        {
            return;
        }

        PrepareDarknessLesson();
        StartCoroutine(RunOpeningDarknessChase(0f));
    }

    public void HideAllPrompts()
    {
        state = TutorialState.Complete;
        ApplyLampMovementLock();
        firstLamp?.SetTutorialHighlighted(false);
        movementPrompt?.SetActive(false);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
    }

    private void HandleRespawnRequested()
    {
        if (respawning || gameManager.IsLevelEnded)
        {
            return;
        }

        if (state == TutorialState.ScriptedDarknessCapture)
        {
            UpdateDarknessRecoveryCheckpoint();
            StartCoroutine(RestoreCheckpointRoutine(
                false,
                TutorialState.WaitingForDarknessRecoveryLamp,
                false,
                false));
            return;
        }

        if (state == TutorialState.WaitingForPuddlePractice)
        {
            StartCoroutine(RestoreCheckpointRoutine(
                false,
                TutorialState.WaitingForPuddlePractice));
            return;
        }

        RespawnAtLatestCheckpoint(false);
    }

    private void HandleLevelEnded(bool succeeded)
    {
        // A pending fade must never restore movement or checkpoints after failure.
        StopAllCoroutines();
        respawning = false;
        ApplyLampMovementLock();
        if (fadeOverlay != null)
        {
            SetFadeAlpha(0f);
        }
        HideAllPrompts();
    }

    private void HandleOpeningMovement()
    {
        if (state == TutorialState.Opening)
        {
            if (darknessRecoveryCheckpoint == null)
            {
                gameManager.BeginLevel();
                return;
            }

            PrepareDarknessLesson();
            gameManager.BeginLevel();
            StartCoroutine(RunOpeningDarknessChase(openingDarknessDelay));
        }
    }

    private void PrepareDarknessLesson()
    {
        latestCheckpoint = CaptureCheckpoint();
        latestCheckpoint.darknessPosition = darkness.GetPositionWithFrontAt(
            player.transform.position.x - darknessRecoveryDistance);
        state = TutorialState.ScriptedDarknessCapture;
        HideTutorialPrompts();
    }

    private void UpdateDarknessRecoveryCheckpoint()
    {
        if (latestCheckpoint == null)
        {
            return;
        }

        Vector3 capturedPlayerPosition = player.transform.position;
        latestCheckpoint.playerPosition = capturedPlayerPosition;

        Vector3 recoveryRoutePosition = tutorialRoute.position;
        recoveryRoutePosition.x +=
            capturedPlayerPosition.x + recoveryLampHorizontalOffset -
            firstLamp.transform.position.x;
        latestCheckpoint.routePosition = recoveryRoutePosition;
        latestCheckpoint.darknessPosition = darkness.GetPositionWithFrontAt(
            capturedPlayerPosition.x - darknessRecoveryDistance);
    }

    private IEnumerator RunOpeningDarknessChase(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (respawning || state != TutorialState.ScriptedDarknessCapture)
        {
            yield break;
        }

        yield return darkness.CaptureTutorialPlayer(
            player.transform,
            darknessCaptureDuration);
    }

    private void HandleLampLit(LampController lamp, bool isRelight)
    {
        if (
            (state == TutorialState.WaitingForDarknessRecoveryLamp ||
            state == TutorialState.WaitingForFirstLamp) &&
            lamp == firstLamp)
        {
            lampPrompt?.SetActive(false);
            firstLamp.SetTutorialHighlighted(false);
            ResetLampPromptText();
            darkness.SetActiveThreat(true);
            state = TutorialState.NormalPlay;
            ApplyLampMovementLock();
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
            return;
        }

        if (state == TutorialState.WaitingForRelight && lamp == gustLamp && isRelight)
        {
            relightPrompt?.SetActive(false);
            state = TutorialState.NormalPlay;
            ApplyLampMovementLock();
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
        }

        if (state == TutorialState.WaitingForGustLamp && lamp == gustLamp)
        {
            lampPrompt?.SetActive(false);
            state = TutorialState.NormalPlay;
            ApplyLampMovementLock();
            latestCheckpoint = CaptureCheckpoint();
            gameManager.ResumeWorld();
        }
    }

    private CheckpointSnapshot CaptureCheckpoint()
    {
        CheckpointSnapshot snapshot = new CheckpointSnapshot
        {
            world = worldScroller.CaptureCheckpoint(),
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

    private IEnumerator RestoreCheckpointRoutine(
        bool restoreLives,
        TutorialState stateAfterRespawn = TutorialState.NormalPlay,
        bool resumeWorldAfterRespawn = true,
        bool restoreDarknessOffScreen = true)
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

        worldScroller.RestoreCheckpoint(latestCheckpoint.world);
        tutorialRoute.position = latestCheckpoint.routePosition;
        player.Teleport(latestCheckpoint.playerPosition);
        ResetTutorialPuddles();

        if (restoreDarknessOffScreen)
        {
            darkness.RestoreJustOffScreen(
                Camera.main,
                darknessRespawnMargin);
        }
        else
        {
            darkness.RestorePosition(latestCheckpoint.darknessPosition);
        }

        gameManager.RestoreProgress(latestCheckpoint.progress, restoreLives);

        foreach (LampState lampState in latestCheckpoint.lampStates)
        {
            lampState.lamp.RestoreState(lampState.isLit, lampState.hasEverBeenLit);
        }

        state = stateAfterRespawn;
        ApplyLampMovementLock();
        jumpPrompt?.SetActive(state == TutorialState.WaitingForPuddlePractice);
        lampPrompt?.SetActive(state == TutorialState.WaitingForDarknessRecoveryLamp);
        relightPrompt?.SetActive(false);

        if (state == TutorialState.WaitingForDarknessRecoveryLamp)
        {
            firstLamp.SetTutorialHighlighted(true);
            ResetLampPromptText();
        }

        yield return FadeTo(0f);
        player.SetControlsEnabled(true);
        ApplyLampMovementLock();
        respawning = false;

        if (resumeWorldAfterRespawn)
        {
            gameManager.ResumeWorld();
        }
    }

    private void ResetTutorialPuddles()
    {
        if (tutorialPracticePuddles == null)
        {
            return;
        }

        foreach (PuddleHazard puddle in tutorialPracticePuddles)
        {
            puddle?.ResetHazard();
        }
    }

    private void HideTutorialPrompts()
    {
        movementPrompt?.SetActive(false);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
    }

    private void SetLampPromptText(string message)
    {
        if (lampPromptText != null)
        {
            lampPromptText.text = message;
        }
    }

    private void ResetLampPromptText()
    {
        SetLampPromptText(defaultLampPromptText);
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
