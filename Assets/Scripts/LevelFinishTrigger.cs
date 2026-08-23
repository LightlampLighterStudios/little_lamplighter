using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class LevelFinishTrigger : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private bool triggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void Configure(GameManager manager)
    {
        gameManager = manager;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        triggered = true;
        gameManager?.CompleteLevel();
    }
}
