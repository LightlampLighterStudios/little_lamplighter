using System.Collections.Generic;
using UnityEngine;

public enum WorldScrollMode
{
    Automatic,
    PlayerFollow
}

public sealed class WorldScroller : MonoBehaviour
{
    public sealed class CheckpointSnapshot
    {
        public Vector3[][] layerPositions;
        public Vector3[][] scatterLayerPositions;
        public Vector3[] finiteGroupPositions;
    }

    // Controls the shared movement speed of the world.
    // Individual layers multiply this value to create parallax depth.
    [Header("World Settings")]
    [SerializeField] private WorldScrollMode scrollMode = WorldScrollMode.Automatic;
    [SerializeField, Min(0f)] private float scrollSpeed = 2f;
    [SerializeField] private Transform followTarget;
    [SerializeField, Min(0f)] private float followDeadZoneHalfWidth = 3f;
    [SerializeField] private DarknessController playerFollowDarkness;

    // References used to initialize each scrolling layer.
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ScrollingLayer[] layers;

    // Scattered scenery preserves its authored spacing while recycling in
    // either direction across one shared world-width cycle.
    [SerializeField] private LoopingScatterLayer[] scatterLayers;

    // Finite groups move with the world but are not recycled.
    // These will later contain trees, lamps, puddles, and bushes.
    [SerializeField] private FiniteScrollingGroup[] finiteGroups;

    // These lists contain only references that passed validation.
    private readonly List<ScrollingLayer> activeLayers =
        new List<ScrollingLayer>();

    private readonly List<LoopingScatterLayer> activeScatterLayers =
        new List<LoopingScatterLayer>();

    private readonly List<FiniteScrollingGroup> activeFiniteGroups =
        new List<FiniteScrollingGroup>();

    // The world starts stopped so the tutorial can control when scrolling begins.
    private bool isScrolling;
    private Rigidbody2D followBody;
    private float followCenterX;

    public bool IsScrolling => isScrolling;
    public bool IsPlayerDriven => scrollMode == WorldScrollMode.PlayerFollow;

    public float CurrentScrollSpeed =>
        isScrolling && !IsPlayerDriven ? scrollSpeed : 0f;

    public void Configure(
        Camera cameraToUse,
        ScrollingLayer[] scrollingLayers,
        FiniteScrollingGroup[] scrollingGroups,
        float speed = 2f)
    {
        targetCamera = cameraToUse;
        layers = scrollingLayers;
        finiteGroups = scrollingGroups;
        scrollSpeed = Mathf.Max(0f, speed);
    }

    public void Configure(
        Camera cameraToUse,
        ScrollingLayer[] scrollingLayers,
        LoopingScatterLayer[] loopingScatterLayers,
        FiniteScrollingGroup[] scrollingGroups,
        float speed = 2f)
    {
        Configure(cameraToUse, scrollingLayers, scrollingGroups, speed);
        scatterLayers = loopingScatterLayers;
    }

    public void ConfigurePlayerFollow(
        Transform target,
        float deadZoneHalfWidth = 3f)
    {
        scrollMode = WorldScrollMode.PlayerFollow;
        followTarget = target;
        followDeadZoneHalfWidth = Mathf.Max(0f, deadZoneHalfWidth);

        if (followTarget != null)
        {
            followCenterX = followTarget.position.x;
            followBody = followTarget.GetComponent<Rigidbody2D>();
        }
    }

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

        if (IsPlayerDriven)
        {
            if (followTarget == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                followTarget = player != null ? player.transform : null;
            }

            if (followTarget == null)
            {
                Debug.LogError(
                    "WorldScroller requires a follow target in PlayerFollow mode.",
                    this);
                enabled = false;
                return;
            }

            followCenterX = followTarget.position.x;
            followBody = followTarget.GetComponent<Rigidbody2D>();
        }

        // Require at least one looping layer or finite group.
        if (
            (layers == null || layers.Length == 0) &&
            (scatterLayers == null || scatterLayers.Length == 0) &&
            (finiteGroups == null || finiteGroups.Length == 0)
        )
        {
            Debug.LogError(
                "WorldScroller requires at least one " +
                "scrolling layer, scatter layer, or finite group.",
                this
            );

            enabled = false;
            return;
        }


        HashSet<LoopingScatterLayer> uniqueScatterLayers =
            new HashSet<LoopingScatterLayer>();

