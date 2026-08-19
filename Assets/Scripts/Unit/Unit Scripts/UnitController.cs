using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UnitController : MonoBehaviour
{

    // Events
    public UnityEvent<Unit> unitSelected;
    public UnityEvent<Unit> unitDeselected;
    
    public void Select(Unit unit)
    {
        unitSelected.Invoke(unit);
    }

    public void Deselect(Unit unit)
    {
        unitDeselected.Invoke(unit);
    }
}


    /*
    Issue:

        Trying to tell all the UI the player selected / deselected something.
    
    Psudo Code:
    
        public UnityEvent<?> Selected;
        public UnityEvent<?> Deselected;

        public void Select()
        {
            Selected.Invoke(Select);
        }

        public void Deselect()
        {
            unitSelected.Invoke(Deselect);
        }
    
    */
    