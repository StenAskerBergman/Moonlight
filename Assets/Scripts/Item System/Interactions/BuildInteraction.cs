// Start - BuildInteraction.cs
using UnityEngine;

public class BuildInteraction : MonoBehaviour, IBuildable
{
    public Inventory unitInventory;

    private void Awake()
    {
        unitInventory = GetComponent<Inventory>();
    }

    public void Build(ItemData item)
    {
        // Building logic here

        // Maybe something like...
        //if(unitData.unitType == UnitType.Builder)
        //{
        //  Anyway to check if we are building on land or under water?
        //  Anyway to check if we can build on Land or under Water?
        //}
        // TO build the Unit requires to be a Builder Unit

    }
}
// End - BuildInteraction.cs