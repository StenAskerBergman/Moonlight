using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableOnAwake : MonoBehaviour
{
    public GameObject thisObject;

    private void Awake()
    {
        thisObject = gameObject;
        thisObject.SetActive(true);
    }
}
