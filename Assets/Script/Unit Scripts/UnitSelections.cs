using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UnitSelections : MonoBehaviour
{

    public GameObject groundMarker;

    public List<Unit> unitList = new List<Unit>();
    public List<Unit> unitsSelected = new List<Unit>();
    public bool hasHouses = false;
    public bool hasCharacters = false;
    public UnityEvent<List<Unit>> selectionChanged;

    private static UnitSelections _instance;
    public static UnitSelections Instance { get { return _instance; } }
    
    #region On Awake List
    private void Awake()
    {  
        if (_instance != null && _instance != this) 
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
    public void ClickSelect(Unit unitToAdd) 
    {
        this.DeselectAll();
        unitsSelected.Add(unitToAdd); 
        unitToAdd.transform.GetChild(0).gameObject.SetActive(true); // Enables the Selection Child Game Object
        unitToAdd.GetComponent<UnitMovement>().enabled = true;
        selectionChanged.Invoke(unitsSelected);

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

    public void ShiftClickSelect(Unit unitToAdd)
    {
        if (!unitsSelected.Contains(unitToAdd))
        {
            unitsSelected.Add(unitToAdd);
            //Always Sets First Child Active ( include the first child of any children )
            unitToAdd.transform.GetChild(0).gameObject.SetActive(true); 
            unitToAdd.GetComponent<UnitMovement>().enabled = true;
            selectionChanged.Invoke(unitsSelected);
        }
        else
        {
            unitToAdd.GetComponent<UnitMovement>().enabled = false;
            unitToAdd.transform.GetChild(0).gameObject.SetActive(false);
            unitsSelected.Remove(unitToAdd);
            selectionChanged.Invoke(unitsSelected);

            if (unitsSelected.Count == 0)
            {
                hasHouses = false;
                hasCharacters = false;
            }
        }
    }
    #endregion

    #region Drag Selection 

    public void DragSelect(Unit unitToAdd)
    {
        if (!unitsSelected.Contains(unitToAdd))
        {
            //var unit = unitToAdd.GetComponent<Unit>();
           
            if (unitToAdd != null) 
            {
                if (unitToAdd.type == Unit.UnitType.Character)
                {
                    hasCharacters = true;
                    unitToAdd.GetComponent<UnitMovement>().enabled = true;

                };
            }

            unitsSelected.Add(unitToAdd);
            unitToAdd.transform.GetChild(0).gameObject.SetActive(true);
            selectionChanged.Invoke(unitsSelected);

        }
    }

    #endregion

    #region Deselect All

    public void DeselectAll()
    {
        foreach (var unit in unitsSelected)
        {
            if (unit.type == Unit.UnitType.Character) unit.GetComponent<UnitMovement>().enabled = false;
            unit.transform.GetChild(0).gameObject.SetActive(false);

        }
        hasHouses = false;
        hasCharacters = false;
        unitsSelected.Clear();
        selectionChanged.Invoke(unitsSelected);
    }

    #endregion

    #region Deselect
    public void Deselect(GameObject unitToDeselect)
    {
        selectionChanged.Invoke(unitsSelected);
    }

    #endregion
}
