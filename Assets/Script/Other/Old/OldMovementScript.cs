using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// All this script does is unit movement, it handles the position and does not handle the pathing
public class OldMovementScript : MonoBehaviour
{
    public GameObject RequestedPosition;  // Movement Position
    public GameObject Unit;               // Current Position

    public bool Alive = true;             // if alive = false = delete
    public bool attacking = false;        // if attack = true then unit can't move
    public float UnitMS = 1f;             // Movement Speed

    void Update()
    {

        if (Alive == true)
        {
            if (attacking == false)
            {
                //Player.transform.position += new Vector3(0f, 0f, 1f);
                //Unit.transform.position = Vector3.MoveTowards(Unit.transform.position, RequestedPosition.transform.position, UnitMS * Time.deltaTime);
                //Debug.Log("The Unit is now at: " + Unit.transform.position);
                //Debug.Log("The goal is now at: " + RequestedPosition.transform.position);
            }
        }
        else
        {
                //Destroy(gameObject);

        }
    }
}