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

    public TerrainSampleCache(int gridSize, int visualSamplesPerCell)
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
