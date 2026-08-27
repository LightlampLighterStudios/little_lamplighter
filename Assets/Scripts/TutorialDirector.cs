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
    [SerializeField] private Text lampPromptText;
    [SerializeField] private Image fadeOverlay;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.3f;
    [SerializeField, Min(0f)] private float splashHoldDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float darknessCaptureDuration = 0.8f;
    [SerializeField, Min(0f)] private float darknessRecoveryDistance = 8f;

    private TutorialState state = TutorialState.Opening;
    private CheckpointSnapshot latestCheckpoint;
    private bool respawning;
    private string defaultLampPromptText;

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
        defaultLampPromptText = lampPromptText != null ? lampPromptText.text : string.Empty;

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
                if (state == TutorialState.WaitingForDarknessRecoveryLamp)
                {
                    gameManager.PauseWorld();
                    SetLampPromptText("HOLD ENTER TO LIGHT - PUSH IT BACK");
                    lampPrompt?.SetActive(true);
                    break;
                }

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

    public void BeginScriptedDarknessLesson(Transform recoveryCheckpoint)
    {
        if (
            respawning ||
            state != TutorialState.NormalPlay ||
            recoveryCheckpoint == null)
        {
            return;
        }

        latestCheckpoint = CaptureCheckpoint();
        latestCheckpoint.playerPosition = recoveryCheckpoint.position;
        Vector3 recoveryDarknessPosition = darkness.GetPositionWithFrontAt(
            recoveryCheckpoint.position.x - darknessRecoveryDistance);
        latestCheckpoint.darknessPosition = recoveryDarknessPosition;
        state = TutorialState.ScriptedDarknessCapture;
        gameManager.PauseWorld();
        player.SetControlsEnabled(false);
        HideTutorialPrompts();
        StartCoroutine(darkness.CaptureTutorialPlayer(player.transform, darknessCaptureDuration));
    }

    public void HideAllPrompts()
    {
        state = TutorialState.Complete;
        firstLamp?.SetTutorialHighlighted(false);
        movementPrompt?.SetActive(false);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
    }

    private void HandleRespawnRequested()
    {
        if (state == TutorialState.ScriptedDarknessCapture)
        {
            StartCoroutine(RestoreCheckpointRoutine(
                false,
                TutorialState.WaitingForDarknessRecoveryLamp,
                false));
            return;
        }

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
            StartCoroutine(RestoreCheckpointRoutine(
                true,
                TutorialState.WaitingForPuddleJump,
                false));
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

    private IEnumerator RestoreCheckpointRoutine(
        bool restoreLives,
        TutorialState stateAfterRespawn = TutorialState.NormalPlay,
        bool resumeWorldAfterRespawn = true)
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

        state = stateAfterRespawn;
        jumpPrompt?.SetActive(state == TutorialState.WaitingForPuddleJump);
        lampPrompt?.SetActive(state == TutorialState.WaitingForDarknessRecoveryLamp);
        relightPrompt?.SetActive(false);

        if (state == TutorialState.WaitingForDarknessRecoveryLamp)
        {
            firstLamp.SetTutorialHighlighted(true);
            SetLampPromptText("HOLD ENTER TO LIGHT - PUSH IT BACK");
        }

        yield return FadeTo(0f);
        player.SetControlsEnabled(true);
        respawning = false;

        if (resumeWorldAfterRespawn)
        {
            gameManager.ResumeWorld();
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
