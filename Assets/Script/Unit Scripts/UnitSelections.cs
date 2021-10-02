using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitSelections : MonoBehaviour
{

    public GameObject groundMarker;

    public List<GameObject> unitList = new List<GameObject>();
    public List<GameObject> unitsSelected = new List<GameObject>();


    private static UnitSelections _instance;
    public static UnitSelections Instance { get { return _instance; } }
    
    #region On Awake List
    private void Awake()
    {
        if(_instance != null && _instance != this) 
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }
    #endregion

    #region Click Select

    // When Clicked on Unit Then...
    public void ClickSelect(GameObject unitToAdd) 
    {   
        this.DeselectAll();
        unitsSelected.Add(unitToAdd);
        unitToAdd.transform.GetChild(0).gameObject.SetActive(true);
        unitToAdd.GetComponent<UnitMovement>().enabled = true;
    }
    #endregion

    #region Shift Click

    // Shift Click Select

    /* 
     * 
     * It will always activate the first child 
     * of the game object no matter, this also 
     * include the first child of any children
     * 
     * Try to always make children unselectable 
     * from UC or the US Scripts for less bugs.
     * 
     */

    public void ShiftClickSelect(GameObject unitToAdd)
    {
        if (!unitsSelected.Contains(unitToAdd))
        {
            unitsSelected.Add(unitToAdd);
            //Always Sets First Child Active ( include the first child of any children )
            unitToAdd.transform.GetChild(0).gameObject.SetActive(true); 
            unitToAdd.GetComponent<UnitMovement>().enabled = true;
        }
        else
        {
            unitToAdd.GetComponent<UnitMovement>().enabled = false;
            unitToAdd.transform.GetChild(0).gameObject.SetActive(false);
            unitsSelected.Remove(unitToAdd);
        }
    }
    #endregion

    #region Drag Selection 

    public void DragSelect(GameObject unitToAdd)
    {
        if (!unitsSelected.Contains(unitToAdd))
        {
            unitsSelected.Add(unitToAdd);
            unitToAdd.transform.GetChild(0).gameObject.SetActive(true);
            unitToAdd.GetComponent<UnitMovement>().enabled = true;
            

        }
    }

    #endregion

    #region Deselect All

    public void DeselectAll()
    {
        foreach (var unit in unitsSelected)
        {
            unit.GetComponent<UnitMovement>().enabled = false;
            unit.transform.GetChild(0).gameObject.SetActive(false);
            

        }
        unitsSelected.Clear();
    }

    #endregion

    #region Deselect
    public void Deselect(GameObject unitToDeselect)
    {
        
    }

    #endregion
}
