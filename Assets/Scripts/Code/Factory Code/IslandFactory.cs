using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IslandFactory : ITerrainFactory
{
    private Biome biome;

    public IslandFactory(Biome biome)
    {
        this.biome = biome;
    }

    public ITerrain CreateTerrain()
    {
        // Implementation for creating an island with the specified biome
        return new IslandTerrain(biome);
    }
}
