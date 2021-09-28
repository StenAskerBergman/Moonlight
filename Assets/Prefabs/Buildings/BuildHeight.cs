using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildHeight : MonoBehaviour
{
    private float BuildHeightDebug = 1f; // Height Debug

    // Start is called before the first frame update
    void Start()
    {
        Vector3 position = transform.position;
        position.y += BuildHeightDebug;
        transform.position = position;
    }
}
