using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapSwitcher : MonoBehaviour
{
    public GameObject Map1;
    public GameObject Map2;

    [Space(20)]
    public bool Map = false;

    public void Start()
    {
        if (Map = true)
        {
            Map1.SetActive(true);
            Map2.SetActive(false);
        }
        if (Map = false) 
        { 
            Map1.SetActive(false);
            Map2.SetActive(true);
        }
    }
}
