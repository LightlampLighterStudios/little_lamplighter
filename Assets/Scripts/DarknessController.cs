using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DarknessController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameManager gameManager;
    [SerializeField, Min(0f)] private float advanceSpeed = 0.35f;
    [SerializeField, Min(0f)] private float pushBackDistance = 5f;
    [SerializeField] private float resetX = -31f;

    private bool activeThreat;
    private bool contactLocked;

    public bool IsActiveThreat => activeThreat;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (
            !activeThreat ||
            gameManager == null ||
            !gameManager.IsClockRunning)
        {
            return;
        }

        transform.position += Vector3.right * advanceSpeed * Time.deltaTime;
    }

    public void Configure(Transform targetPlayer, GameManager manager)
    {
        player = targetPlayer;
        gameManager = manager;
        resetX = transform.position.x;
    }

    public void SetActiveThreat(bool active)
    {
        activeThreat = active;
        gameObject.SetActive(active);
    }

    public void PushBack()
    {
        if (!activeThreat)
        {
            activeThreat = true;
            gameObject.SetActive(true);
        }

        Vector3 position = transform.position;
        position.x = Mathf.Max(resetX, position.x - pushBackDistance);
        transform.position = position;
    }

    public void ResetThreat()
    {
        Vector3 position = transform.position;
        position.x = resetX;
        transform.position = position;
        contactLocked = false;
    }

    public void RestorePosition(Vector3 position)
    {
        transform.position = position;
        contactLocked = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (contactLocked || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        contactLocked = true;
        gameManager?.HandleDarknessContact();
    }
}
