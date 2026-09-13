using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialDirector : MonoBehaviour
{
    private enum TutorialState { Opening, DarknessCapture, NormalPlay, FirstLamp, Puddle, GustLamp, Relight, Complete }
    [Serializable] private sealed class LampState
    {
        public LampController lamp;
        public bool isLit, hasEverBeenLit;
    }
    private sealed class CheckpointSnapshot
    {
        public WorldScroller.CheckpointSnapshot world;
        public Vector3 routePosition, playerPosition, darknessPosition;
        public GameManager.ProgressSnapshot progress;
        public TutorialState state;
        public bool firstLampLearned, jumpLearned, relightLearned;
        public int guidedPuddleIndex;
        public readonly List<LampState> lamps = new List<LampState>();
        public bool[] sections, gusts;
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
    [Header("Contextual Tutorial")]
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private Transform promptCanvas;
    [SerializeField] private Transform moveAnchor;
    [SerializeField] private Transform jumpAnchor;
    [SerializeField] private Vector3 playerPromptOffset = new Vector3(0, 5, 0);
    [SerializeField] private Vector3 lampPromptOffset = new Vector3(0, -11.8f, 0);
    [Tooltip("Extra world offset from the authored first-puddle anchor.")]
    [SerializeField] private Vector3 firstJumpPromptOffset = new Vector3(0, -8.6f, 0);
    [Tooltip("World offset from the second puddle; negative X makes it appear earlier.")]
    [SerializeField] private Vector3 secondJumpPromptOffset = new Vector3(0, -8.6f, 0);
    [SerializeField, Min(0)] private float openingDarknessDelay = .4f;
    [SerializeField, Min(.05f)] private float darknessCaptureDuration = 4f;
    [SerializeField, Min(0)] private float darknessRecoveryDistance = 8f;
    [SerializeField, Min(0)] private float recoveryLampHorizontalOffset = 2.8f;
    [Header("Existing HUD / Respawn")]
    [SerializeField] private GameObject movementPrompt, lampPrompt, jumpPrompt, relightPrompt;
    [SerializeField] private Image fadeOverlay;
    [SerializeField, Min(.05f)] private float fadeDuration = .3f;
    [SerializeField, Min(0)] private float splashHoldDuration = .35f;
    [SerializeField, Min(0)] private float darknessRespawnMargin = 1f;

    private TutorialState state;
    private CheckpointSnapshot latestCheckpoint;
    private TutorialKeyPrompt prompt, firstJumpPrompt, secondJumpPrompt;
    private TutorialSectionTrigger[] sectionTriggers;
    private ScriptedGustTrigger[] gustTriggers;
    private bool respawning, firstLampLearned, jumpLearned, relightLearned;
    private bool jumpedThisAttempt, touchedPuddle;
    private PuddleHazard[] guidedPuddles;
    private int guidedPuddleIndex;
    private Collider2D puddleCollider, playerCollider;

    private IEnumerator Start()
    {
        // Other levels contain an unused director with empty lesson references.
        if (gameManager == null || player == null || worldScroller == null ||
            tutorialRoute == null || darkness == null || firstLamp == null || gustLamp == null || tutorialPuddle == null)
        {
            enabled = false;
            yield break;
        }
        movementPrompt?.SetActive(false);
        lampPrompt?.SetActive(false);
        jumpPrompt?.SetActive(false);
        relightPrompt?.SetActive(false);
        prompt = CreatePrompt(promptCanvas);
        firstJumpPrompt = CreatePrompt(promptCanvas);
        secondJumpPrompt = CreatePrompt(promptCanvas);
        sectionTriggers = tutorialRoute.GetComponentsInChildren<TutorialSectionTrigger>(true);
        gustTriggers = tutorialRoute.GetComponentsInChildren<ScriptedGustTrigger>(true);
        guidedPuddles = BuildGuidedPuddles();
        puddleCollider = guidedPuddles[0].GetComponent<Collider2D>();
        playerCollider = player.GetComponent<Collider2D>();
        gameManager.LevelStartedEvent += NotifyLevelStarted;
        gameManager.RespawnRequestedEvent += HandleRespawnRequested;
        gameManager.LevelEndedEvent += HandleLevelEnded;
        gameManager.LampLitEvent += HandleLampLit;
        player.MovementPerformed += HandleOpeningMovement;
        player.JumpPerformed += HandleJump;
        foreach (PuddleHazard puddle in guidedPuddles) puddle.PlayerEntered += HandlePuddleContact;
        darkness.SetTutorialProtection(true);
        darkness.SetActiveThreat(false);
        if (fadeOverlay != null) { SetFadeAlpha(0); fadeOverlay.raycastTarget = false; }
        RefreshLesson();
        // Capture only after the scroller and its finite groups have initialized.
        yield return null;
        latestCheckpoint = CaptureCheckpoint();
    }

    private void Update()
    {
        if (respawning || gameManager == null || gameManager.IsLevelEnded || Time.timeScale == 0) return;
        if (state == TutorialState.Puddle && jumpedThisAttempt && !touchedPuddle &&
            player.IsGrounded && playerCollider.bounds.min.x > puddleCollider.bounds.max.x + .15f)
        {
            if (guidedPuddleIndex + 1 < guidedPuddles.Length)
            {
                guidedPuddleIndex++;
                puddleCollider = guidedPuddles[guidedPuddleIndex].GetComponent<Collider2D>();
                jumpedThisAttempt = touchedPuddle = false;
                RefreshLesson();
                latestCheckpoint = CaptureCheckpoint();
            }
            else
            {
                jumpLearned = true;
                state = TutorialState.NormalPlay;
                RefreshLesson();
                latestCheckpoint = CaptureCheckpoint();
            }
        }
    }

    public void NotifyLevelStarted()
    {
        darkness.SetTutorialProtection(state != TutorialState.DarknessCapture &&
            (!firstLampLearned || IsGuidedLesson()));
    }

    public void EnterSection(TutorialSectionType sectionType)
    {
        if (respawning || !enabled || gameManager.IsLevelEnded || state == TutorialState.Complete) return;
        switch (sectionType)
        {
            case TutorialSectionType.FirstLamp:
                if (firstLampLearned || firstLamp.IsLit) return;
                state = TutorialState.FirstLamp;
                gameManager.PauseWorld();
                break;
            case TutorialSectionType.FirstPuddle:
                if (jumpLearned) return;
                state = TutorialState.Puddle;
                guidedPuddleIndex = 0;
                puddleCollider = guidedPuddles[guidedPuddleIndex].GetComponent<Collider2D>();
                jumpedThisAttempt = touchedPuddle = false;
                break;
            case TutorialSectionType.PuddleCleared:
                // Crossing this volume in mid-air is not success; Update checks the landing.
                return;
            case TutorialSectionType.GustLamp:
                if (gustLamp.IsLit || relightLearned) return;
                state = TutorialState.GustLamp;
                gameManager.PauseWorld();
                break;
        }
        RefreshLesson();
        latestCheckpoint = CaptureCheckpoint();
    }

    public void BeginRelightTutorial(LampController lamp)
    {
        if (respawning || !enabled || gameManager.IsLevelEnded || lamp != gustLamp || relightLearned) return;
        if (lamp.IsLit || !lamp.HasEverBeenLit) return;
        state = TutorialState.Relight;
        gameManager.PauseWorld();
        RefreshLesson();
        latestCheckpoint = CaptureCheckpoint();
    }

    public void BeginScriptedDarknessLesson(Transform recoveryCheckpoint)
    {
        if (respawning || gameManager.IsLevelEnded || state != TutorialState.NormalPlay || recoveryCheckpoint == null) return;
        darknessRecoveryCheckpoint = recoveryCheckpoint;
        PrepareDarknessLesson();
        StartCoroutine(RunOpeningDarknessChase(0));
    }

    public void HideAllPrompts()
    {
        state = TutorialState.Complete;
        prompt?.Hide();
        firstJumpPrompt?.Hide();
        secondJumpPrompt?.Hide();
        firstLamp?.SetTutorialHighlighted(false);
        gustLamp?.SetTutorialHighlighted(false);
        darkness?.SetTutorialProtection(false);
        if (gameManager != null && !gameManager.IsLevelEnded) player?.SetMovementEnabled(true);
    }

    private bool IsGuidedLesson() => state == TutorialState.FirstLamp || state == TutorialState.GustLamp ||
        state == TutorialState.Relight || state == TutorialState.Puddle;

    private void RefreshLesson()
    {
        secondJumpPrompt?.Hide();
        firstJumpPrompt?.Hide();
        firstLamp.SetTutorialHighlighted(state == TutorialState.FirstLamp);
        gustLamp.SetTutorialHighlighted(state == TutorialState.GustLamp || state == TutorialState.Relight);
        darkness.SetTutorialProtection(!firstLampLearned || IsGuidedLesson());
        // Relighting requires walking back from the gust trigger, so movement stays available.
        player.SetMovementEnabled(state != TutorialState.FirstLamp && state != TutorialState.GustLamp);
        switch (state)
        {
            case TutorialState.Opening:
                prompt?.Show(TutorialKeyPrompt.Lesson.Move, moveAnchor, Vector3.zero, player); break;
            case TutorialState.FirstLamp:
                prompt?.Show(TutorialKeyPrompt.Lesson.Light, firstLamp.transform, lampPromptOffset, player, firstLamp); break;
            case TutorialState.GustLamp:
                prompt?.Show(TutorialKeyPrompt.Lesson.Light, gustLamp.transform, lampPromptOffset, player, gustLamp); break;
            case TutorialState.Relight:
                prompt?.Show(TutorialKeyPrompt.Lesson.Relight, gustLamp.transform, lampPromptOffset, player, gustLamp); break;
            case TutorialState.Puddle:
                bool firstPuddle = guidedPuddleIndex == 0;
                if (firstPuddle)
                {
                    prompt?.Hide();
                    firstJumpPrompt?.Show(TutorialKeyPrompt.Lesson.Jump, guidedPuddles[0].transform,
                        firstJumpPromptOffset, player);
                }
                if (guidedPuddles.Length > 1)
                    secondJumpPrompt?.Show(TutorialKeyPrompt.Lesson.Jump, guidedPuddles[1].transform,
                        secondJumpPromptOffset, player);
                break;
            default: prompt?.Hide(); break;
        }
    }

    private void HandleJump() { if (state == TutorialState.Puddle) jumpedThisAttempt = true; }
    private void HandlePuddleContact(PuddleHazard puddle, PlayerController target)
    {
        if (state != TutorialState.Puddle || target != player || respawning ||
            puddle != guidedPuddles[guidedPuddleIndex]) return;
        touchedPuddle = true;
        // Retry the same approach. Walking through the water never dismisses JUMP.
        RespawnAtLatestCheckpoint(false);
    }

    private void HandleLampLit(LampController lamp, bool isRelight)
    {
        if (respawning || gameManager.IsLevelEnded) return;
        if (lamp == firstLamp)
        {
            firstLampLearned = true;
            if (state == TutorialState.FirstLamp || state == TutorialState.Opening) state = TutorialState.NormalPlay;
            darkness.SetActiveThreat(true);
        }
        if (lamp == gustLamp && state == TutorialState.Relight && isRelight)
        {
            relightLearned = true;
            state = TutorialState.NormalPlay;
        }
        else if (lamp == gustLamp && state == TutorialState.GustLamp) state = TutorialState.NormalPlay;
        RefreshLesson();
        gameManager.ResumeWorld();
        latestCheckpoint = CaptureCheckpoint();
    }

    private CheckpointSnapshot CaptureCheckpoint()
    {
        var snapshot = new CheckpointSnapshot {
            world = worldScroller.CaptureCheckpoint(), routePosition = tutorialRoute.position,
            playerPosition = player.transform.position, darknessPosition = darkness.transform.position,
            progress = gameManager.CaptureProgress(), state = state,
            firstLampLearned = firstLampLearned, jumpLearned = jumpLearned, relightLearned = relightLearned,
            guidedPuddleIndex = guidedPuddleIndex,
            sections = new bool[sectionTriggers.Length], gusts = new bool[gustTriggers.Length]
        };
        foreach (var lamp in allLamps)
            if (lamp != null) snapshot.lamps.Add(new LampState { lamp = lamp, isLit = lamp.IsLit, hasEverBeenLit = lamp.HasEverBeenLit });
        for (int i = 0; i < sectionTriggers.Length; i++) snapshot.sections[i] = sectionTriggers[i].IsTriggered;
        for (int i = 0; i < gustTriggers.Length; i++) snapshot.gusts[i] = gustTriggers[i].IsTriggered;
        return snapshot;
    }

    public void RespawnAtLatestCheckpoint(bool restoreLives)
    {
        if (!respawning && enabled && !gameManager.IsLevelEnded && latestCheckpoint != null)
            StartCoroutine(RestoreCheckpointRoutine(restoreLives));
    }
    private void HandleRespawnRequested()
    {
        if (respawning || gameManager.IsLevelEnded) return;
        if (state == TutorialState.DarknessCapture)
        {
            UpdateDarknessRecoveryCheckpoint();
            StartCoroutine(RestoreCheckpointRoutine(false, TutorialState.FirstLamp, false, false));
            return;
        }
        RespawnAtLatestCheckpoint(false);
    }

    private IEnumerator RestoreCheckpointRoutine(bool restoreLives, TutorialState? stateAfterRespawn = null,
        bool resumeWorldAfterRespawn = true, bool restoreDarknessOffScreen = true)
    {
        respawning = true;
        var snapshot = latestCheckpoint;
        prompt?.Hide();
        firstJumpPrompt?.Hide();
        secondJumpPrompt?.Hide();
        gameManager.PauseWorld();
        player.SetControlsEnabled(false);
        yield return new WaitForSeconds(splashHoldDuration);
        yield return FadeTo(1);
        worldScroller.RestoreCheckpoint(snapshot.world);
        tutorialRoute.position = snapshot.routePosition;
        player.Teleport(snapshot.playerPosition);
        if (tutorialPracticePuddles != null)
            foreach (var puddle in tutorialPracticePuddles) puddle?.ResetHazard();
        tutorialPuddle.ResetHazard();
        foreach (var lamp in snapshot.lamps) lamp.lamp.RestoreState(lamp.isLit, lamp.hasEverBeenLit);
        for (int i = 0; i < sectionTriggers.Length; i++) sectionTriggers[i].RestoreState(snapshot.sections[i]);
        for (int i = 0; i < gustTriggers.Length; i++) gustTriggers[i].RestoreState(snapshot.gusts[i]);
        gameManager.RestoreProgress(snapshot.progress, restoreLives);
        if (restoreDarknessOffScreen) darkness.RestoreJustOffScreen(Camera.main, darknessRespawnMargin);
        else darkness.RestorePosition(snapshot.darknessPosition);
        state = stateAfterRespawn ?? snapshot.state;
        firstLampLearned = snapshot.firstLampLearned;
        jumpLearned = snapshot.jumpLearned;
        relightLearned = snapshot.relightLearned;
        guidedPuddleIndex = Mathf.Clamp(snapshot.guidedPuddleIndex, 0, guidedPuddles.Length - 1);
        puddleCollider = guidedPuddles[guidedPuddleIndex].GetComponent<Collider2D>();
        jumpedThisAttempt = touchedPuddle = false;
        yield return FadeTo(0);
        player.SetControlsEnabled(true);
        RefreshLesson();
        respawning = false;
        if (resumeWorldAfterRespawn && state != TutorialState.FirstLamp &&
            state != TutorialState.GustLamp && state != TutorialState.Relight)
            gameManager.ResumeWorld();
    }

    private void HandleLevelEnded(bool succeeded)
    {
        StopAllCoroutines();
        respawning = false;
        SetFadeAlpha(0);
        if (fadeOverlay != null) fadeOverlay.raycastTarget = false;
        HideAllPrompts();
    }
    private IEnumerator FadeTo(float alpha)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.raycastTarget = true;
        float start = fadeOverlay.color.a;
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            SetFadeAlpha(Mathf.Lerp(start, alpha, t / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(alpha);
        fadeOverlay.raycastTarget = alpha > .01f;
    }
    private void SetFadeAlpha(float alpha)
    {
        if (fadeOverlay == null) return;
        Color c = fadeOverlay.color; c.a = alpha; fadeOverlay.color = c;
    }
    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.LevelStartedEvent -= NotifyLevelStarted;
            gameManager.RespawnRequestedEvent -= HandleRespawnRequested;
            gameManager.LevelEndedEvent -= HandleLevelEnded;
            gameManager.LampLitEvent -= HandleLampLit;
        }
        if (player != null)
        {
            player.MovementPerformed -= HandleOpeningMovement;
            player.JumpPerformed -= HandleJump;
        }
        if (guidedPuddles != null)
            foreach (PuddleHazard puddle in guidedPuddles) puddle.PlayerEntered -= HandlePuddleContact;
    }

    private PuddleHazard[] BuildGuidedPuddles()
    {
        var puddles = new List<PuddleHazard> { tutorialPuddle };
        if (tutorialPracticePuddles != null)
            foreach (PuddleHazard puddle in tutorialPracticePuddles)
                if (puddle != null && !puddles.Contains(puddle)) puddles.Add(puddle);
        puddles.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        if (puddles.Count > 2) puddles.RemoveRange(2, puddles.Count - 2);
        return puddles.ToArray();
    }

    private TutorialKeyPrompt CreatePrompt(Transform parent)
    {
        if (promptPrefab == null || parent == null) return null;
        GameObject instance = Instantiate(promptPrefab, parent, false);
        TutorialKeyPrompt result = instance.GetComponent<TutorialKeyPrompt>();
        if (result != null) return result;
        Debug.LogError("Tutorial key-prompt prefab has no TutorialKeyPrompt component.", instance);
        Destroy(instance);
        return null;
    }

    private void HandleOpeningMovement()
    {
        if (state != TutorialState.Opening) return;
        PrepareDarknessLesson();
        gameManager.BeginLevel();
        StartCoroutine(RunOpeningDarknessChase(openingDarknessDelay));
    }

    private void PrepareDarknessLesson()
    {
        latestCheckpoint = CaptureCheckpoint();
        latestCheckpoint.darknessPosition = darkness.GetPositionWithFrontAt(
            player.transform.position.x - darknessRecoveryDistance);
        state = TutorialState.DarknessCapture;
        prompt?.Hide();
        secondJumpPrompt?.Hide();
        darkness.SetTutorialProtection(false);
    }

    private void UpdateDarknessRecoveryCheckpoint()
    {
        Vector3 capturedPlayerPosition = player.transform.position;
        latestCheckpoint.playerPosition = capturedPlayerPosition;
        Vector3 recoveryRoutePosition = tutorialRoute.position;
        recoveryRoutePosition.x += capturedPlayerPosition.x + recoveryLampHorizontalOffset - firstLamp.transform.position.x;
        latestCheckpoint.routePosition = recoveryRoutePosition;
        latestCheckpoint.darknessPosition = darkness.GetPositionWithFrontAt(
            capturedPlayerPosition.x - darknessRecoveryDistance);
    }

    private IEnumerator RunOpeningDarknessChase(float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (respawning || state != TutorialState.DarknessCapture) yield break;
        yield return darkness.CaptureTutorialPlayer(player.transform, darknessCaptureDuration);
    }
}
