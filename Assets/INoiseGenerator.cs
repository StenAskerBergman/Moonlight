using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INoiseGenerator
{
    float Generate(float x, float y, float scale);
}
