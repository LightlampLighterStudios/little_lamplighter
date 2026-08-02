using UnityEngine;
using UnityEngine.UI;

// GameManager is the brain of the level.
// It keeps the clock, counts lamps, stores Sparks,
// and decides when the level ends.

public class GameManager : MonoBehaviour
{
    // Singleton access used by LampController and other gameplay scripts.
    public static GameManager instance;

    [Header("Lamps")]
    public int totalLamps = 1;
    private int lampsLit = 0;

    [Header("Sparks")]
    public int sparks = 0;
    public Text sparkText;

    [Header("Lives")]
    public int lives = 3;

    [Header("Sky Clock")]
    public float nightLength = 90f;

    public Color sunsetColor =
        new Color(0.95f, 0.55f, 0.25f);

    public Color nightColor =
        new Color(0.07f, 0.08f, 0.20f);

    [Header("Level Flow")]
    // The WorldScroller is stopped until BeginLevel is called.
    [SerializeField] private WorldScroller worldScroller;

    // Time spent in the active level.
    private float timer = 0f;

    private bool gameOver = false;
    private bool levelStarted = false;

    private Camera cam;

    // Normalized progress from 0 at sunset to 1 at full night.
    // SkyProgressController uses this value for moon and star fading.
    public float NightProgress =>
        Mathf.Clamp01(
            timer / Mathf.Max(0.01f, nightLength)
        );

    public bool IsLevelStarted => levelStarted;

    private void Awake()
    {
        // Awake runs before other scripts, so the singleton is ready early.
        instance = this;
    }

    private void Start()
    {
        // Find the camera used to display the level.
        cam = Camera.main;

        if (cam != null)
        {
            // The level begins with the warm sunset colour.
            cam.backgroundColor = sunsetColor;
        }

        // Use a scene WorldScroller automatically when one is not assigned.
        if (worldScroller == null)
        {
            worldScroller =
                FindFirstObjectByType<WorldScroller>();
        }

        // The tutorial trigger will start the level later.
        if (worldScroller != null)
        {
            worldScroller.StopScrolling();
        }
    }

    private void Update()
    {
        // Do not advance the clock before the level starts.
        if (gameOver || !levelStarted)
        {
            return;
        }

        // Advance the sky clock.
        timer += Time.deltaTime;

        // Move the camera colour gradually toward night.
        if (cam != null)
        {
            cam.backgroundColor =
                Color.Lerp(
                    sunsetColor,
                    nightColor,
                    NightProgress
                );
        }

        // End the level when the sky reaches full night.
        if (timer >= nightLength)
        {
            NightOver();
        }
    }

    // Called by the tutorial trigger once the player reaches the street.
    public void BeginLevel()
    {
        // Ignore duplicate trigger calls or calls after game over.
        if (gameOver || levelStarted)
        {
            return;
        }

        levelStarted = true;

        // Find the scroller if the Inspector reference is empty.
        if (worldScroller == null)
        {
            worldScroller =
                FindFirstObjectByType<WorldScroller>();
        }

        // Start all configured parallax layers.
        if (worldScroller != null)
        {
            worldScroller.StartScrolling();
        }
    }

    // Called when a lamp finishes lighting.
    public void LampLit()
    {
        lampsLit++;
        sparks++;

        Debug.Log(
            "Lamp lit! Sparks: " + sparks
        );

        if (sparkText != null)
        {
            sparkText.text = " " + sparks;
        }
    }

    // Called when the player loses a life.
    public void LoseLife()
    {
        lives--;

        Debug.Log(
            "Lost a life! Orbs left: " + lives
        );

        if (lives <= 0)
        {
            GameOver();
        }
    }

    // Reserved for the future lamp-lighting progress bar.
    public void UpdateLoadingBar(float progress)
    {
        // The loading bar will use this value later.
    }

    // Called when the 90-second night reaches its end.
    private void NightOver()
    {
        gameOver = true;

        if (worldScroller != null)
        {
            worldScroller.StopScrolling();
        }

        Debug.Log(
            "NIGHT FELL. The city sleeps. " +
            "You survived with " +
            sparks +
            " sparks!"
        );
    }

    // Called when all lives have been lost.
    private void GameOver()
    {
        gameOver = true;

        if (worldScroller != null)
        {
            worldScroller.StopScrolling();
        }

        Debug.Log(
            "GAME OVER. You ran out of orbs."
        );
    }
}