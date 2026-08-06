using System.Collections.Generic;
using UnityEngine;

public sealed class WorldScroller : MonoBehaviour
{
    // Controls the shared movement speed of the world.
    // Individual layers multiply this value to create parallax depth.
    [Header("World Settings")]
    [SerializeField, Min(0f)] private float scrollSpeed = 2f;

    // References used to initialize each scrolling layer.
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ScrollingLayer[] layers;

    // Finite groups move with the world but are not recycled.
    // These will later contain trees, lamps, puddles, and bushes.
    [SerializeField] private FiniteScrollingGroup[] finiteGroups;

    // These lists contain only references that passed validation.
    private readonly List<ScrollingLayer> activeLayers =
        new List<ScrollingLayer>();

    private readonly List<FiniteScrollingGroup> activeFiniteGroups =
        new List<FiniteScrollingGroup>();

    // The world starts stopped so the tutorial can control when scrolling begins.
    private bool isScrolling;

    public bool IsScrolling => isScrolling;

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

        // Require at least one looping layer or finite group.
        if (
            (layers == null || layers.Length == 0) &&
            (finiteGroups == null || finiteGroups.Length == 0)
        )
        {
            Debug.LogError(
                "WorldScroller requires at least one " +
                "scrolling layer or finite group.",
                this
            );

            enabled = false;
            return;
        }

        // Track references so a layer cannot be registered twice.
        HashSet<ScrollingLayer> uniqueLayers =
            new HashSet<ScrollingLayer>();

        if (layers != null)
        {
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
        }

        // Track finite groups separately from looping layers.
        HashSet<FiniteScrollingGroup> uniqueFiniteGroups =
            new HashSet<FiniteScrollingGroup>();

        if (finiteGroups != null)
        {
            foreach (FiniteScrollingGroup group in finiteGroups)
            {
                // Ignore empty Inspector entries with a clear warning.
                if (group == null)
                {
                    Debug.LogWarning(
                        "WorldScroller contains an empty finite group reference.",
                        this
                    );

                    continue;
                }

                // Prevent one group from moving more than once per frame.
                if (!uniqueFiniteGroups.Add(group))
                {
                    Debug.LogError(
                        $"WorldScroller contains duplicate finite group '{group.name}'.",
                        this
                    );

                    continue;
                }

                // Finite groups store their starting position here.
                group.Initialize();
                activeFiniteGroups.Add(group);
            }
        }

        // Stop when every assigned reference failed validation.
        if (
            activeLayers.Count == 0 &&
            activeFiniteGroups.Count == 0
        )
        {
            Debug.LogError(
                "WorldScroller has no valid scrolling content.",
                this
            );

            enabled = false;
            return;
        }

        // The tutorial trigger will call StartScrolling later.
        StopScrolling();
    }

    private void Update()
    {
        // The world remains stationary until gameplay begins.
        if (!isScrolling)
        {
            return;
        }

        // Calculate the shared movement distance for this frame.
        float distance =
            scrollSpeed * Time.deltaTime;

        // Send the same world distance to every looping layer.
        // Each layer applies its own speed multiplier.
        foreach (ScrollingLayer layer in activeLayers)
        {
            layer.Scroll(distance);
        }

        // Send the same world distance to every finite group.
        // Finite groups move away and are not recycled.
        foreach (FiniteScrollingGroup group in activeFiniteGroups)
        {
            group.Scroll(distance);
        }
    }

    // Starts the world and the finite gameplay content.
    public void StartScrolling()
    {
        isScrolling = true;
    }

    // Stops every scrolling layer without changing its current position.
    public void StopScrolling()
    {
        isScrolling = false;
    }

    // Returns every registered layer to its starting arrangement.
    public void ResetWorld()
    {
        StopScrolling();

        foreach (ScrollingLayer layer in activeLayers)
        {
            layer.ResetLayer();
        }

        foreach (FiniteScrollingGroup group in activeFiniteGroups)
        {
            group.ResetGroup();
        }
    }
}