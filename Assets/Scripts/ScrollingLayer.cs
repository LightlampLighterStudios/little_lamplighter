using System.Collections.Generic;
using UnityEngine;

public sealed class ScrollingLayer : MonoBehaviour
{
    // Tiles belonging to this scrolling layer.
    [Header("Tiles")]
    [SerializeField] private SpriteRenderer[] tiles;

    // Controls this layer's movement relative to the world speed.
    [Header("Layer Settings")]
    [SerializeField, Min(0f)] private float speedMultiplier = 1f;

    // Slightly overlaps neighbouring tiles to hide thin seams.
    [SerializeField, Range(0f, 5f)] private float seamOverlap = 0.05f;

    private Camera targetCamera;
    private bool isInitialized;
    private float smallestRecycleStep;

    public bool Initialize(Camera cameraToUse)
    {
        // Clear any previous initialization state.
        isInitialized = false;
        targetCamera = cameraToUse;

        // Reject invalid Inspector configuration.
        if (!HasValidSetup())
        {
            return false;
        }

        // Reject layers that cannot reliably cover the camera.
        if (!HasEnoughCoverage())
        {
            return false;
        }

        // Arrange the tiles into a continuous horizontal row.
        ArrangeTiles();

        // Store the smallest safe horizontal recycling distance.
        smallestRecycleStep =
            GetSmallestTileWidth() - seamOverlap;

        isInitialized = true;
        return true;
    }

    public void Scroll(float worldDistance)
    {
        // Ignore movement when initialization failed.
        if (!isInitialized)
        {
            return;
        }

        // Apply this layer's individual movement multiplier.
        float layerDistance =
            worldDistance * speedMultiplier;

        // Avoid unnecessary work when this layer is stationary.
        if (Mathf.Approximately(layerDistance, 0f))
        {
            return;
        }

        // Calculate the movement applied to every tile.
        Vector3 movement =
            Vector3.left * layerDistance;

        // Move all tiles by exactly the same distance.
        foreach (SpriteRenderer tile in tiles)
        {
            tile.transform.position += movement;
        }

        // Recycle repeatedly when a large frame movement expires several tiles.
        RecycleOffscreenTiles(layerDistance);
    }

    private bool HasValidSetup()
    {
        // The system requires an orthographic camera aligned with world X.
        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError(
                $"{name} requires an orthographic camera.",
                this
            );

            return false;
        }

        // Rotating the camera would invalidate the horizontal edge calculation.
        if (
            Quaternion.Angle(
                targetCamera.transform.rotation,
                Quaternion.identity
            ) > 0.01f
        )
        {
            Debug.LogError(
                $"{name} requires an unrotated camera.",
                this
            );

            return false;
        }

        // At least two tiles are required to create a loop.
        if (tiles == null || tiles.Length < 2)
        {
            Debug.LogError(
                $"{name} requires at least two tiles.",
                this
            );

            return false;
        }

        // Track references so duplicate assignments can be rejected.
        HashSet<SpriteRenderer> uniqueTiles =
            new HashSet<SpriteRenderer>();

        foreach (SpriteRenderer tile in tiles)
        {
            // Every array position must contain a tile.
            if (tile == null)
            {
                Debug.LogError(
                    $"{name} contains an empty tile reference.",
                    this
                );

                return false;
            }

            // The same tile must not appear more than once.
            if (!uniqueTiles.Add(tile))
            {
                Debug.LogError(
                    $"{name} contains the duplicate tile '{tile.name}'.",
                    this
                );

                return false;
            }

            // Rotated tiles do not provide a reliable horizontal width.
            if (
                Quaternion.Angle(
                    tile.transform.rotation,
                    Quaternion.identity
                ) > 0.01f
            )
            {
                Debug.LogError(
                    $"{tile.name} must not be rotated.",
                    tile
                );

                return false;
            }

            // Flipped or zero-width transforms are not supported.
            if (tile.transform.lossyScale.x <= 0f)
            {
                Debug.LogError(
                    $"{tile.name} requires a positive X scale.",
                    tile
                );

                return false;
            }

            // The overlap must remain smaller than every tile.
            if (tile.bounds.size.x <= seamOverlap)
            {
                Debug.LogError(
                    $"{tile.name} is not wider than the seam overlap.",
                    tile
                );

                return false;
            }
        }

