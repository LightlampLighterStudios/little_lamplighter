using UnityEngine;

public class LampController : MonoBehaviour
{
    public Sprite unlitSprite;
    public Sprite litSprite;

    // This will be the time of how long the player must hold E to light this lamp will later install loading bar sp player can see
    public float timeToLight = 2f;

    // set internal state variables
    public bool isLit = false;
    private bool playerNearby = false;
    private float holdTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;   // the lamp's lighting sound

    void Start()
    {
        // this will get the SpriteRenderer so we can swap sprites when lit
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = unlitSprite;

        // grab the AudioSource if there is one (won't crash if missing)
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // only run this code if the lamp isn't already lit
        if (!isLit)
        {
            // This will check players proximity so if player is nearby AND holding E
            if (playerNearby && Input.GetKey(KeyCode.E))
            {
                // adding to the hold timer
                holdTimer += Time.deltaTime;

                // Updating said loading bar through GameManager
                GameManager.instance.UpdateLoadingBar(holdTimer / timeToLight);

                // If held long enough then light the lamp
                if (holdTimer >= timeToLight)
                {
                    LightThisLamp();
                }
            }
            else
            {
                // must reset timer if player lets go or walks away (like later on when dog comes to steal tapperPole)
                holdTimer = 0f;
                GameManager.instance.UpdateLoadingBar(0f);
            }
        }
    }

    void LightThisLamp()
    {
        isLit = true;
        spriteRenderer.sprite = litSprite;

        // play the lighting sound, only if one is attached
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // will tell the GameManager a lamp was lit
        GameManager.instance.LampLit();
    }

    // Called when player enters the trigger zone
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.gameObject.name == "Player")
        {
            playerNearby = true;
        }
    }

    // Will be called when player leaves the trigger zone
    void OnTriggerExit2D(Collider2D col)
    {
        if (col.gameObject.name == "Player")
        {
            playerNearby = false;
            holdTimer = 0f;
        }
    }
}