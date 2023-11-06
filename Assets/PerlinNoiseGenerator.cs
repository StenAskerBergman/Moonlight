using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoiseGenerator : INoiseGenerator
{
    public float Generate(float x, float y, float scale)
    {
        return Mathf.PerlinNoise(x, y) * scale;
    }
}

