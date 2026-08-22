using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// GameManager is the brain of the level.
// It keeps the clock, counts lamps, stores Sparks,
// and decides when the level ends.
public sealed class GameManager : MonoBehaviour
{
    [Serializable]
    public sealed class ProgressSnapshot
    {
        public float activeTime;
        public int sparks;
        public int lives;
        public List<string> uniqueLampIds = new List<string>();
    }

    // Singleton access used by LampController and other gameplay scripts.
    public static GameManager instance;

    [Header("Level")]
    // The WorldScroller is stopped until BeginLevel is called.
    [SerializeField, Min(1)] private int totalLamps = 6;
    [SerializeField, Min(1f)] private float nightLength = 90f;
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField] private DarknessController darkness;
    [SerializeField] private GameHudController hud;
    [SerializeField] private ResultsPanel resultsPanel;
    [SerializeField] private bool canLoseFinalLife;
    [SerializeField, Min(1)] private int silverLampRequirement = 4;

    [Header("State")]
    [SerializeField, Min(0)] private int sparks;
    [SerializeField, Range(1, 3)] private int lives = 3;

    private readonly HashSet<string> uniqueLampIds = new HashSet<string>();
    // Time spent in the active level. Pauses do not advance this value.
    private float activeTime;
    private bool levelStarted;
    private bool levelComplete;
    private bool levelFailed;
    private Camera cam;

    public event Action<LampController, bool> LampLitEvent;
    public event Action<int> SparksChanged;
    public event Action<int> LivesChanged;
    public event Action LevelStartedEvent;
    public event Action RespawnRequestedEvent;
    public event Action<bool> LevelEndedEvent;

    public int Sparks => sparks;
    public int Lives => lives;
    public int UniqueLampsLit => uniqueLampIds.Count;
    public int TotalLamps => totalLamps;
    public float ActiveTime => activeTime;
    public float NightLength => nightLength;
    // Normalized progress from 0 at the opening to 1 at the 90-second cap.
    public float NightProgress => Mathf.Clamp01(activeTime / Mathf.Max(0.01f, nightLength));
    public bool IsLevelStarted => levelStarted;
    public bool IsLevelComplete => levelComplete;
    public bool IsLevelFailed => levelFailed;
    public bool IsLevelEnded => levelComplete || levelFailed;
    public bool IsClockRunning =>
        levelStarted &&
        !IsLevelEnded &&
        worldScroller != null &&
        worldScroller.IsScrolling;

    private void Awake()
    {
        // Awake runs before other scripts, so the singleton is ready early.
        // Awake runs before the other gameplay scripts, so the singleton is
        // ready when lamps and hazards receive their first callbacks.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        // Find the camera used to display the level.
        cam = Camera.main;

        if (cam != null)
        {
            // The level begins with the warm sunset colour.
            cam.backgroundColor = GetSunsetColor(0f);
        }

        // The opening prompt owns the first movement; keep the route stopped
        // until the player responds.
        if (worldScroller == null)
        {
            // Use a scene WorldScroller automatically when one is not assigned.
            worldScroller = FindFirstObjectByType<WorldScroller>();
        }

        // The tutorial trigger will start the level later.
        worldScroller?.StopScrolling();
        hud?.SetSparks(sparks);
        hud?.SetLives(lives);
    }

    private void Update()
    {
        // Do not advance the clock before the level starts.
        // Pauses also leave this active-route clock unchanged.
        if (!IsClockRunning)
        {
            return;
        }

        // The authored finish trigger ends the level. The clock is a route
        // measurement only, so a long or hesitant run cannot bypass the
        // final stretch and run-off sequence.
        activeTime = Mathf.Min(activeTime + Time.deltaTime, nightLength);

        // Keep the existing sunset-to-night presentation without allowing the
        // clock to bypass the authored finish trigger.
        if (cam != null)
        {
            cam.backgroundColor = GetSunsetColor(NightProgress);
        }
    }

    // Returns the sky color for a given time progress (0 to 1).
    // Interpolates smoothly through the original sunset stages.
    private Color GetSunsetColor(float t)
    {
        // Define the gradient stops for a realistic sunset with pastel tones
        // for that warm story book effect.
        Color[] sunsetColors = new Color[]
        {
            new Color(0.714f, 0.890f, 1.0f),  // light blue (#b6e3ff)
            new Color(0.961f, 1.0f, 0.859f),  // pale yellow-green (#f5ffdb)
            new Color(0.996f, 0.937f, 0.698f), // yellow (#feefb2)
            new Color(0.996f, 0.831f, 0.698f), // peach-orange (#fed4b2)
            new Color(0.976f, 0.827f, 0.902f), // pink (#f9d3e6)
            new Color(0.392f, 0.337f, 0.471f), // purple (#645678)
            new Color(0.090f, 0.227f, 0.388f)  // dark blue (#173a63)
        };

        // Scale t to span across all color stops.
        float scaledT = t * (sunsetColors.Length - 1);
        int colorIndex = Mathf.FloorToInt(scaledT);
        colorIndex = Mathf.Clamp(colorIndex, 0, sunsetColors.Length - 2);

        // Get the interpolation value between the two current colors.
        float localT = scaledT - colorIndex;
        return Color.Lerp(sunsetColors[colorIndex], sunsetColors[colorIndex + 1], localT);
    }

    public void Configure(
        WorldScroller scroller,
        DarknessController darknessController,
        GameHudController gameHud,
        ResultsPanel levelResults,
        int lampCount = 6,
        float duration = 90f,
        bool allowFinalLifeLoss = false,
        int silverLamps = 4)
    {
        worldScroller = scroller;
        darkness = darknessController;
        hud = gameHud;
        resultsPanel = levelResults;
        totalLamps = Mathf.Max(1, lampCount);
        nightLength = Mathf.Max(1f, duration);
        canLoseFinalLife = allowFinalLifeLoss;
        silverLampRequirement = Mathf.Clamp(silverLamps, 1, totalLamps);
    }

    public void BeginLevel()
    {
        // Called by the tutorial trigger once the player reaches the street.
        // The opening movement response also reaches this same entry point.
        if (levelComplete || levelStarted)
        {
            // Ignore duplicate trigger calls or calls after game over.
            return;
        }

        levelStarted = true;
        worldScroller?.StartScrolling();
        LevelStartedEvent?.Invoke();
    }

    public void PauseWorld()
    {
        worldScroller?.StopScrolling();
    }

    public void ResumeWorld()
    {
        if (levelStarted && !IsLevelEnded)
        {
            worldScroller?.StartScrolling();
        }
    }

    public void RegisterLampLit(LampController lamp, bool isRelight)
    {
        // Called when a lamp finishes lighting.
        // The unique ID drives results while every valid first light or
        // mandatory relight awards a Spark.
        if (IsLevelEnded || lamp == null)
        {
            return;
        }

        uniqueLampIds.Add(lamp.LampId);
        sparks++;
        hud?.SetSparks(sparks);
        darkness?.PushBack();
        SparksChanged?.Invoke(sparks);
        LampLitEvent?.Invoke(lamp, isRelight);
    }

    // Retained for compatibility with older scene objects.
    public void LampLit()
    {
        if (IsLevelEnded)
        {
            return;
        }

        sparks++;
        hud?.SetSparks(sparks);
        SparksChanged?.Invoke(sparks);
    }

    public void UpdateLoadingBar(LampController lamp, float progress)
    {
        // Called every frame while the player holds Enter/E near a lamp.
        // The progress value runs from 0 to 1.
        // Progress is forwarded to the world-space loading bar.
        hud?.ShowLampProgress(lamp, progress);
    }

    public void HideLoadingBar()
    {
        hud?.HideLampProgress();
    }

    public void HandleDarknessContact()
    {
        // Called when the player loses a life.
        // The Level 1 tutorial protects the final life from darkness.
        if (!levelStarted || IsLevelEnded)
        {
            return;
        }

        if (lives > 1 || canLoseFinalLife)
        {
            lives--;
            hud?.SetLives(lives);
            LivesChanged?.Invoke(lives);
        }

        if (lives <= 0)
        {
            FailLevel();
            return;
        }

        RespawnRequestedEvent?.Invoke();
    }

    public ProgressSnapshot CaptureProgress()
    {
        return new ProgressSnapshot
        {
            activeTime = activeTime,
            sparks = sparks,
            lives = lives,
            uniqueLampIds = new List<string>(uniqueLampIds)
        };
    }

    public void RestoreProgress(ProgressSnapshot snapshot, bool restoreLives)
    {
        if (snapshot == null)
        {
            return;
        }

        activeTime = snapshot.activeTime;
        sparks = snapshot.sparks;

        if (restoreLives)
        {
            lives = snapshot.lives;
        }

        uniqueLampIds.Clear();

        foreach (string lampId in snapshot.uniqueLampIds)
        {
            uniqueLampIds.Add(lampId);
        }

        hud?.SetSparks(sparks);
        hud?.SetLives(lives);
        SparksChanged?.Invoke(sparks);
        LivesChanged?.Invoke(lives);
    }

    public string GetMedal()
    {
        if (uniqueLampIds.Count >= totalLamps && lives == 3)
        {
            return "GOLD";
        }

        if (uniqueLampIds.Count >= silverLampRequirement && lives >= 2)
        {
            return "SILVER";
        }

        return "BRONZE";
    }

    public void CompleteLevel()
    {
        // The authored finish trigger calls this after the player's run-off.
        if (IsLevelEnded)
        {
            return;
        }

        levelComplete = true;
        worldScroller?.StopScrolling();
        darkness?.SetActiveThreat(false);
        hud?.HideLampProgress();
        LevelEndedEvent?.Invoke(true);
        resultsPanel?.Show(this);
    }

    public void FailLevel()
    {
        if (IsLevelEnded)
        {
            return;
        }

        levelFailed = true;
        worldScroller?.StopScrolling();
        darkness?.SetActiveThreat(false);
        hud?.HideLampProgress();
        LevelEndedEvent?.Invoke(false);
        resultsPanel?.ShowFailure(this);
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
