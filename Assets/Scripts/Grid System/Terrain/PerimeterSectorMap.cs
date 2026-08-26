using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the local terrain and gameplay profile permitted along an arc sector of an island's coastline.
/// </summary>
public enum PerimeterSectorType
{
    Beach,          // Low-slope sandy shoreline band
    RockyCliff,     // Medium/steep rocky face dropping to water
    MountainCoast,  // Mountain range plunging directly into the sea as cliff rock (no beach)
    RiverMouth      // Dedicated coastal outlet anchor for rivers
}

/// <summary>
/// Sector segment defining an angle range [StartAngle, EndAngle] in radians [-PI, PI].
/// </summary>
[Serializable]
public sealed class PerimeterSector
{
    public PerimeterSectorType sectorType;
    public float startAngle;    // Radians [-PI, PI]
    public float endAngle;      // Radians [-PI, PI]
    public float transitionWidth; // Angular blend transition in radians

    public PerimeterSector(PerimeterSectorType type, float start, float end, float transition = 0.15f)
    {
        sectorType = type;
        startAngle = start;
        endAngle = end;
        transitionWidth = Mathf.Max(0.01f, transition);
    }

    public bool ContainsAngle(float angle)
    {
        float normAngle = NormalizeAngle(angle);
        float normStart = NormalizeAngle(startAngle);
        float normEnd = NormalizeAngle(endAngle);

        if (normStart <= normEnd)
        {
            return normAngle >= normStart && normAngle <= normEnd;
        }
        else
        {
            // Wraps around +/- PI
            return normAngle >= normStart || normAngle <= normEnd;
        }
    }

    public float CalculateWeight(float angle)
    {
        float normAngle = NormalizeAngle(angle);
        float normStart = NormalizeAngle(startAngle);
        float normEnd = NormalizeAngle(endAngle);

        float distToStart = Mathf.Abs(Mathf.DeltaAngle(normAngle * Mathf.Rad2Deg, normStart * Mathf.Rad2Deg) * Mathf.Deg2Rad);
        float distToEnd = Mathf.Abs(Mathf.DeltaAngle(normAngle * Mathf.Rad2Deg, normEnd * Mathf.Rad2Deg) * Mathf.Deg2Rad);

        if (ContainsAngle(normAngle))
        {
            float edgeDist = Mathf.Min(distToStart, distToEnd);
            if (edgeDist >= transitionWidth) return 1f;
            return Mathf.SmoothStep(0f, 1f, edgeDist / transitionWidth);
        }

        return 0f;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > Mathf.PI) angle -= Mathf.PI * 2f;
        while (angle < -Mathf.PI) angle += Mathf.PI * 2f;
        return angle;
    }
}

/// <summary>
/// Authoritative perimeter sector mapping around an island's circumference.
/// </summary>
public sealed class PerimeterSectorMap
{
    private readonly List<PerimeterSector> sectors = new List<PerimeterSector>();
    private readonly Vector2 center;

    public IReadOnlyList<PerimeterSector> Sectors => sectors;
    public Vector2 Center => center;

    public PerimeterSectorMap(Vector2 center)
    {
        this.center = center;
    }

    public void AddSector(PerimeterSectorType type, float startAngle, float endAngle, float transition = 0.15f)
    {
        sectors.Add(new PerimeterSector(type, startAngle, endAngle, transition));
    }

    public PerimeterSectorType GetDominantSector(float localX, float localZ)
    {
        float angle = Mathf.Atan2(localZ - center.y, localX - center.x);
        float highestWeight = -1f;
        PerimeterSectorType dominant = PerimeterSectorType.Beach;

        for (int i = 0; i < sectors.Count; i++)
        {
            float weight = sectors[i].CalculateWeight(angle);
            if (weight > highestWeight)
            {
                highestWeight = weight;
                dominant = sectors[i].sectorType;
            }
        }

        return dominant;
    }

    public float GetMountainCoastWeight(float localX, float localZ)
    {
        float angle = Mathf.Atan2(localZ - center.y, localX - center.x);
        float totalWeight = 0f;

        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i].sectorType == PerimeterSectorType.MountainCoast)
            {
                totalWeight = Mathf.Max(totalWeight, sectors[i].CalculateWeight(angle));
            }
        }

        return totalWeight;
    }

    public float GetBeachWeight(float localX, float localZ)
    {
        float angle = Mathf.Atan2(localZ - center.y, localX - center.x);
        float totalWeight = 0f;

        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i].sectorType == PerimeterSectorType.Beach)
            {
                totalWeight = Mathf.Max(totalWeight, sectors[i].CalculateWeight(angle));
            }
        }

        return totalWeight;
    }

    public PerimeterSector FindFirstSector(PerimeterSectorType type)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i].sectorType == type) return sectors[i];
        }
        return null;
    }
}
