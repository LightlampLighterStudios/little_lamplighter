using System.Collections.Generic;
using UnityEngine;

public sealed class WorldScroller : MonoBehaviour
{
    // Controls the shared movement speed of the world.
    [Header("World Settings")]
    [SerializeField, Min(0f)] private float scrollSpeed = 2f;

    // References used to initialize each scrolling layer.
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ScrollingLayer[] layers;

    private readonly List<ScrollingLayer> activeLayers =
        new List<ScrollingLayer>();

    private void Start()
    {
        // Use the scene's main camera when none is assigned.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Stop when the world has no usable camera.
        if (targetCamera == null)
        {
            Debug.LogError(
                "WorldScroller requires a camera.",
                this
            );

            enabled = false;
            return;
        }

        // Stop when no scrolling layers are configured.
        if (layers == null || layers.Length == 0)
        {
            Debug.LogError(
                "WorldScroller requires at least one layer.",
                this
            );

            enabled = false;
            return;
        }

        // Track references so layers cannot be registered twice.
        HashSet<ScrollingLayer> uniqueLayers =
            new HashSet<ScrollingLayer>();

        foreach (ScrollingLayer layer in layers)
        {
            // Ignore empty Inspector entries with a clear warning.
            if (layer == null)
            {
                Debug.LogWarning(
                    "WorldScroller contains an empty layer reference.",
                    this
                );

                continue;
            }

            // Prevent a layer from moving more than once per frame.
            if (!uniqueLayers.Add(layer))
            {
                Debug.LogError(
                    $"WorldScroller contains duplicate layer '{layer.name}'.",
                    this
                );

                continue;
            }

            // Register only layers that initialize successfully.
            if (layer.Initialize(targetCamera))
            {
                activeLayers.Add(layer);
            }
            else
            {
                Debug.LogError(
                    $"Failed to initialize scrolling layer '{layer.name}'.",
                    layer
                );
            }
        }

        // Stop when every assigned layer failed validation.
        if (activeLayers.Count == 0)
        {
            Debug.LogError(
                "WorldScroller has no valid scrolling layers.",
                this
            );

            enabled = false;
        }
    }

    private void Update()
    {
        // Calculate the shared movement distance for this frame.
        float distance =
            scrollSpeed * Time.deltaTime;

        // Send the same world distance to every valid layer.
        foreach (ScrollingLayer layer in activeLayers)
        {
            layer.Scroll(distance);
        }
    }
}