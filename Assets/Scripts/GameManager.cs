
using UnityEngine;
using UnityEngine.UI;

//  GameManager is the "brain" of the level.
// It keeps the clock (the darkening sky), counts lamps,
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
    public Color sunsetColor = new Color(0.95f, 0.55f, 0.25f); // warm start
    public Color nightColor  = new Color(0.07f, 0.08f, 0.20f); // navy end
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
            cam.backgroundColor = sunsetColor;  // begin at sunset
        }
    }
    void Update()
    {
        if (gameOver) return;   // stop once the round is decided
        // advance the clock
        timer += Time.deltaTime;
        // how far through the night: 0 at start, 1 at full dark
        float t = timer / nightLength;
        // darken the sky from sunset toward night
        if (cam != null)
        {
            cam.backgroundColor = Color.Lerp(sunsetColor, nightColor, t);
        }
        // night is over: you survived to full dark, the city sleeps, shift done
        if (timer >= nightLength)
        {
            NightOver();
        }
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