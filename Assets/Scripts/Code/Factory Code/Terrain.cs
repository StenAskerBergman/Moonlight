using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Terrain : ITerrain
{
    public virtual void GenerateMesh()
    {
        // Implementation for generating the base terrain mesh
    }
    public virtual void ApplyNoise()
    {
        // Implementation for applying noise to the terrain mesh
    }
    // Other common methods and properties for terrains
}
