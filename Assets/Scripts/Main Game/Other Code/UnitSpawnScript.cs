using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitSpawnScript : MonoBehaviour
{
    public GameObject Unit_Model;
    private Vector3 Spawn;

    //transform.position = Spawn;

    public void spawn_unit_model()
    {
      Instantiate(Unit_Model, Spawn, transform.rotation);
    }
}
