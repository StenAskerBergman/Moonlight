using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitSpawnScript : MonoBehaviour
{
    public GameObject Unit_Model;
    public GameObject SpawningLocation;
    public Vector3 Spawn;

    //transform.position = Spawn;

    public void spawn_unit_model()
    {
      Instantiate(Unit_Model, Spawn, transform.rotation);
    }
}
