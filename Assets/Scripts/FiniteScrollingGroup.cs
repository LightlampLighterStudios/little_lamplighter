using UnityEngine;

public sealed class FiniteScrollingGroup : MonoBehaviour
{
    // Controls this group's movement relative to the shared world speed.
    // A value of 1 moves with the gameplay world.
    // A value of 0.8 creates a slower midground layer.
    [Header("Movement")]
    [SerializeField, Min(0f)] private float speedMultiplier = 1f;

    // Position saved when the group is initialized.
    // This allows the group to be reset for a restart.
    private Vector3 initialPosition;

    // Prevents movement before WorldScroller has initialized the group.
    private bool isInitialized;

    public float SpeedMultiplier => speedMultiplier;

    public void Initialize()
    {
        // Store the group's original world position.
        initialPosition = transform.position;
        isInitialized = true;
    }

    public void Scroll(float signedWorldDistance)
    {
        // Ignore movement if initialization failed.
        if (!isInitialized)
        {
            return;
        }

        // Positive distance is forward travel and moves the group left.
        // Negative distance is backtracking and moves the group right.
        transform.position +=
            Vector3.left *
            signedWorldDistance *
            speedMultiplier;
    }

    public void ResetGroup()
    {
        // Initialize automatically if this method is called early.
        if (!isInitialized)
        {
            Initialize();
        }

        // Restore the position from before scrolling began.
        transform.position = initialPosition;
    }

    public Vector3 CaptureCheckpointPosition()
    {
        return transform.position;
    }

    public void RestoreCheckpointPosition(Vector3 position)
    {
        transform.position = position;
    }
}
