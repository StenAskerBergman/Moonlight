using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HouseBuilderScript : MonoBehaviour
{
    public GameObject house_blueprint;

    public void spawn_house_blueprint()
    {
        Instantiate(house_blueprint);
    }
}