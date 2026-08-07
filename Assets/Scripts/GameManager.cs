
using UnityEngine;
using UnityEngine.UI;

//
// our gamemanager - It keeps the clock (the darkening sky), counts lamps,
// holds the Sparks which will be the currency and decides win or lose.


public class GameManager : MonoBehaviour

{
    // SINGLETON: lets other scripts reach this one by typing
    // GameManager.instance (that is what LampController uses).

    public static GameManager instance;

    [Header("Lamps")]
    public int totalLamps = 1;   // how many lamps are in the level (for reference)
    private int lampsLit = 0;    // how many are lit so far

    [Header("Sparks (currency)")]
    public int sparks = 0;       // earned 1 per lamp, saved for later
    public Text sparkText;   //  SparkCounter will wdit later in inspector

    [Header("Lives (orbs on the taper)")]
    public int lives = 3;        // lose one to the dog etc; lose all three = game over

    //will be adding more colors later to give a better sunset vibe 
    //colors will be going from yellow-orange-red-pink-purple-navy (pastelcolors)

    [Header("The Sky Clock")]
    public float nightLength = 90f;   // seconds from sunset to full dark
    private float timer = 0f;
    private bool gameOver = false;
    private Camera cam;
    // Awake runs before everything, so instance is ready
    // before any lamp tries to use it.

    void Awake()
    {
        instance = this;
    }


    void Start()
    {
        cam = Camera.main;            // finds the object tagged as that of MainCamera
        if (cam != null)
        {
            cam.backgroundColor = GetSunsetColor(0f);  // begin at sunset
        }
    }

    void Update()
    {
        if (gameOver) return;   // stop once the round is decided
        // advance the clock
        timer += Time.deltaTime;
        // how far through the night: 0 at start, 1 at full dark
        float t = timer / nightLength;
        // darken the sky from sunset toward night through multiple color stages
        if (cam != null)
        {
            cam.backgroundColor = GetSunsetColor(t);
        }
        // night is over: you survived to full dark, the city sleeps, shift done

        if (timer >= nightLength)
        {
            NightOver();
        }
    }
    // Returns the sky color for a given time progress (0 to 1).
    // Interpolates smoothly through 7 beautiful sunset stages.


    private Color GetSunsetColor(float t)
    {
        // Define the gradient stops for a realistic sunset with pastel tones for that warm story book effect
        Color[] sunsetColors = new Color[]
        {
            
            new Color(0.714f, 0.890f, 1.0f),  // light blue (#b6e3ff)
            new Color(0.961f, 1.0f, 0.859f),  // pale yellow-green (#f5ffdb)
            new Color(0.996f, 0.937f, 0.698f),  // yellow (#feefb2)
            new Color(0.996f, 0.831f, 0.698f),  // peach-orange (#fed4b2)
            new Color(0.976f, 0.827f, 0.902f),  // pink (#f9d3e6)
            new Color(0.392f, 0.337f, 0.471f),  // purple (#645678)
            new Color(0.090f, 0.227f, 0.388f)   // dark blue (#173a63)
        };
        
        // Scale t to span across all color stops
        float scaledT = t * (sunsetColors.Length - 1);
        
        // for find which two colors we're currently between
        int colorIndex = Mathf.FloorToInt(scaledT);
        colorIndex = Mathf.Clamp(colorIndex, 0, sunsetColors.Length - 2);
        
        // Get the interpolation value (0 to 1) between these two colors
        float localT = scaledT - colorIndex;
        
        // Smoothly blend from one color to the next
        return Color.Lerp(sunsetColors[colorIndex], sunsetColors[colorIndex + 1], localT);
    }

    // Called by a lamp when it finishes lighting.

    public void LampLit()
    {
        lampsLit++;
        sparks++;
        Debug.Log("Lamp lit!  Sparks: " + sparks);
        if (sparkText != null)
        {
            //later add picture I drew of spark instead of plain number
            sparkText.text = " " + sparks;
        }
        // NOTE: missing a lamp no longer loses the game.
        // Lamps are just score now. You only lose by running out of lives.
    }
    // Called by the dog (or other obstacles) later when the player fails.


    public void LoseLife()
    {
        lives--;
        Debug.Log("Lost a life! Orbs left: " + lives);
        if (lives <= 0)
        {
            GameOver();
        }
    }
    // will be called every frame while the player holds E.
    // progress goes 0 to 1. No on-screen bar yet so this is empty for now. 
    // The real loading bar plugs in here later.


    public void UpdateLoadingBar(float progress)
    {
        // intentionally empty for the prototype
    }
    // Reached full dark with at least one orb left = you survived the night.


    void NightOver()
    {
        gameOver = true;
        Debug.Log("NIGHT FELL. The city sleeps. You survived with " + sparks + " sparks!");
    }
    // Ran out of orbs = game over early.



    void GameOver()
    {
        gameOver = true;
        Debug.Log("GAME OVER. You ran out of orbs.");
    }
}