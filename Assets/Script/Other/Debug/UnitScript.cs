using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitScript : MonoBehaviour
{
    // Unit Type
    public GameObject model;


    #region Unit Systematics

        #region Selection
        public bool UnitSelected; // Selected Identifier
        #endregion

        #region Movement Systematics
            public Vector3 RequestedPosition;  // Movement Position
            public GameObject CurrentPosition;   // Current Position
            private Vector3 UnitPosition;    // Current Position
        #endregion

    #endregion

    #region Unit Behaviour
    public float UnitState; // Moving Idle Attacking Dead
    public float UnitMode; // Moving - Still | Majority of units cannot attack and move at the same time
    public bool Alive = true; // if alive = false = delete
    public bool attacking = false; // if attack = true then unit can't move
    #endregion

    #region Unit Stats
    public float UnitMS = 1f;  // Movement Speed
    public float UnitHP = 1f;  // Health Points
    public float UnitAS = 1f;  // Attack Speed
    public float UnitAD = 1f;  // Attack Damage
    #endregion


    private void Update()
    {

            Vector3 UnitPosition = transform.position;

        

        if (Alive == true) 
        {
            if (attacking == false)
            {
                // Transform.HitMarker.position = RequestedPosition;
                // transform.Translate(Vector3.forward * UnitMS);
                // transform.Translate(Vector3.up * Time.deltaTime, Space.World);
                //  UnitPosition = CurrentPosition + RequestedPosition;
               
                model.transform.position = Vector3.MoveTowards(model.transform.position, RequestedPosition, UnitMS * Time.deltaTime);



            }
        } 
        else 
        {
                Destroy(gameObject);
        
        }
    }   
}
