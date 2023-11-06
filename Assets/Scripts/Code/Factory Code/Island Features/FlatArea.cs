using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlatArea : IFeature
{
    private Biome biome;
    private bool isBuildable;

    public FlatArea(Biome biome)
    {
        this.biome = biome;
        this.isBuildable = true;
    }

    public void GenerateFeature()
    {
        // Implementation for generating a flat area
        GenerateVegetation();
    }

    private void GenerateVegetation()
    {
        // Implementation for generating vegetation based on the biome
        switch (biome)
        {
            case Biome.Forest:
                // Generate forest vegetation
                break;
            case Biome.Tundra:
                // Generate tundra vegetation
                break;
            case Biome.Desert:
                // Generate desert vegetation
                break;
                // Add cases for other biomes as needed

        }
    }
}

public enum Biome
{
    // Undecided
    None,

    // Currenty
    Forest,
    Tundra,
    Desert,

    // To Come! 
    Tropical,
    Arctic,
    Swamp,

}