        return true;
    }

    private bool HasEnoughCoverage()
    {
        // Calculate the camera's visible horizontal width.
        float cameraWidth =
            targetCamera.orthographicSize *
            targetCamera.aspect *
            2f;

        float combinedWidth = 0f;
        float widestTile = 0f;

        // Add the world-space width of every tile.
        foreach (SpriteRenderer tile in tiles)
        {
            float tileWidth = tile.bounds.size.x;

            combinedWidth += tileWidth;
            widestTile = Mathf.Max(widestTile, tileWidth);
        }

        // Remove the space consumed by intentional overlaps.
        float effectiveWidth =
            combinedWidth -
            seamOverlap * (tiles.Length - 1);

        // Keep one tile's width spare while another tile is recycled.
        float requiredWidth =
            cameraWidth + widestTile;

        if (effectiveWidth < requiredWidth)
        {
            Debug.LogError(
                $"{name} does not contain enough tile width. " +
                $"Available: {effectiveWidth:F2}, " +
                $"required: {requiredWidth:F2}.",
                this
            );

            return false;
        }

        return true;
    }

    private void ArrangeTiles()
    {
        // Start the row at the camera's left edge.
        float nextLeftEdge = GetCameraLeftEdge();

        // Place each tile immediately after the previous tile.
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

    private void RecycleOffscreenTiles(float distanceMoved)
    {
        // Read the current left edge of the camera.
        float cameraLeft = GetCameraLeftEdge();

        // Allow enough passes to recover from unusually large movement.
        int expectedWraps =
            Mathf.CeilToInt(
                distanceMoved / smallestRecycleStep
            ) + 1;

        int safetyLimit =
            Mathf.Max(
                tiles.Length * 4,
                expectedWraps * tiles.Length * 2
            );

        int recycleCount = 0;

        // Continue until the leftmost tile reaches the visible row again.
        while (true)
        {
            SpriteRenderer leftmostTile =
                FindLeftmostTile();

            if (leftmostTile.bounds.max.x >= cameraLeft)
            {
                break;
            }

            MoveToEnd(leftmostTile);
            recycleCount++;

            // Stop safely if configuration changes create an invalid loop.
            if (recycleCount > safetyLimit)
            {
                Debug.LogError(
                    $"{name} exceeded its recycling safety limit.",
                    this
                );

                isInitialized = false;
                return;
            }
        }
    }

    private void MoveToEnd(SpriteRenderer tileToMove)
    {
        // Find the current final tile in the row.
        SpriteRenderer rightmostTile =
            FindRightmostTile(tileToMove);

        // Place the recycled tile immediately after it.
        float newLeftEdge =
            rightmostTile.bounds.max.x - seamOverlap;

        SetX(
            tileToMove.transform,
            newLeftEdge + tileToMove.bounds.extents.x
        );
    }

    private SpriteRenderer FindLeftmostTile()
    {
        // Begin with the first configured tile.
        SpriteRenderer leftmostTile = tiles[0];

        // Find the tile whose left edge is furthest left.
        foreach (SpriteRenderer tile in tiles)
        {
            if (tile.bounds.min.x < leftmostTile.bounds.min.x)
            {
                leftmostTile = tile;
            }
        }

        return leftmostTile;
    }

    private SpriteRenderer FindRightmostTile(
        SpriteRenderer excludedTile
    )
    {
        SpriteRenderer rightmostTile = null;

        // Find the furthest-right tile except the one being recycled.
        foreach (SpriteRenderer tile in tiles)
        {
            if (tile == excludedTile)
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

        return rightmostTile;
    }

    private float GetSmallestTileWidth()
    {
        // Begin with the width of the first tile.
        float smallestWidth = tiles[0].bounds.size.x;

        // Find the smallest configured tile width.
        foreach (SpriteRenderer tile in tiles)
        {
            smallestWidth =
                Mathf.Min(
                    smallestWidth,
                    tile.bounds.size.x
                );
        }

        return smallestWidth;
    }

    private float GetCameraLeftEdge()
    {
        // Calculate half of the camera's visible horizontal width.
        float cameraHalfWidth =
            targetCamera.orthographicSize *
            targetCamera.aspect;

        return targetCamera.transform.position.x -
            cameraHalfWidth;
    }

    private static void SetX(
        Transform target,
        float xPosition
    )
    {
        // Preserve the vertical and depth coordinates.
        Vector3 position = target.position;

        position.x = xPosition;
        target.position = position;
    }
}