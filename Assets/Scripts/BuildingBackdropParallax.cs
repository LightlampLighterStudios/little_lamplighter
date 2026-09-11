using System;
using System.Collections.Generic;
using UnityEngine;

// Drives the serialized near and far building layers that live under each
// prefab's Distant Background hierarchy. The layers reuse the existing
// building sprites, but keep their own authored transforms and scroll rates.
[DisallowMultipleComponent]
public sealed class BuildingBackdropParallax : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldScroller worldScroller;
    [SerializeField] private Camera targetCamera;

    [Header("Near backdrop")]
    [SerializeField, Range(0.2f, 0.95f)] private float speedMultiplier = 0.48f;
    [SerializeField, Range(0.5f, 2.5f)] private float verticalOffset = 1.25f;
    [SerializeField, Range(0.65f, 0.98f)] private float scale = 0.84f;
    [SerializeField] private Color tint = new Color(0.62f, 0.70f, 0.84f, 1f);
    [SerializeField, Range(0f, 1f)] private float saturation = 0.30f;
    [SerializeField, Range(0f, 1f)] private float brightness = 0.78f;
    [SerializeField, Range(0f, 1f)] private float tintAmount = 0.45f;
    [SerializeField] private int sortingOrderOffset = -1;

    [Header("Far backdrop")]
    [SerializeField, Range(0.05f, 0.5f)] private float farSpeedMultiplier = 0.22f;
    [SerializeField, Range(0.5f, 3.5f)] private float farVerticalOffset = 2.35f;
    [SerializeField, Range(0.5f, 0.9f)] private float farScale = 0.72f;
    [SerializeField] private Color farTint = new Color(0.55f, 0.65f, 0.82f, 1f);
    [SerializeField, Range(0f, 1f)] private float farSaturation = 0.15f;
    [SerializeField, Range(0f, 1f)] private float farBrightness = 0.66f;
    [SerializeField, Range(0f, 1f)] private float farTintAmount = 0.62f;
    [SerializeField] private int farSortingOrderOffset = -2;

    private readonly List<BackdropGroup> groups = new List<BackdropGroup>();
    private readonly List<GameObject> runtimeFallbackObjects = new List<GameObject>();
    private Transform serializedLayerRoot;
    private bool subscribed;

    private sealed class BackdropGroup
    {
        public readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
        public readonly List<Vector3> initialPositions = new List<Vector3>();
        public Vector3[] checkpointPositions;
        public float loopWidth;
        public float speedMultiplier;
    }

    private void Awake()
    {
        if (worldScroller == null)
        {
            worldScroller = FindFirstObjectByType<WorldScroller>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        bool skipDuplicateController;
        serializedLayerRoot = FindSerializedLayerRoot(out skipDuplicateController);
        if (skipDuplicateController)
        {
            // A legacy scene root can remain after the prefab hierarchy has
            // been patched. Let the prefab-owned controller be authoritative.
            enabled = false;
            return;
        }

        BuildBackdropGroups();

        if (worldScroller != null)
        {
            worldScroller.WorldScrolled += OnWorldScrolled;
            worldScroller.WorldCheckpointCaptured += CaptureCheckpoint;
            worldScroller.WorldCheckpointRestored += RestoreCheckpoint;
            worldScroller.WorldReset += ResetBackdrop;
            subscribed = true;
        }
    }

    private void BuildBackdropGroups()
    {
        bool hasNearLayer = CreateBackdropGroup(
            "Building Backdrop Near",
            speedMultiplier,
            verticalOffset,
            scale,
            tint,
            saturation,
            brightness,
            tintAmount,
            sortingOrderOffset);
        bool hasFarLayer = CreateBackdropGroup(
            "Building Backdrop Far",
            farSpeedMultiplier,
            farVerticalOffset,
            farScale,
            farTint,
            farSaturation,
            farBrightness,
            farTintAmount,
            farSortingOrderOffset);

        // This keeps older scene files functional while the prefab patch is
        // imported. The normal path remains the serialized prefab hierarchy.
        if (!hasNearLayer || !hasFarLayer)
        {
            BuildRuntimeFallbackGroups(!hasNearLayer, !hasFarLayer);
        }
    }

    private bool CreateBackdropGroup(
        string layerName,
        float layerSpeedMultiplier,
        float layerVerticalOffset,
        float layerScale,
        Color layerTint,
        float layerSaturation,
        float layerBrightness,
        float layerTintAmount,
        int layerSortingOrderOffset)
    {
        Transform layerRoot = serializedLayerRoot ?? transform;
        Transform layer = layerRoot.Find(layerName);
        if (layer == null)
        {
            return false;
        }

        Vector3 layerPosition = layer.localPosition;
        layerPosition.y = layerVerticalOffset;
        layer.localPosition = layerPosition;
        layer.localScale = new Vector3(layerScale, layerScale, layer.localScale.z);

        SpriteRenderer[] renderers = layer.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length < 2)
        {
            return false;
        }

        BackdropGroup group = new BackdropGroup
        {
            speedMultiplier = layerSpeedMultiplier
        };
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float smallestWidth = float.PositiveInfinity;

        foreach (SpriteRenderer copy in renderers)
        {
            if (copy == null)
            {
                continue;
            }

            copy.color = Tone(
                copy.color,
                layerTint,
                layerSaturation,
                layerBrightness,
                layerTintAmount);
            copy.sortingOrder += layerSortingOrderOffset;

            group.copies.Add(copy);
            group.initialPositions.Add(copy.transform.position);
            minimumX = Mathf.Min(minimumX, copy.bounds.min.x);
            maximumX = Mathf.Max(maximumX, copy.bounds.max.x);
            smallestWidth = Mathf.Min(smallestWidth, copy.bounds.size.x);
        }

        if (group.copies.Count > 1 &&
            !float.IsInfinity(smallestWidth) &&
            !float.IsInfinity(minimumX) &&
            !float.IsInfinity(maximumX))
        {
            group.loopWidth = Mathf.Max(
                smallestWidth,
                maximumX - minimumX + smallestWidth);
            groups.Add(group);
        }

        return group.copies.Count > 1;
    }

    private Transform FindSerializedLayerRoot(out bool skipDuplicateController)
    {
        skipDuplicateController = false;

        if (HasBackdropLayers(transform))
        {
            return transform;
        }

        // Older scene files may still contain the previous scene-root
        // controller. Find the prefab-owned hierarchy so that controller can
        // continue working until the old scene object is removed locally.
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Transform candidate in candidates)
        {
            if (candidate == null || candidate == transform ||
                !HasBackdropLayers(candidate))
            {
                continue;
            }

            BuildingBackdropParallax existing =
                candidate.GetComponent<BuildingBackdropParallax>();
            if (existing != null && existing != this)
            {
                skipDuplicateController = true;
                return null;
            }

            return candidate;
        }

        return null;
    }

    private static bool HasBackdropLayers(Transform root)
    {
        return root != null &&
            root.Find("Building Backdrop Near") != null &&
            root.Find("Building Backdrop Far") != null;
    }

    private void BuildRuntimeFallbackGroups(bool includeNear, bool includeFar)
    {
        Dictionary<Transform, List<SpriteRenderer>> sourcesByLayer =
            new Dictionary<Transform, List<SpriteRenderer>>();
        SpriteRenderer[] renderers =
            FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == transform ||
                renderer.transform.IsChildOf(transform))
            {
                continue;
            }

            Transform layer = FindBuildingsLayer(renderer.transform);
            if (layer == null)
            {
                continue;
            }

            if (!sourcesByLayer.TryGetValue(layer, out List<SpriteRenderer> sources))
            {
                sources = new List<SpriteRenderer>();
                sourcesByLayer.Add(layer, sources);
            }

            sources.Add(renderer);
        }

        foreach (List<SpriteRenderer> sources in sourcesByLayer.Values)
        {
            if (sources.Count < 2)
            {
                continue;
            }

            if (includeNear)
            {
                CreateRuntimeFallbackGroup(
                    sources,
                    "Building Backdrop Near (Runtime Fallback)",
                    speedMultiplier,
                    verticalOffset,
                    scale,
                    tint,
                    saturation,
                    brightness,
                    tintAmount,
                    sortingOrderOffset);
            }

            if (includeFar)
            {
                CreateRuntimeFallbackGroup(
                    sources,
                    "Building Backdrop Far (Runtime Fallback)",
                    farSpeedMultiplier,
                    farVerticalOffset,
                    farScale,
                    farTint,
                    farSaturation,
                    farBrightness,
                    farTintAmount,
                    farSortingOrderOffset);
            }
        }
    }

    private void CreateRuntimeFallbackGroup(
        List<SpriteRenderer> sources,
        string layerName,
        float layerSpeedMultiplier,
        float layerVerticalOffset,
        float layerScale,
        Color layerTint,
        float layerSaturation,
        float layerBrightness,
        float layerTintAmount,
        int layerSortingOrderOffset)
    {
        GameObject layerObject = new GameObject(layerName);
        runtimeFallbackObjects.Add(layerObject);
        layerObject.transform.SetParent(transform, false);

        BackdropGroup group = new BackdropGroup
        {
            speedMultiplier = layerSpeedMultiplier
        };
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float smallestWidth = float.PositiveInfinity;

        foreach (SpriteRenderer source in sources)
        {
            GameObject copyObject = new GameObject(source.name + " (Backdrop Fallback)");
            runtimeFallbackObjects.Add(copyObject);
            copyObject.transform.SetParent(layerObject.transform, false);
            copyObject.transform.position =
                source.transform.position + Vector3.up * layerVerticalOffset;
            copyObject.transform.localRotation = source.transform.localRotation;
            copyObject.transform.localScale =
                source.transform.localScale * layerScale;

            SpriteRenderer copy = copyObject.AddComponent<SpriteRenderer>();
            copy.sprite = source.sprite;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder + layerSortingOrderOffset;
            copy.color = Tone(
                source.color,
                layerTint,
                layerSaturation,
                layerBrightness,
                layerTintAmount);

            group.copies.Add(copy);
            group.initialPositions.Add(copy.transform.position);
            minimumX = Mathf.Min(minimumX, copy.bounds.min.x);
            maximumX = Mathf.Max(maximumX, copy.bounds.max.x);
            smallestWidth = Mathf.Min(smallestWidth, copy.bounds.size.x);
        }

        if (group.copies.Count > 1 &&
            !float.IsInfinity(smallestWidth) &&
            !float.IsInfinity(minimumX) &&
            !float.IsInfinity(maximumX))
        {
            group.loopWidth = Mathf.Max(
                smallestWidth,
                maximumX - minimumX + smallestWidth);
            groups.Add(group);
        }
    }

    private static Transform FindBuildingsLayer(Transform current)
    {
        while (current != null)
        {
            if (current.name.IndexOf("BuildingsLayer",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private void OnWorldScrolled(float signedDistance)
    {
        foreach (BackdropGroup group in groups)
        {
            float movement = -signedDistance * group.speedMultiplier;
            if (Mathf.Approximately(movement, 0f))
            {
                continue;
            }

            foreach (SpriteRenderer copy in group.copies)
            {
                if (copy == null)
                {
                    continue;
                }

                Vector3 position = copy.transform.position;
                position.x += movement;
                copy.transform.position = position;
                Recycle(copy, group.loopWidth, movement);
            }
        }
    }

    private void Recycle(SpriteRenderer copy, float loopWidth, float movement)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null || loopWidth <= 0f)
        {
            return;
        }

        float halfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float cameraLeft = targetCamera.transform.position.x - halfWidth;
        float cameraRight = targetCamera.transform.position.x + halfWidth;

        if (movement < 0f)
        {
            while (copy.bounds.max.x < cameraLeft)
            {
                Vector3 position = copy.transform.position;
                position.x += loopWidth;
                copy.transform.position = position;
            }
        }
        else
        {
            while (copy.bounds.min.x > cameraRight)
            {
                Vector3 position = copy.transform.position;
                position.x -= loopWidth;
                copy.transform.position = position;
            }
        }
    }

    private void CaptureCheckpoint()
    {
        foreach (BackdropGroup group in groups)
        {
            group.checkpointPositions = new Vector3[group.copies.Count];
            for (int index = 0; index < group.copies.Count; index++)
            {
                if (group.copies[index] != null)
                {
                    group.checkpointPositions[index] =
                        group.copies[index].transform.position;
                }
            }
        }
    }

    private void RestoreCheckpoint()
    {
        foreach (BackdropGroup group in groups)
        {
            if (group.checkpointPositions == null)
            {
                continue;
            }

            int count = Mathf.Min(group.copies.Count,
                group.checkpointPositions.Length);
            for (int index = 0; index < count; index++)
            {
                if (group.copies[index] != null)
                {
                    group.copies[index].transform.position =
                        group.checkpointPositions[index];
                }
            }
        }
    }

    private void ResetBackdrop()
    {
        foreach (BackdropGroup group in groups)
        {
            int count = Mathf.Min(group.copies.Count,
                group.initialPositions.Count);
            for (int index = 0; index < count; index++)
            {
                if (group.copies[index] != null)
                {
                    group.copies[index].transform.position =
                        group.initialPositions[index];
                }
            }
        }
    }

    private static Color Tone(
        Color source,
        Color layerTint,
        float layerSaturation,
        float layerBrightness,
        float layerTintAmount)
    {
        Color.RGBToHSV(source, out float h, out float s, out float v);
        Color result = Color.HSVToRGB(h, s * layerSaturation,
            v * layerBrightness);
        result = Color.Lerp(result, layerTint, layerTintAmount);
        result.a = 1f;
        return result;
    }

    private void OnDestroy()
    {
        if (subscribed && worldScroller != null)
        {
            worldScroller.WorldScrolled -= OnWorldScrolled;
            worldScroller.WorldCheckpointCaptured -= CaptureCheckpoint;
            worldScroller.WorldCheckpointRestored -= RestoreCheckpoint;
            worldScroller.WorldReset -= ResetBackdrop;
        }

        foreach (GameObject fallbackObject in runtimeFallbackObjects)
        {
            if (fallbackObject != null)
            {
                Destroy(fallbackObject);
            }
        }
    }
}
