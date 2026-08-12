using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ScrollStartTrigger : MonoBehaviour
{
    // Optional reference to the level manager.
    // If empty, the script finds the scene singleton automatically.
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // Optional tutorial prompt to hide after movement is learned.
    [SerializeField] private GameObject tutorialPrompt;

    // Prevents the trigger from starting the level repeatedly.
    private bool hasTriggered;

    private void Awake()
    {
        // This collider should detect the player without blocking movement.
        Collider2D triggerCollider =
            GetComponent<Collider2D>();

        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore repeated entries and objects that are not the player.
        if (
            hasTriggered ||
            other.GetComponent<PlayerController>() == null
        )
        {
            return;
        }

        hasTriggered = true;

        // Hide the tutorial prompt if one is assigned.
        if (tutorialPrompt != null)
        {
            tutorialPrompt.SetActive(false);
        }

        // Find GameManager automatically if the Inspector field is empty.
        if (gameManager == null)
        {
            gameManager = GameManager.instance;
        }

        // Starts both the sky clock and world scrolling.
        if (gameManager != null)
        {
            gameManager.BeginLevel();
        }

        // The trigger is no longer needed after activation.
        gameObject.SetActive(false);
    }
}