        if (scatterLayers != null)
        {
            foreach (LoopingScatterLayer layer in scatterLayers)
            {
                if (layer == null)
                {
                    Debug.LogWarning(
                        "WorldScroller contains an empty scatter layer reference.",
                        this);
                    continue;
                }

                if (!uniqueScatterLayers.Add(layer))
                {
                    Debug.LogError(
                        $"WorldScroller contains duplicate scatter layer '{layer.name}'.",
                        this);
                    continue;
                }

                if (layer.Initialize(targetCamera))
                {
                    activeScatterLayers.Add(layer);
                }
                else
                {
                    Debug.LogError(
                        $"Failed to initialize scatter layer '{layer.name}'.",
                        layer);
                }
            }
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
            activeScatterLayers.Count == 0 &&
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

        if (IsPlayerDriven)
        {
            return;
        }

        ScrollWorld(scrollSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!isScrolling || !IsPlayerDriven || followTarget == null)
        {
            return;
        }

        float leftBoundary = followCenterX - followDeadZoneHalfWidth;
        float rightBoundary = followCenterX + followDeadZoneHalfWidth;
        // Read and write the same authoritative position source. A Rigidbody2D
        // can update on a different cadence from rendered Transforms, so mixing
        // the two can consume the same dead-zone overflow more than once.
        float currentX = followBody != null
            ? followBody.position.x
            : followTarget.position.x;
        float clampedX = Mathf.Clamp(currentX, leftBoundary, rightBoundary);
        float overflow = currentX - clampedX;

        if (Mathf.Approximately(overflow, 0f))
        {
            return;
        }

        if (followBody != null)
        {
            followBody.position = new Vector2(clampedX, followBody.position.y);
        }
        else
        {
            Vector3 position = followTarget.position;
            position.x = clampedX;
            followTarget.position = position;
        }

        ScrollWorld(overflow);
    }

    private void ScrollWorld(float signedDistance)
    {
        if (Mathf.Approximately(signedDistance, 0f))
        {
            return;
        }

        // Send the same world distance to every looping layer.
        // Each layer applies its own speed multiplier.
        foreach (ScrollingLayer layer in activeLayers)
        {
            layer.Scroll(signedDistance);
        }

        foreach (LoopingScatterLayer layer in activeScatterLayers)
        {
            layer.Scroll(signedDistance);
        }

        // Send the same world distance to every finite group.
        // Finite groups move away and are not recycled.
        foreach (FiniteScrollingGroup group in activeFiniteGroups)
        {
            group.Scroll(signedDistance);
        }

        // Darkness has its own forward advance, but it still occupies a place
        // in the level. Move it with player-driven route travel so the player
        // can create distance by moving forward and lose distance by returning.
        if (IsPlayerDriven)
        {
            playerFollowDarkness?.ApplyWorldScroll(signedDistance);
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

    public CheckpointSnapshot CaptureCheckpoint()
    {
        CheckpointSnapshot snapshot = new CheckpointSnapshot
        {
            layerPositions = new Vector3[activeLayers.Count][],
            scatterLayerPositions = new Vector3[activeScatterLayers.Count][],
            finiteGroupPositions = new Vector3[activeFiniteGroups.Count]
        };

        for (int index = 0; index < activeLayers.Count; index++)
        {
            snapshot.layerPositions[index] =
                activeLayers[index].CaptureCheckpointPositions();
        }

        for (int index = 0; index < activeScatterLayers.Count; index++)
        {
            snapshot.scatterLayerPositions[index] =
                activeScatterLayers[index].CaptureCheckpointPositions();
        }

        for (int index = 0; index < activeFiniteGroups.Count; index++)
        {
            snapshot.finiteGroupPositions[index] =
                activeFiniteGroups[index].CaptureCheckpointPosition();
        }

        return snapshot;
    }

    public void RestoreCheckpoint(CheckpointSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        StopScrolling();

        int layerCount = Mathf.Min(
            activeLayers.Count,
            snapshot.layerPositions == null ? 0 : snapshot.layerPositions.Length);

        for (int index = 0; index < layerCount; index++)
        {
            activeLayers[index].RestoreCheckpointPositions(
                snapshot.layerPositions[index]);
        }

        int scatterCount = Mathf.Min(
            activeScatterLayers.Count,
            snapshot.scatterLayerPositions == null
                ? 0
                : snapshot.scatterLayerPositions.Length);

        for (int index = 0; index < scatterCount; index++)
        {
            activeScatterLayers[index].RestoreCheckpointPositions(
                snapshot.scatterLayerPositions[index]);
        }

        int groupCount = Mathf.Min(
            activeFiniteGroups.Count,
            snapshot.finiteGroupPositions == null
                ? 0
                : snapshot.finiteGroupPositions.Length);

        for (int index = 0; index < groupCount; index++)
        {
            activeFiniteGroups[index].RestoreCheckpointPosition(
                snapshot.finiteGroupPositions[index]);
        }
    }

    // Returns every registered layer to its starting arrangement.
    public void ResetWorld()
    {
        StopScrolling();

        foreach (ScrollingLayer layer in activeLayers)
        {
            layer.ResetLayer();
        }

        foreach (LoopingScatterLayer layer in activeScatterLayers)
        {
            layer.ResetLayer();
        }

        foreach (FiniteScrollingGroup group in activeFiniteGroups)
        {
            group.ResetGroup();
        }
    }
}
