using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Authoritative high-resolution sampled terrain field.
/// Generated ONCE per island regeneration to serve continuous mesh generation,
/// texture splatting, gameplay cell extraction, and neighbor tests with zero redundant noise evaluations.
/// </summary>
public sealed class TerrainSampleCache
{
    public int GridSize { get; }
    public int VisualSamplesPerCell { get; }
    public int Resolution { get; } // (GridSize * VisualSamplesPerCell) + 1
    public float Step { get; } // 1f / VisualSamplesPerCell

    public float[] Heights { get; }
    public float[] BaseFields { get; }
    public Cell.TerrainType[] TerrainTypes { get; }
    public float[] Slopes { get; }
    public float[] MountainAllowances { get; }
    public float[] MountainBoosts { get; }
    public float[] RiverCarveDepths { get; }
    public float[] PlateauInfluences { get; }
    public PlateauSampleData[] PlateauData { get; }

    /// <summary>
    /// Optional diagnostic provenance data. Null during standard generation to avoid 50-70+ MB allocation;
    /// populated when diagnostic/heat-map visualization is requested.
    /// </summary>
    public TerrainAttributionData Attribution { get; }
    public bool HasAttribution => Attribution != null;

    public TerrainSampleCache(
        int gridSize,
        int visualSamplesPerCell,
        bool trackAttribution = false,
        bool includePlateauData = false)
    {
        GridSize = gridSize;
        VisualSamplesPerCell = Mathf.Max(1, visualSamplesPerCell);
        Resolution = gridSize * VisualSamplesPerCell + 1;
        Step = 1f / VisualSamplesPerCell;

        int totalCount = Resolution * Resolution;
        Heights = new float[totalCount];
        BaseFields = new float[totalCount];
        TerrainTypes = new Cell.TerrainType[totalCount];
        Slopes = new float[totalCount];
        MountainAllowances = new float[totalCount];
        MountainBoosts = new float[totalCount];
        RiverCarveDepths = new float[totalCount];
        PlateauInfluences = new float[totalCount];
        PlateauData = includePlateauData ? new PlateauSampleData[totalCount] : null;

        if (trackAttribution)
        {
            Attribution = new TerrainAttributionData(totalCount);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int z)
    {
        return z * Resolution + x;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float GetHeight(int x, int z)
    {
        x = Mathf.Clamp(x, 0, Resolution - 1);
        z = Mathf.Clamp(z, 0, Resolution - 1);
        return Heights[z * Resolution + x];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float GetBaseField(int x, int z)
    {
        x = Mathf.Clamp(x, 0, Resolution - 1);
        z = Mathf.Clamp(z, 0, Resolution - 1);
        return BaseFields[z * Resolution + x];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cell.TerrainType GetTerrainType(int x, int z)
    {
        x = Mathf.Clamp(x, 0, Resolution - 1);
        z = Mathf.Clamp(z, 0, Resolution - 1);
        return TerrainTypes[z * Resolution + x];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float GetSlope(int x, int z)
    {
        x = Mathf.Clamp(x, 0, Resolution - 1);
        z = Mathf.Clamp(z, 0, Resolution - 1);
        return Slopes[z * Resolution + x];
    }
}

/// <summary>
/// Fine-grained subsystem provenance and per-stage elevation deltas.
/// Captured directly during terrain synthesis passes.
/// </summary>
public sealed class TerrainAttributionData
{
    public float[] RawBaseHeights { get; }
    public float[] TerraceDeltas { get; }
    public float[] PlateauDeltas { get; }
    public short[] DominantRidgeIds { get; }
    public short[] DominantRiverIds { get; }

    public TerrainAttributionData(int totalCount)
    {
        RawBaseHeights = new float[totalCount];
        TerraceDeltas = new float[totalCount];
        PlateauDeltas = new float[totalCount];
        DominantRidgeIds = new short[totalCount];
        DominantRiverIds = new short[totalCount];
        System.Array.Fill(DominantRidgeIds, (short)-1);
        System.Array.Fill(DominantRiverIds, (short)-1);
    }
}
