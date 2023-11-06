using UnityEngine;

public class Unit : MonoBehaviour
{

    public UnitType type;
    public MoveType moveType;
    public Inventory inventory;
    public GameObject inventoryUIPrefab; // Specific Inventory UI prefab for this unit.

    public bool wasViewed;
    public bool isViewed;
    public bool Selectable;
    public bool Targetable;

    void Start()
    {
        UnitSelections.Instance.unitList.Add(this);
    }

    // Method in Unit.cs
    public void Select()
    {
        // Enable movement, set child object active, etc.
        Debug.Log($"Selected {this.name}");
    }

    public void Deselect()
    {
        // Disable movement, set child object inactive, etc.
    }

    void OnDestroy()
    {
        UnitSelections.Instance.unitList.Remove(this);
    }
}

    // public enum UnitType { Character, House, Drone };