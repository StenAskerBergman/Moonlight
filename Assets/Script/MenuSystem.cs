using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuSystem : MonoBehaviour
{
    
    public void OnSelectionChanged(List<Unit> unitsSelected)
    {
        if (unitsSelected.Count>0)
        {
            GetComponentInChildren<SelectionBar>(true).gameObject.SetActive(true);
        }
        else 
        {
            GetComponentInChildren<SelectionBar>(true).gameObject.SetActive(false);
        }
        Debug.Log("This part works!");
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
