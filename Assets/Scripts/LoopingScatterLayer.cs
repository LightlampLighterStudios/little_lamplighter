using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LoopingScatterLayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] items;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float loopWidth = 192f;

    private Camera targetCamera;
    private Vector3[] initialPositions;
    private bool isInitialized;

    public float SpeedMultiplier => speedMultiplier;
    public float LoopWidth => loopWidth;

    public bool Initialize(Camera cameraToUse)
    {
        targetCamera = cameraToUse;

        if (targetCamera == null || items == null || items.Length == 0 || loopWidth <= 0f)
        {
            return false;
        }

        HashSet<SpriteRenderer> uniqueItems = new HashSet<SpriteRenderer>();
        initialPositions = new Vector3[items.Length];

        for (int index = 0; index < items.Length; index++)
        {
            SpriteRenderer item = items[index];
            if (item == null || !uniqueItems.Add(item))
            {
                return false;
            }

            initialPositions[index] = item.transform.position;
        }

        isInitialized = true;
        return true;
    }

    public void Scroll(float signedDistance)
    {
        if (!isInitialized || Mathf.Approximately(signedDistance, 0f))
        {
            return;
        }

        float movement = -signedDistance * speedMultiplier;
        float cameraHalfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float cameraLeft = targetCamera.transform.position.x - cameraHalfWidth;
        float cameraRight = targetCamera.transform.position.x + cameraHalfWidth;

        foreach (SpriteRenderer item in items)
        {
            Vector3 position = item.transform.position;
            position.x += movement;
            item.transform.position = position;

            if (movement < 0f)
            {
                while (item.bounds.max.x < cameraLeft)
                {
                    position = item.transform.position;
                    position.x += loopWidth;
                    item.transform.position = position;
                }
            }
            else
            {
                while (item.bounds.min.x > cameraRight)
                {
                    position = item.transform.position;
                    position.x -= loopWidth;
                    item.transform.position = position;
                }
            }
        }
    }

    public Vector3[] CaptureCheckpointPositions()
    {
        Vector3[] positions = new Vector3[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            positions[index] = items[index].transform.position;
        }

        return positions;
    }

    public void RestoreCheckpointPositions(Vector3[] positions)
    {
        if (!isInitialized || positions == null)
        {
            return;
        }

        int count = Mathf.Min(items.Length, positions.Length);
        for (int index = 0; index < count; index++)
        {
            items[index].transform.position = positions[index];
        }
    }

    public void ResetLayer()
    {
        RestoreCheckpointPositions(initialPositions);
    }
}
