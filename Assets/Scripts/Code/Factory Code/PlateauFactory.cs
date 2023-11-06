using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlateauFactory : ITerrainFactory
{
    public ITerrain CreateTerrain()
    {
        // Implementation for creating an underwater plateau
        return new PlateauTerrain();
    }
}

