using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable data classes representing a saved stamp layout.
/// A stamp records the arrangement of buildings and roads relative to an
/// origin point so the layout can be replayed anywhere on any island.
/// </summary>

// ──────────────────────────────────────────────
// Entry for a single building inside a stamp
// ──────────────────────────────────────────────
[System.Serializable]
public class StampBuildingEntry
{
    /// <summary>Namespaced building identifier from <see cref="BuildingData.Id"/> (e.g. "core:worker_resident").</summary>
    public string buildingIdentifier;

    /// <summary>Position relative to the stamp origin in world units.</summary>
    public Vector3 relativePosition;

    /// <summary>Y-axis rotation in degrees (0, 90, 180, 270).</summary>
    public float rotationY;

    /// <summary>Footprint size copied from <see cref="BuildingData.buildingSize"/>.</summary>
    public Vector3 buildingSize;
}

// ──────────────────────────────────────────────
// Entry for a single road cell inside a stamp
// ──────────────────────────────────────────────
[System.Serializable]
public class StampRoadEntry
{
    /// <summary>Grid-cell offset from the stamp origin cell.</summary>
    public Vector2Int relativeCell;
}

// ──────────────────────────────────────────────
// The stamp itself
// ──────────────────────────────────────────────
[System.Serializable]
public class StampData
{
    /// <summary>Unique identifier (GUID string).</summary>
    public string id;

    /// <summary>Player-assigned display name.</summary>
    public string stampName;

    /// <summary>Index into a shared icon atlas for the library thumbnail.</summary>
    public int iconIndex;

    /// <summary>Category or folder name for library organisation.</summary>
    public string category;

    /// <summary>Axis-aligned bounding footprint in grid cells.</summary>
    public Vector2Int footprint;

    /// <summary>All buildings captured in the stamp.</summary>
    public List<StampBuildingEntry> buildings = new List<StampBuildingEntry>();

    /// <summary>All road cells captured in the stamp.</summary>
    public List<StampRoadEntry> roads = new List<StampRoadEntry>();

    /// <summary>ISO-8601 creation timestamp.</summary>
    public string createdDate;

    // ── Convenience ──

    public int TotalEntries => buildings.Count + roads.Count;

    /// <summary>
    /// Creates a new empty stamp with a fresh GUID and the current timestamp.
    /// </summary>
    public static StampData CreateNew(string name, int iconIndex = 0, string category = "Default")
    {
        return new StampData
        {
            id = Guid.NewGuid().ToString(),
            stampName = name,
            iconIndex = iconIndex,
            category = category,
            createdDate = DateTime.UtcNow.ToString("o")
        };
    }

    /// <summary>
    /// Recalculates <see cref="footprint"/> from the current entries.
    /// Call after all entries have been added.
    /// </summary>
    public void RecalculateFootprint()
    {
        if (buildings.Count == 0 && roads.Count == 0)
        {
            footprint = Vector2Int.zero;
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var b in buildings)
        {
            float bMinX = b.relativePosition.x;
            float bMaxX = b.relativePosition.x + b.buildingSize.x;
            float bMinZ = b.relativePosition.z;
            float bMaxZ = b.relativePosition.z + b.buildingSize.z;

            if (bMinX < minX) minX = bMinX;
            if (bMaxX > maxX) maxX = bMaxX;
            if (bMinZ < minZ) minZ = bMinZ;
            if (bMaxZ > maxZ) maxZ = bMaxZ;
        }

        foreach (var r in roads)
        {
            if (r.relativeCell.x < minX) minX = r.relativeCell.x;
            if (r.relativeCell.x + 1 > maxX) maxX = r.relativeCell.x + 1;
            if (r.relativeCell.y < minZ) minZ = r.relativeCell.y;
            if (r.relativeCell.y + 1 > maxZ) maxZ = r.relativeCell.y + 1;
        }

        footprint = new Vector2Int(
            Mathf.CeilToInt(maxX - minX),
            Mathf.CeilToInt(maxZ - minZ)
        );
    }
}
