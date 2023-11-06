using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainFactory : ITerrainFactory
{
    public ITerrain CreateTerrain()
    {
        // Implementation for creating a generic terrain
        return new Terrain();
    }
}
