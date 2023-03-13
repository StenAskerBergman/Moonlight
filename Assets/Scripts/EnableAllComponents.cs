using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableAllComponents : MonoBehaviour
{
    private void Start()
    {
        Component[] components = GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component is Behaviour)
            {
                (component as Behaviour).enabled = true;
            }
        }
    }
}