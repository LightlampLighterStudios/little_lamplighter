using UnityEngine;

public class BackgroundLoop : MonoBehaviour
{
    // Tiles that form the continuous scrolling layer.
    [Header("Scrolling Tiles")]
    [SerializeField] private SpriteRenderer[] tiles;

    // Controls the movement speed and tile overlap.
    [Header("Movement")]
    [SerializeField, Min(0f)] private float scrollSpeed = 2f;
    [SerializeField, Min(0f)] private float seamOverlap = 0.05f;

    // Camera used to determine when a tile leaves the screen.
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    private void Start()
    {
        // Use the main camera when one has not been assigned.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Stop the script when the required setup is missing.
        if (!HasValidSetup())
        {
            enabled = false;
            return;
        }

        // Arrange every tile in one continuous horizontal row.
        ArrangeTiles();
    }

    private void Update()
    {
        // Calculate how far the tiles move during this frame.
        Vector3 movement =
            Vector3.left * scrollSpeed * Time.deltaTime;

        // Move every tile at the same speed.
        foreach (SpriteRenderer tile in tiles)
        {
            tile.transform.position += movement;
        }

        // Recycle any tiles that have fully left the camera.
        RecycleOffscreenTiles();
    }

    private bool HasValidSetup()
    {
        // A working loop requires an orthographic camera.
        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError(
                "BackgroundLoop requires an orthographic camera.",
                this
            );

            return false;
        }

        // At least two tiles are required to create a loop.
        if (tiles == null || tiles.Length < 2)
        {
            Debug.LogError(
                "BackgroundLoop requires at least two tiles.",
                this
            );

            return false;
        }

        // Every array entry must contain a SpriteRenderer.
        foreach (SpriteRenderer tile in tiles)
        {
            if (tile == null)
            {
                Debug.LogError(
                    "BackgroundLoop contains an empty tile reference.",
                    this
                );

                return false;
            }
        }

        return true;
    }

    private void ArrangeTiles()
    {
        // Begin at the left edge of the camera view.
        float nextLeftEdge = GetCameraLeftEdge();

        // Place each tile directly after the previous tile.
        foreach (SpriteRenderer tile in tiles)
        {
            float halfWidth = tile.bounds.extents.x;

            SetX(
                tile.transform,
                nextLeftEdge + halfWidth
            );

            nextLeftEdge =
                tile.bounds.max.x - seamOverlap;
        }
    }

    private void RecycleOffscreenTiles()
    {
        // Read the current left edge of the camera.
        float cameraLeft = GetCameraLeftEdge();

        // Move each expired tile to the end of the row.
        foreach (SpriteRenderer tile in tiles)
        {
            if (tile.bounds.max.x < cameraLeft)
            {
                MoveToEnd(tile);
            }
        }
    }

    private void MoveToEnd(SpriteRenderer tileToMove)
    {
        // Find the tile currently furthest to the right.
        SpriteRenderer rightmostTile = null;

        foreach (SpriteRenderer tile in tiles)
        {
            if (tile == tileToMove)
            {
                continue;
            }

            if (
                rightmostTile == null ||
                tile.bounds.max.x > rightmostTile.bounds.max.x
            )
            {
                rightmostTile = tile;
            }
        }

        // Place the recycled tile directly after the rightmost tile.
        float newLeftEdge =
            rightmostTile.bounds.max.x - seamOverlap;

        SetX(
            tileToMove.transform,
            newLeftEdge + tileToMove.bounds.extents.x
        );
    }

    private float GetCameraLeftEdge()
    {
        // Calculate half of the camera's visible width.
        float cameraHalfWidth =
            targetCamera.orthographicSize * targetCamera.aspect;

        // Return the world position of the left screen edge.
        return targetCamera.transform.position.x - cameraHalfWidth;
    }

    private static void SetX(Transform target, float xPosition)
    {
        // Preserve the existing vertical and depth positions.
        Vector3 position = target.position;

        // Change only the horizontal position.
        position.x = xPosition;

        // Apply the updated position.
        target.position = position;
    }
}