using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildHeight : MonoBehaviour
{
    // Build Height without Terrain Collider
    // private float BuildHeightDebug = 1.5f; // Height Debug

    // Build Height with Terrain Collider
    [SerializeField] private float BuildHeightDebug = 0.50f; // Height Debug
    [SerializeField] private bool useHeightDebug;

    // Start is called before the first frame update
    void Start()
    {
        if (useHeightDebug)
        {
            Vector3 position = transform.position;
            position.y += BuildHeightDebug;
            transform.position = position;
        }
    }
}
